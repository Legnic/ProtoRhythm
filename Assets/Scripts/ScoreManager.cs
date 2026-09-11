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

    void Awake()
    {
        Instance = this;
    }

    public void ReportHit(Grade grade)
    {
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

    public void ReportMiss()
    {
        combo = 0;
        Debug.Log("Miss | Combo Reset");
    }
}
