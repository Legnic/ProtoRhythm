using UnityEngine;
using UnityEngine.UI;

// ============================================================
// OptionsUI.cs
//   옵션 화면의 탭("키 매핑"/"판정 보정"/"볼륨") 전환과 돌아가기 버튼을
//   담당한다. 각 탭은 패널 하나씩(GameObject)이고, 탭 버튼을 누르면 그
//   패널만 켜고 나머지는 꺼서 전환한다 — SongSelectUI가 곡 목록/난이도
//   목록을 ClearList()로 갈아끼우는 것과 달리, 여기는 패널을 모두 미리
//   만들어두고 SetActive만 토글하는 더 단순한 방식이다(패널 내용이
//   동적으로 몇 개가 될지 모르는 목록이 아니라 고정된 화면 몇 개뿐이라
//   이렇게 해도 충분하다).
// ============================================================
public class OptionsUI : MonoBehaviour
{
    [Header("탭 버튼")]
    public Button keyMappingTabButton;
    public Button calibrationTabButton;
    public Button volumeTabButton;

    [Header("패널")]
    public GameObject keyMappingPanel;
    public GameObject calibrationPanel;
    public GameObject volumePanel;

    [Header("버튼")]
    public Button backButton;

    // 기능: 씬이 시작될 때 버튼 클릭을 연결하고, 기본 탭(일반)을 보여준다.
    void Start()
    {
        if (keyMappingTabButton != null) keyMappingTabButton.onClick.AddListener(ShowKeyMapping);
        if (calibrationTabButton != null) calibrationTabButton.onClick.AddListener(ShowCalibration);
        if (volumeTabButton != null) volumeTabButton.onClick.AddListener(ShowVolume);
        if (backButton != null) backButton.onClick.AddListener(() => SceneLoader.Load(this, "TitleScene"));

        ShowVolume();
    }

    // 기능: "키 매핑" 탭을 보여준다.
    public void ShowKeyMapping()
    {
        if (keyMappingPanel != null) keyMappingPanel.SetActive(true);
        if (calibrationPanel != null) calibrationPanel.SetActive(false);
        if (volumePanel != null) volumePanel.SetActive(false);
    }

    // 기능: "판정 보정" 탭을 보여준다.
    // 원리: CalibrationPanel이 켜지는 순간 그 위의 CalibrationController가
    //       OnEnable로 스스로 메트로놈 재생을 시작한다 — 여기서는 그냥
    //       SetActive만 하면 된다.
    public void ShowCalibration()
    {
        if (keyMappingPanel != null) keyMappingPanel.SetActive(false);
        if (calibrationPanel != null) calibrationPanel.SetActive(true);
        if (volumePanel != null) volumePanel.SetActive(false);
    }

    // 기능: "볼륨" 탭을 보여준다.
    public void ShowVolume()
    {
        if (keyMappingPanel != null) keyMappingPanel.SetActive(false);
        if (calibrationPanel != null) calibrationPanel.SetActive(false);
        if (volumePanel != null) volumePanel.SetActive(true);
    }
}
