using System.Collections.Generic;

// ============================================================
// NoteRegistry.cs
//   레인별로 "지금 화면에 살아있는 노트들"을 추적하는 명부.
//   MonoBehaviour가 아닌 순수 C# 정적 클래스로 만들어서,
//   Note와 InputJudge가 서로를 직접 참조하지 않고도
//   이 명부를 통해서만 상호작용하도록 한다.
//
//   InputJudge는 "이 레인에서 가장 먼저 맞아야 할 노트가 뭐지?"를
//   물어보고, Note는 스폰될 때 등록, 사라질 때(맞았든 놓쳤든) 등록 해제한다.
// ============================================================
public static class NoteRegistry
{
    private static List<Note>[] lanes;

    // 기능: 레인 개수만큼 빈 리스트 배열을 만들어 명부를 초기화한다.
    // 원리: GameManager.Awake()에서 씬 시작 시 한 번 호출된다. 정적 클래스라
    //       씬을 새로 열거나 도메인 리로드가 일어나도 이전 값이 남아있을 수
    //       있으므로, 매번 배열을 새로 만들어 깨끗한 상태에서 시작하게 한다.
    public static void Init(int laneCount)
    {
        lanes = new List<Note>[laneCount];
        for (int i = 0; i < laneCount; i++)
        {
            lanes[i] = new List<Note>();
        }
    }

    // 기능: 노트 하나를 자신의 레인 명부에 추가한다.
    // 원리: Note.Setup()이 lane 값을 채운 직후 호출한다. Init이 아직 호출되지
    //       않았거나(lanes == null) 레인 번호가 범위를 벗어나면 조용히 무시해서
    //       잘못된 설정이 예외로 게임을 멈추지 않게 한다.
    public static void Register(Note note)
    {
        if (lanes == null || note.lane < 0 || note.lane >= lanes.Length)
        {
            return;
        }
        lanes[note.lane].Add(note);
    }

    // 기능: 노트 하나를 자신의 레인 명부에서 제거한다.
    // 원리: Note.OnDestroy()에서 호출된다. 판정이 끝났든(Judged) 놓쳤든,
    //       사라지는 노트는 반드시 여기서 빠져야 GetEarliestNote/GetHoldingNote가
    //       이미 없는 노트를 계속 붙잡고 있지 않는다.
    public static void Unregister(Note note)
    {
        if (lanes == null || note.lane < 0 || note.lane >= lanes.Length)
        {
            return;
        }
        lanes[note.lane].Remove(note);
    }

    // 기능: 해당 레인에서 hitTime이 가장 빠른(=가장 먼저 판정선에 도달할)
    //       "판정 대기 중(Waiting)" 노트를 찾는다.
    // 원리: 입력이 들어왔을 때 "지금 맞춰야 할 노트"는 항상 이 노트다. 리스트를
    //       한 번 순회하면서 State가 Waiting인 것만 후보로 삼고, 그중 hitTime이
    //       가장 작은 것을 고른다. 이미 홀드 중인 롱노트는 제외하는데, 그렇게
    //       하지 않으면 홀드 중에 같은 레인 키를 다시 눌렀을 때 이미 판정된
    //       노트를 또 판정 대상으로 잡아버리는 문제가 생기기 때문이다.
    public static Note GetEarliestNote(int lane)
    {
        if (lanes == null || lane < 0 || lane >= lanes.Length)
        {
            return null;
        }

        Note earliest = null;
        foreach (Note note in lanes[lane])
        {
            if (note.State != NoteState.Waiting)
            {
                continue;
            }
            if (earliest == null || note.hitTime < earliest.hitTime)
            {
                earliest = note;
            }
        }
        return earliest;
    }

    // 기능: 화면에 남아있는 노트를 전부 풀로 돌려보내고 명부를 깨끗하게 비운다.
    // 원리: "다시하기"처럼 씬을 새로 로드하지 않고 곡을 처음부터 다시 트는
    //       경우에 필요하다 — ResetSpawner()는 "다음에 스폰할 노트" 포인터만
    //       되돌릴 뿐, 이전 시도에서 이미 스폰되어 화면에 떠 있던 노트는 그대로
    //       남아있는다. 그 상태로 그냥 다시 재생하면 이전 시도의 노트와 새
    //       시도의 노트가 뒤섞여 화면에 남는다. 각 레인 리스트를 복사해서
    //       순회하는 이유는, Note.ReturnToPool() -> Unregister가 원본 리스트를
    //       변경하기 때문에 순회 중인 리스트를 직접 건드리면 안 되기 때문이다.
    //       Destroy 대신 ReturnToPool을 쓰는 이유는, "다시하기"를 반복할 때마다
    //       노트를 또 Instantiate하지 않고 풀에 있던 것을 그대로 재사용하기
    //       위함이다.
    public static void ClearAll()
    {
        if (lanes == null) return;

        foreach (List<Note> lane in lanes)
        {
            var toClear = new List<Note>(lane);
            foreach (Note note in toClear)
            {
                if (note != null)
                {
                    note.ReturnToPool();
                }
            }
        }
    }

    // 기능: 해당 레인에서 현재 홀드 중인 롱노트를 찾는다(없으면 null).
    // 원리: 키를 뗐을 때(릴리즈) "어떤 노트의 꼬리를 판정해야 하는지" 알아야 하는데,
    //       한 레인에서 동시에 홀드 중인 노트는 최대 하나뿐이라고 가정하고
    //       리스트에서 State가 Holding인 첫 노트를 찾아 바로 반환한다.
    public static Note GetHoldingNote(int lane)
    {
        if (lanes == null || lane < 0 || lane >= lanes.Length)
        {
            return null;
        }

        foreach (Note note in lanes[lane])
        {
            if (note.State == NoteState.Holding)
            {
                return note;
            }
        }
        return null;
    }
}
