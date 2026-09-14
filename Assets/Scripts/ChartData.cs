using System.Collections.Generic;
using UnityEngine;

// ============================================================
// ChartData.cs
//   DJMAX 방식 채보의 핵심: "이 노트가 몇 번 레인에서, 몇 초에
//   정확히 맞아야 하는가"만 담은 아주 단순한 데이터.
//
//   라노타/Phigros처럼 화면 좌표 애니메이션이 필요 없다.
//   노트의 화면 위치는 이 시각(hitTime) 하나로부터 매 프레임
//   계산해내기 때문이다. (Note.cs 참고)
// ============================================================
[System.Serializable]
public struct NoteData
{
    public int lane;          // 레인 번호 (0부터 시작)
    public float hitTime;     // 이 노트가 정확히 판정선과 만나야 하는 시각(초) — 롱노트는 "누르는" 시각
    public float holdDuration; // 0이면 탭 노트. 0보다 크면 이 길이(초)만큼 눌러서 유지해야 하는 롱노트.

    // 기능: NoteData 값을 한 번에 채워서 만든다.
    // 원리: holdDuration은 기본값 0(=탭 노트)이라, 롱노트가 아니라면
    //       마지막 인자를 생략하고 new NoteData(lane, hitTime)처럼 써도 된다.
    public NoteData(int lane, float hitTime, float holdDuration = 0f)
    {
        this.lane = lane;
        this.hitTime = hitTime;
        this.holdDuration = holdDuration;
    }
}

// ScriptableObject로 만들어두면 Unity 에디터에서 채보 애셋을 파일처럼
// 관리할 수 있다 (Assets 폴더에 저장, 여러 씬에서 재사용 가능).
[CreateAssetMenu(fileName = "NewChart", menuName = "Rhythm/Chart Data")]
public class ChartData : ScriptableObject
{
    [Tooltip("판정 시각(hitTime) 순으로 정렬되어 있어야 한다. SortByTime()으로 정렬 가능.")]
    public List<NoteData> notes = new List<NoteData>();

    // 기능: notes 리스트를 hitTime이 빠른 순서대로 정렬한다.
    // 원리: NoteSpawner는 "포인터 하나로 앞에서부터 훑는" 방식으로 노트를 스폰하기
    //       때문에(NoteSpawner.cs 참고), 리스트가 시간순이 아니면 늦게 나와야 할
    //       노트가 먼저 스폰되거나 아예 스폰이 안 되는 문제가 생긴다. List<T>.Sort의
    //       기본 비교자 대신 hitTime끼리 비교하는 람다를 넘겨서 정렬 기준을 지정한다.
    public void SortByTime()
    {
        notes.Sort((a, b) => a.hitTime.CompareTo(b.hitTime));
    }

    // 기능: 간단한 텍스트 포맷("레인,시각" 또는 "레인,시각,홀드길이" 한 줄에 하나씩)을
    //       파싱해서 NoteData 리스트를 만든다. BPM 계산기나 전용 채보 에디터 없이도
    //       빠르게 테스트 채보를 만들 수 있게 하기 위함이다. 세 번째 값(홀드 길이)은
    //       생략하면 탭 노트(0)로 취급한다.
    // 원리: 한 줄씩 텍스트를 훑으면서(Split('\n')) 빈 줄/주석(#)은 건너뛰고,
    //       나머지는 ','로 다시 나눠(Split(',')) 각 칸을 int/float로 파싱한다.
    //       파싱에 실패한 줄은 조용히 무시하지 않고 Debug.LogWarning으로 어떤 줄이
    //       왜 실패했는지 알려준 뒤 계속 진행한다. 마지막에 SortByTime()과 같은
    //       기준(hitTime 오름차순)으로 한 번 정렬해서 돌려준다.
    //
    // 예시 텍스트:
    //   # 4비트 기본 박자, BPM 120 (한 박자 = 0.5초)
    //   0,1.0
    //   1,1.5
    //   2,2.0
    //   3,2.5,1.0   # 레인 3, 2.5초에 눌러서 1초간(=3.5초까지) 들고 있어야 하는 롱노트
    public static List<NoteData> ParseSimpleText(string text)
    {
        var result = new List<NoteData>();
        string[] lines = text.Split('\n');

        foreach (string rawLine in lines)
        {
            string line = rawLine.Trim();

            // 빈 줄이나 '#'으로 시작하는 주석 줄은 건너뛴다.
            if (string.IsNullOrEmpty(line) || line.StartsWith("#"))
            {
                continue;
            }

            string[] parts = line.Split(',');
            if (parts.Length != 2 && parts.Length != 3)
            {
                Debug.LogWarning($"채보 파싱 실패(형식 오류): \"{line}\"");
                continue;
            }

            bool laneOk = int.TryParse(parts[0].Trim(), out int lane);
            bool timeOk = float.TryParse(parts[1].Trim(), out float time);
            float holdDuration = 0f;
            bool holdOk = parts.Length < 3 || float.TryParse(parts[2].Trim(), out holdDuration);

            if (laneOk && timeOk && holdOk)
            {
                result.Add(new NoteData(lane, time, holdDuration));
            }
            else
            {
                Debug.LogWarning($"채보 파싱 실패(값 오류): \"{line}\"");
            }
        }

        result.Sort((a, b) => a.hitTime.CompareTo(b.hitTime));
        return result;
    }
}
