using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// ============================================================
// ResultUI.cs
//   결과 화면. GameSession에 PlaySceneController가 남겨둔 최종
//   점수/최고 콤보를 읽어서 보여주고, 곡 선택/타이틀로 돌아가는
//   버튼 두 개를 연결한다.
// ============================================================
public class ResultUI : MonoBehaviour
{
    [Header("표시")]
    public Text scoreText;
    public Text comboText;

    [Header("버튼")]
    public Button backToSelectButton;
    public Button backToTitleButton;

    // 기능: 화면이 뜨자마자 GameSession에 담겨온 값을 그려주고, 버튼 클릭을
    //       코드로 연결한다(이 프로젝트 전체에서 쓰는 패턴 — SongSelectUI,
    //       PlaySceneController, TitleUI 참고).
    void Start()
    {
        if (scoreText != null)
        {
            scoreText.text = $"Score: {GameSession.FinalScore}";
        }
        if (comboText != null)
        {
            comboText.text = $"Max Combo: {GameSession.FinalMaxCombo}";
        }

        if (backToSelectButton != null) backToSelectButton.onClick.AddListener(BackToSelect);
        if (backToTitleButton != null) backToTitleButton.onClick.AddListener(BackToTitle);
    }

    // 기능: "선택 화면으로" 버튼에 연결한다.
    public void BackToSelect()
    {
        SceneManager.LoadScene("SelectScene");
    }

    // 기능: "처음으로" 버튼에 연결한다.
    public void BackToTitle()
    {
        SceneManager.LoadScene("TitleScene");
    }
}
