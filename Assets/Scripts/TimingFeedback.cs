using UnityEngine;

// ============================================================
// TimingFeedback.cs
//   판정 점수(Grade/ScoreManager)와는 완전히 별개로, 플레이어에게 "얼마나
//   빠르게/늦게 눌렀는지"만 알려주는 교정용 피드백 시스템.
//
//   판정 등급은 |오차|만으로 정해지지만 FAST/LATE는 오차의 부호로
//   정해진다 — 목적 자체가 다르다(하나는 점수, 하나는 사용자 교정 정보)
//   보니 서로의 계산 결과에 의존하지 않고 완전히 독립적으로 계산한다.
//   호출하는 쪽(InputJudge, Note)도 ScoreManager.ReportHit/Note.Finish와
//   이 클래스의 Report를 각각 따로 호출할 뿐, 두 시스템 사이에 아무 연결
//   고리가 없다.
//
//   자동으로 타임아웃 처리되는 Miss(노트를 아예 누르지 않고 놓친 경우)는
//   호출하는 쪽에서 애초에 Report를 부르지 않는다 — 실제로 입력한 시각이
//   없으니 "얼마나 빠르거나 늦었는지" 말할 수 있는 근거 자체가 없다.
//
//   ±DeadZone 이내는 "오차 없음"으로 보고 FAST/LATE 어느 쪽으로도 집계하지
//   않는다 — 화면에도 안 뜨고 카운트에도 안 잡힌다. 이 값 자체를
//   하드코딩하지 않고 GameSettings(설정값)로 분리해둔 이유는, 나중에
//   밸런스를 조정하거나 사용자에게 노출하고 싶어질 때 이 파일을 건드리지
//   않고 바꿀 수 있게 하기 위해서다.
// ============================================================
public enum TimingFeedbackDisplayMode
{
    Off,          // 표시하지 않음
    FastLate,     // FAST/LATE만 표시 (기본값)
    Milliseconds, // 밀리초 수치까지 표시 (예: +12ms LATE)
}

public readonly struct TimingFeedbackResult
{
    public readonly bool isFast;       // true: FAST(빠름), false: LATE(늦음)
    public readonly float diffSeconds; // 부호 있는 시간 오차(초). 음수=FAST, 양수=LATE
    public readonly int lane;

    public TimingFeedbackResult(bool isFast, float diffSeconds, int lane)
    {
        this.isFast = isFast;
        this.diffSeconds = diffSeconds;
        this.lane = lane;
    }
}

public static class TimingFeedback
{
    // 기능: 데드존 밖의 판정마다(FAST/LATE가 정해진 뒤) 알리는 이벤트.
    public static event System.Action<TimingFeedbackResult> OnFeedback;

    public static int FastCount { get; private set; }
    public static int LateCount { get; private set; }

    // 기능: 부호 있는 시간 오차(초, currentTime - targetTime)를 보고한다.
    //       음수면 목표 시각보다 일찍(FAST), 양수면 늦게(LATE) 입력한 것이다.
    // 원리: ±GameSettings.TimingFeedbackDeadZone 이내는 무시하고 리턴한다 —
    //       이 안쪽은 "사실상 정확히 맞음"으로 보고 FAST/LATE 어느 쪽으로도
    //       세지 않는다(집계와 화면 표시 모두).
    public static void Report(float signedDiffSeconds, int lane)
    {
        if (Mathf.Abs(signedDiffSeconds) <= GameSettings.TimingFeedbackDeadZone)
        {
            return;
        }

        bool isFast = signedDiffSeconds < 0f;
        if (isFast) FastCount++; else LateCount++;

        OnFeedback?.Invoke(new TimingFeedbackResult(isFast, signedDiffSeconds, lane));
    }

    // 기능: 새 곡을 시작할 때 이전 곡의 집계를 지운다(ScoreManager.ResetScore와 같은 시점).
    public static void ResetCounts()
    {
        FastCount = 0;
        LateCount = 0;
    }
}
