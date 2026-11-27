using TMPro;
using UnityEngine;
using UnityEngine.UI;

// ============================================================
// ScrollSpeedSlider.cs
//   배속(노트가 떨어지는 속도) 슬라이더. 옵션 화면의 "일반" 탭과 플레이 중
//   일시정지 패널에서 같은 컴포넌트를 쓴다. 슬라이더를 움직이면 즉시
//   GameSettings.ScrollSpeed에 반영되고, 플레이 중이면 GameManager가 그 변경을
//   구독하고 있어서 노트 속도가 바로 바뀐다(GameManager.OnScrollSpeedChanged).
//
//   슬라이더는 0.1배 단위의 정수 눈금(5~50 = 0.5~5.0배)으로 쓴다 — 실수
//   슬라이더를 그대로 쓰면 1.2999 같은 값이 저장되고 표시와 어긋나기 쉽다.
//
//   디스크 저장은 VolumeController와 같은 이유로 드래그 중에는 하지 않고, 이
//   슬라이더가 꺼질 때(탭 전환, 뒤로가기, 일시정지 해제) 한 번만 한다.
// ============================================================
public class ScrollSpeedSlider : MonoBehaviour
{
    [Tooltip("배속을 조절할 슬라이더. 비워두면 같은 오브젝트의 Slider를 쓴다.")]
    public Slider slider;

    [Tooltip("현재 배속을 보여줄 라벨(선택 사항).")]
    public TMP_Text valueLabel;

    private const float StepsPerUnit = 10f;
    private bool dirty;

    void Awake()
    {
        if (slider == null) slider = GetComponent<Slider>();
        if (slider == null) return;

        slider.wholeNumbers = true;
        slider.minValue = Mathf.Round(GameSettings.ScrollSpeedMin * StepsPerUnit);
        slider.maxValue = Mathf.Round(GameSettings.ScrollSpeedMax * StepsPerUnit);

        // 키보드/패드 내비게이션을 끈다 — 일시정지 중 슬라이더가 선택된 채로 남아 있으면
        // 방향키나 WASD(레인 키와 겹칠 수 있다)가 배속을 바꿔버릴 수 있기 때문이다.
        Navigation nav = slider.navigation;
        nav.mode = Navigation.Mode.None;
        slider.navigation = nav;

        slider.onValueChanged.AddListener(OnSliderChanged);
    }

    // 기능: 켜질 때마다 저장된 배속을 슬라이더와 라벨에 반영한다.
    void OnEnable()
    {
        dirty = false;
        float speed = GameSettings.ScrollSpeed;
        if (slider != null) slider.SetValueWithoutNotify(Mathf.Round(speed * StepsPerUnit));
        UpdateLabel(speed);
    }

    // 기능: 꺼질 때 드래그 중 미뤄둔 저장을 확정한다.
    void OnDisable()
    {
        if (dirty)
        {
            GameSettings.Save();
            dirty = false;
        }
    }

    private void OnSliderChanged(float steps)
    {
        float speed = steps / StepsPerUnit;
        GameSettings.ScrollSpeed = speed;
        dirty = true;
        UpdateLabel(GameSettings.ScrollSpeed);
    }

    private void UpdateLabel(float speed)
    {
        if (valueLabel != null)
        {
            // "배속" 대신 "노트 속도"라고 쓰는 이유: Jua SDF 폰트 애셋은 UI에 쓰는 글자만
            // 담은 정적 아틀라스라 '배' 글리프가 없다(□로 보인다). 표기를 바꾸려면 Font
            // Asset Creator로 아틀라스에 글자를 추가해야 한다.
            valueLabel.text = $"노트 속도: x{speed:0.0}";
        }
    }
}
