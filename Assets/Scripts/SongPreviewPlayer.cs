using UnityEngine;

// ============================================================
// SongPreviewPlayer.cs
//   곡 선택 화면에서 "이 곡은 이런 느낌이다"를 짧게 들려주는 미리듣기.
//   지정한 구간(previewStart~previewEnd)만 반복 재생하되, 매번 뚝 끊기듯
//   멈추거나 시작하지 않도록 시작할 때는 서서히 커지고(fadeInDuration),
//   구간이 끝나기 직전엔 서서히 줄어들며(fadeOutDuration), 그 사이에
//   잠깐(gapDuration) 완전한 무음을 둔 뒤 처음부터 다시 재생한다.
//
//   RhythmClock처럼 dspTime 기반 정밀 타이밍이 전혀 필요 없다 — 판정에
//   쓰이는 게 아니라 그냥 분위기를 보여주는 배경음이므로, AudioSource.time
//   (초 단위 재생 위치)을 그대로 기준 삼아 매 프레임(Update) 검사하는
//   방식으로 충분하다.
// ============================================================
public class SongPreviewPlayer : MonoBehaviour
{
    public AudioSource previewSource;

    [Tooltip("미리듣기 구간 시작(초).")]
    public float previewStart = 60f;
    [Tooltip("미리듣기 구간 끝(초).")]
    public float previewEnd = 90f;
    [Tooltip("구간이 시작될 때(첫 재생 포함, 무음 뒤 다시 시작할 때도) 이 시간(초) 동안 서서히 소리를 키운다.")]
    public float fadeInDuration = 2f;
    [Tooltip("구간이 끝나기 전 이 시간(초) 동안 서서히 소리를 줄인다.")]
    public float fadeOutDuration = 2f;
    [Tooltip("한 바퀴가 끝난 뒤 다시 재생하기 전까지 무음으로 두는 시간(초).")]
    public float gapDuration = 2f;

    private enum State { Idle, FadingIn, Playing, FadingOut, Gap }
    private State state = State.Idle;
    private float gapTimer;
    private float actualStart;
    private float actualEnd;

    // 기능: 지정한 클립의 미리듣기 구간을 처음부터 재생하기 시작한다.
    // 원리: 클립 길이가 미리듣기 구간보다 짧을 수도 있으므로(테스트용 짧은
    //       클립 등), previewStart/previewEnd를 클립 길이 안으로 잘라서
    //       (actualStart/actualEnd) 어떤 클립을 넣어도 예외 없이 동작하게 한다.
    public void Play(AudioClip clip)
    {
        if (previewSource == null || clip == null)
        {
            Stop();
            return;
        }

        // AudioSource.time에 클립 길이와 정확히 같은 값(또는 그 이상)을 넣으면
        // "유효하지 않은 seek 위치"로 처리되어 재생이 실패한다(짧은 테스트
        // 클립으로 직접 확인됨) — 그래서 clip.length보다 살짝 못 미치는
        // 값까지만 시작 지점으로 허용한다.
        float latestValidStart = Mathf.Max(0f, clip.length - 0.01f);
        actualStart = Mathf.Clamp(previewStart, 0f, latestValidStart);
        actualEnd = Mathf.Clamp(previewEnd, actualStart, clip.length);

        previewSource.Stop();
        previewSource.clip = clip;
        StartFromBeginning();
    }

    // 기능: 구간의 맨 처음(actualStart)부터 다시 재생을 건다 — 최초 재생과
    //       무음(Gap) 이후 재시작 둘 다 여기로 모은다.
    // 원리: 두 경우 모두 "처음부터 다시 트는" 동작 자체는 완전히 같다
    //       (볼륨을 0으로 되돌리고 시작 지점으로 seek한 뒤 Play) — 여기서
    //       fadeInDuration이 0보다 크면 서서히 커지는 FadingIn 상태로,
    //       아니면 곧바로 최대 볼륨인 Playing 상태로 들어간다.
    private void StartFromBeginning()
    {
        previewSource.volume = fadeInDuration > 0f ? 0f : 1f;
        previewSource.time = actualStart;
        previewSource.Play();
        state = fadeInDuration > 0f ? State.FadingIn : State.Playing;
    }

    // 기능: 미리듣기를 즉시 멈춘다(곡 목록으로 돌아가거나, 난이도를 골라 플레이 화면으로 넘어갈 때).
    public void Stop()
    {
        if (previewSource != null)
        {
            previewSource.Stop();
        }
        state = State.Idle;
    }

    // 기능: 매 프레임 재생 위치를 보고 "구간 끝이 다가왔는지(페이드아웃)",
    //       "구간이 끝났는지(정지 후 무음 대기)", "무음 대기가 끝났는지
    //       (처음부터 다시 재생)"를 판단한다.
    void Update()
    {
        if (state == State.Idle || previewSource == null || previewSource.clip == null)
        {
            return;
        }

        switch (state)
        {
            case State.FadingIn:
                float elapsed = previewSource.time - actualStart;
                float fadeInT = fadeInDuration > 0f ? Mathf.Clamp01(elapsed / fadeInDuration) : 1f;
                previewSource.volume = fadeInT;

                if (elapsed >= fadeInDuration)
                {
                    previewSource.volume = 1f;
                    state = State.Playing;
                }
                break;

            case State.Playing:
                if (previewSource.time >= actualEnd - fadeOutDuration)
                {
                    state = State.FadingOut;
                }
                break;

            case State.FadingOut:
                float remaining = actualEnd - previewSource.time;
                float t = fadeOutDuration > 0f ? Mathf.Clamp01(remaining / fadeOutDuration) : 0f;
                previewSource.volume = t;

                if (remaining <= 0f || !previewSource.isPlaying)
                {
                    previewSource.Stop();
                    gapTimer = gapDuration;
                    state = State.Gap;
                }
                break;

            case State.Gap:
                gapTimer -= Time.deltaTime;
                if (gapTimer <= 0f)
                {
                    StartFromBeginning();
                }
                break;
        }
    }
}
