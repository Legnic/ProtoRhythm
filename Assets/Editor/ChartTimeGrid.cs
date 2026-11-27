using System;
using System.Collections.Generic;

// ============================================================
// ChartTimeGrid.cs
//   Chart Editor 격자의 "칸(step) <-> 시각(초)" 변환만 담당하는 순수 계산 클래스.
//   UnityEngine/UnityEditor에 의존하지 않아서 에디터 밖에서도 그대로 테스트할 수 있다.
//
//   두 가지 방식을 지원한다.
//   - 고정 템포: offsetSeconds + step * (60/bpm/subdivision). 일반적인 곡.
//   - 템포 맵: beatTimes[i] = i번째 박(0번이 step 0)의 실제 시각. 박 사이는 선형
//     보간하고, 맵 앞뒤 범위 밖은 가장 가까운 박 간격으로 연장한다. 템포가 곡
//     진행에 따라 조금씩 변하는 곡(예: ChipTune)에서 고정 BPM 격자를 쓰면 곡 뒤로
//     갈수록 격자가 실제 박에서 수십 ms씩 어긋나는데, 템포 맵을 쓰면 모든 칸이
//     실제 박 위치에 놓인다.
// ============================================================
public sealed class ChartTimeGrid
{
    private readonly double bpm;
    private readonly double offsetSeconds;
    private readonly int subdivision;
    private readonly IReadOnlyList<float> beatTimes;

    public ChartTimeGrid(double bpm, double offsetSeconds, int subdivision, IReadOnlyList<float> beatTimes)
    {
        this.bpm = Math.Max(1.0, bpm);
        this.offsetSeconds = offsetSeconds;
        this.subdivision = Math.Max(1, subdivision);
        this.beatTimes = beatTimes != null && beatTimes.Count >= 2 ? beatTimes : null;
    }

    public bool UsesTempoMap => beatTimes != null;

    // 기능: 칸 번호(소수 허용)를 시각(초)으로 바꾼다.
    public double StepToTime(double step)
    {
        double beat = step / subdivision;
        if (beatTimes == null) return offsetSeconds + beat * 60.0 / bpm;

        int last = beatTimes.Count - 1;
        if (beat <= 0) return beatTimes[0] + beat * (beatTimes[1] - beatTimes[0]);
        if (beat >= last) return beatTimes[last] + (beat - last) * (beatTimes[last] - beatTimes[last - 1]);
        int i = (int)Math.Floor(beat);
        double frac = beat - i;
        return beatTimes[i] + frac * (beatTimes[i + 1] - beatTimes[i]);
    }

    // 기능: 시각(초)을 칸 번호(소수)로 바꾼다. StepToTime의 정확한 역함수다.
    // 원리: 템포 맵에서는 박 시각 목록이 오름차순이므로 이진 탐색으로 구간을 찾고
    //       그 구간 안에서 선형 보간을 거꾸로 푼다.
    public double TimeToStep(double time)
    {
        if (beatTimes == null) return (time - offsetSeconds) * bpm / 60.0 * subdivision;

        int last = beatTimes.Count - 1;
        double beat;
        if (time <= beatTimes[0]) beat = (time - beatTimes[0]) / (beatTimes[1] - beatTimes[0]);
        else if (time >= beatTimes[last]) beat = last + (time - beatTimes[last]) / (beatTimes[last] - beatTimes[last - 1]);
        else
        {
            int lo = 0, hi = last;
            while (hi - lo > 1)
            {
                int mid = (lo + hi) / 2;
                if (beatTimes[mid] <= time) lo = mid; else hi = mid;
            }
            beat = lo + (time - beatTimes[lo]) / (beatTimes[lo + 1] - beatTimes[lo]);
        }
        return beat * subdivision;
    }

    // 기능: 시각이 가장 가까운 격자 칸에서 몇 초 떨어져 있는지 돌려준다(0이면 격자 위).
    public double DistanceFromGrid(double time)
    {
        double step = TimeToStep(time);
        return Math.Abs(time - StepToTime(Math.Round(step)));
    }

    // 기능: 템포 맵 전체의 평균 BPM(표시용). 맵이 없으면 고정 BPM 그대로.
    public double AverageBpm()
    {
        if (beatTimes == null) return bpm;
        int last = beatTimes.Count - 1;
        return 60.0 * last / (beatTimes[last] - beatTimes[0]);
    }
}
