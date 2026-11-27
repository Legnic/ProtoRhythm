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
//   따로 판정한다. 꼬리도 머리처럼 정확한 시각에 떼야 하는 판정이라, 꼬리
//   판정 구간(꼬리 시각 + MissWindow)이 지나도록 떼지 않고 계속 누르고 있으면
//   Note.cs가 꼬리를 Miss로 처리한다(의도된 동작).
//
//   기본 배치는 리듬 게임에서 널리 쓰이는 D F J K (4키) 이다.
//   6키/8키로 확장하려면 laneKeys 배열과 LaneManager의
//   laneXPositions 배열 길이를 함께 늘리면 된다.
// ============================================================
public class InputJudge : MonoBehaviour
{
    [Header("참조")]
    public GameManager gameManager;

    [Tooltip("키를 누를 때마다 울리는 효과음. 비워두면 소리 없음.")]
    public KeyHitSound keySound;

    [Tooltip("키를 누르면 레인 아래에서 솟는 빛줄기(키 빔). 비워두면 연출 없음.")]
    public LaneKeyBeam keyBeam;

    [Tooltip("레인 순서에 대응하는 키. LaneManager의 레인 개수와 길이가 같아야 한다.")]
    public Key[] laneKeys = { Key.D, Key.F, Key.J, Key.K };

    // 레인별 KeyControl 캐시 — 매 프레임 레인마다 keyboard[Key] 조회를 반복하지 않도록,
    // 키보드 장치가 바뀔 때(연결/교체)만 다시 만든다.
    private Keyboard cachedKeyboard;
    private UnityEngine.InputSystem.Controls.KeyControl[] laneControls;

    // 기능: 씬이 시작될 때, 옵션 화면(키 매핑 탭)에서 저장해둔 키가 있으면
    //       인스펙터 기본값 대신 그 키로 덮어쓴다.
    // 원리: 옵션 화면(KeyMappingController)은 PlayScene과 다른 씬에 있어서
    //       이 배열을 직접 바꿀 수 없다 — 대신 GameSettings(PlayerPrefs)에
    //       레인별로 저장해두면, 여기서 씬이 시작될 때마다 읽어와 반영한다
    //       (SongLoader가 GameSettings.AudioOffset을 읽어오는 것과 같은 패턴).
    //       레인을 한 번도 바꾼 적이 없으면 GameSettings.GetLaneKey가 기존
    //       기본값을 그대로 돌려주므로 동작이 달라지지 않는다.
    void Start()
    {
        for (int lane = 0; lane < laneKeys.Length; lane++)
        {
            laneKeys[lane] = GameSettings.GetLaneKey(lane, laneKeys[lane]);
        }
    }

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

        if (keyboard != cachedKeyboard || laneControls == null || laneControls.Length != laneKeys.Length)
        {
            cachedKeyboard = keyboard;
            laneControls = new UnityEngine.InputSystem.Controls.KeyControl[laneKeys.Length];
            for (int i = 0; i < laneKeys.Length; i++) laneControls[i] = keyboard[laneKeys[i]];
        }

        for (int lane = 0; lane < laneControls.Length; lane++)
        {
            var key = laneControls[lane];
            if (key.wasPressedThisFrame)
            {
                // 판정과 무관하게 키를 누르는 즉시 효과음과 키 빔을 낸다(헛치기/연타 포함).
                if (keySound != null) keySound.Play();
                if (keyBeam != null) keyBeam.Press(lane);
                TryPress(lane);
            }
            else if (key.wasReleasedThisFrame)
            {
                if (keyBeam != null) keyBeam.Release(lane);
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
        float signedDiff = currentTime - note.hitTime;
        float diff = Mathf.Abs(signedDiff);

        if (diff > JudgmentWindows.MissWindow)
        {
            // 노트가 있긴 하지만 너무 멀리 떨어져 있다 (아직 한참 남았거나 이미 지나감).
            // 이런 경우까지 Miss로 처리하면 너무 가혹하므로 그냥 무시한다.
            return;
        }

        Grade grade = JudgmentWindows.Evaluate(diff);

        // 판정 점수(grade)와는 완전히 별개로, FAST/LATE 교정 피드백을 보고한다.
        // 이 지점의 diff는 항상 MissWindow 이내이므로(위에서 이미 걸러짐) 실제로
        // 입력이 있었던 경우만 해당하고, TimingFeedback.cs 참고.
        TimingFeedback.Report(signedDiff, lane);

        if (note.IsLongNote)
        {
            // 머리 판정만 바로 반영하고, 노트는 유지한 채 홀드 상태로 넘긴다.
            ScoreManager.Instance.ReportHit(grade, lane);
            note.BeginHold();
            // 홀드가 이어지는 동안(키를 뗄 때까지) 키 빔을 최대 밝기로 유지한다.
            if (keyBeam != null) keyBeam.BeginHold(lane, note);
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

        // 롱노트 꼬리를 처리하는 순간에도 키를 누를 때와 같은 효과음을 낸다.
        if (keySound != null) keySound.Play();
        note.ReleaseHold(gameManager.clock.CurrentTime);
    }
}
