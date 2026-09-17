using UnityEngine;

// ============================================================
// JudgmentHitEffectSpawner.cs
//   Miss를 제외한 판정이 날 때마다, 그 노트가 있던 레인의 판정선
//   위치에 히트 이펙트를 하나 띄운다. ScoreManager.OnJudged가 등급과
//   함께 레인 번호도 알려주므로, 정확히 그 레인에서만 이펙트가 나온다.
//   이펙트는 매번 Instantiate하지 않고 ObjectPool에서 재사용한다.
// ============================================================
public class JudgmentHitEffectSpawner : MonoBehaviour
{
    [Header("참조")]
    public LaneManager laneManager;
    public GameManager gameManager;

    [Tooltip("판정선에 띄울 이펙트 프리팹.")]
    public GameObject hitEffectPrefab;

    [Tooltip("미리 만들어둘 이펙트 개수.")]
    public int prewarmCount = 8;

    private ObjectPool pool;

    void Awake()
    {
        if (hitEffectPrefab != null)
        {
            pool = ObjectPool.Create("HitEffectPool", hitEffectPrefab, prewarmCount);
            pool.transform.SetParent(transform);
        }
    }

    void OnEnable()
    {
        ScoreManager.OnJudged += HandleJudged;
    }

    void OnDisable()
    {
        ScoreManager.OnJudged -= HandleJudged;
    }

    // 기능: Miss가 아닌 판정마다 해당 레인의 판정선 위치에 이펙트를 하나 꺼내 재생한다.
    private void HandleJudged(Grade grade, int lane)
    {
        if (grade == Grade.Miss || pool == null || laneManager == null || gameManager == null)
        {
            return;
        }

        float x = laneManager.GetLaneX(lane);
        GameObject obj = pool.Get(new Vector3(x, gameManager.judgmentY, 0f), Quaternion.identity);

        LaneHitEffect effect = obj.GetComponent<LaneHitEffect>();
        if (effect != null)
        {
            effect.SetPool(pool);
            effect.ResetEffect();
        }
    }
}
