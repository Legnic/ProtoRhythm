using UnityEngine;
using TMPro;

// ============================================================
// JudgmentPopupUI.cs
//   DJMAX처럼 노트를 판정할 때마다 화면 중앙에 등급(Perfect/Great/Good/Miss)을
//   작게, 반투명하게 잠깐 띄워주는 연출.
//
//   ScoreManager.OnJudged 이벤트만 구독한다 — 어떤 노트가, 어떤 레인에서
//   판정됐는지는 몰라도 되고, "방금 어떤 등급이 나왔는지"만 알면 되기
//   때문이다. 세부 판정(빠름/느림 등)은 아직 다루지 않고 등급 이름만 보여준다.
// ============================================================
public class JudgmentPopupUI : MonoBehaviour
{
    public TMP_Text judgmentText;

    [Range(0f, 1f)]
    [Tooltip("판정 직후 텍스트의 최대 투명도(알파). 플레이에 방해되지 않도록 0.5~0.7 정도를 권장한다.")]
    public float maxAlpha = 0.9f;

    [Tooltip("최대 투명도를 그대로 유지하는 시간(초).")]
    public float holdDuration = 0.15f;

    [Tooltip("holdDuration이 지난 뒤 서서히 사라지는 데 걸리는 시간(초).")]
    public float fadeDuration = 0.25f;

    // 지금 표시 중인 판정 하나의 "남은 수명"(홀드 + 페이드를 합친 시간에서 줄어든다).
    private float remaining;
    private Color baseColor;

    // 기능: 켜지면 ScoreManager의 판정 이벤트를 구독하고, 시작 시 텍스트를 투명하게 비운다.
    // 원리: 씬이 다시 로드되거나(Retry) 이 오브젝트가 다시 활성화될 때마다
    //       새로 구독해야 하므로 OnEnable/OnDisable 쌍으로 관리한다 — Start
    //       한 번만 쓰면 재구독 없이 이벤트가 끊길 수 있다.
    void OnEnable()
    {
        ScoreManager.OnJudged += HandleJudged;

        if (judgmentText != null)
        {
            baseColor = judgmentText.color;
            remaining = 0f;
            SetAlpha(0f);
        }
    }

    void OnDisable()
    {
        ScoreManager.OnJudged -= HandleJudged;
    }

    // 기능: 새 판정이 들어올 때마다 텍스트를 바꾸고 다시 최대 밝기로 되돌린다.
    // 원리: 이전 판정의 페이드가 끝나기 전에 다음 판정이 들어와도(연타 등)
    //       그냥 remaining을 리셋해서 항상 "가장 최근 판정"이 온전히 보이게 한다.
    private void HandleJudged(Grade grade)
    {
        if (judgmentText == null) return;

        judgmentText.text = grade.ToString();
        remaining = holdDuration + fadeDuration;
        SetAlpha(maxAlpha);
    }

    // 기능: holdDuration 동안은 그대로 두고, 그 이후 fadeDuration에 걸쳐 알파를 0으로 줄인다.
    void Update()
    {
        if (judgmentText == null || remaining <= 0f)
        {
            return;
        }

        remaining -= Time.deltaTime;

        if (remaining <= fadeDuration)
        {
            float t = fadeDuration > 0f ? Mathf.Clamp01(remaining / fadeDuration) : 0f;
            SetAlpha(maxAlpha * t);
        }
    }

    private void SetAlpha(float alpha)
    {
        Color c = baseColor;
        c.a = alpha;
        judgmentText.color = c;
    }
}
