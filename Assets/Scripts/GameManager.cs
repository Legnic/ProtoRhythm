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

    // 기능: 노트 이동/스폰 계산에 실제로 쓰이는 최종 속도를 계산해서 돌려준다.
    // 원리: 매 프레임 새로 계산하는 프로퍼티(get-only)라서, hiSpeed를 실행 중에
    //       바꿔도 다음 프레임부터 바로 반영된다 — 별도의 갱신 로직이 필요 없다.
    public float EffectiveSpeed => baseScrollSpeed * hiSpeed;

    // 기능: 씬이 시작될 때 NoteRegistry(레인별 노트 명부)를 레인 개수만큼 초기화한다.
    // 원리: Awake는 씬의 모든 오브젝트에서 Start보다 먼저 실행되는 것이 보장되므로,
    //       InputJudge나 Note가 NoteRegistry를 쓰기 시작하는(Start/Update) 시점보다
    //       반드시 앞서 초기화가 끝나 있게 하기 위해 여기서 처리한다.
    void Awake()
    {
        NoteRegistry.Init(laneManager.LaneCount);
    }
}
