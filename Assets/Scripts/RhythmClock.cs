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
//
//   곡 선택 UI가 생기면서 씬이 시작하자마자 자동으로 재생하지 않고,
//   플레이어가 곡을 고른 시점에 SongLoader가 Play()를 호출해서
//   재생을 시작한다. NoteSpawner는 IsActive(Play가 호출됐는지)를 직접
//   확인해서, 곡을 고르기 전에는 chart가 미리 배선되어 있어도 스폰하지
//   않는다 — chart == null 여부만으로는 "아직 재생 전"을 구분할 수 없기
//   때문이다(씬에 남아있던 이전 채보 참조 때문에 곡 선택 화면에서
//   노트가 미리 스폰되는 버그가 실제로 있었다).
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

    // 기능: Play()가 이미 호출되어 곡 재생이 예약/진행 중인지 알려준다.
    // 원리: HasStarted와 다르다 — HasStarted는 "리드인(대기 시간)까지 지나
    //       실제로 노래가 흘러나오기 시작했는지"까지 확인하지만(CurrentTime >= 0),
    //       IsActive는 리드인 중이어도(CurrentTime이 아직 음수여도) true다.
    //       NoteSpawner는 리드인 중에도 이른 노트를 미리 스폰해야 하므로,
    //       "Play()가 호출됐는가"만 보는 이 값을 기준으로 삼아야 한다.
    public bool IsActive => started;

    // 기능: 지정된 오디오 클립의 재생을 예약하고, 시간 계산을 시작한다.
    //       SongLoader가 곡을 배선한 직후 호출한다.
    // 원리: musicSource.Play()로 즉시 재생하지 않고 PlayScheduled로 "앞으로
    //       startDelay초 뒤"에 재생하도록 예약한다 — 오디오 시스템이 실제로
    //       소리를 내보내기까지 약간의 준비 시간이 필요한데, 즉시 재생을
    //       요청하면 그 준비 시간만큼 밀려서 "노트가 판정선에 도달하는 시점"과
    //       "실제로 소리가 나는 시점"이 어긋날 수 있다. 예약한 dsp 시각
    //       (dspSongStartTime)을 기준점으로 저장해두면, Update()에서 그 시각
    //       이후로 얼마나 지났는지를 계산해 정확한 재생 위치를 구할 수 있다.
    //       다른 곡이 이미 재생 중일 수 있으므로 예약 전에 Stop()으로 먼저 멈춘다.
    public void Play()
    {
        if (musicSource == null || musicSource.clip == null)
        {
            Debug.LogWarning("RhythmClock: musicSource 또는 musicSource.clip이 비어 있습니다.");
            return;
        }

        musicSource.Stop(); // 다른 곡이 재생 중이었을 수도 있으니 먼저 멈춘다.

        double scheduledStartDspTime = AudioSettings.dspTime + startDelay;
        musicSource.PlayScheduled(scheduledStartDspTime);

        dspSongStartTime = scheduledStartDspTime;
        started = true;
    }

    // 기능: 매 프레임 CurrentTime(현재 곡 재생 위치, 초)을 갱신한다.
    // 원리: 현재 dspTime에서 곡이 시작하기로 예약된 dspTime을 빼면 "곡이 시작한
    //       뒤로 몇 초가 지났는지"가 그대로 나온다. Play()가 아직 호출되지
    //       않았다면(started == false) 아무 것도 하지 않아 CurrentTime이
    //       기본값(0)에 머무르고, NoteSpawner 등도 조용히 대기 상태를 유지한다.
    void Update()
    {
        if (!started) return;

        double elapsed = AudioSettings.dspTime - dspSongStartTime;
        CurrentTime = (float)elapsed + audioOffset;
    }
}
