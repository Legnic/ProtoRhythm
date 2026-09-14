using System.Collections.Generic;
using UnityEngine;

// ============================================================
// ChartSpeedTable.cs
//   ChartData.speedChanges(계단식 변속 지점들)로부터 "0초부터 어떤 시각
//   t까지 이동한 누적 거리"를 미리 구워두고, 이후에는 이진 탐색으로만
//   조회하는 테이블. 변속 구간이 생기면 더 이상 (끝시각-시작시각)*속도
//   처럼 단순 곱셈으로 두 시각 사이의 이동 거리를 구할 수 없다 —
//   그 사이에 배속이 여러 번 바뀔 수 있기 때문이다.
//
//   핵심 아이디어: S(t) = "0초부터 t초까지 이동한 거리"라는 함수를 한 번만
//   구해두면, 임의의 두 시각 t1, t2 사이의 이동 거리는 그냥 S(t2) - S(t1)
//   이다(적분의 성질). 배속이 계단식(구간 내에서 상수)이므로 S(t)는
//   구간별 기울기가 다른 꺾은선(구간 선형) 함수가 되고, 꺾이는 지점(변속
//   지점)에서의 값만 미리 계산해두면 그 사이는 직선 보간(사실상 그냥
//   그 구간의 배속을 곱하기)으로 정확히 구해진다.
//
//   왜 baseScrollSpeed/hiSpeed를 여기 포함하지 않는가:
//     hiSpeed는 플레이 중에도 바뀔 수 있는 값인데, 그때마다 이 테이블을
//     다시 굽는 건 낭비다. 그래서 이 테이블은 "채보 자체의 배속 배율"만
//     담당하고, baseScrollSpeed/hiSpeed는 GameManager.GetDistance에서
//     마지막에 한 번 곱해준다.
// ============================================================
public class ChartSpeedTable
{
    private struct Breakpoint
    {
        public float time;             // 이 변속 지점의 시각
        public float cumulativeDistance; // 0초부터 이 지점까지의 누적 거리(배속 배율 기준, 실제 유닛 아님)
        public float multiplier;       // 이 지점부터 다음 지점 전까지 적용되는 배속 배율
    }

    private readonly List<Breakpoint> breakpoints = new List<Breakpoint>();

    // 기능: speedChanges 목록으로부터 누적 거리 테이블을 굽는다.
    // 원리: 시각순으로 정렬한 뒤, 0초 이전(또는 정확히 0초)에 해당하는
    //       기준점이 없으면 "0초부터 배속 1배"라는 기본 지점을 맨 앞에
    //       끼워 넣는다(그래야 곡 시작부터 값을 물어봐도 항상 답이 나온다).
    //       그다음 앞 지점의 배속으로 "그 지점부터 다음 지점까지" 이동한
    //       거리를 누적해서 각 지점의 cumulativeDistance를 채운다.
    public ChartSpeedTable(List<SpeedChange> speedChanges)
    {
        var sorted = new List<SpeedChange>();
        if (speedChanges != null)
        {
            sorted.AddRange(speedChanges);
        }
        sorted.Sort((a, b) => a.time.CompareTo(b.time));

        if (sorted.Count == 0 || sorted[0].time > 0f)
        {
            sorted.Insert(0, new SpeedChange(0f, 1f));
        }

        float cumulative = 0f;
        float prevTime = sorted[0].time;
        float prevMultiplier = sorted[0].multiplier;
        breakpoints.Add(new Breakpoint { time = prevTime, cumulativeDistance = 0f, multiplier = prevMultiplier });

        for (int i = 1; i < sorted.Count; i++)
        {
            cumulative += prevMultiplier * (sorted[i].time - prevTime);
            breakpoints.Add(new Breakpoint
            {
                time = sorted[i].time,
                cumulativeDistance = cumulative,
                multiplier = sorted[i].multiplier,
            });

            prevTime = sorted[i].time;
            prevMultiplier = sorted[i].multiplier;
        }
    }

    // 기능: 0초부터 time초까지의 누적 거리(배속 배율 기준)를 구한다.
    // 원리: "time보다 작거나 같은 시각을 가진 지점 중 가장 늦은 것"을 이진
    //       탐색으로 찾은 뒤, 그 지점의 누적 거리에 "그 지점부터 time까지"를
    //       그 지점의 배속으로 직선 보간해서 더한다. time이 마지막 지점보다
    //       뒤라면 마지막 지점의 배속을 그대로 곡 끝까지 유지한다고 보고
    //       외삽한다 — "다음 변속 지점이 없으면 이 배속이 계속 유지된다"는
    //       규칙과 일치한다. 지점이 수천 개여도 이진 탐색이라 매 프레임
    //       호출해도 비용이 거의 없다.
    public float GetDistance(float time)
    {
        int lo = 0;
        int hi = breakpoints.Count - 1;
        while (lo < hi)
        {
            int mid = (lo + hi + 1) / 2;
            if (breakpoints[mid].time <= time)
            {
                lo = mid;
            }
            else
            {
                hi = mid - 1;
            }
        }

        Breakpoint bp = breakpoints[lo];
        return bp.cumulativeDistance + bp.multiplier * (time - bp.time);
    }
}
