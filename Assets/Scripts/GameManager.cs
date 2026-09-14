using UnityEngine;

// ============================================================
// GameManager.cs
//   DJMAX 방식의 핵심 설정을 한곳에 모아둔 컴포넌트.
//
//   특히 중요한 부분: baseScrollSpeed(채보/판정과 무관한 기준 속도)와
//   hiSpeed(플레이어가 조절하는 배속)를 분리해뒀다는 점이다.
//   EffectiveSpeed = baseScrollSpeed * hiSpeed 는 "노트가 화면에서
//   얼마나 빨리 움직여 보이는가"에만 쓰이고, 판정 시각(hitTime) 자체와
//   판정 로직에는 전혀 관여하지 않는다. 그래서 게임 중에 배속을
//   바꿔도 판정 정확도가 흔들리지 않는다.
//
//   변속(SV) 구간이 생기면서, 두 시각 사이의 실제 이동 거리를 구하는
//   일을 이제 GetDistance가 전담한다 — Note.cs와 NoteSpawner.cs는 더 이상
//   (시간 차)*속도 같은 단순 곱셈을 직접 하지 않고 전부 이 메서드를 거친다.
// ============================================================
public class GameManager : MonoBehaviour
{
    [Header("참조")]
    public RhythmClock clock;
    public LaneManager laneManager;

    [Header("스크롤 설정")]
    [Tooltip("hiSpeed 1.0일 때 기준이 되는 스크롤 속도 (유니티 유닛/초)")]
    public float baseScrollSpeed = 8f;

    [Tooltip("플레이어가 조절하는 배속(하이스피드). 표시 속도에만 영향을 주고 판정에는 영향이 없다.")]
    public float hiSpeed = 1f;

    [Header("레이아웃")]
    [Tooltip("판정선의 고정 Y좌표")]
    public float judgmentY = -4f;

    [Tooltip("노트가 처음 나타나는(스폰되는) Y좌표")]
    public float spawnY = 6f;

    // 현재 곡의 변속 구간으로 구워둔 누적 거리 테이블. SetChart가 곡이 바뀔 때마다 새로 만든다.
    private ChartSpeedTable speedTable;

    // 기능: 노트 이동/스폰 계산에 쓰이는 "변속 없는 기준 속도"를 계산해서 돌려준다.
    // 원리: 매 프레임 새로 계산하는 프로퍼티(get-only)라서, hiSpeed를 실행 중에
    //       바꿔도 다음 프레임부터 바로 반영된다 — 별도의 갱신 로직이 필요 없다.
    //       변속 구간 자체의 배율은 여기 포함되지 않는다 — GetDistance에서
    //       speedTable 조회 결과에 이 값을 곱해서 최종 거리를 만든다.
    public float EffectiveSpeed => baseScrollSpeed * hiSpeed;

    // 기능: 새 곡(채보)으로 전환될 때 SongLoader가 호출해서, 그 채보의 변속
    //       구간으로 누적 거리 테이블을 다시 굽는다.
    // 원리: chart가 null이거나 변속 구간이 비어있어도 ChartSpeedTable 생성자가
    //       "곡 전체 배속 1배"로 안전하게 처리하므로, 변속이 없는 기존 채보들도
    //       그대로 동일하게 동작한다(하위 호환).
    public void SetChart(ChartData chart)
    {
        speedTable = new ChartSpeedTable(chart != null ? chart.speedChanges : null);
    }

    // 기능: t1부터 t2까지 노트가 실제로 이동해야 하는 화면 거리(월드 유닛)를 계산한다.
    //       Note.cs(위치 계산)와 NoteSpawner.cs(스폰 타이밍)가 공통으로 이 메서드를 쓴다.
    // 원리: speedTable.GetDistance는 "채보의 변속 배율만 반영한, 아직 실제
    //       유닛이 아닌 거리"를 돌려준다. 여기에 EffectiveSpeed(기준 속도 *
    //       배속)를 곱해야 비로소 화면에서 실제로 몇 유닛을 움직여야 하는지가
    //       나온다. 두 시각의 결과를 빼는 이유(S(t2)-S(t1))는, 그 사이에
    //       변속 지점이 몇 개가 끼어있든 상관없이 정확한 구간 이동 거리가
    //       되기 때문이다(적분의 성질). SetChart가 아직 호출되지 않은
    //       경우(speedTable == null)를 대비해, 변속이 아예 없다고 가정한
    //       기존 공식으로 안전하게 대체한다.
    public float GetDistance(float t1, float t2)
    {
        if (speedTable == null)
        {
            return (t2 - t1) * EffectiveSpeed;
        }
        return (speedTable.GetDistance(t2) - speedTable.GetDistance(t1)) * EffectiveSpeed;
    }

    // 기능: 씬이 시작될 때 NoteRegistry(레인별 노트 명부)를 레인 개수만큼 초기화한다.
    // 원리: Awake는 씬의 모든 오브젝트에서 Start보다 먼저 실행되는 것이 보장되므로,
    //       InputJudge나 Note가 NoteRegistry를 쓰기 시작하는(Start/Update) 시점보다
    //       반드시 앞서 초기화가 끝나 있게 하기 위해 여기서 처리한다.
    void Awake()
    {
        NoteRegistry.Init(laneManager.LaneCount);
    }
}
