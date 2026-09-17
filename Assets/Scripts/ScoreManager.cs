using UnityEngine;

// ============================================================
// ScoreManager.cs
//   콤보와 점수를 추적하는 간단한 매니저.
//   씬에 하나만 존재한다고 가정하고 정적 Instance로 접근한다.
//   (UI 연동은 범위 밖이라 Debug.Log로 대체해두었다.
//    실제 프로젝트에서는 OnJudged 같은 이벤트를 추가해 UI에 연결하면 된다)
// ============================================================
public class ScoreManager : MonoBehaviour
{
    public static ScoreManager Instance { get; private set; }

    public int score = 0;
    public int combo = 0;
    public int maxCombo = 0;

    // 기능: 노트 하나가 판정될 때마다(Miss 포함) 그 등급과 레인을 알려주는 이벤트.
    // 원리: JudgmentPopupUI 같은 화면 연출 스크립트가 ScoreManager 내부
    //       구현(점수 계산 방식 등)을 몰라도, 이 이벤트만 구독하면 "지금 막
    //       어떤 레인에서 어떤 등급이 나왔는지"를 바로 알 수 있게 한다.
    //       레인 정보는 JudgmentHitEffectSpawner처럼 "그 레인의 판정선에만"
    //       연출을 넣어야 하는 구독자를 위해 추가했다.
    public static event System.Action<Grade, int> OnJudged;

    // 기능: 이 컴포넌트를 전역에서 접근 가능한 싱글턴으로 등록한다.
    // 원리: Note, InputJudge, SongLoader 등이 서로를 거치지 않고 바로
    //       ScoreManager.Instance로 접근할 수 있게, 씬 시작 시(Awake) 자기
    //       자신을 정적 필드에 저장해둔다.
    void Awake()
    {
        Instance = this;
    }

    // 기능: 노트 하나의 판정 결과(등급, 레인)를 받아 점수/콤보에 반영한다.
    // 원리: 등급별로 점수를 더하고 콤보를 1 올린다. Miss는 점수 계산 없이
    //       바로 ReportMiss()로 넘겨서 콤보를 끊는 공통 처리를 재사용한다.
    //       (Miss 쪽은 switch 안에서 return하기 때문에 아래의 "콤보 갱신 +
    //       로그" 코드는 Miss가 아닌 경우에만 실행된다) maxCombo는 매번
    //       현재 콤보와 비교해서, 이번에 갱신된 콤보가 지금까지의 최고 기록보다
    //       높을 때만 갱신한다.
    public void ReportHit(Grade grade, int lane)
    {
        OnJudged?.Invoke(grade, lane);

        switch (grade)
        {
            case Grade.Perfect:
                score += 100;
                combo++;
                break;
            case Grade.Great:
                score += 70;
                combo++;
                break;
            case Grade.Good:
                score += 30;
                combo++;
                break;
            case Grade.Miss:
                ReportMiss();
                return;
        }

        if (combo > maxCombo)
        {
            maxCombo = combo;
        }

        Debug.Log($"{grade} | Combo {combo} | Score {score}");
    }

    // 기능: 노트를 놓쳤을 때(Miss) 콤보를 끊는다.
    // 원리: combo를 0으로 되돌리기만 한다 — score/maxCombo는 그대로 둬서,
    //       이미 쌓은 점수와 이전 최고 콤보 기록은 Miss 한 번으로 사라지지 않는다.
    public void ReportMiss()
    {
        combo = 0;
        Debug.Log("Miss | Combo Reset");
    }

    // 기능: 점수/콤보/최고 콤보를 전부 0으로 되돌린다.
    // 원리: 곡을 새로 고를 때(SongLoader.Load) 호출한다 — 호출하지 않으면
    //       이전 곡에서 쌓은 점수와 콤보가 다음 곡으로 그대로 이어져 버린다.
    public void ResetScore()
    {
        score = 0;
        combo = 0;
        maxCombo = 0;
    }
}
