#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

// ============================================================
// ChartAutoPlayTester.cs  (에디터 전용 — 빌드에는 포함되지 않는다)
//   메뉴 "Rhythm/Tests/Auto Play All Charts"(ChartAutoPlayTestMenu)가
//   PlayScene을 플레이 모드로 열면, 이 컴포넌트가 SongLibrary의 모든 곡/난이도를
//   차례로 실제 시계·음원으로 끝까지 자동 플레이한다.
//
//   확인하는 것:
//   - 모든 노트가 생성되고, 정확한 시각에 누르면 전부 Perfect가 나오는지
//     (롱노트는 머리/꼬리 두 번)
//   - 플레이 중 일시정지 패널의 배속 슬라이더(실제 UI 경로)로 배속을
//     0.5~5.0배 사이에서 바꿔도 판정 오차가 달라지지 않는지
//   - 누르는 순간 노트가 판정선 위에 그려져 있는지(배속과 무관하게)
//   - 판정 보정(audioOffset)이 배속과 무관하게 음원 위치와 판정 시각 사이의
//     일정한 차이로만 작용하는지
//   결과는 프로젝트의 Temp/ChartAutoPlay/에 기록하고, 끝나면 원래 설정(배속,
//   판정 보정)을 되돌리고 플레이 모드를 끝낸다.
// ============================================================
public class ChartAutoPlayTester : MonoBehaviour
{
    public const string ActiveKey = "ChartAutoPlay.Active";
    public const string OrigSpeedKey = "ChartAutoPlay.OrigSpeed";
    public const string OrigOffsetKey = "ChartAutoPlay.OrigOffset";
    public const string LibraryPath = "Assets/Musics/SongLibrary.asset";

    static readonly float[] SpeedCycle = { 1.0f, 0.5f, 2.0f, 5.0f, 3.3f, 0.7f, 1.5f, 4.2f };
    static readonly float[] OffsetCycle = { 0f, 0.060f, -0.040f };
    const float SpeedChangeEverySeconds = 9f;

    static string OutDir => Path.Combine(Path.GetDirectoryName(Application.dataPath), "Temp", "ChartAutoPlay");

    class Job { public SongData song; public SongDifficulty diff; }
    class Stat { public int n; public double sum, sumSq, min = double.MaxValue, max = double.MinValue;
        public void Add(double v) { n++; sum += v; sumSq += v * v; if (v < min) min = v; if (v > max) max = v; }
        public string Ms() => n == 0 ? "-" : $"평균 {sum / n * 1000:0.0}ms (최소 {min * 1000:0.0} / 최대 {max * 1000:0.0}, n={n})"; }

    readonly List<Job> jobs = new List<Job>();
    int jobIndex = -1;

    RhythmClock clock; GameManager gm; SongLoader loader; NoteSpawner spawner; InputJudge judge; PlaySceneController psc;
    ScrollSpeedSlider pauseSlider;
    MethodInfo tryPress, tryRelease;

    // per-chart state
    List<NoteData> notes; int nextPress; readonly List<(float t, int lane)> releases = new List<(float, int)>();
    readonly Dictionary<Grade, int> grades = new Dictionary<Grade, int>();
    int missingNotes, wrongNotes, judgedEvents;
    float nextSpeedChangeAt; int speedIdx; bool pausing; float resumeAtRealtime; float currentOffset;
    readonly Dictionary<float, Stat> pressErrBySpeed = new Dictionary<float, Stat>();
    readonly Dictionary<float, Stat> visualErrBySpeed = new Dictionary<float, Stat>();
    readonly Dictionary<float, Stat> audioMinusClockBySpeed = new Dictionary<float, Stat>();
    int speedChanges; float chartStartRealtime; int chartStartFrame;
    readonly StringBuilder report = new StringBuilder();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void BeforeScene()
    {
        if (!SessionState.GetBool(ActiveKey, false)) return;
        var lib = AssetDatabase.LoadAssetAtPath<SongLibrary>(LibraryPath);
        if (lib == null || lib.songs.Count == 0 || lib.songs[0].difficulties.Count == 0) { Debug.LogError("ChartAutoPlay: SongLibrary를 찾을 수 없습니다."); return; }
        // the first chart: SongLoader.Start reads GameSession; first job uses offset 0
        GameSettings.AudioOffset = OffsetCycle[0];
        GameSettings.ScrollSpeed = SpeedCycle[0];
        GameSession.SelectedSong = lib.songs[0];
        GameSession.SelectedDifficulty = lib.songs[0].difficulties[0];
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AfterScene()
    {
        if (!SessionState.GetBool(ActiveKey, false)) return;
        if (Object.FindAnyObjectByType<SongLoader>() == null) return; // not PlayScene
        new GameObject("ChartAutoPlayTester").AddComponent<ChartAutoPlayTester>();
    }

    void Start()
    {
        Application.runInBackground = true;
        Directory.CreateDirectory(OutDir);
        clock = FindAnyObjectByType<RhythmClock>(); gm = FindAnyObjectByType<GameManager>();
        loader = FindAnyObjectByType<SongLoader>(); spawner = FindAnyObjectByType<NoteSpawner>();
        judge = FindAnyObjectByType<InputJudge>(); psc = FindAnyObjectByType<PlaySceneController>();
        pauseSlider = FindAnyObjectByType<ScrollSpeedSlider>(FindObjectsInactive.Include);
        tryPress = typeof(InputJudge).GetMethod("TryPress", BindingFlags.Instance | BindingFlags.NonPublic);
        tryRelease = typeof(InputJudge).GetMethod("TryRelease", BindingFlags.Instance | BindingFlags.NonPublic);
        if (psc != null) psc.resultDelaySeconds = 100000f; // stay in PlayScene between charts
        ScoreManager.OnJudged += OnJudged;

        var lib = AssetDatabase.LoadAssetAtPath<SongLibrary>(LibraryPath);
        foreach (var s in lib.songs) foreach (var d in s.difficulties) jobs.Add(new Job { song = s, diff = d });
        report.AppendLine($"# 자동 플레이 테스트 ({System.DateTime.Now:yyyy-MM-dd HH:mm})");
        report.AppendLine($"채보 {jobs.Count}개, 배속 순환 [{string.Join(", ", SpeedCycle)}] ({SpeedChangeEverySeconds}초마다 일시정지 → 패널 슬라이더로 변경 → 재개), 판정 보정 순환 [{string.Join(", ", OffsetCycle)}]s");
        report.AppendLine($"일시정지 패널 슬라이더 발견: {(pauseSlider != null)}, TryPress/TryRelease: {(tryPress != null && tryRelease != null)}");
        Log($"시작: 채보 {jobs.Count}개");
        BeginJob(0, alreadyLoading: true);
    }

    void OnDestroy()
    {
        ScoreManager.OnJudged -= OnJudged;
        Restore();
    }

    static void Restore()
    {
        if (!SessionState.GetBool(ActiveKey, false)) return;
        GameSettings.ScrollSpeed = SessionState.GetFloat(OrigSpeedKey, GameSettings.ScrollSpeedDefault);
        GameSettings.AudioOffset = SessionState.GetFloat(OrigOffsetKey, 0f);
        GameSettings.Save();
        SessionState.SetBool(ActiveKey, false);
    }

    void OnJudged(Grade g, int lane)
    {
        if (jobIndex < 0) return;
        grades[g] = grades.TryGetValue(g, out int c) ? c + 1 : 1;
        judgedEvents++;
    }

    void BeginJob(int idx, bool alreadyLoading = false)
    {
        jobIndex = idx;
        var job = jobs[idx];
        notes = new List<NoteData>(job.diff.chart.notes);
        notes.Sort((a, b) => a.hitTime.CompareTo(b.hitTime));
        nextPress = 0; releases.Clear(); grades.Clear(); missingNotes = 0; wrongNotes = 0; judgedEvents = 0;
        pressErrBySpeed.Clear(); visualErrBySpeed.Clear(); audioMinusClockBySpeed.Clear();
        speedChanges = 0; speedIdx = 0; pausing = false;
        currentOffset = OffsetCycle[idx % OffsetCycle.Length];
        GameSettings.AudioOffset = currentOffset;
        SetSpeedDirect(SpeedCycle[0]);
        nextSpeedChangeAt = SpeedChangeEverySeconds;
        if (!alreadyLoading)
        {
            NoteRegistry.ClearAll();
            GameSession.SelectedSong = job.song; GameSession.SelectedDifficulty = job.diff;
            loader.Load(job.song, job.diff);
        }
        chartStartRealtime = Time.realtimeSinceStartup; chartStartFrame = Time.frameCount;
        Log($"[{idx + 1}/{jobs.Count}] {job.song.songName} {job.diff.label} 시작 (노트 {notes.Count}, 판정 보정 {currentOffset * 1000:0}ms)");
    }

    void SetSpeedDirect(float s) { GameSettings.ScrollSpeed = s; }

    float CurrentSpeed => gm.hiSpeed;

    void Update()
    {
        if (jobIndex < 0 || jobIndex >= jobs.Count || clock == null) return;
        if (spawner.chart != jobs[jobIndex].diff.chart || !clock.IsActive) return; // still loading

        // ---- pause → change speed with the pause-panel slider → resume ----
        if (pausing)
        {
            if (Time.realtimeSinceStartup >= resumeAtRealtime)
            {
                psc.TogglePause();
                pausing = false;
            }
            return;
        }
        float t = clock.CurrentTime;
        if (t >= nextSpeedChangeAt && !clock.IsPaused)
        {
            nextSpeedChangeAt += SpeedChangeEverySeconds;
            speedIdx = (speedIdx + 1) % SpeedCycle.Length;
            psc.TogglePause(); // shows the pause panel, freezes the clock
            if (pauseSlider != null && pauseSlider.slider != null && pauseSlider.isActiveAndEnabled)
                pauseSlider.slider.value = Mathf.Round(SpeedCycle[speedIdx] * 10f); // the real UI path
            else
                SetSpeedDirect(SpeedCycle[speedIdx]);
            speedChanges++;
            pausing = true;
            resumeAtRealtime = Time.realtimeSinceStartup + 0.25f;
            return;
        }

        // ---- auto play: press exactly when the clock reaches each hit time ----
        while (nextPress < notes.Count && t >= notes[nextPress].hitTime)
        {
            var nd = notes[nextPress++];
            Note n = NoteRegistry.GetEarliestNote(nd.lane);
            if (n == null) { missingNotes++; continue; }
            if (Mathf.Abs(n.hitTime - nd.hitTime) > 0.0005f) wrongNotes++;
            float speed = CurrentSpeed;
            Get(pressErrBySpeed, speed).Add(t - nd.hitTime);
            // where the note is drawn right now, as a time error: (y - judgmentY) / speed in units/s
            float unitsPerSec = Mathf.Max(0.0001f, gm.GetDistance(t, t + 0.01f) / 0.01f);
            Get(visualErrBySpeed, speed).Add((n.transform.position.y - gm.judgmentY) / unitsPerSec);
            if (clock.musicSource != null && clock.musicSource.clip != null && clock.musicSource.isPlaying)
                Get(audioMinusClockBySpeed, speed).Add((double)clock.musicSource.timeSamples / clock.musicSource.clip.frequency - t);
            tryPress.Invoke(judge, new object[] { nd.lane });
            if (nd.holdDuration > 0f) releases.Add((nd.hitTime + nd.holdDuration, nd.lane));
        }
        for (int i = releases.Count - 1; i >= 0; i--)
        {
            if (t >= releases[i].t)
            {
                Get(pressErrBySpeed, CurrentSpeed).Add(t - releases[i].t);
                tryRelease.Invoke(judge, new object[] { releases[i].lane });
                releases.RemoveAt(i);
            }
        }

        // ---- chart finished ----
        float lastEnd = 0f; foreach (var nd in notes) lastEnd = Mathf.Max(lastEnd, nd.hitTime + nd.holdDuration);
        if (nextPress >= notes.Count && releases.Count == 0 && t > lastEnd + 0.5f)
        {
            FinishJob();
            if (jobIndex + 1 < jobs.Count) BeginJob(jobIndex + 1);
            else EndAll();
        }
    }

    static Stat Get(Dictionary<float, Stat> d, float k) { if (!d.TryGetValue(k, out var s)) d[k] = s = new Stat(); return s; }

    void FinishJob()
    {
        var job = jobs[jobIndex];
        int holds = 0; foreach (var nd in notes) if (nd.holdDuration > 0f) holds++;
        int expected = notes.Count + holds;
        int perfect = grades.TryGetValue(Grade.Perfect, out int p) ? p : 0;
        bool pass = missingNotes == 0 && wrongNotes == 0 && judgedEvents == expected && perfect == expected;
        float secs = Time.realtimeSinceStartup - chartStartRealtime; int frames = Time.frameCount - chartStartFrame;
        report.AppendLine();
        report.AppendLine($"## {job.song.songName} — {job.diff.label} (Lv.{job.diff.level}) : {(pass ? "통과" : "실패")}");
        report.AppendLine($"- 노트 {notes.Count}개(롱노트 {holds}) → 판정 기대 {expected}회, 실제 {judgedEvents}회 | Perfect {perfect}, Great {Cnt(Grade.Great)}, Good {Cnt(Grade.Good)}, Miss {Cnt(Grade.Miss)}");
        report.AppendLine($"- 생성되지 않은 노트 {missingNotes}, 다른 노트가 잡힌 경우 {wrongNotes} | 배속 변경 {speedChanges}회(일시정지 패널 슬라이더) | 판정 보정 {currentOffset * 1000:0}ms | 평균 {frames / Mathf.Max(0.001f, secs):0} FPS");
        report.AppendLine("- 배속별 (누른 시각 - 판정 시각) / (누를 때 노트가 그려진 위치를 시간으로 환산) / (음원 재생 위치 - 게임 시계):");
        var keys = new List<float>(pressErrBySpeed.Keys); keys.Sort();
        foreach (var k in keys)
            report.AppendLine($"  - x{k:0.0}: 입력 {pressErrBySpeed[k].Ms()} | 화면 {(visualErrBySpeed.ContainsKey(k) ? visualErrBySpeed[k].Ms() : "-")} | 음원 {(audioMinusClockBySpeed.ContainsKey(k) ? audioMinusClockBySpeed[k].Ms() : "-")}");
        File.WriteAllText(Path.Combine(OutDir, "results.md"), report.ToString(), new UTF8Encoding(false));
        Log($"[{jobIndex + 1}/{jobs.Count}] 완료: {(pass ? "통과" : "실패")} (Perfect {perfect}/{expected}, 누락 {missingNotes})");
    }

    int Cnt(Grade g) => grades.TryGetValue(g, out int c) ? c : 0;

    void EndAll()
    {
        jobIndex = jobs.Count;
        report.AppendLine();
        report.AppendLine("완료.");
        File.WriteAllText(Path.Combine(OutDir, "results.md"), report.ToString(), new UTF8Encoding(false));
        Log("전체 완료");
        Restore();
        EditorApplication.isPlaying = false;
    }

    static void Log(string msg)
    {
        Debug.Log("ChartAutoPlay: " + msg);
        File.AppendAllText(Path.Combine(OutDir, "progress.log"), $"{System.DateTime.Now:HH:mm:ss} {msg}\n", new UTF8Encoding(false));
    }
}
#endif
