#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

// ============================================================
// KeyInputAutoTester.cs  (에디터 전용 — 빌드에는 포함되지 않는다)
//   메뉴 "Rhythm/Tests/Key Input And SFX Test"(ChartAutoPlayTestMenu)가 시작한다.
//   ChartAutoPlayTester와 달리 판정 함수를 직접 부르지 않고, 가상 키보드 장치에
//   실제 키 상태 이벤트를 넣는다 — Input System → InputJudge.Update(키 감지) →
//   KeyHitSound.Play / 판정까지 실제 게임과 같은 경로를 그대로 탄다.
//
//   1) 효과음 지연: 곡이 시작하기 전(카운트다운 중, 음악 없음)에 키를 하나씩
//      12번 눌러, 키 이벤트를 넣은 순간부터 효과음 소스가 실제로 소리를 내기
//      시작한 오디오 블록까지의 시간을 잰다(KeyHitSound의 AudioSource 출력을
//      OnAudioFilterRead로 감시).
//   2) 동시치기: 동시치기가 가장 많은 채보(ChipTune)를 끝까지 실제 키 입력으로
//      쳐서, 동시에 눌린 키가 하나라도 씹히면 Miss로 드러나게 한다.
// ============================================================
[DefaultExecutionOrder(1000)]
public class KeyInputAutoTester : MonoBehaviour
{
    public const string ActiveKey = "KeyInputTest.Active";
    public const string OrigSfxOnKey = "KeyInputTest.OrigSfxOn";
    public const string OrigSfxVolKey = "KeyInputTest.OrigSfxVol";
    public const string OrigSpeedKey = "KeyInputTest.OrigSpeed";
    const int SfxPressCount = 8;
    const string TargetSong = "Chip Tune";

    static string OutDir => Path.Combine(Path.GetDirectoryName(Application.dataPath), "Temp", "ChartAutoPlay");

    RhythmClock clock; InputJudge judge; KeyHitSound sfx; SfxProbe probe;
    Keyboard kb;
    InputSettings.EditorInputBehaviorInPlayMode origEditorBehavior;
    InputSettings.BackgroundBehavior origBackground;

    // sfx latency phase
    int sfxPresses; float nextSfxPressRealtime; readonly List<double> pressDsp = new List<double>();
    readonly List<double> sfxLatency = new List<double>();
    readonly List<double> sfxStrong = new List<double>();
    bool sfxPhaseDone;

    // chord phase
    List<NoteData> notes; int next; readonly List<(float tail, int lane)> holding = new List<(float, int)>();
    readonly HashSet<int> tapThisFrame = new HashSet<int>();
    readonly Dictionary<Grade, int> grades = new Dictionary<Grade, int>();
    readonly List<string> missLog = new List<string>();
    float avgDt = 1f / 45f; float lastT = float.NaN;
    int chordCount, chordNotes; bool done;
    readonly StringBuilder report = new StringBuilder();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void BeforeScene()
    {
        if (!SessionState.GetBool(ActiveKey, false)) return;
        var lib = AssetDatabase.LoadAssetAtPath<SongLibrary>(ChartAutoPlayTester.LibraryPath);
        foreach (var s in lib.songs)
            if (s.songName == TargetSong) { GameSession.SelectedSong = s; GameSession.SelectedDifficulty = s.difficulties[0]; }
        // KeyHitSound reads these in Awake: make sure the effect sound is audible for the measurement
        GameSettings.SfxEnabled = true;
        GameSettings.SfxVolume = 1f;
        // slowest speed = longest note travel = longest silent countdown for the isolated sound presses
        GameSettings.ScrollSpeed = GameSettings.ScrollSpeedMin;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AfterScene()
    {
        if (!SessionState.GetBool(ActiveKey, false)) return;
        if (Object.FindAnyObjectByType<SongLoader>() == null) return;
        new GameObject("KeyInputAutoTester").AddComponent<KeyInputAutoTester>();
    }

    void Start()
    {
        Application.runInBackground = true;
        Directory.CreateDirectory(OutDir);
        clock = FindAnyObjectByType<RhythmClock>(); judge = FindAnyObjectByType<InputJudge>(); sfx = FindAnyObjectByType<KeyHitSound>();
        // listen on the AudioListener: it receives the final mix every audio block, sound or not
        // (a filter on the effect AudioSource only runs while that source is playing)
        var listener = FindAnyObjectByType<AudioListener>();
        probe = listener.gameObject.AddComponent<SfxProbe>();

        // Keyboard input normally only reaches the game while the Game view has focus — the test must
        // work while the Editor is in the background, so route all input to the game for its duration.
        origEditorBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
        origBackground = InputSystem.settings.backgroundBehavior;
        InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;

        kb = InputSystem.AddDevice<Keyboard>("AutoTestKeyboard");
        kb.MakeCurrent();
        ScoreManager.OnJudged += OnJudged;

        notes = new List<NoteData>(GameSession.SelectedDifficulty.chart.notes);
        notes.Sort((a, b) => a.hitTime.CompareTo(b.hitTime));
        var byTime = new Dictionary<float, int>();
        foreach (var n in notes) byTime[n.hitTime] = byTime.TryGetValue(n.hitTime, out int c) ? c + 1 : 1;
        foreach (var kv in byTime) if (kv.Value > 1) { chordCount++; chordNotes += kv.Value; }

        AudioSettings.GetDSPBufferSize(out int bufLen, out int numBuf);
        int rate = AudioSettings.outputSampleRate;
        report.AppendLine($"# 실제 키 입력 · 효과음 테스트 ({System.DateTime.Now:yyyy-MM-dd HH:mm})");
        report.AppendLine($"오디오: 출력 {rate}Hz, DSP 버퍼 {bufLen}샘플 x {numBuf}개 = 버퍼 하나 {bufLen * 1000.0 / rate:0.0}ms, 출력 대기열 최대 {bufLen * numBuf * 1000.0 / rate:0.0}ms");
        report.AppendLine($"레인 키: {string.Join(", ", judge.laneKeys)} | 효과음 클립 로드 대기 중");
        nextSfxPressRealtime = Time.realtimeSinceStartup + 0.3f;
    }

    void OnDestroy()
    {
        ScoreManager.OnJudged -= OnJudged;
        if (kb != null && kb.added) InputSystem.RemoveDevice(kb);
        InputSystem.settings.editorInputBehaviorInPlayMode = origEditorBehavior;
        InputSystem.settings.backgroundBehavior = origBackground;
        RestoreSettings();
    }

    static void RestoreSettings()
    {
        if (!SessionState.GetBool(ActiveKey, false)) return;
        GameSettings.SfxEnabled = SessionState.GetBool(OrigSfxOnKey, true);
        GameSettings.SfxVolume = SessionState.GetFloat(OrigSfxVolKey, 1f);
        GameSettings.ScrollSpeed = SessionState.GetFloat(OrigSpeedKey, GameSettings.ScrollSpeedDefault);
        GameSettings.Save();
        SessionState.SetBool(ActiveKey, false);
    }

    void OnJudged(Grade g, int lane)
    {
        grades[g] = grades.TryGetValue(g, out int c) ? c + 1 : 1;
        if (g == Grade.Miss)
        {
            float t = clock.CurrentTime;
            // find the chart note this Miss belongs to (tap: hit time ~ t - MissWindow; hold tail: tail ~ t - MissWindow)
            NoteData best = default; float bestD = float.MaxValue;
            foreach (var n in notes) if (n.lane == lane) { float d = Mathf.Min(Mathf.Abs(n.hitTime - t), Mathf.Abs(n.hitTime + n.holdDuration - t)); if (d < bestD) { bestD = d; best = n; } }
            int same = 0; foreach (var n in notes) if (Mathf.Abs(n.hitTime - best.hitTime) < 0.0005f) same++;
            missLog.Add($"{t:0.000}s 레인 {lane} (노트 {best.hitTime:0.000}s, {(same > 1 ? $"{same}키 동시치기" : "단타")}{(best.holdDuration > 0 ? ", 롱노트" : "")})");
        }
    }

    void Update()
    {
        if (done || kb == null) return;
        kb.MakeCurrent(); // a real key press would make the physical keyboard current again
        float t = clock.CurrentTime;
        if (!float.IsNaN(lastT) && t > lastT) avgDt = Mathf.Lerp(avgDt, t - lastT, 0.1f);
        lastT = t;
        tapThisFrame.Clear();

        if (!sfxPhaseDone) { SfxPhase(t); return; }
        if (!clock.HasStarted && t < -0.5f) { QueueState(); return; }

        // press so the key is seen in the frame closest to the hit time (state events apply next frame)
        float horizon = t + avgDt * 0.5f;
        while (next < notes.Count && notes[next].hitTime <= horizon)
        {
            var n = notes[next++];
            tapThisFrame.Add(n.lane);
            if (n.holdDuration > 0f) holding.Add((n.hitTime + n.holdDuration, n.lane));
        }
        holding.RemoveAll(h => h.tail <= horizon);
        QueueState();

        float lastEnd = 0f; foreach (var n in notes) lastEnd = Mathf.Max(lastEnd, n.hitTime + n.holdDuration);
        if (next >= notes.Count && holding.Count == 0 && t > lastEnd + 0.5f) Finish();
    }

    void SfxPhase(float t)
    {
        // isolated presses during the countdown, before the music starts
        if (!clock.IsActive) { QueueState(); return; } // song not scheduled yet: the clock still reads 0
        if (sfxPresses < SfxPressCount && t < -0.6f && Time.realtimeSinceStartup >= nextSfxPressRealtime && probe.Ready && probe.Silent)
        {
            int lane = sfxPresses % judge.laneKeys.Length;
            tapThisFrame.Add(lane);
            probe.Arm();
            pressDsp.Add(AudioSettings.dspTime);
            sfxPresses++;
            nextSfxPressRealtime = Time.realtimeSinceStartup + 0.45f; // capture window is 0.35 s
            QueueState();
            return;
        }
        QueueState();
        if (pressDsp.Count > sfxLatency.Count && probe.TryTakeOnset(out double onsetDsp, out double strongDsp))
        {
            sfxLatency.Add(onsetDsp - pressDsp[sfxLatency.Count]);
            sfxStrong.Add(strongDsp - pressDsp[sfxStrong.Count]);
        }
        if (sfxPresses >= SfxPressCount && sfxLatency.Count >= SfxPressCount || t >= -0.6f)
        {
            sfxPhaseDone = true;
            sfxLatency.Sort(); sfxStrong.Sort();
            report.AppendLine();
            report.AppendLine("## 1. 효과음 지연 (키 이벤트 → 효과음 소스가 소리를 내기 시작한 오디오 블록)");
            report.AppendLine($"- 누른 키 {sfxPresses}회, 출력 감시 준비 {probe.Ready}");
            if (sfxLatency.Count == 0) report.AppendLine("- 측정 실패: 효과음 출력이 감지되지 않았습니다.");
            else
            {
                double sum = 0; foreach (var v in sfxLatency) sum += v;
                report.AppendLine($"- 소리가 나기 시작한 시점: {sfxLatency.Count}회 평균 {sum / sfxLatency.Count * 1000:0.0}ms, 최소 {sfxLatency[0] * 1000:0.0}ms, 최대 {sfxLatency[sfxLatency.Count - 1] * 1000:0.0}ms");
                double ss = 0; foreach (var v in sfxStrong) ss += v;
                report.AppendLine($"- 소리가 최고 크기의 절반에 도달한 시점(체감상 '맞는' 순간): 평균 {ss / sfxStrong.Count * 1000:0.0}ms, 최소 {sfxStrong[0] * 1000:0.0}ms, 최대 {sfxStrong[sfxStrong.Count - 1] * 1000:0.0}ms");
                report.AppendLine("- 이 값에는 입력 처리 1프레임 + 오디오 스레드가 소리를 섞는 시점까지가 들어간다. 여기에 출력 대기열(위 '출력 대기열 최대')과 운영체제/장치 지연이 더해져 스피커로 나간다.");
            }
        }
    }

    readonly List<Key> keysDown = new List<Key>();
    void QueueState()
    {
        keysDown.Clear();
        foreach (var h in holding) keysDown.Add(judge.laneKeys[h.lane]);
        foreach (int lane in tapThisFrame) { var k = judge.laneKeys[lane]; if (!keysDown.Contains(k)) keysDown.Add(k); }
        InputSystem.QueueStateEvent(kb, new KeyboardState(keysDown.ToArray()));
    }

    void Finish()
    {
        done = true;
        QueueState();
        int holds = 0; foreach (var n in notes) if (n.holdDuration > 0f) holds++;
        int expected = notes.Count + holds;
        int perfect = grades.TryGetValue(Grade.Perfect, out int p) ? p : 0;
        int total = 0; foreach (var kv in grades) total += kv.Value;
        report.AppendLine();
        report.AppendLine($"## 2. 실제 키 입력으로 {GameSession.SelectedSong.songName} {GameSession.SelectedDifficulty.label} 끝까지 플레이");
        report.AppendLine($"- 노트 {notes.Count}개(롱노트 {holds}, 동시치기 {chordCount}회 / {chordNotes}노트) → 판정 기대 {expected}회, 실제 {total}회");
        report.AppendLine($"- Perfect {perfect}, Great {Cnt(Grade.Great)}, Good {Cnt(Grade.Good)}, Miss {Cnt(Grade.Miss)}");
        if (missLog.Count > 0) { report.AppendLine("- Miss 목록:"); foreach (var m in missLog) report.AppendLine("  - " + m); }
        else report.AppendLine("- 씹힌 입력 없음");
        File.WriteAllText(Path.Combine(OutDir, "keyinput.md"), report.ToString(), new UTF8Encoding(false));
        Debug.Log("KeyInputTest: 완료");
        RestoreSettings();
        EditorApplication.isPlaying = false;
    }

    int Cnt(Grade g) => grades.TryGetValue(g, out int c) ? c : 0;
}

// Watches the effect sound's AudioSource output on the audio thread and reports when sound starts.
public class SfxProbe : MonoBehaviour
{
    // after Arm(), records (dspTime, block peak) for ~0.35 s of output so the main thread can find
    // both the first audible block and the block where the sound reaches half its loudest level
    volatile bool armed, captured; volatile int silentBlocks = 100; volatile bool ready;
    double armDsp; readonly List<(double t, float peak)> blocks = new List<(double, float)>();
    public bool Ready => ready;
    public bool Silent => silentBlocks >= 4;
    public void Arm() { lock (blocks) blocks.Clear(); captured = false; armDsp = AudioSettings.dspTime; armed = true; }

    // onset: first block above the noise floor; strong: first block >= 50% of the loudest block
    public bool TryTakeOnset(out double onset, out double strong)
    {
        onset = strong = 0;
        if (!captured) return false;
        captured = false;
        lock (blocks)
        {
            float max = 0f; foreach (var b in blocks) if (b.peak > max) max = b.peak;
            if (max <= 1e-4f) return false;
            bool gotOnset = false;
            foreach (var b in blocks)
            {
                if (!gotOnset && b.peak > 1e-4f) { onset = b.t; gotOnset = true; }
                if (b.peak >= 0.5f * max) { strong = b.t; break; }
            }
        }
        return true;
    }

    void OnAudioFilterRead(float[] data, int channels)
    {
        ready = true;
        float peak = 0f;
        for (int i = 0; i < data.Length; i++) { float a = data[i] < 0 ? -data[i] : data[i]; if (a > peak) peak = a; }
        if (peak > 1e-4f) silentBlocks = 0; else if (silentBlocks < 1000) silentBlocks++;
        if (armed)
        {
            double now = AudioSettings.dspTime;
            lock (blocks) blocks.Add((now, peak));
            if (now - armDsp > 0.35) { armed = false; captured = true; }
        }
    }
}
#endif
