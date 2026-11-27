using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.AddressableAssets;
using TMPro;

// ============================================================
// VolumeController.cs
//   옵션 화면의 "사운드" 탭 — 모든 음량 조절(전체 볼륨, 효과음 on/off와
//   효과음 음량)이 여기에 모여 있다. 전체 볼륨은 개별 AudioSource(BGM, 효과음 등)를 하나씩 따로 관리하는
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

    [Header("효과음 (키 입력 bells)")]
    public Slider sfxVolumeSlider;
    public TMP_Text sfxValueLabel;
    [Tooltip("같은 ToggleGroup에 묶인 켜기/끄기 라디오 버튼.")]
    public Toggle sfxOnToggle;
    public Toggle sfxOffToggle;
    [Tooltip("효과음 볼륨 슬라이더에서 손을 뗄 때 들려줄 미리듣기 소리(Addressables, 플레이 중 키 효과음과 같은 클립).")]
    public AssetReferenceT<AudioClip> sfxPreviewClip;

    private bool dirty;
    private AudioSource previewSource;
    private AudioClip previewClip;
    private readonly AddressableAssetSlot<AudioClip> previewSlot = new AddressableAssetSlot<AudioClip>();

    void Awake()
    {
        // 미리듣기: 클립은 비동기로 미리 받아두고, 슬라이더에서 포인터를 뗄 때 한 번 재생한다.
        previewSource = gameObject.AddComponent<AudioSource>();
        previewSource.playOnAwake = false;
        previewSource.spatialBlend = 0f;
        previewSlot.Load(sfxPreviewClip, clip => previewClip = clip);

        if (sfxVolumeSlider != null)
        {
            var trigger = sfxVolumeSlider.gameObject.GetComponent<EventTrigger>();
            if (trigger == null) trigger = sfxVolumeSlider.gameObject.AddComponent<EventTrigger>();
            var entry = new EventTrigger.Entry { eventID = EventTriggerType.PointerUp };
            entry.callback.AddListener(_ => PlaySfxPreview());
            trigger.triggers.Add(entry);
        }

        if (masterVolumeSlider != null)
        {
            masterVolumeSlider.onValueChanged.AddListener(OnVolumeChanged);
        }
        if (sfxVolumeSlider != null) sfxVolumeSlider.onValueChanged.AddListener(OnSfxVolumeChanged);
        if (sfxOnToggle != null) sfxOnToggle.onValueChanged.AddListener(isOn => { if (isOn) SetSfxEnabled(true); });
        if (sfxOffToggle != null) sfxOffToggle.onValueChanged.AddListener(isOn => { if (isOn) SetSfxEnabled(false); });
    }

    void OnDestroy()
    {
        previewSlot.Release();
    }

    // 기능: 효과음 볼륨 슬라이더에서 손을 뗄 때 현재 음량으로 효과음을 한 번 들려준다.
    // 원리: 전체 볼륨(AudioListener.volume)은 자동으로 곱해지므로 여기서는 효과음
    //       음량만 곱한다. 효과음이 꺼져 있어도 음량을 맞춰볼 수 있게 항상 재생한다.
    private void PlaySfxPreview()
    {
        if (previewClip != null)
        {
            previewSource.PlayOneShot(previewClip, GameSettings.SfxVolume);
        }
    }

    // 효과음 on/off는 드래그처럼 연속으로 호출되지 않으므로 바로 저장한다.
    private void SetSfxEnabled(bool enabled)
    {
        GameSettings.SfxEnabled = enabled;
        GameSettings.Save();
    }

    // 마스터 볼륨과 같은 이유로 드래그 중에는 메모리에만 반영하고 OnDisable에서 저장한다.
    private void OnSfxVolumeChanged(float value)
    {
        GameSettings.SfxVolume = value;
        dirty = true;
        UpdateSfxLabel(value);
    }

    private void UpdateSfxLabel(float value)
    {
        if (sfxValueLabel != null)
        {
            sfxValueLabel.text = $"효과음 볼륨: {Mathf.RoundToInt(value * 100f)}%";
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

        float sfx = GameSettings.SfxVolume;
        if (sfxVolumeSlider != null) sfxVolumeSlider.SetValueWithoutNotify(sfx);
        UpdateSfxLabel(sfx);
        bool sfxOn = GameSettings.SfxEnabled;
        if (sfxOnToggle != null) sfxOnToggle.SetIsOnWithoutNotify(sfxOn);
        if (sfxOffToggle != null) sfxOffToggle.SetIsOnWithoutNotify(!sfxOn);
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
