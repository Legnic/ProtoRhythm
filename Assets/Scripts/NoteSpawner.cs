using UnityEngine;

// ============================================================
// NoteSpawner.cs
//   채보의 각 노트를 "적절한 시점"에 생성(Instantiate)한다.
//
//   "적절한 시점"의 계산:
//     노트가 spawnY에서 judgmentY까지 이동하는 데 걸리는 시간을
//     travelTime이라고 하면, 그 노트는 (hitTime - travelTime)
//     시점에 화면에 나타나야 정확히 hitTime에 판정선에 도달한다.
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

    private int nextNoteIndex = 0;

    // 기능: 매 프레임 "지금 스폰해야 할 노트가 있는지" 확인하고, 있으면 만든다.
    // 원리: travelTime(스폰 지점에서 판정선까지 이동하는 데 걸리는 시간)을
    //       현재 배속(EffectiveSpeed)으로 매 프레임 다시 계산한다 — 배속이
    //       바뀌면 스폰 타이밍도 같이 조정되어야 화면에서 노트 간격이
    //       올바르게 보인다. nextNoteIndex는 "다음에 스폰할 노트"를 가리키는
    //       포인터라서, chart.notes를 매번 처음부터 훑지 않고 앞으로만
    //       전진한다(정렬되어 있다는 전제 덕분에 가능한 최적화). while로
    //       도는 이유는, 프레임 사이 간격이 벌어지거나(랙 등) 여러 노트의
    //       스폰 시점이 한 프레임 안에 몰릴 수 있어 한 번에 여러 개를
    //       스폰해야 할 수도 있기 때문이다.
    void Update()
    {
        if (chart == null || gameManager == null || gameManager.clock == null)
        {
            return;
        }

        float currentTime = gameManager.clock.CurrentTime;

        // spawnY에서 judgmentY까지 이동하는 데 걸리는 시간.
        // EffectiveSpeed가 0이 되는 극단적인 경우를 방지한다.
        float speed = Mathf.Max(gameManager.EffectiveSpeed, 0.0001f);
        float travelTime = (gameManager.spawnY - gameManager.judgmentY) / speed;

        while (nextNoteIndex < chart.notes.Count &&
               chart.notes[nextNoteIndex].hitTime - travelTime <= currentTime)
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
        if (notePrefab == null)
        {
            Debug.LogWarning("NoteSpawner: notePrefab이 연결되지 않았습니다.");
            return;
        }

        Vector3 spawnPos = new Vector3(laneManager.GetLaneX(data.lane), gameManager.spawnY, 0f);
        GameObject obj = Instantiate(notePrefab, spawnPos, Quaternion.identity);

        Note note = obj.GetComponent<Note>();
        if (note == null)
        {
            Debug.LogWarning("NoteSpawner: notePrefab에 Note 컴포넌트가 없습니다.");
            Destroy(obj);
            return;
        }

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
