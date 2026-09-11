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

    // GameManager.Awake()에서 레인 개수만큼 초기화해줘야 한다.
    public static void Init(int laneCount)
    {
        lanes = new List<Note>[laneCount];
        for (int i = 0; i < laneCount; i++)
        {
            lanes[i] = new List<Note>();
        }
    }

    public static void Register(Note note)
    {
        if (lanes == null || note.lane < 0 || note.lane >= lanes.Length)
        {
            return;
        }
        lanes[note.lane].Add(note);
    }

    public static void Unregister(Note note)
    {
        if (lanes == null || note.lane < 0 || note.lane >= lanes.Length)
        {
            return;
        }
        lanes[note.lane].Remove(note);
    }

    // 해당 레인에서 hitTime이 가장 빠른(=가장 먼저 판정선에 도달할) "판정 대기 중(Waiting)"
    // 노트를 찾는다. 입력이 들어왔을 때 "지금 맞춰야 할 노트"는 항상 이 노트이다.
    // 이미 홀드 중인 롱노트는 제외한다 — 홀드 중에 같은 레인 키를 다시 눌러도 새로
    // 판정하지 않기 위해서다.
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

    // 해당 레인에서 현재 홀드 중인 롱노트를 찾는다(없으면 null).
    // 키를 뗐을 때(릴리즈) 어떤 노트의 꼬리를 판정해야 하는지 알기 위해 쓴다.
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
