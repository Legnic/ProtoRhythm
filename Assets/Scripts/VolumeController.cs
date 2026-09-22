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
//   따른다). 슬라이더를 움직이면 바로 소리와 설정값(메모리)에 반영한다.
//
//   디스크 저장(GameSettings.Save)은 드래그 중에는 하지 않는다 — 슬라이더의
//   onValueChanged는 드래그하는 동안 매 프레임 호출되는데, 그때마다
//   PlayerPrefs.Save()(동기 디스크 쓰기)를 부르면 드래그하는 내내 디스크
//   I/O가 반복된다(CalibrationController가 슬라이더 조작 중엔 저장하지
//   않고 "저장" 버튼에서만 Save()하는 것과 같은 이유로 피해야 하는
//   패턴). 대신 dirty 플래그만 세워두고, 이 패널을 벗어나는 시점
//   (OnDisable, 탭 전환/뒤로가기)에 한 번만 실제로 디스크에 쓴다 —
//   가장 최신 값은 이미 GameSettings.MasterVolume(메모리)에 반영되어
//   있으므로, 앱이 비정상 종료돼도 유니티가 OnApplicationQuit 시점에
//   최종적으로 flush해주는 안전망은 그대로 유지된다.
// ============================================================
public class VolumeController : MonoBehaviour
{
    [Header("참조")]
    public Slider masterVolumeSlider;
    public TMP_Text valueLabel;

    private bool dirty;

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
        dirty = false;
        float saved = GameSettings.MasterVolume;

        if (masterVolumeSlider != null)
        {
            masterVolumeSlider.SetValueWithoutNotify(saved);
        }
        UpdateLabel(saved);
    }

    // 기능: 이 패널을 벗어날 때(탭 전환/뒤로가기) 드래그 중 미뤄뒀던 저장을 확정한다.
    void OnDisable()
    {
        if (dirty)
        {
            GameSettings.Save();
            dirty = false;
        }
    }

    // 기능: 슬라이더 값이 바뀔 때마다 즉시 실제 소리 크기와 설정값(메모리)에
    //       반영한다. 디스크 저장은 미뤄뒀다가 OnDisable에서 한 번에 한다.
    public void OnVolumeChanged(float value)
    {
        AudioListener.volume = value;
        GameSettings.MasterVolume = value;
        dirty = true;
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
