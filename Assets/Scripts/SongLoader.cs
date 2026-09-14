using UnityEngine;

// ============================================================
// SongLoader.cs
//   "이 음원엔 이 채보"라는 매칭을 사람이 직접 기억해서
//   RhythmClock.musicSource.clip과 NoteSpawner.chart를 따로따로
//   맞춰줘야 하는 실수를 막기 위한 컴포넌트. 곡(SongData)과 그 안의
//   난이도(SongDifficulty)를 함께 받아 Load() 한 번 호출로 전부
//   배선하고 곡을 재생하기 시작한다.
//
//   곡 선택 UI(SongSelectUI)가 플레이어의 선택(곡 -> 난이도)을 받아
//   이 메서드를 호출한다. 씬 시작 시 자동으로 아무 곡도 재생하지
//   않는다 — 플레이어가 고르기 전에는 NoteSpawner.chart가 비어 있어서
//   NoteSpawner.Update()도, RhythmClock도 조용히 대기한다.
// ============================================================
public class SongLoader : MonoBehaviour
{
    [Header("참조")]
    public RhythmClock clock;
    public NoteSpawner noteSpawner;
    public GameManager gameManager;

    // 기능: 곡(SongData)과 난이도(SongDifficulty)를 받아 이 조합으로 완전히
    //       전환한다 — 음원/채보 배선, 변속 테이블 재구성, 스포너·점수 리셋,
    //       재생 시작까지 한 번에 처리한다.
    // 원리: 음원(AudioClip)은 song에서, 채보(ChartData)는 difficulty에서 각각
    //       가져온다 — 같은 곡이라도 난이도마다 채보만 다르고 음원은 같기 때문에
    //       이렇게 나눠서 받는다. 순서가 중요한데, clip과 chart를 먼저 채워 넣고,
    //       gameManager.SetChart로 이 난이도의 변속 구간에 맞춰 거리 테이블을
    //       다시 굽고, ResetSpawner()로 "다음에 스폰할 노트" 포인터를 처음으로
    //       되돌리고, ResetScore()로 이전 곡의 점수/콤보를 지운 뒤, 마지막에
    //       clock.Play()를 호출해서 실제 재생을 시작한다. clip/chart가 아직
    //       비어 있는 상태로 Play()가 먼저 실행되면 안 되므로 반드시 이
    //       순서를 지킨다.
    public void Load(SongData song, SongDifficulty difficulty)
    {
        if (song == null || difficulty == null)
        {
            Debug.LogWarning("SongLoader: song 또는 difficulty가 null입니다.");
            return;
        }

        if (clock != null && clock.musicSource != null)
        {
            clock.musicSource.clip = song.audioClip;
        }

        if (noteSpawner != null)
        {
            noteSpawner.chart = difficulty.chart;
            noteSpawner.ResetSpawner();
        }

        if (gameManager != null)
        {
            gameManager.SetChart(difficulty.chart);
        }

        ScoreManager.Instance.ResetScore();

        if (clock != null)
        {
            clock.Play();
        }
    }
}
