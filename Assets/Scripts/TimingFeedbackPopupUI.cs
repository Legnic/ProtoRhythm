using UnityEngine;
using TMPro;

// ============================================================
// TimingFeedbackPopupUI.cs
//   JudgmentPopupUI(등급 표시) 바로 아래에서, "그 판정이 얼마나 빠르거나
//   늦었는지"만 잠깐 보여주는 교정용 연출. 점수 시스템과 완전히 분리된
//   TimingFeedback의 이벤트만 구독한다 — 판정 등급/점수가 어떻게
//   계산되는지는 몰라도 되고, 데드존 안쪽은 애초에 TimingFeedback이
//   이벤트를 보내지 않으므로 여기서는 "이벤트가 온 경우"만 다루면 된다.
//
//   표시 방식(끔/FAST·LATE만/밀리초까지)은 이벤트가 올 때마다
//   GameSettings.TimingFeedbackDisplayMode를 다시 읽는다 — 옵션 화면에서
//   설정을 바꾸면 다음 판정부터 바로 반영되게 하기 위해서다.
// ============================================================
public class TimingFeedbackPopupUI : MonoBehaviour
{
    public TMP_Text feedbackText;

    [Range(0f, 1f)]
    [Tooltip("표시 직후 텍스트의 최대 투명도(알파).")]
    public float maxAlpha = 0.9f;

    [Tooltip("최대 투명도를 그대로 유지하는 시간(초).")]
    public float holdDuration = 0.15f;

    [Tooltip("holdDuration이 지난 뒤 서서히 사라지는 데 걸리는 시간(초).")]
    public float fadeDuration = 0.25f;

    private float remaining;
    private Color baseColor;

    void OnEnable()
    {
        TimingFeedback.OnFeedback += HandleFeedback;

        if (feedbackText != null)
        {
            baseColor = feedbackText.color;
            remaining = 0f;
            SetAlpha(0f);
        }
    }

    void OnDisable()
    {
        TimingFeedback.OnFeedback -= HandleFeedback;
    }

    // 기능: FAST/LATE 이벤트가 올 때마다 설정된 표시 방식에 맞춰 텍스트를 바꾸고
    //       다시 최대 밝기로 되돌린다. [끔]이면 아무것도 하지 않는다.
    private void HandleFeedback(TimingFeedbackResult result)
    {
        if (feedbackText == null) return;

        TimingFeedbackDisplayMode mode = GameSettings.TimingFeedbackDisplayMode;
        if (mode == TimingFeedbackDisplayMode.Off)
        {
            return;
        }

        string label = result.isFast ? "FAST" : "LATE";

        if (mode == TimingFeedbackDisplayMode.Milliseconds)
        {
            int ms = Mathf.RoundToInt(result.diffSeconds * 1000f);
            string sign = ms >= 0 ? "+" : "";
            feedbackText.text = $"{sign}{ms}ms {label}";
        }
        else
        {
            feedbackText.text = label;
        }

        remaining = holdDuration + fadeDuration;
        SetAlpha(maxAlpha);
    }

    void Update()
    {
        if (feedbackText == null || remaining <= 0f)
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
        feedbackText.color = c;
    }
}
