using System.Collections.Generic;
using UnityEngine;

// ============================================================
// ChartData.cs
//   DJMAX 방식 채보의 핵심: "이 노트가 몇 번 레인에서, 몇 초에
//   정확히 맞아야 하는가"만 담은 아주 단순한 데이터.
//
//   라노타/Phigros처럼 화면 좌표 애니메이션이 필요 없다.
//   노트의 화면 위치는 이 시각(hitTime) 하나로부터 매 프레임
//   계산해내기 때문이다. (Note.cs 참고)
// ============================================================
[System.Serializable]
public struct NoteData
{
    public int lane;          // 레인 번호 (0부터 시작)
    public float hitTime;     // 이 노트가 정확히 판정선과 만나야 하는 시각(초) — 롱노트는 "누르는" 시각
    public float holdDuration; // 0이면 탭 노트. 0보다 크면 이 길이(초)만큼 눌러서 유지해야 하는 롱노트.

    // 기능: NoteData 값을 한 번에 채워서 만든다.
    // 원리: holdDuration은 기본값 0(=탭 노트)이라, 롱노트가 아니라면
    //       마지막 인자를 생략하고 new NoteData(lane, hitTime)처럼 써도 된다.
    public NoteData(int lane, float hitTime, float holdDuration = 0f)
    {
        this.lane = lane;
        this.hitTime = hitTime;
        this.holdDuration = holdDuration;
    }
}

// 기능: 변속(SV) 구간 하나 — 이 시각부터 새 배속이 "계단식으로" 즉시 적용되고,
//       다음 SpeedChange가 나올 때까지(혹은 곡이 끝날 때까지) 그대로 유지된다.
// 원리: 부드러운 가속/감속 곡선을 따로 표현하지 않는다 — StepMania류 VSRG의
//       SV처럼, 이 지점들을 충분히 촘촘하게 찍으면 계단식만으로도 사람 눈에는
//       부드러운 가감속처럼 보인다. 실제 거리 계산(ChartSpeedTable)은
//       GameManager.cs 참고.
[System.Serializable]
public struct SpeedChange
{
    public float time;       // 이 시각부터 새 배속이 적용된다.
    public float multiplier; // 기준 스크롤 속도(baseScrollSpeed)에 곱해지는 배속. 1이 기본.

    public SpeedChange(float time, float multiplier)
    {
        this.time = time;
        this.multiplier = multiplier;
    }
}

// ScriptableObject로 만들어두면 Unity 에디터에서 채보 애셋을 파일처럼
// 관리할 수 있다 (Assets 폴더에 저장, 여러 씬에서 재사용 가능).
[CreateAssetMenu(fileName = "NewChart", menuName = "Rhythm/Chart Data")]
public class ChartData : ScriptableObject
{
    [Tooltip("판정 시각(hitTime) 순으로 정렬되어 있어야 한다. SortByTime()으로 정렬 가능.")]
    public List<NoteData> notes = new List<NoteData>();

    [Tooltip("변속 구간 목록. 비어있으면 곡 전체가 배속 1배(=변속 없음)로 취급된다.")]
    public List<SpeedChange> speedChanges = new List<SpeedChange>();

    // Chart Editor의 격자 계산(secondsPerStep)에만 쓰는 값이다 — 판정/재생에는
    // 전혀 관여하지 않는다. 이 채보를 만들 때 쓴 BPM/오프셋을 여기 저장해두면,
    // Chart Editor가 "불러오기" 할 때 자동으로 채워줘서 매번 다시 입력할 필요가 없다.
    [Tooltip("이 채보를 만들 때 쓴 BPM. 외부 사이트 등에서 실제 곡의 BPM을 측정해 입력한다.")]
    public float bpm = 120f;
    [Tooltip("이 채보를 만들 때 쓴 시작 오프셋(초).")]
    public float offsetSeconds = 0f;
    [Tooltip("Chart Editor 격자의 레인 수(편집 화면 표시용).")]
    public int laneCount = 4;
    [Tooltip("Chart Editor 격자의 한 마디당 박자 수.")]
    public int beatsPerMeasure = 4;
    [Tooltip("Chart Editor 격자의 박자 세분화 (1=4분, 2=8분, 4=16분, 8=32분, 16=64분음표).")]
    public int subdivision = 4;
    [Tooltip("Chart Editor 격자의 마디 수.")]
    public int measureCount = 8;

    // 기능: notes 리스트를 hitTime이 빠른 순서대로 정렬한다.
    // 원리: NoteSpawner는 "포인터 하나로 앞에서부터 훑는" 방식으로 노트를 스폰하기
    //       때문에(NoteSpawner.cs 참고), 리스트가 시간순이 아니면 늦게 나와야 할
    //       노트가 먼저 스폰되거나 아예 스폰이 안 되는 문제가 생긴다. List<T>.Sort의
    //       기본 비교자 대신 hitTime끼리 비교하는 람다를 넘겨서 정렬 기준을 지정한다.
    public void SortByTime()
    {
        notes.Sort((a, b) => a.hitTime.CompareTo(b.hitTime));
    }

}
