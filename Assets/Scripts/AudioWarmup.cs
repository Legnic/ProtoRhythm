using UnityEngine;

// ============================================================
// AudioWarmup.cs
//   AudioClip.Create로 만든 클립을 그 종류로는 처음 재생하는 순간,
//   오디오 백엔드가 내부 DSP 그래프/버퍼를 새로 마련하느라 한 번은
//   비용을 문다(그 뒤로는 이미 마련되어 있어 빠르다). 판정 보정 탭의
//   메트로놈(CalibrationController.cs)이 옵션 화면에 들어가자마자
//   바로 이 방식으로 재생을 시작하는데, 하필 그게 이 앱에서 처음
//   재생되는 절차적 오디오라 그 초기화 비용이 고스란히 "옵션 화면
//   첫 진입이 느리다"로 보였다 — 폰트 글리프 문제가 아니었다.
//   앱이 켜지자마자, 들리지 않게 한 번 재생해서 그 비용을 미리 치른다.
// ============================================================
public static class AudioWarmup
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Run()
    {
        var go = new GameObject("AudioWarmup");
        Object.DontDestroyOnLoad(go);

        var source = go.AddComponent<AudioSource>();
        source.volume = 0f;
        source.spatialBlend = 0f;

        var clip = AudioClip.Create("AudioWarmupSilence", 512, 1, 44100, false);
        clip.SetData(new float[512], 0);

        source.PlayOneShot(clip);
        Object.Destroy(go, 1f);
    }
}
