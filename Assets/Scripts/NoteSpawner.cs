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

    // 채보를 다시 처음부터 재생하고 싶을 때(재시도 등) 호출한다.
    public void ResetSpawner()
    {
        nextNoteIndex = 0;
    }
}
