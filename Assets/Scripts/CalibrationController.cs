using UnityEngine;
using UnityEngine.UI;
using TMPro;

// ============================================================
// CalibrationController.cs
//   판정 보정(오디오 오프셋) 화면의 핵심 로직 — "틱 소리"와 "깜빡임"을
//   실제 게임(RhythmClock)과 똑같은 dspTime 기반 공식으로 서로 다른
//   기준으로 재생해서, 여기서 맞춘 testOffset을 그대로
//   RhythmClock.audioOffset에 넣으면 실제 플레이에서도 정확히 같은
//   관계로 재현되게 한다.
//
//   틱 소리는 dspStart부터 시작해 tickInterval마다 끝없이 반복되는
//   오디오 클립 하나를 PlayScheduled + loop로 재생한다 — testOffset의
//   영향을 전혀 받지 않는다(실제 게임에서 음악 재생 자체가
//   audioOffset의 영향을 받지 않는 것과 같은 이유. RhythmClock.cs 참고).
//
//   깜빡임은 노트가 판정선에 닿는 순간과 똑같은 방식으로 판단한다 —
//   실제 게임에서는 CurrentTime(=경과시간+audioOffset)이 hitTime에
//   도달하는 순간 노트가 판정선에 닿는다. 여기서는 "몇 번째 틱"이 곧
//   hitTime 역할이라, (경과시간 + testOffset)을 tickInterval로 나눈
//   나머지가 0에 가까워지는(=막 틱 하나를 지난) 순간 짧게 깜빡인다.
// ============================================================
public class CalibrationController : MonoBehaviour
{
    [Header("참조")]
    public AudioSource tickSource;
    public Image flashImage;
    public TMP_Text offsetLabel;
    public Slider offsetSlider;
    public Button saveButton;
    public Button resetButton;

    [Header("설정")]
    [Tooltip("틱(메트로놈) 간격(초).")]
    public float tickInterval = 0.5f;
    [Tooltip("깜빡임이 유지되는 시간(초).")]
    public float flashDuration = 0.08f;
    [Tooltip("슬라이더로 조절 가능한 범위(밀리초, ±).")]
    public float sliderRangeMs = 200f;

    public Color dimColor = new Color(1f, 1f, 1f, 0.15f);
    public Color brightColor = Color.white;

    private double dspStart;
    private float testOffset; // 초 단위. 저장 전까지의 작업 값.
    private AudioClip cachedTickClip;

    // 기능: 오브젝트가 처음 만들어질 때 한 번만 버튼/슬라이더 리스너를 코드로
    //       연결한다. 탭을 오갈 때마다 이 패널이 SetActive로 꺼졌다 켜지므로,
    //       매번 다시 연결되면 리스너가 중복으로 쌓인다 — Awake는 오브젝트가
    //       실제로 다시 만들어질 때만 호출되므로 여기서 한 번만 연결하면 된다
    //       (이 프로젝트 전체에서 쓰는 "코드로 직접 연결" 패턴 — SongSelectUI,
    //       PlaySceneController 등 참고. 재생/초기화 상태 리셋은 OnEnable에서 한다).
    void Awake()
    {
        if (offsetSlider != null) offsetSlider.onValueChanged.AddListener(OnSliderChanged);
        if (saveButton != null) saveButton.onClick.AddListener(Save);
        if (resetButton != null) resetButton.onClick.AddListener(ResetToZero);
    }

    // 기능: 이 패널이 켜질 때마다(탭 전환 포함) 저장된 값을 불러와 슬라이더에
    //       반영하고, 메트로놈 재생을 새로 시작한다.
    void OnEnable()
    {
        testOffset = GameSettings.AudioOffset;

        if (offsetSlider != null)
        {
            offsetSlider.minValue = -sliderRangeMs;
            offsetSlider.maxValue = sliderRangeMs;
            offsetSlider.SetValueWithoutNotify(testOffset * 1000f);
        }
        UpdateLabel();

        if (tickSource != null)
        {
            if (cachedTickClip == null)
            {
                cachedTickClip = GenerateClickClip(tickInterval);
            }
            tickSource.clip = cachedTickClip;
            tickSource.loop = true;
            dspStart = AudioSettings.dspTime + 0.2;
            tickSource.PlayScheduled(dspStart);
        }
    }

    // 기능: 다른 탭으로 전환되어 이 패널이 꺼지면 메트로놈을 멈춘다.
    void OnDisable()
    {
        if (tickSource != null)
        {
            tickSource.Stop();
        }
    }

    void Update()
    {
        double now = AudioSettings.dspTime;
        bool flashActive = false;

        if (now >= dspStart)
        {
            float t = (float)(now - dspStart) + testOffset;
            if (t >= 0f)
            {
                float phase = t % tickInterval;
                if (phase < 0f) phase += tickInterval;
                flashActive = phase < flashDuration;
            }
        }

        if (flashImage != null)
        {
            flashImage.color = flashActive ? brightColor : dimColor;
        }
    }

    // 기능: 슬라이더(밀리초 단위) 값이 바뀔 때 연결한다.
    public void OnSliderChanged(float valueMs)
    {
        testOffset = valueMs / 1000f;
        UpdateLabel();
    }

    // 기능: "초기화" 버튼에 연결한다 — 저장하지 않고 작업 값만 0으로 되돌린다.
    public void ResetToZero()
    {
        testOffset = 0f;
        if (offsetSlider != null) offsetSlider.SetValueWithoutNotify(0f);
        UpdateLabel();
    }

    // 기능: "저장" 버튼에 연결한다 — 지금 작업 값을 실제 설정으로 확정한다.
    public void Save()
    {
        GameSettings.AudioOffset = testOffset;
        GameSettings.Save();
    }

    private void UpdateLabel()
    {
        if (offsetLabel != null)
        {
            offsetLabel.text = $"오프셋: {Mathf.RoundToInt(testOffset * 1000f)}ms";
        }
    }

    // 기능: tickInterval 길이의 무음 버퍼 맨 앞에 짧은 클릭음 하나를 넣은
    //       클립을 만든다. 이 클립을 loop 재생하면 정확히 tickInterval마다
    //       클릭이 반복된다 — 별도의 오디오 애셋이 필요 없다.
    private static AudioClip GenerateClickClip(float intervalSeconds)
    {
        const int sampleRate = 44100;
        const float clickDuration = 0.008f;
        const float clickFrequency = 1500f;

        int totalSamples = Mathf.Max(1, Mathf.RoundToInt(intervalSeconds * sampleRate));
        int clickSamples = Mathf.Min(totalSamples, Mathf.RoundToInt(clickDuration * sampleRate));

        var data = new float[totalSamples];
        for (int i = 0; i < clickSamples; i++)
        {
            float t = i / (float)sampleRate;
            float envelope = 1f - (i / (float)clickSamples);
            data[i] = Mathf.Sin(2f * Mathf.PI * clickFrequency * t) * envelope * 0.8f;
        }

        var clip = AudioClip.Create("CalibrationClick", totalSamples, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
