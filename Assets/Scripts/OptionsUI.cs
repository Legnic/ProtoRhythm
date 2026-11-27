using UnityEngine;
using UnityEngine.UI;

// ============================================================
// OptionsUI.cs
//   옵션 화면의 탭("일반"/"사운드"/"판정 보정"/"키 매핑") 전환과 돌아가기
//   버튼을 담당한다. 각 탭은 패널 하나씩(GameObject)이고, 탭 버튼을 누르면 그
//   패널만 켜고 나머지는 꺼서 전환한다 — SongSelectUI가 곡 목록/난이도
//   목록을 ClearList()로 갈아끼우는 것과 달리, 여기는 패널을 모두 미리
//   만들어두고 SetActive만 토글하는 더 단순한 방식이다(패널 내용이
//   동적으로 몇 개가 될지 모르는 목록이 아니라 고정된 화면 몇 개뿐이라
//   이렇게 해도 충분하다).
//
//   참고: volumePanel/volumeTabButton은 "일반" 탭이다(이름은 예전에 이 탭이
//   볼륨 전용이던 흔적). 음량 조절은 이제 soundPanel(사운드 탭)에 있다.
// ============================================================
public class OptionsUI : MonoBehaviour
{
    [Header("탭 버튼")]
    public Button keyMappingTabButton;
    public Button calibrationTabButton;
    public Button volumeTabButton;
    public Button soundTabButton;

    [Header("패널")]
    public GameObject keyMappingPanel;
    public GameObject calibrationPanel;
    public GameObject volumePanel;
    public GameObject soundPanel;

    [Header("버튼")]
    public Button backButton;

    // 기능: 씬이 시작될 때 버튼 클릭을 연결하고, 기본 탭(일반)을 보여준다.
    void Start()
    {
        if (keyMappingTabButton != null) keyMappingTabButton.onClick.AddListener(ShowKeyMapping);
        if (calibrationTabButton != null) calibrationTabButton.onClick.AddListener(ShowCalibration);
        if (volumeTabButton != null) volumeTabButton.onClick.AddListener(ShowVolume);
        if (soundTabButton != null) soundTabButton.onClick.AddListener(ShowSound);
        if (backButton != null) backButton.onClick.AddListener(() => SceneLoader.Load(this, "TitleScene"));

        ShowVolume();
    }

    // 기능: "키 매핑" 탭을 보여준다.
    public void ShowKeyMapping() => Show(keyMappingPanel);

    // 기능: "판정 보정" 탭을 보여준다.
    // 원리: CalibrationPanel이 켜지는 순간 그 위의 CalibrationController가
    //       OnEnable로 스스로 메트로놈 재생을 시작한다 — 여기서는 그냥
    //       SetActive만 하면 된다.
    public void ShowCalibration() => Show(calibrationPanel);

    // 기능: "일반" 탭을 보여준다.
    public void ShowVolume() => Show(volumePanel);

    // 기능: "사운드" 탭(전체 볼륨, 효과음 on/off와 음량)을 보여준다.
    public void ShowSound() => Show(soundPanel);

    // 기능: 지정한 패널만 켜고 나머지는 전부 끈다.
    private void Show(GameObject target)
    {
        SetActive(keyMappingPanel, target);
        SetActive(calibrationPanel, target);
        SetActive(volumePanel, target);
        SetActive(soundPanel, target);
    }

    private static void SetActive(GameObject panel, GameObject target)
    {
        if (panel != null) panel.SetActive(panel == target);
    }
}
