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
    private bool paused = false;
    private double dspPausedAt;

    // 외부(NoteSpawner, Note 등)에서 읽어가는, 보정이 반영된 현재 노래 재생 시각
    public float CurrentTime { get; private set; }

    [Tooltip("SongLoader가 계산해서 채워주는, 노트가 spawnY에서 judgmentY까지 떨어지는 데 걸리는 시간(초). " +
             "리드인 전체(카운트다운+이 시간) 중 뒤쪽 이 시간만큼이 '노트가 실제로 떨어지는 구간'이다. " +
             "CountdownUI가 카운트다운 숫자를 언제 그만 보여줄지 판단하는 데 쓴다.")]
    public float noteLeadTime = 0f;

    public bool HasStarted => started && CurrentTime >= 0f;

    // 기능: 일시정지 중인지 알려준다. InputJudge가 이 값을 보고 일시정지
    //       중에는 입력을 아예 무시한다.
    public bool IsPaused => paused;

    // 기능: Play()가 이미 호출되어 곡 재생이 예약/진행 중인지 알려준다.
    // 원리: HasStarted와 다르다 — HasStarted는 "리드인(대기 시간)까지 지나
    //       실제로 노래가 흘러나오기 시작했는지"까지 확인하지만(CurrentTime >= 0),
    //       IsActive는 리드인 중이어도(CurrentTime이 아직 음수여도) true다.
    //       NoteSpawner는 리드인 중에도 이른 노트를 미리 스폰해야 하므로,
    //       "Play()가 호출됐는가"만 보는 이 값을 기준으로 삼아야 한다.
    public bool IsActive => started;

    // 기능: 지정된 오디오 클립의 재생을 예약하고, 시간 계산을 시작한다.
    //       SongLoader가 곡을 배선한 직후 호출한다. minLeadIn을 주면
    //       startDelay보다 그 값이 더 크면 그 값을 대신 쓴다.
    // 원리: musicSource.Play()로 즉시 재생하지 않고 PlayScheduled로 "앞으로
    //       (startDelay와 minLeadIn 중 더 큰 값)초 뒤"에 재생하도록 예약한다.
    //       startDelay는 원래 "오디오 하드웨어가 소리를 낼 준비 시간"만을 위한
    //       값(0.3~1초면 충분)이었는데, 그 값만 쓰면 문제가 생긴다 — 노트가
    //       spawnY에서 judgmentY까지 떨어지는 데 걸리는 시간(travelTime)이
    //       startDelay보다 길면, 채보 맨 앞쪽의 이른 노트들은 "리드인 동안
    //       화면 맨 위에서부터 떨어질 시간"을 다 못 받고 이미 절반쯘 떨어진
    //       채로 갑자기 나타나 버린다 — 음악은 정확히 제때 시작되는데(dspTime
    //       기준이라 항상 정확하다) 노트가 나오는 모양이 이상해서 "싱크가
    //       어긋난 것처럼" 보이는 원인이다. minLeadIn으로 SongLoader가
    //       "이 채보의 가장 이른 노트가 온전히 떨어지려면 최소 몇 초가
    //       필요한지"를 계산해서 넘겨주면, 실제 리드인이 그보다 짧아지지
    //       않는다. 다른 곡이 이미 재생 중일 수 있으므로 예약 전에 Stop()으로
    //       먼저 멈춘다.
    public void Play(float minLeadIn = 0f)
    {
        if (musicSource == null || musicSource.clip == null)
        {
            Debug.LogWarning("RhythmClock: musicSource 또는 musicSource.clip이 비어 있습니다.");
            return;
        }

        musicSource.Stop(); // 다른 곡이 재생 중이었을 수도 있으니 먼저 멈춘다.

        double actualDelay = System.Math.Max(startDelay, minLeadIn);
        double scheduledStartDspTime = AudioSettings.dspTime + actualDelay;
        musicSource.PlayScheduled(scheduledStartDspTime);

        dspSongStartTime = scheduledStartDspTime;
        started = true;
        paused = false;
    }

    // 기능: 재생을 일시정지한다 — 오디오도 멈추고, CurrentTime도 그 순간에서
    //       더 흐르지 않게 얼려둔다.
    // 원리: dspTime은 Time.timeScale과 무관하게 항상 흐르는 하드웨어 시계라서,
    //       "일시정지"라고 해서 저절로 멈추지 않는다. 여기서 직접 paused
    //       플래그로 Update()의 CurrentTime 갱신을 막아야 한다. 정지한 시각
    //       (dspPausedAt)을 기억해두는 이유는, Resume()에서 "멈춰있던 만큼"을
    //       계산해 dspSongStartTime을 뒤로 밀어야 하기 때문이다 — 그렇게
    //       해야 재개했을 때 CurrentTime이 멈췄던 지점부터 다시 흐른다
    //       (안 그러면 일시정지 동안 흘러버린 실제 시간만큼 노트가 튀어버린다).
    public void Pause()
    {
        if (!started || paused) return;

        // 리드인(카운트다운) 중이라 아직 실제로 재생을 시작하기 전이라면,
        // PlayScheduled로 걸어둔 예약은 아직 대기 중인 상태다. Unity의
        // AudioSource.Pause()/UnPause()는 이렇게 "아직 시작 안 한 예약
        // 재생"을 올바르게 다루지 못한다 — Pause() 후 UnPause()하면 원래
        // 예약된 미래 시각이 아니라 UnPause()한 바로 그 순간부터 재생이
        // 시작돼 버려서, 음악이 노트보다 먼저 나오는 문제가 생긴다(직접
        // 확인됨). 그래서 이 경우엔 아예 Stop()으로 예약 자체를 취소하고,
        // Resume()에서 새로 계산한 시각으로 PlayScheduled를 다시 건다.
        // 이미 재생이 시작된 뒤(HasStarted)라면 평범하게 Pause()/UnPause()로
        // 충분하다.
        if (HasStarted)
        {
            musicSource.Pause();
        }
        else
        {
            musicSource.Stop();
        }

        dspPausedAt = AudioSettings.dspTime;
        paused = true;
    }

    // 기능: 일시정지를 풀고 멈췄던 지점부터 다시 재생한다.
    public void Resume()
    {
        if (!started || !paused) return;

        bool wasStartedBeforePause = HasStarted;

        double pausedDuration = AudioSettings.dspTime - dspPausedAt;
        dspSongStartTime += pausedDuration; // 멈춰있던 시간만큼 시작 기준점을 뒤로 민다.

        if (wasStartedBeforePause)
        {
            musicSource.UnPause();
        }
        else
        {
            // Pause()에서 예약을 취소했으므로, 뒤로 밀린 새 시작 시각으로
            // 다시 예약한다 — 리드인이 남은 만큼 노트가 마저 떨어지고,
            // CurrentTime이 정확히 0이 되는 시점에 맞춰 재생이 시작된다.
            musicSource.PlayScheduled(dspSongStartTime);
        }

        paused = false;
    }

    // 기능: 매 프레임 CurrentTime(현재 곡 재생 위치, 초)을 갱신한다.
    // 원리: 현재 dspTime에서 곡이 시작하기로 예약된 dspTime을 빼면 "곡이 시작한
    //       뒤로 몇 초가 지났는지"가 그대로 나온다. Play()가 아직 호출되지
    //       않았다면(started == false) 아무 것도 하지 않아 CurrentTime이
    //       기본값(0)에 머무르고, NoteSpawner 등도 조용히 대기 상태를 유지한다.
    //       일시정지 중(paused)에도 갱신을 멈춰서 CurrentTime을 그 자리에 얼려둔다.
    void Update()
    {
        if (!started || paused) return;

        double elapsed = AudioSettings.dspTime - dspSongStartTime;
        CurrentTime = (float)elapsed + audioOffset;
    }
}
