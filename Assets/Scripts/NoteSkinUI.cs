using UnityEngine;
using UnityEngine.UI;

// ============================================================
// NoteSkinUI.cs
//   옵션 "일반" 탭 맨 아래, 노트 모양(스킨)을 고르는 라디오 버튼
//   (사각형/원형) 두 개의 선택 처리를 담당한다. 버튼 자체는 두
//   Toggle이 하나의 ToggleGroup에 묶여 있어 항상 하나만 켜진다
//   (ToggleGroup.allowSwitchOff = false로 "아예 둘 다 꺼짐"을 막는다) —
//   그래서 여기서는 켜진 쪽을 설정에 반영하기만 하면 된다.
//
//   실제 적용은 NoteSpawner.GetLaneSprite가 매 노트 스폰 시점에
//   GameSettings.NoteSkin을 다시 읽어서 하므로, 여기서는 설정값을
//   저장하고 라디오 버튼의 On/Off만 맞춰두면 된다.
// ============================================================
public class NoteSkinUI : MonoBehaviour
{
    [Header("라디오 버튼")]
    public Toggle squareToggle;
    public Toggle circleToggle;

    // 기능: 토글 값이 바뀔 때 설정에 반영하도록 연결하고, 저장된 스킨에
    //       맞춰 초기 On/Off를 맞춘다.
    void Start()
    {
        if (squareToggle != null) squareToggle.onValueChanged.AddListener(OnSquareToggled);
        if (circleToggle != null) circleToggle.onValueChanged.AddListener(OnCircleToggled);
        Refresh();
    }

    // 기능: 패널이 다시 보일 때마다(탭 전환 등) 혹시 모를 외부 변경에 맞춰 표시를 갱신한다.
    void OnEnable()
    {
        Refresh();
    }

    private void OnSquareToggled(bool isOn)
    {
        if (isOn) SetSkin(NoteSkin.Square);
    }

    private void OnCircleToggled(bool isOn)
    {
        if (isOn) SetSkin(NoteSkin.Circle);
    }

    private void SetSkin(NoteSkin skin)
    {
        GameSettings.NoteSkin = skin;
        GameSettings.Save();
    }

    // 기능: 토글 On/Off를 현재 설정값에 맞춘다.
    // 원리: Toggle.isOn을 그냥 대입하면 onValueChanged가 다시 호출되어
    //       SetSkin이 불필요하게 재호출될 수 있으므로, 알림 없이 값만
    //       맞추는 SetIsOnWithoutNotify를 쓴다.
    private void Refresh()
    {
        NoteSkin current = GameSettings.NoteSkin;
        if (squareToggle != null) squareToggle.SetIsOnWithoutNotify(current == NoteSkin.Square);
        if (circleToggle != null) circleToggle.SetIsOnWithoutNotify(current == NoteSkin.Circle);
    }
}
