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

    // 기능: 매 프레임 모든 레인의 키를 검사해서, 그 프레임에 새로 눌리거나(TryPress)
    //       새로 떼진(TryRelease) 키가 있으면 해당 처리로 넘긴다.
    // 원리: 새 Input System의 Keyboard.current[Key] 인덱서로 그 키의 현재 상태를
    //       가져오고, wasPressedThisFrame/wasReleasedThisFrame으로 "이번 프레임에
    //       상태가 바뀐 순간"만 잡아낸다 — 누르고 있는 동안 매 프레임 반응하면
    //       안 되므로(한 번 누르면 한 번만 판정해야 하므로) 이 두 플래그를 쓴다.
    //       일시정지 중(clock.IsPaused)에는 아예 아무 것도 하지 않는다 —
    //       CurrentTime이 얼어있어도 입력 자체를 막아야, 정지 화면에서 몰래
    //       키를 눌러 판정을 만들어내는 일이 없다.
    void Update()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null || gameManager == null || gameManager.clock == null || gameManager.clock.IsPaused)
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

    // 기능: 레인의 키를 눌렀을 때(머리 판정) 호출된다. 그 레인에서 가장 먼저
    //       맞아야 할 노트를 찾아 시간 오차를 계산하고 등급을 매긴다.
    // 원리: NoteRegistry.GetEarliestNote로 대상 노트를 찾고, 없으면(헛치기)
    //       그냥 무시한다. 있어도 |현재시각 - hitTime|이 MissWindow를 넘으면
    //       "너무 멀리 떨어진 노트"이므로 역시 무시한다(이걸 Miss로 처리하면
    //       판정 범위 밖에서 아무 키나 눌러도 콤보가 끊겨서 너무 가혹해진다).
    //       탭 노트는 Note.Judge로 바로 끝내고, 롱노트는 머리 등급만 즉시
    //       ScoreManager에 반영한 뒤 note.BeginHold()로 상태만 넘겨서 노트를
    //       유지시킨다(꼬리 판정은 나중에 따로 한다).
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

    // 기능: 레인의 키를 뗐을 때(꼬리 판정) 호출된다. 그 레인에서 현재 홀드 중인
    //       롱노트를 찾아 꼬리 판정을 넘긴다.
    // 원리: NoteRegistry.GetHoldingNote로 "지금 이 레인에서 눌린 채 유지되고
    //       있는 노트"를 찾는다. 탭 노트를 뗀 경우처럼 홀드 중인 노트가 없으면
    //       조용히 무시한다. 실제 판정 계산(오차 비교, 등급 매기기)은
    //       Note.ReleaseHold 쪽에 맡긴다 — InputJudge는 "누가, 언제" 뗐는지만
    //       전달하는 역할이다.
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
