using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// ============================================================
// ComboDisplayUI.cs
//   노트 스폰선과 판정 이미지(JudgmentPopupUI) 사이에서, 지금 콤보 수를
//   숫자 이미지(0~9 스프라이트)를 자릿수만큼 나란히 배치해 보여주는 상시
//   표시. JudgmentPopupUI/TimingFeedbackPopupUI처럼 잠깐 떴다 사라지는
//   연출이 아니라, 콤보가 바뀔 때마다 즉시 갱신되고 계속 떠 있는 카운터다.
//
//   ScoreManager.OnJudged 이벤트를 구독하는 대신 매 프레임 combo 값을
//   직접 읽어 비교한다 — ScoreManager.ReportHit는 OnJudged를 combo를
//   갱신하기 "전"에 호출하므로, 이벤트 핸들러 안에서 combo를 읽으면 한
//   판정 늦은 값을 보게 된다. 폴링하면 이 순서 문제와 완전히 무관하게,
//   갱신된 값을 그다음 프레임에 그대로 반영할 수 있다.
//
//   콤보가 0이면(아직 아무것도 못 쳤거나 Miss로 막 끊긴 경우) 아무 숫자도
//   띄우지 않는다 — "0 콤보"는 플레이어에게 의미 있는 정보가 아니다.
//
//   자릿수 이미지는 콤보가 늘 때마다 Instantiate/Destroy하지 않고 풀처럼
//   재사용한다 — 한 번 만든 Image는 자릿수가 줄어들면 SetActive(false)로
//   꺼둔 채 남겨두고, 다시 늘어나면 그대로 켜서 쓴다.
// ============================================================
public class ComboDisplayUI : MonoBehaviour
{
    [Header("참조")]
    [Tooltip("숫자 이미지들이 나열될 부모. Horizontal Layout Group + Content Size Fitter(가로: Preferred Size)가 붙어있어야 한다.")]
    public RectTransform digitContainer;

    [Header("숫자 스프라이트")]
    [Tooltip("인덱스 0~9가 숫자 0~9에 그대로 대응한다.")]
    public Sprite[] digitSprites = new Sprite[10];

    [Tooltip("숫자 이미지 하나의 크기(정사각형 스프라이트 기준).")]
    public Vector2 digitSize = new Vector2(100f, 100f);

    private readonly List<Image> digitPool = new List<Image>();
    private int lastCombo = -1;

    void OnEnable()
    {
        lastCombo = -1; // 켜지자마자 한 번은 무조건 갱신되게 한다.
    }

    void Update()
    {
        if (ScoreManager.Instance == null || digitContainer == null)
        {
            return;
        }

        int combo = ScoreManager.Instance.combo;
        if (combo == lastCombo)
        {
            return;
        }

        lastCombo = combo;
        Display(combo);
    }

    // 기능: combo의 각 자릿수마다 숫자 이미지를 하나씩 배정해 왼쪽부터 순서대로
    //       보여준다. 0 이하면 전부 숨긴다.
    private void Display(int combo)
    {
        if (combo <= 0)
        {
            SetVisibleCount(0);
            return;
        }

        string digits = combo.ToString();
        EnsurePoolSize(digits.Length);

        for (int i = 0; i < digits.Length; i++)
        {
            int d = digits[i] - '0';
            Image image = digitPool[i];
            image.sprite = digitSprites[d];
            image.gameObject.SetActive(true);
        }

        SetVisibleCount(digits.Length);
    }

    private void EnsurePoolSize(int count)
    {
        while (digitPool.Count < count)
        {
            digitPool.Add(CreateDigitImage());
        }
    }

    // 기능: 숫자 하나를 보여줄 Image를 코드로 직접 만든다 — 고정된 프리팹이
    //       필요 없을 만큼 단순한 구성(RectTransform + Image)이라, SceneLoader가
    //       로딩 오버레이 UI를 코드로 직접 만드는 것과 같은 방식을 쓴다.
    private Image CreateDigitImage()
    {
        var go = new GameObject("Digit", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(digitContainer, false);

        var image = go.GetComponent<Image>();
        image.preserveAspect = true;

        var rt = (RectTransform)go.transform;
        rt.sizeDelta = digitSize;

        // Horizontal Layout Group이 "Control Child Size"를 끄고 있어도 각
        // 이미지의 배치 폭을 알 수 있도록 LayoutElement로 크기를 명시해둔다.
        var layoutElement = go.AddComponent<LayoutElement>();
        layoutElement.preferredWidth = digitSize.x;
        layoutElement.preferredHeight = digitSize.y;

        return image;
    }

    // 기능: 앞의 count개만 켜고 나머지(자릿수가 줄어들며 남은 이전 이미지)는 끈다.
    private void SetVisibleCount(int count)
    {
        for (int i = 0; i < digitPool.Count; i++)
        {
            digitPool[i].gameObject.SetActive(i < count);
        }
    }
}
