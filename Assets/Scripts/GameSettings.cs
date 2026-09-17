using UnityEngine;
using UnityEngine.InputSystem;

// ============================================================
// GameSettings.cs
//   앱을 재시작해도 유지되어야 하는 사용자 설정(판정 보정, 키 매핑)을
//   PlayerPrefs로 감싸서 읽고 쓴다. 씬이 바뀌어도 유지되지만 앱을 껐다
//   켜면 사라지는 GameSession과 달리, 이 값들은 디스크에 저장되어 앱을
//   재시작해도 그대로 남아있어야 하는 값이라 별도 클래스로 분리했다.
// ============================================================
public static class GameSettings
{
    private const string AudioOffsetKey = "AudioOffset";
    private const string LaneKeyPrefix = "LaneKey_";

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

    // 기능: 지금까지 바뀐 값을 디스크에 실제로 기록한다.
    // 원리: PlayerPrefs.SetFloat 등은 메모리에만 반영될 수도 있어서, 세션이
    //       비정상 종료되면 저장이 안 남을 수 있다. 값을 바꾼 직후 반드시
    //       호출해서 확실히 디스크에 남긴다.
    public static void Save()
    {
        PlayerPrefs.Save();
    }
}
