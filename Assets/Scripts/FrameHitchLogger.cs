using UnityEngine;
using UnityEngine.SceneManagement;

// ============================================================
// FrameHitchLogger.cs (임시 진단용 — 원인 확인 후 지워도 된다)
//   "어디서 얼마나 끊기는지"를 추측 대신 실제 로그로 확인하기 위한
//   임시 계측 도구. 프레임 하나가 임계값보다 오래 걸리면 그 순간의
//   씬 이름과 소요 시간을 Player.log에 남긴다.
// ============================================================
public class FrameHitchLogger : MonoBehaviour
{
    private const float ThresholdSeconds = 0.05f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        var go = new GameObject("FrameHitchLogger");
        Object.DontDestroyOnLoad(go);
        go.AddComponent<FrameHitchLogger>();
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime;
        if (dt > ThresholdSeconds)
        {
            Debug.Log($"[Hitch] {dt * 1000f:F0}ms scene={SceneManager.GetActiveScene().name} t={Time.realtimeSinceStartup:F2}s");
        }
    }
}
