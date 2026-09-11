using UnityEngine;

// ============================================================
// RhythmClock.cs
//   오디오 하드웨어의 정밀 시계(dspTime)를 기준으로
//   "지금이 노래의 몇 초 지점인지"를 계산해주는 시간 소스.
//   (이전에 Phigros 역설계 프로젝트에서 만든 것과 동일한 컴포넌트.
//    DJMAX 방식이든 Phigros 방식이든, "정확한 시간"이 필요하다는
//    점은 똑같기 때문에 그대로 재사용한다)
//
//   왜 Time.time이 아니라 AudioSettings.dspTime을 쓰는가:
//     Time.time은 프레임 간격(deltaTime)을 누적한 값이라
//     프레임이 드랍되면 오차가 누적되어 실제 오디오 재생 위치와
//     서서히 어긋난다. dspTime은 오디오 하드웨어의 절대 시각이라
//     이런 드리프트가 생기지 않는다.
// ============================================================
public class RhythmClock : MonoBehaviour
{
    [Header("오디오")]
    public AudioSource musicSource;

    [Tooltip("PlayScheduled 예약을 위한 준비 시간(초). 0.3~1초 정도를 권장한다.")]
    public float startDelay = 0.5f;

    [Header("레이턴시 보정")]
    [Tooltip("기기별 오디오/출력 지연 보정값(초). 캘리브레이션 결과를 저장해서 쓴다.")]
    public float audioOffset = 0f;

    private double dspSongStartTime;
    private bool started = false;

    // 외부(NoteSpawner, Note 등)에서 읽어가는, 보정이 반영된 현재 노래 재생 시각
    public float CurrentTime { get; private set; }

    public bool HasStarted => started && CurrentTime >= 0f;

    void Start()
    {
        if (musicSource == null || musicSource.clip == null)
        {
            Debug.LogWarning("RhythmClock: musicSource 또는 musicSource.clip이 비어 있습니다.");
            return;
        }

        double scheduledStartDspTime = AudioSettings.dspTime + startDelay;
        musicSource.PlayScheduled(scheduledStartDspTime);

        dspSongStartTime = scheduledStartDspTime;
        started = true;
    }

    void Update()
    {
        if (!started) return;

        double elapsed = AudioSettings.dspTime - dspSongStartTime;
        CurrentTime = (float)elapsed + audioOffset;
    }
}
