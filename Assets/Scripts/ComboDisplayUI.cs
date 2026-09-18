using UnityEngine;
using TMPro;

// ============================================================
// ComboDisplayUI.cs
//   노트 스폰선과 판정 텍스트(JudgmentPopupUI) 사이에서, 지금 콤보 수를
//   그대로 보여주는 상시 표시. JudgmentPopupUI/TimingFeedbackPopupUI처럼
//   잠깐 떴다 사라지는 연출이 아니라, 콤보가 바뀔 때마다 즉시 갱신되고
//   계속 떠 있는 카운터다.
//
//   ScoreManager.OnJudged 이벤트를 구독하는 대신 매 프레임 combo 값을
//   직접 읽어 비교한다 — ScoreManager.ReportHit는 OnJudged를 combo를
//   갱신하기 "전"에 호출하므로, 이벤트 핸들러 안에서 combo를 읽으면 한
//   판정 늦은 값을 보게 된다. 폴링하면 이 순서 문제와 완전히 무관하게,
//   갱신된 값을 그다음 프레임에 그대로 반영할 수 있다.
//
//   콤보가 0이면(아직 아무것도 못 쳤거나 Miss로 막 끊긴 경우) 아무 숫자도
//   띄우지 않는다 — "0 콤보"는 플레이어에게 의미 있는 정보가 아니다.
// ============================================================
public class ComboDisplayUI : MonoBehaviour
{
    public TMP_Text comboText;

    private int lastCombo = -1;

    void OnEnable()
    {
        lastCombo = -1; // 켜지자마자 한 번은 무조건 갱신되게 한다.
    }

    void Update()
    {
        if (ScoreManager.Instance == null || comboText == null)
        {
            return;
        }

        int combo = ScoreManager.Instance.combo;
        if (combo == lastCombo)
        {
            return;
        }

        lastCombo = combo;
        comboText.text = combo > 0 ? combo.ToString() : "";
    }
}
