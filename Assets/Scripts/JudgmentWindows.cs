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
    // Phigros 수준의 널널한 판정 — Perfect/Good 경계(80ms, 160ms)는 Phigros의
    // 실제 판정 범위를 그대로 따른 것이다. 이 프로젝트에는 Phigros에 없는
    // Great 등급이 하나 더 있어서, 그 사이(120ms)를 자연스럽게 채워 넣었다.
    public const float PerfectWindow = 0.080f; // ±80ms (Phigros Perfect)
    public const float GreatWindow = 0.120f;   // ±120ms
    public const float GoodWindow = 0.160f;    // ±160ms (Phigros Good)

    // 이 범위를 넘어서면 더 이상 판정 대상이 아니라 자동으로 Miss가 된다.
    // (Note.cs가 이 값을 기준으로 스스로 사라진다) Phigros는 Good 밖을 바로
    // Miss로 치므로 GoodWindow와 같은 값을 쓴다.
    public const float MissWindow = 0.160f;    // ±160ms

    // 기능: 시간 오차(절대값, 초)를 넣으면 그에 해당하는 판정 등급을 돌려준다.
    // 원리: 오차가 작은 순서대로(Perfect -> Great -> Good) 문턱값과 비교해서
    //       가장 먼저 만족하는 등급을 그대로 반환한다. 어느 문턱값도 만족하지
    //       못하면(=MissWindow보다도 크면) Miss를 돌려준다. 호출하는 쪽
    //       (InputJudge, Note)이 이미 MissWindow 이내인지 먼저 걸러주는 경우가
    //       많아서, 실제로는 Miss가 나오지 않는 경로에서도 안전하게 호출할 수 있다.
    public static Grade Evaluate(float timeDiffAbs)
    {
        if (timeDiffAbs <= PerfectWindow) return Grade.Perfect;
        if (timeDiffAbs <= GreatWindow) return Grade.Great;
        if (timeDiffAbs <= GoodWindow) return Grade.Good;
        return Grade.Miss;
    }
}
