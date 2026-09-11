using UnityEngine;
using UnityEngine.InputSystem;

// ============================================================
// InputJudge.cs
//   레인별 키 입력을 감지해서, 그 레인에서 가장 먼저 맞아야 할
//   노트(NoteRegistry.GetEarliestNote)와 시간을 비교해 판정한다.
//
//   롱노트는 판정이 두 번 생긴다 — 누르는 순간(TryPress, 머리)과
//   떼는 순간(TryRelease, 꼬리)이다. 머리를 정확히 누르면 노트는
//   사라지지 않고 Holding 상태로 바뀌어 유지되며, 꼬리는 키를 뗄 때
//   따로 판정한다(끝까지 떼지 않으면 Note.cs가 자동으로 성공 처리한다).
//
//   기본 배치는 리듬 게임에서 널리 쓰이는 D F J K (4키) 이다.
//   6키/8키로 확장하려면 laneKeys 배열과 LaneManager의
//   laneXPositions 배열 길이를 함께 늘리면 된다.
// ============================================================
public class InputJudge : MonoBehaviour
{
    [Header("참조")]
    public GameManager gameManager;

    [Tooltip("레인 순서에 대응하는 키. LaneManager의 레인 개수와 길이가 같아야 한다.")]
    public Key[] laneKeys = { Key.D, Key.F, Key.J, Key.K };

    void Update()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null || gameManager == null || gameManager.clock == null)
        {
            return;
        }

        for (int lane = 0; lane < laneKeys.Length; lane++)
        {
            if (keyboard[laneKeys[lane]].wasPressedThisFrame)
            {
                TryPress(lane);
            }
            else if (keyboard[laneKeys[lane]].wasReleasedThisFrame)
            {
                TryRelease(lane);
            }
        }
    }

    void TryPress(int lane)
    {
        Note note = NoteRegistry.GetEarliestNote(lane);
        if (note == null)
        {
            // 이 레인에 판정할 노트가 없다 — 아무 일도 하지 않는다(헛치기 무시).
            return;
        }

        float currentTime = gameManager.clock.CurrentTime;
        float diff = Mathf.Abs(currentTime - note.hitTime);

        if (diff > JudgmentWindows.MissWindow)
        {
            // 노트가 있긴 하지만 너무 멀리 떨어져 있다 (아직 한참 남았거나 이미 지나감).
            // 이런 경우까지 Miss로 처리하면 너무 가혹하므로 그냥 무시한다.
            return;
        }

        Grade grade = JudgmentWindows.Evaluate(diff);

        if (note.IsLongNote)
        {
            // 머리 판정만 바로 반영하고, 노트는 유지한 채 홀드 상태로 넘긴다.
            ScoreManager.Instance.ReportHit(grade);
            note.BeginHold();
        }
        else
        {
            note.Judge(grade);
        }
    }

    void TryRelease(int lane)
    {
        Note note = NoteRegistry.GetHoldingNote(lane);
        if (note == null)
        {
            // 이 레인에 홀드 중인 롱노트가 없다 — 탭 노트를 뗀 경우 등이라 무시한다.
            return;
        }

        note.ReleaseHold(gameManager.clock.CurrentTime);
    }
}
