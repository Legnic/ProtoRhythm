using UnityEngine;

// ============================================================
// LaneHitEffect.cs
//   판정선에서 노트가 처리됐을 때 잠깐 나타났다 사라지는 이펙트 하나의
//   수명을 관리한다. SpriteRenderer가 붙어있으면 커지면서 옅어지는
//   기본 연출을 재생하고, 없어도(파티클 시스템 등 다른 이펙트로
//   교체된 경우) duration이 지나면 스스로 풀로 돌아간다 — 나중에 스프라이트
//   대신 다른 이펙트 애셋으로 갈아끼워도 이 스크립트를 그대로 재사용할
//   수 있게 하기 위함이다.
// ============================================================
public class LaneHitEffect : MonoBehaviour
{
    [Tooltip("이펙트가 유지되는 시간(초). 이 시간이 지나면 스스로 풀로 돌아간다.")]
    public float duration = 0.25f;

    [Tooltip("시작 대비 끝났을 때의 스케일 배율 (SpriteRenderer가 있을 때만 적용).")]
    public float endScaleMultiplier = 1.6f;

    private SpriteRenderer sr;
    private Vector3 startScale;
    private Color startColor;
    private float elapsed;
    private ObjectPool pool;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        startScale = transform.localScale;
        if (sr != null)
        {
            startColor = sr.color;
        }
    }

    // 기능: JudgmentHitEffectSpawner가 이 이펙트를 풀에서 꺼낸 직후 호출해서,
    //       어느 풀로 돌아가야 하는지 기억해둔다.
    public void SetPool(ObjectPool effectPool)
    {
        pool = effectPool;
    }

    // 기능: 풀에서 재사용될 때 이전 재생분의 진행 상태(경과 시간, 스케일, 색)를
    //       처음 값으로 되돌린다.
    // 원리: 오브젝트가 파괴되지 않고 재사용되므로, Awake 때 한 번만 기억해둔
    //       startScale/startColor를 기준으로 elapsed만 0으로 리셋하면
    //       매번 새로 태어난 것과 동일하게 애니메이션이 처음부터 재생된다.
    public void ResetEffect()
    {
        elapsed = 0f;
        transform.localScale = startScale;
        if (sr != null)
        {
            sr.color = startColor;
        }
    }

    void Update()
    {
        elapsed += Time.deltaTime;
        float t = duration > 0f ? Mathf.Clamp01(elapsed / duration) : 1f;

        if (sr != null)
        {
            transform.localScale = Vector3.LerpUnclamped(startScale, startScale * endScaleMultiplier, t);

            Color c = startColor;
            c.a = startColor.a * (1f - t);
            sr.color = c;
        }

        if (elapsed >= duration)
        {
            if (pool != null)
            {
                pool.Release(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }
    }
}
