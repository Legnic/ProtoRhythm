using UnityEngine;
using UnityEngine.UI;
using TMPro;

// ============================================================
// ResultUI.cs
//   결과 화면. GameSession에 PlaySceneController가 남겨둔 최종
//   점수/최고 콤보를 읽어서 보여주고, 곡 선택/타이틀로 돌아가는
//   버튼 두 개를 연결한다.
// ============================================================
public class ResultUI : MonoBehaviour
{
    [Header("표시")]
    public TMP_Text scoreText;
    public TMP_Text comboText;

    [Header("버튼")]
    public Button backToSelectButton;
    public Button backToTitleButton;

    [Header("배경")]
    [Tooltip("방금 플레이한 곡의 배경 이미지를 보여줄 SpriteRenderer — 선택 사항.")]
    public SpriteRenderer backgroundRenderer;
    [Tooltip("곡에 backgroundImage가 없을 때 대신 보여줄 기본 배경.")]
    public Sprite defaultBackground;

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

        SongData song = GameSession.SelectedSong;
        SongBackgroundCache.Load(song != null ? song.backgroundImage : null, sprite =>
        {
            if (backgroundRenderer != null)
            {
                backgroundRenderer.sprite = sprite != null ? sprite : defaultBackground;
            }
        });

        if (AmbientLoopPlayer.Instance != null) AmbientLoopPlayer.Instance.EnsurePlaying();

        if (backToSelectButton != null) backToSelectButton.onClick.AddListener(BackToSelect);
        if (backToTitleButton != null) backToTitleButton.onClick.AddListener(BackToTitle);
    }

    // 기능: "선택 화면으로" 버튼에 연결한다.
    public void BackToSelect()
    {
        SceneLoader.Load(this, "SelectScene");
    }

    // 기능: "처음으로" 버튼에 연결한다.
    public void BackToTitle()
    {
        SceneLoader.Load(this, "TitleScene");
    }
}
