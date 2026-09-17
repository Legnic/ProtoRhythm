using UnityEngine;

// ============================================================
// PlaySceneController.cs
//   플레이 화면(PlayScene) 자체의 흐름을 담당한다 — 일시정지
//   (Esc, 계속하기/다시하기/돌아가기)와, 곡이 끝났을 때 결과 화면으로
//   넘어가는 것 두 가지.
//
//   일시정지는 Time.timeScale이 아니라 RhythmClock.Pause()/Resume()로
//   처리한다 — RhythmClock은 Time.timeScale과 무관한 dspTime을 쓰기
//   때문에, timeScale=0으로는 곡 시각이 계속 흘러버린다(RhythmClock.cs
//   참고). InputJudge는 clock.IsPaused를 직접 확인해서 일시정지 중
//   입력을 무시하므로, 여기서는 화면(패널) 표시와 클럭 제어만 하면 된다.
// ============================================================
public class PlaySceneController : MonoBehaviour
{
    [Header("참조")]
    public RhythmClock clock;
    public SongLoader songLoader;

    [Header("일시정지 UI")]
    [Tooltip("일시정지 중에만 보이는 패널(계속하기/다시하기/돌아가기 버튼을 담고 있다). 시작할 때 꺼져있어야 한다.")]
    public GameObject pausePanel;
    public UnityEngine.UI.Button resumeButton;
    public UnityEngine.UI.Button retryButton;
    public UnityEngine.UI.Button backToSelectButton;

    [Header("결과 화면 전환")]
    [Tooltip("음원이 끝난 뒤 결과 화면으로 넘어가기까지 기다리는 여유 시간(초).")]
    public float resultDelaySeconds = 1f;

    private bool hasGoneToResult;

    // 기능: 시작할 때 버튼 클릭을 코드로 연결하고, 일시정지 패널을 꺼둔다.
    // 원리: 인스펙터의 "영구 리스너"를 쓰지 않고 여기서 매번 코드로 연결하는
    //       이유는, 이 프로젝트의 다른 버튼들(SongSelectUI 등)과 같은 패턴을
    //       유지하기 위해서다 — 씬이 새로 열릴 때마다 스스로 다시 연결되므로,
    //       씬 파일 저장 방식이나 순서에 의존하지 않는다.
    void Start()
    {
        if (resumeButton != null)
        {
            resumeButton.onClick.AddListener(Resume);
        }
        if (retryButton != null)
        {
            retryButton.onClick.AddListener(Retry);
        }
        if (backToSelectButton != null)
        {
            backToSelectButton.onClick.AddListener(BackToSelect);
        }
        if (pausePanel != null)
        {
            pausePanel.SetActive(false);
        }
    }

    // 기능: 매 프레임 Esc 입력으로 일시정지를 토글하고, 곡이 끝났는지 확인한다.
    // 원리: 곡이 끝났는지는 AudioSource.isPlaying이 아니라 clock.CurrentTime과
    //       클립 길이를 직접 비교해서 판단한다 — musicSource.Pause()도
    //       isPlaying을 false로 만들기 때문에, isPlaying만 보면 "일시정지"와
    //       "곡이 끝남"을 구분할 수 없다. CurrentTime 기준으로 비교하면
    //       일시정지 중에는 CurrentTime 자체가 얼어있어 이 조건에 걸리지 않는다.
    void Update()
    {
        if (clock == null) return;

        if (Input.GetKeyDown(KeyCode.Escape) && clock.IsActive)
        {
            TogglePause();
        }

        if (!hasGoneToResult && clock.IsActive && !clock.IsPaused &&
            clock.musicSource != null && clock.musicSource.clip != null &&
            clock.CurrentTime >= clock.musicSource.clip.length + resultDelaySeconds)
        {
            GoToResult();
        }
    }

    // 기능: 일시정지 상태를 켜고 끈다.
    public void TogglePause()
    {
        if (clock.IsPaused)
        {
            Resume();
        }
        else
        {
            Pause();
        }
    }

    private void Pause()
    {
        clock.Pause();
        if (pausePanel != null) pausePanel.SetActive(true);
    }

    // 기능: "계속하기" 버튼에 연결한다.
    public void Resume()
    {
        clock.Resume();
        if (pausePanel != null) pausePanel.SetActive(false);
    }

    // 기능: 일시정지 패널의 "돌아가기" 버튼에 연결한다 — 곡 선택 화면으로 돌아간다.
    public void BackToSelect()
    {
        SceneLoader.Load(this, "SelectScene");
    }

    // 기능: 일시정지 패널의 "다시하기" 버튼에 연결한다 — 씬을 새로 로드하지
    //       않고, 지금 이 씬에서 같은 곡/난이도를 처음부터 다시 재생한다.
    // 원리: SelectScene을 거치지 않으므로 GameSession은 그대로 두고
    //       songLoader.Load()를 다시 호출한다(클립/채보 재배선, 변속 테이블
    //       재구성, 스포너 포인터·점수 리셋, clock.Play() 재예약까지 전부
    //       Load() 안에서 이미 하던 일이라 그대로 재사용한다). 여기서 꼭
    //       추가로 해줘야 하는 한 가지는 NoteRegistry.ClearAll()이다 —
    //       Load()의 ResetSpawner()는 "다음에 스폰할 노트" 포인터만 되돌릴
    //       뿐, 이전 시도에서 이미 화면에 떠 있던 노트는 그대로 남기 때문에
    //       먼저 전부 지워야 한다.
    public void Retry()
    {
        NoteRegistry.ClearAll();

        if (songLoader != null)
        {
            songLoader.Load(GameSession.SelectedSong, GameSession.SelectedDifficulty);
        }

        hasGoneToResult = false;
        if (pausePanel != null) pausePanel.SetActive(false);
    }

    // 기능: 곡이 끝났을 때 최종 점수/콤보를 GameSession에 담아 결과 화면으로 넘어간다.
    private void GoToResult()
    {
        hasGoneToResult = true;
        GameSession.FinalScore = ScoreManager.Instance.score;
        GameSession.FinalMaxCombo = ScoreManager.Instance.maxCombo;
        SceneLoader.Load(this, "ResultScene");
    }
}
