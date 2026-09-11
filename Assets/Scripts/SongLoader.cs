using UnityEngine;

// ============================================================
// SongLoader.cs
//   여러 곡을 다루기 시작하면 "이 음원엔 이 채보"라는 매칭을 사람이
//   직접 기억해서 RhythmClock.musicSource.clip과 NoteSpawner.chart를
//   따로따로 맞춰줘야 한다. SongData로 그 둘을 한 세트로 묶어두고,
//   이 컴포넌트가 시작 시 한 번에 배선해준다.
//
//   RhythmClock.Start()가 musicSource.clip을 읽어서 재생을 예약하므로,
//   반드시 그보다 먼저 실행되어야 한다. Unity는 씬에 이미 존재하는
//   모든 오브젝트의 Awake()가 어떤 Start()보다도 먼저 끝나는 것을
//   보장하므로, 여기서는 Awake에서 배선한다.
//
//   주의: RhythmClock의 musicSource.clip과 NoteSpawner의 chart는
//   인스펙터에 남아있는 값과 상관없이 여기서 매번 덮어써진다.
// ============================================================
public class SongLoader : MonoBehaviour
{
    [Header("참조")]
    public RhythmClock clock;
    public NoteSpawner noteSpawner;

    [Header("재생할 곡")]
    public SongData song;

    void Awake()
    {
        if (song == null)
        {
            Debug.LogWarning("SongLoader: song이 지정되지 않았습니다.");
            return;
        }

        if (clock != null && clock.musicSource != null)
        {
            clock.musicSource.clip = song.audioClip;
        }

        if (noteSpawner != null)
        {
            noteSpawner.chart = song.chart;
        }
    }
}
