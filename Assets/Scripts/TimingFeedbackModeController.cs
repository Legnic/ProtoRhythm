using UnityEngine;
using UnityEngine.UI;

// ============================================================
// TimingFeedbackModeController.cs
//   옵션 화면(볼륨 탭)의 "FAST/LATE 표시" 설정 — 끔 / FAST·LATE만 /
//   밀리초까지 표시 세 가지 중 하나를 고른다. 볼륨 탭에 함께 두는
//   이유는 둘 다 "플레이 중 화면·소리를 어떻게 보여줄지"를 정하는
//   비슷한 성격의 설정이기 때문이다.
//
//   버튼 3개 중 하나를 누르면 즉시 GameSettings에 저장한다 — 볼륨
//   슬라이더나 키 매핑 탭과 같은 방식이라 별도의 "저장" 버튼이 없다.
//   현재 선택된 버튼은 색으로 표시한다(라디오 버튼 대용).
// ============================================================
public class TimingFeedbackModeController : MonoBehaviour
{
    [Header("버튼")]
    public Button offButton;
    public Button fastLateButton;
    public Button millisecondsButton;

    [Header("선택 표시")]
    public Color selectedColor = new Color(0.4f, 0.7f, 1f);
    public Color normalColor = Color.white;

    // 기능: 리스너는 한 번만 연결한다 — 이 패널은 탭 전환 때마다 SetActive로
    //       꺼졌다 켜지므로, OnEnable에서 연결하면 중복으로 쌓인다.
    void Awake()
    {
        if (offButton != null) offButton.onClick.AddListener(() => SetMode(TimingFeedbackDisplayMode.Off));
        if (fastLateButton != null) fastLateButton.onClick.AddListener(() => SetMode(TimingFeedbackDisplayMode.FastLate));
        if (millisecondsButton != null) millisecondsButton.onClick.AddListener(() => SetMode(TimingFeedbackDisplayMode.Milliseconds));
    }

    // 기능: 이 패널이 켜질 때마다 저장된 설정으로 선택 표시를 맞춘다.
    void OnEnable()
    {
        RefreshButtons(GameSettings.TimingFeedbackDisplayMode);
    }

    private void SetMode(TimingFeedbackDisplayMode mode)
    {
        GameSettings.TimingFeedbackDisplayMode = mode;
        GameSettings.Save();
        RefreshButtons(mode);
    }

    private void RefreshButtons(TimingFeedbackDisplayMode mode)
    {
        SetSelected(offButton, mode == TimingFeedbackDisplayMode.Off);
        SetSelected(fastLateButton, mode == TimingFeedbackDisplayMode.FastLate);
        SetSelected(millisecondsButton, mode == TimingFeedbackDisplayMode.Milliseconds);
    }

    private void SetSelected(Button button, bool selected)
    {
        if (button == null) return;
        Image image = button.GetComponent<Image>();
        if (image != null)
        {
            image.color = selected ? selectedColor : normalColor;
        }
    }
}
