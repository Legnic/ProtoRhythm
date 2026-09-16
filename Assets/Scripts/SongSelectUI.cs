using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// ============================================================
// SongSelectUI.cs
//   곡 선택 -> 난이도 선택, 2단계로 진행되는 선택 화면.
//   1단계: SongLibrary에 담긴 곡마다 버튼을 하나씩 보여준다. 이
//          단계에서는 quitToTitleButton("돌아가기")이 보여서 타이틀로
//          되돌아갈 수 있다.
//   2단계: 그 곡을 누르면 목록이 그 곡의 난이도(SongDifficulty)
//          버튼들로 바뀐다. 이 단계에서는 대신 backButton("뒤로")이
//          보여서 1단계로 되돌아갈 수 있다.
//   난이도를 최종 선택하면 GameSession에 (곡, 난이도)를 적어두고
//   PlayScene을 로드한다 — 실제 재생 배선(SongLoader.Load)은 이제
//   다른 씬에서 그 씬이 시작될 때 스스로 GameSession을 읽어서 한다
//   (SongLoader.cs 참고). 같은 listParent/buttonPrefab을 곡 목록과
//   난이도 목록 양쪽에 재사용한다 — 화면을 새로 만들지 않고 내용만
//   갈아끼우는 방식이다.
// ============================================================
public class SongSelectUI : MonoBehaviour
{
    [Header("참조")]
    public SongLibrary library;

    [Header("UI")]
    [Tooltip("곡/난이도 버튼들이 생성될 부모 (Vertical Layout Group이 붙어있는 오브젝트)")]
    public RectTransform listParent;
    [Tooltip("항목 하나당 만들어질 버튼 프리팹. 자식에 Text가 있어야 한다.")]
    public Button buttonPrefab;
    [Tooltip("화면 위쪽 제목 텍스트 (선택 사항). 단계에 따라 문구가 바뀐다.")]
    public Text titleText;
    [Tooltip("난이도 선택 단계에서만 보이는 뒤로가기 버튼(곡 목록으로) — 선택 사항.")]
    public Button backButton;
    [Tooltip("곡 목록 단계에서만 보이는 돌아가기 버튼(타이틀로) — 선택 사항.")]
    public Button quitToTitleButton;

    // 난이도 목록을 보여주는 중일 때, 어떤 곡을 골랐었는지 기억해둔다.
    private SongData selectedSong;

    // 기능: 씬이 시작될 때 뒤로가기/타이틀로 버튼 리스너를 걸어두고, 곡 목록 화면으로 시작한다.
    void Start()
    {
        if (backButton != null)
        {
            backButton.onClick.AddListener(ShowSongList);
        }
        if (quitToTitleButton != null)
        {
            quitToTitleButton.onClick.AddListener(() => SceneManager.LoadScene("TitleScene"));
        }

        ShowSongList();
    }

    // 기능: 목록 영역을 SongLibrary의 곡들로 채운다 (선택의 1단계).
    // 원리: selectedSong을 비우고, 이 단계에서만 의미 있는 quitToTitleButton을
    //       보이고 backButton은 숨긴다. ClearList()로 이전 목록(난이도 버튼이었을
    //       수도 있는)을 지우고 곡마다 버튼을 새로 만든다. 버튼을 누르면 그 곡을
    //       인자로 ShowDifficultyList를 호출하도록 연결한다.
    void ShowSongList()
    {
        selectedSong = null;

        if (backButton != null)
        {
            backButton.gameObject.SetActive(false);
        }
        if (quitToTitleButton != null)
        {
            quitToTitleButton.gameObject.SetActive(true);
        }
        if (titleText != null)
        {
            titleText.text = "곡 선택";
        }

        ClearList();

        if (library == null || buttonPrefab == null || listParent == null)
        {
            Debug.LogWarning("SongSelectUI: library/buttonPrefab/listParent가 지정되지 않았습니다.");
            return;
        }

        foreach (SongData song in library.songs)
        {
            if (song == null) continue;

            Button button = Instantiate(buttonPrefab, listParent);
            Text label = button.GetComponentInChildren<Text>();
            if (label != null)
            {
                label.text = song.songName;
            }

            // 클로저가 반복 변수를 그대로 참조하는 문제를 피하려고 지역 변수에 담아 넘긴다.
            SongData capturedSong = song;
            button.onClick.AddListener(() => ShowDifficultyList(capturedSong));
        }
    }

    // 기능: 목록 영역을 고른 곡의 난이도들로 채운다 (선택의 2단계).
    // 원리: selectedSong을 기억해둬야 난이도를 최종 선택했을 때 "어떤 곡의
    //       난이도인지"를 알 수 있다. backButton을 보이고 quitToTitleButton을
    //       숨겨서, 이 단계의 "돌아가기"는 곡 목록으로만 가게 한다. 버튼
    //       라벨은 difficulty.label을 그대로 쓴다.
    void ShowDifficultyList(SongData song)
    {
        selectedSong = song;

        if (backButton != null)
        {
            backButton.gameObject.SetActive(true);
        }
        if (quitToTitleButton != null)
        {
            quitToTitleButton.gameObject.SetActive(false);
        }
        if (titleText != null)
        {
            titleText.text = $"{song.songName} - 난이도 선택";
        }

        ClearList();

        foreach (SongDifficulty difficulty in song.difficulties)
        {
            if (difficulty == null) continue;

            Button button = Instantiate(buttonPrefab, listParent);
            Text label = button.GetComponentInChildren<Text>();
            if (label != null)
            {
                label.text = difficulty.label;
            }

            SongDifficulty capturedDifficulty = difficulty;
            button.onClick.AddListener(() => SelectDifficulty(capturedDifficulty));
        }
    }

    // 기능: 난이도까지 최종 선택됐을 때, GameSession에 선택 결과를 적어두고
    //       플레이 화면(PlayScene)으로 넘어간다.
    // 원리: SongLoader가 이제 다른 씬(PlayScene)에 있어서 여기서 직접 부를 수
    //       없다 — 대신 GameSession(정적 클래스라 씬이 바뀌어도 값이 유지된다)에
    //       곡/난이도를 적어두면, PlayScene이 시작될 때 그 씬의 SongLoader가
    //       스스로 읽어서 재생을 시작한다.
    void SelectDifficulty(SongDifficulty difficulty)
    {
        GameSession.SelectedSong = selectedSong;
        GameSession.SelectedDifficulty = difficulty;
        SceneManager.LoadScene("PlayScene");
    }

    // 기능: listParent 밑의 버튼들을 전부 지운다.
    // 원리: 곡 목록 <-> 난이도 목록을 오갈 때마다 이전 단계의 버튼이 남아있으면
    //       안 되므로, 자식을 뒤에서부터 순서대로 Destroy한다(앞에서부터 지우면
    //       인덱스가 밀려서 하나씩 건너뛰는 실수가 생기기 쉽다).
    // DestroyImmediate를 쓰는 이유: 에디터에서 Play 버튼을 눌러 테스트할 때
    //       Destroy()를 쓰면 "Play 모드 종료 후에도 버튼이 남아서 씬이 꼬이는"
    //       문제가 생긴다. DestroyImmediate()를 쓰면 즉시 제거되므로 이런
    //       문제를 피할 수 있다(실제 게임에서는 Destroy()를 써도 상관없다).
    void ClearList()
    {
        for (int i = listParent.childCount - 1; i >= 0; i--)
        {
            DestroyImmediate(listParent.GetChild(i).gameObject);
        }
    }
}
