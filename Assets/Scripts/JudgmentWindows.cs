// ============================================================
// JudgmentWindows.cs
//   판정 등급과, 각 등급을 가르는 시간 오차 허용 범위(초).
//
//   DJMAX 방식의 판정은 결국 이것 하나뿐이다:
//   "|현재 시각 - hitTime|이 어느 범위 안에 들어오는가"
//   판정선이 고정이든(DJMAX) 움직이든(Phigros) 이 비교 자체는 동일하다 —
//   차이는 화면에 노트가 "어디에 보이는가"를 계산하는 방식에만 있다.
// ============================================================
public enum Grade
{
    Perfect,
    Great,
    Good,
    Miss,
}

public static class JudgmentWindows
{
    // 실제 상용 VSRG들은 보통 ±16ms~50ms 수준의 좁은 창을 쓰지만,
    // 여기서는 이해하기 쉽도록 넉넉한 값으로 시작한다. 실제 게임에
    // 맞게 조정해서 쓰면 된다.
    public const float PerfectWindow = 0.033f; // ±33ms
    public const float GreatWindow = 0.066f;   // ±66ms
    public const float GoodWindow = 0.100f;    // ±100ms

    // 이 범위를 넘어서면 더 이상 판정 대상이 아니라 자동으로 Miss가 된다.
    // (Note.cs가 이 값을 기준으로 스스로 사라진다)
    public const float MissWindow = 0.150f;    // ±150ms

    // 시간 오차(절대값)를 넣으면 해당하는 등급을 돌려준다.
    public static Grade Evaluate(float timeDiffAbs)
    {
        if (timeDiffAbs <= PerfectWindow) return Grade.Perfect;
        if (timeDiffAbs <= GreatWindow) return Grade.Great;
        if (timeDiffAbs <= GoodWindow) return Grade.Good;
        return Grade.Miss;
    }
}
