using UnityEngine;
using UnityEngine.UI;
using TMPro;

// ============================================================
// VolumeController.cs
//   옵션 화면의 "볼륨" 탭 — 전체 볼륨(마스터 볼륨) 하나만 슬라이더로
//   조절한다. 개별 AudioSource(BGM, 효과음 등)를 하나씩 따로 관리하는
//   대신 AudioListener.volume을 직접 조절한다 — 이 값은 씬에 어떤
//   AudioSource가 몇 개 있든 상관없이 그 순간 스피커로 나가는 모든
//   소리에 한꺼번에 곱해지는 전역 배율이라, "전체 볼륨 하나만"이라는
//   요구사항에 정확히 들어맞고 AudioMixer 같은 별도 에셋 없이도 바로
//   적용된다.
//
//   리스너 연결은 Awake에서 한 번만 한다 — 탭을 오갈 때마다 이 패널이
//   SetActive로 꺼졌다 켜지는데, OnEnable에서 연결하면 리스너가 매번
//   중복으로 쌓인다(CalibrationController와 같은 이유로 같은 패턴을
//   따른다). 슬라이더를 움직이면 바로 소리에 반영하고 바로 저장한다 —
//   키 매핑 탭이 레인을 바꿀 때마다 바로 저장하는 것과 같은 방식이라
//   별도의 "저장" 버튼이 없다.
// ============================================================
public class VolumeController : MonoBehaviour
{
    [Header("참조")]
    public Slider masterVolumeSlider;
    public TMP_Text valueLabel;

    void Awake()
    {
        if (masterVolumeSlider != null)
        {
            masterVolumeSlider.onValueChanged.AddListener(OnVolumeChanged);
        }
    }

    // 기능: 이 패널이 켜질 때마다(탭 전환 포함) 저장된 볼륨을 슬라이더에 반영한다.
    void OnEnable()
    {
        float saved = GameSettings.MasterVolume;

        if (masterVolumeSlider != null)
        {
            masterVolumeSlider.SetValueWithoutNotify(saved);
        }
        UpdateLabel(saved);
    }

    // 기능: 슬라이더 값이 바뀔 때마다 즉시 실제 소리 크기에 반영하고 저장한다.
    public void OnVolumeChanged(float value)
    {
        AudioListener.volume = value;
        GameSettings.MasterVolume = value;
        GameSettings.Save();
        UpdateLabel(value);
    }

    private void UpdateLabel(float value)
    {
        if (valueLabel != null)
        {
            valueLabel.text = $"볼륨: {Mathf.RoundToInt(value * 100f)}%";
        }
    }
}
