using UnityEngine;

// ============================================================
// AmbientLoopPlayer.cs
//   TitleScene/ResultScene, 그리고 SelectScene의 곡 목록 단계(아직 곡을
//   고르기 전)에서 배경으로 계속 흐르는 환경음(새소리 루프 등)을 관리한다.
//
//   ScoreManager와 같은 패턴으로 TitleScene(앱의 실제 진입점)에 하나만
//   배치해두고, DontDestroyOnLoad로 씬이 바뀌어도 파괴되지 않게 한다.
//   씬을 오가다 TitleScene으로 다시 돌아오면 이미 살아있는 인스턴스와
//   중복되므로, Awake에서 기존 Instance가 있으면 자기 자신을 파괴한다.
//
//   핵심은 EnsurePlaying()이 "이미 재생 중이면 아무 것도 하지 않는다"는
//   점이다 — TitleScene에서 재생을 시작한 뒤 SelectScene(곡 목록 단계)으로
//   넘어가도 그냥 EnsurePlaying()만 다시 부르면 되고, 이미 재생 중이므로
//   끊기지 않고 이어진다. 곡을 골라 미리듣기가 시작되기 직전이나 플레이
//   화면으로 넘어가기 전에는 Stop()을 불러서 겹쳐 들리지 않게 한다.
// ============================================================
public class AmbientLoopPlayer : MonoBehaviour
{
    public static AmbientLoopPlayer Instance { get; private set; }

    public AudioClip clip;

    private AudioSource source;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        source = gameObject.AddComponent<AudioSource>();
        source.clip = clip;
        source.loop = true;
        source.playOnAwake = false;
    }

    // 기능: 아직 재생 중이 아니라면 처음부터 재생을 시작한다. 이미 재생 중이면
    //       아무 일도 하지 않아, 씬이 바뀌어도 소리가 끊기지 않는다.
    public void EnsurePlaying()
    {
        if (source == null || source.clip == null || source.isPlaying)
        {
            return;
        }

        source.Play();
    }

    // 기능: 곡 미리듣기나 실제 플레이가 시작되기 직전에 불러서 겹쳐 들리지 않게 한다.
    public void Stop()
    {
        if (source != null)
        {
            source.Stop();
        }
    }

    // 기능: 잠깐만 멈춰뒀다가 같은 위치에서 이어 들을 때 쓴다(Stop처럼 처음으로
    //       되감기지 않는다) — 판정 보정 탭처럼 짧게 다른 소리(메트로놈)만
    //       들려야 하는 동안 쓰기 위한 용도다.
    public void Pause()
    {
        if (source != null && source.isPlaying)
        {
            source.Pause();
        }
    }

    // 기능: Pause()로 멈춰뒀던 지점부터 다시 재생한다.
    public void Resume()
    {
        if (source != null)
        {
            source.UnPause();
        }
    }
}
