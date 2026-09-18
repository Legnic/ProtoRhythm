using UnityEngine;

// ============================================================
// AudioBootstrap.cs
//   저장된 전체 볼륨(GameSettings.MasterVolume)을 게임이 시작되자마자
//   AudioListener.volume에 적용한다. AudioListener.volume은 어떤 씬에
//   몇 개의 AudioSource가 있든 상관없이 그 순간 스피커로 나가는 모든
//   소리에 곱해지는 전역 값이고, 씬 전환과 무관하게 유지되므로 앱 시작
//   시점에 한 번만 반영해두면 된다 — 이후에는 VolumeController가 옵션
//   화면에서 슬라이더를 움직일 때만 다시 바꾼다.
// ============================================================
public static class AudioBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ApplySavedVolume()
    {
        AudioListener.volume = GameSettings.MasterVolume;
    }
}
