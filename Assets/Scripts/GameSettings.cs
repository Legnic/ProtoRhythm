using UnityEngine;
using UnityEngine.InputSystem;

// ============================================================
// GameSettings.cs
//   앱을 재시작해도 유지되어야 하는 사용자 설정(판정 보정, 키 매핑, 배속 등)을
//   PlayerPrefs로 감싸서 읽고 쓴다. 씬이 바뀌어도 유지되지만 앱을 껐다
//   켜면 사라지는 GameSession과 달리, 이 값들은 디스크에 저장되어 앱을
//   재시작해도 그대로 남아있어야 하는 값이라 별도 클래스로 분리했다.
// ============================================================
public static class GameSettings
{
    private const string AudioOffsetKey = "AudioOffset";
    private const string LaneKeyPrefix = "LaneKey_";
    private const string MasterVolumeKey = "MasterVolume";
    private const string TimingFeedbackDeadZoneKey = "TimingFeedbackDeadZone";
    private const string TimingFeedbackDisplayModeKey = "TimingFeedbackDisplayMode";
    private const string NoteSkinKey = "NoteSkin";
    private const string SfxEnabledKey = "SfxEnabled";
    private const string SfxVolumeKey = "SfxVolume";
    private const string ScrollSpeedKey = "ScrollSpeed";

    // 배속(노트 낙하 속도) 범위와 단위. 1.0이 기존 기본 속도다.
    public const float ScrollSpeedMin = 0.5f;
    public const float ScrollSpeedMax = 5.0f;
    public const float ScrollSpeedDefault = 1.0f;

    // 배속이 바뀔 때마다 새 값과 함께 알린다 — 플레이 중인 GameManager가 구독해서
    // 일시정지 패널에서 슬라이더를 움직이는 즉시 노트 속도에 반영한다.
    public static event System.Action<float> ScrollSpeedChanged;

    // 기능: 저장된 오디오 오프셋(초)을 읽어온다. 저장된 적이 없으면 0(보정 없음).
    public static float AudioOffset
    {
        get => PlayerPrefs.GetFloat(AudioOffsetKey, 0f);
        set => PlayerPrefs.SetFloat(AudioOffsetKey, value);
    }

    // 기능: 특정 레인에 저장된 키를 읽어온다. 저장된 적이 없으면(사용자가 아직
    //       한 번도 그 레인을 바꾼 적이 없으면) fallback(보통 InputJudge의
    //       인스펙터 기본값)을 그대로 돌려준다.
    // 원리: PlayerPrefs.HasKey로 "저장된 적이 있는지"부터 확인하는 이유는,
    //       저장 안 된 레인에 임의의 기본값(0 등)을 실수로 돌려주면 그게
    //       실제로 Key.None 같은 엉뚱한 키로 해석될 수 있기 때문이다 —
    //       "아직 손대지 않은 레인은 호출한 쪽이 이미 갖고 있는 기본값을
    //       그대로 쓴다"가 항상 안전하다.
    public static Key GetLaneKey(int lane, Key fallback)
    {
        string prefKey = LaneKeyPrefix + lane;
        if (!PlayerPrefs.HasKey(prefKey))
        {
            return fallback;
        }
        return (Key)PlayerPrefs.GetInt(prefKey);
    }

    // 기능: 레인 하나에 새로 배정된 키를 저장한다.
    public static void SetLaneKey(int lane, Key key)
    {
        PlayerPrefs.SetInt(LaneKeyPrefix + lane, (int)key);
    }

    // 기능: 저장된 전체 볼륨(0~1)을 읽어온다. 저장된 적이 없으면 1(최대).
    public static float MasterVolume
    {
        get => PlayerPrefs.GetFloat(MasterVolumeKey, 1f);
        set => PlayerPrefs.SetFloat(MasterVolumeKey, value);
    }

    // 기능: FAST/LATE 판정에서 "오차 없음"으로 취급할 범위(초, ±). 이 안쪽은
    //       화면에도 안 뜨고 결과 화면 집계에도 잡히지 않는다. TimingFeedback.cs
    //       참고 — 하드코딩하지 않고 여기 설정값으로 분리해뒀다. 기본값 20ms.
    public static float TimingFeedbackDeadZone
    {
        get => PlayerPrefs.GetFloat(TimingFeedbackDeadZoneKey, 0.020f);
        set => PlayerPrefs.SetFloat(TimingFeedbackDeadZoneKey, value);
    }

    // 기능: 플레이 화면에서 FAST/LATE를 얼마나 자세히 보여줄지. 기본값은
    //       FastLate(단순 FAST/LATE만) — 밀리초 수치까지 보여주는 건 원하는
    //       사람만 옵션에서 따로 켜도록 한다.
    public static TimingFeedbackDisplayMode TimingFeedbackDisplayMode
    {
        get => (TimingFeedbackDisplayMode)PlayerPrefs.GetInt(TimingFeedbackDisplayModeKey, (int)TimingFeedbackDisplayMode.FastLate);
        set => PlayerPrefs.SetInt(TimingFeedbackDisplayModeKey, (int)value);
    }

    // 기능: 저장된 노트 모양 스킨을 읽어온다. 저장된 적이 없으면 Square(기본값).
    public static NoteSkin NoteSkin
    {
        get => (NoteSkin)PlayerPrefs.GetInt(NoteSkinKey, (int)NoteSkin.Square);
        set => PlayerPrefs.SetInt(NoteSkinKey, (int)value);
    }

    // 기능: 키 효과음(bells) 켜짐 여부. 저장된 적이 없으면 켜짐.
    public static bool SfxEnabled
    {
        get => PlayerPrefs.GetInt(SfxEnabledKey, 1) != 0;
        set => PlayerPrefs.SetInt(SfxEnabledKey, value ? 1 : 0);
    }

    // 기능: 효과음 음량(0~1). 마스터 볼륨(AudioListener.volume)과 곱해져서 최종 크기가 된다.
    public static float SfxVolume
    {
        get => PlayerPrefs.GetFloat(SfxVolumeKey, 1f);
        set => PlayerPrefs.SetFloat(SfxVolumeKey, value);
    }

    // 기능: 배속(GameManager.hiSpeed에 들어가는 값). 저장된 적이 없으면 1.0(기존 속도).
    // 원리: 0.1 단위로 반올림하고 범위 안으로 잘라서 저장한다 — 슬라이더와 저장값이
    //       항상 같은 눈금에 있게 하기 위해서다. 값을 쓰면 ScrollSpeedChanged로 바로
    //       알리므로, 플레이 중에 바꿔도 다음 프레임부터 노트 속도가 바뀐다.
    public static float ScrollSpeed
    {
        get => Mathf.Clamp(PlayerPrefs.GetFloat(ScrollSpeedKey, ScrollSpeedDefault), ScrollSpeedMin, ScrollSpeedMax);
        set
        {
            float v = Mathf.Clamp(Mathf.Round(value * 10f) / 10f, ScrollSpeedMin, ScrollSpeedMax);
            PlayerPrefs.SetFloat(ScrollSpeedKey, v);
            ScrollSpeedChanged?.Invoke(v);
        }
    }

    // 기능: 지금까지 바뀐 값을 디스크에 실제로 기록한다.
    // 원리: PlayerPrefs.SetFloat 등은 메모리에만 반영될 수도 있어서, 세션이
    //       비정상 종료되면 저장이 안 남을 수 있다. 값을 바꾼 직후 반드시
    //       호출해서 확실히 디스크에 남긴다.
    public static void Save()
    {
        PlayerPrefs.Save();
    }
}
