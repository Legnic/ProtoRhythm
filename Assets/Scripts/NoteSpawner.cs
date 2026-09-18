using System.Collections;
using UnityEngine;

// ============================================================
// NoteSpawner.cs
//   채보의 각 노트를 "적절한 시점"에 생성(Instantiate)한다.
//
//   "적절한 시점"의 계산 (변속 대응):
//     예전에는 "spawnY에서 judgmentY까지 이동하는 데 걸리는 시간
//     (travelTime)"을 미리 구해서 (hitTime - travelTime) 시점에
//     스폰하는 식이었다. 하지만 변속 구간이 있으면 같은 거리를
//     이동하는 데 걸리는 시간이 그때그때 다르므로 이 방식이 더 이상
//     맞지 않는다. 그래서 "몇 초 전에 스폰할까"가 아니라 "지금부터
//     hitTime까지 남은 이동 거리가 화면에 보일 범위(spawnDistance)
//     이내로 들어왔는가"로 조건을 바꿨다 — GameManager.GetDistance가
//     구간 안의 변속을 전부 반영해서 정확한 남은 거리를 돌려주므로,
//     이 조건 하나만으로 변속 유무와 상관없이 항상 올바른 시점에
//     스폰된다.
//
//   chart.notes는 시간순으로 정렬되어 있다고 가정하고,
//   포인터(nextNoteIndex)를 하나만 유지해 전체를 한 번만 훑는다.
// ============================================================
public class NoteSpawner : MonoBehaviour
{
    [Header("참조")]
    public GameManager gameManager;
    public LaneManager laneManager;
    public ChartData chart;
    public GameObject notePrefab;

    [Header("풀링")]
    [Tooltip("미리 만들어둘 노트 개수. 채보에서 동시에 화면에 떠 있을 수 있는 최대 노트 수보다 넉넉하게 잡아둔다.")]
    public int prewarmCount = 32;

    private int nextNoteIndex = 0;
    private ObjectPool pool;

    // 기능: 씬이 시작되자마자(곡을 고르기 전이라도) 노트 풀을 미리 채워두고,
    //       셰이더 워밍업을 예약한다.
    // 원리: Instantiate/Destroy를 매 노트마다 반복하는 대신, 여기서 한 번에
    //       필요한 만큼 만들어두고 이후에는 재사용만 한다 — Awake는 모든
    //       오브젝트의 Start보다 먼저 실행되므로, SongLoader.Start()가 곧바로
    //       스폰을 시작해도 풀이 항상 준비되어 있다.
    void Awake()
    {
        if (notePrefab != null)
        {
            pool = ObjectPool.Create("NotePool", notePrefab, prewarmCount);
            pool.transform.SetParent(transform);
        }
    }

    void Start()
    {
        if (pool != null)
        {
            StartCoroutine(WarmUpShader());
        }
    }

    // 기능: 실제 곡이 시작되어 노트가 쏟아지기 전에, 노트 하나를 카메라 시야
    //       안(판정선 위치)에서 완전히 투명한 상태로 딱 한 프레임 그려서
    //       셰이더 변형을 미리 컴파일해둔다.
    // 원리: URP에서 특정 머티리얼/키워드 조합은 그 조합이 세션 중 처음으로
    //       실제 그려지는 순간 그 자리에서 컴파일된다 — "두 번째부터는
    //       안 그런데 처음에만 심하게 렉 걸린다"는 증상의 전형적인 원인이다.
    //       프러스텀(카메라 시야) 밖에 두면 애초에 그려지지 않아 컴파일이
    //       일어나지 않으므로 반드시 시야 안에 둬야 하고, 대신 알파를 0으로
    //       만들어 화면에는 아무것도 보이지 않게 한다.
    private IEnumerator WarmUpShader()
    {
        Vector3 pos = new Vector3(
            laneManager != null ? laneManager.GetLaneX(0) : 0f,
            gameManager != null ? gameManager.judgmentY : 0f,
            0f);
        GameObject obj = pool.Get(pos, Quaternion.identity);

        SpriteRenderer sr = obj.GetComponent<SpriteRenderer>();
        Color original = Color.white;
        if (sr != null)
        {
            original = sr.color;
            Color invisible = original;
            invisible.a = 0f;
            sr.color = invisible;
        }

        yield return null; // 한 프레임 그려지길 기다린다 (여기서 실제 컴파일이 일어난다).

        if (sr != null)
        {
            sr.color = original;
        }
        pool.Release(obj);
    }

    // 기능: 매 프레임 "지금 스폰해야 할 노트가 있는지" 확인하고, 있으면 만든다.
    // 원리: spawnDistance(화면에 보이는 구간의 전체 길이, spawnY-judgmentY)는
    //       변속과 무관한 상수다. 각 노트에 대해 "지금부터 hitTime까지 이동해야
    //       할 거리"(gameManager.GetDistance)가 이 spawnDistance 이하로
    //       줄어든 순간이 바로 그 노트가 spawnY 위치에 나타나야 할 시점이다.
    //       nextNoteIndex는 "다음에 스폰할 노트"를 가리키는 포인터라서,
    //       chart.notes를 매번 처음부터 훑지 않고 앞으로만 전진한다(정렬되어
    //       있다는 전제 덕분에 가능한 최적화). while로 도는 이유는, 프레임
    //       사이 간격이 벌어지거나(랙 등) 여러 노트의 스폰 시점이 한 프레임
    //       안에 몰릴 수 있어 한 번에 여러 개를 스폰해야 할 수도 있기 때문이다.
    //
    //       gameManager.clock.IsActive를 반드시 함께 확인해야 한다 —
    //       chart는 곡을 고르기 전에도 씬에 미리 배선되어 있을 수 있는데
    //       (곡 선택 화면이 떠 있는 동안에도 chart != null인 상태), 그때
    //       clock.CurrentTime은 Play()가 호출되지 않아 기본값 0에 멈춰있다.
    //       이 상태에서 chart만 검사하면 이른 시간대 노트가 곡 선택 화면에서부터
    //       미리 스폰되어 버리는 문제가 생긴다. IsActive로 "Play()가 실제로
    //       호출된 뒤"인지부터 확인하면 이 문제가 사라진다.
    void Update()
    {
        if (chart == null || gameManager == null || gameManager.clock == null || !gameManager.clock.IsActive)
        {
            return;
        }

        float currentTime = gameManager.clock.CurrentTime;
        float spawnDistance = gameManager.spawnY - gameManager.judgmentY;

        while (nextNoteIndex < chart.notes.Count &&
               gameManager.GetDistance(currentTime, chart.notes[nextNoteIndex].hitTime) <= spawnDistance)
        {
            SpawnNote(chart.notes[nextNoteIndex]);
            nextNoteIndex++;
        }
    }

    // 기능: NoteData 하나를 실제 Note 게임오브젝트로 만들어 화면에 등장시킨다.
    // 원리: notePrefab을 그 레인의 X좌표, spawnY 위치에 Instantiate하고,
    //       프리팹에 Note 컴포넌트가 붙어있는지 확인한 뒤 Setup()으로 레인/시각/
    //       홀드 길이를 넘겨준다(Setup 안에서 NoteRegistry 등록까지 처리된다).
    //       notePrefab이 비어있거나 Note 컴포넌트가 없는 경우는 설정 실수이므로
    //       경고만 남기고 조용히 넘어간다 — 게임 전체가 멈추지 않게 하기 위함이다.
    void SpawnNote(NoteData data)
    {
        if (notePrefab == null || pool == null)
        {
            Debug.LogWarning("NoteSpawner: notePrefab이 연결되지 않았습니다.");
            return;
        }

        Vector3 spawnPos = new Vector3(laneManager.GetLaneX(data.lane), gameManager.spawnY, 0f);
        GameObject obj = pool.Get(spawnPos, Quaternion.identity);

        Note note = obj.GetComponent<Note>();
        if (note == null)
        {
            Debug.LogWarning("NoteSpawner: notePrefab에 Note 컴포넌트가 없습니다.");
            pool.Release(obj);
            return;
        }

        note.SetPool(pool);
        note.Setup(gameManager, laneManager.GetLaneX(data.lane), data.lane, data.hitTime, data.holdDuration);
    }

    // 기능: 스폰 포인터를 처음으로 되돌린다.
    // 원리: nextNoteIndex를 0으로 리셋하기만 하면, 다음 Update()부터 chart.notes를
    //       처음부터 다시 훑으며 스폰한다. 곡을 새로 고를 때(SongLoader.Load)
    //       호출해서, 이전 곡을 끝까지 재생했던 포인터 위치가 새 채보에 그대로
    //       남아 있는 문제를 막는다.
    public void ResetSpawner()
    {
        nextNoteIndex = 0;
    }
}
