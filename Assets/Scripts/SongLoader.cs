using UnityEngine;

// ============================================================
// SongLoader.cs
//   "이 음원엔 이 채보"라는 매칭을 사람이 직접 기억해서
//   RhythmClock.musicSource.clip과 NoteSpawner.chart를 따로따로
//   맞춰줘야 하는 실수를 막기 위한 컴포넌트. 곡(SongData)과 그 안의
//   난이도(SongDifficulty)를 함께 받아 Load() 한 번 호출로 전부
//   배선하고 곡을 재생하기 시작한다.
//
//   곡 선택은 이제 다른 씬(SelectScene)에서 일어난다 — 거기서는 이
//   컴포넌트를 직접 부를 수 없으므로(씬이 다르면 서로의 오브젝트를
//   참조할 수 없다), SongSelectUI는 GameSession에 고른 곡/난이도를
//   적어두고 PlayScene을 로드하기만 한다. 이 씬(PlayScene)이 시작되면
//   Start()에서 GameSession을 읽어 자동으로 Load()를 호출한다.
// ============================================================
public class SongLoader : MonoBehaviour
{
    [Header("참조")]
    public RhythmClock clock;
    public NoteSpawner noteSpawner;
    public GameManager gameManager;

    [Tooltip("3-2-1 카운트다운이 항상 온전히(적어도 3초) 보이도록 하는 최소 리드인(초).")]
    public float minCountdownSeconds = 3f;

    [Header("배경")]
    [Tooltip("곡별 배경 이미지를 보여줄 SpriteRenderer — 선택 사항.")]
    public SpriteRenderer backgroundRenderer;
    [Tooltip("곡에 backgroundImage가 없을 때 대신 보여줄 기본 배경.")]
    public Sprite defaultBackground;

    // 기능: PlayScene이 시작되면 GameSession에 담겨온 곡/난이도로 자동 로드한다.
    // 원리: SelectScene에서 SceneManager.LoadScene으로 넘어왔기 때문에, 여기서는
    //       버튼 클릭 같은 이벤트가 아니라 그냥 씬이 시작됐다는 사실 자체가
    //       "재생을 시작하라"는 신호다. GameSession.SelectedSong이 비어있다면
    //       (예: 이 씬을 곡 선택 없이 직접 열어 테스트하는 경우) 경고만 남기고
    //       조용히 대기한다.
    void Start()
    {
        if (GameSession.SelectedSong == null || GameSession.SelectedDifficulty == null)
        {
            Debug.LogWarning("SongLoader: GameSession에 선택된 곡/난이도가 없습니다. SelectScene을 거치지 않고 PlayScene을 직접 열었을 수 있습니다.");
            return;
        }

        Load(GameSession.SelectedSong, GameSession.SelectedDifficulty);
    }

    // 기능: 곡(SongData)과 난이도(SongDifficulty)를 받아 이 조합으로 완전히
    //       전환한다 — 음원/채보 배선, 변속 테이블 재구성, 스포너·점수 리셋,
    //       재생 시작까지 한 번에 처리한다.
    // 원리: 음원(AudioClip)은 song에서, 채보(ChartData)는 difficulty에서 각각
    //       가져온다 — 같은 곡이라도 난이도마다 채보만 다르고 음원은 같기 때문에
    //       이렇게 나눠서 받는다. 순서가 중요한데, clip과 chart를 먼저 채워 넣고,
    //       gameManager.SetChart로 이 난이도의 변속 구간에 맞춰 거리 테이블을
    //       다시 굽고, ResetSpawner()로 "다음에 스폰할 노트" 포인터를 처음으로
    //       되돌리고, ResetScore()로 이전 곡의 점수/콤보를 지운 뒤, 마지막에
    //       clock.Play()를 호출해서 실제 재생을 시작한다. clip/chart가 아직
    //       비어 있는 상태로 Play()가 먼저 실행되면 안 되므로 반드시 이
    //       순서를 지킨다.
    public void Load(SongData song, SongDifficulty difficulty)
    {
        if (song == null || difficulty == null)
        {
            Debug.LogWarning("SongLoader: song 또는 difficulty가 null입니다.");
            return;
        }

        if (clock != null && clock.musicSource != null)
        {
            clock.musicSource.clip = song.audioClip;
        }

        if (backgroundRenderer != null)
        {
            backgroundRenderer.sprite = song.backgroundImage != null ? song.backgroundImage : defaultBackground;
        }

        if (noteSpawner != null)
        {
            noteSpawner.chart = difficulty.chart;
            noteSpawner.ResetSpawner();
        }

        if (gameManager != null)
        {
            gameManager.SetChart(difficulty.chart);
        }

        ScoreManager.Instance.ResetScore();

        if (clock != null)
        {
            // 노트가 spawnY에서 judgmentY까지 떨어지는 데 걸리는 시간(travelTime).
            // 카운트다운(minCountdownSeconds)이 "완전히 끝난 뒤에" 노트가 나타나
            // 떨어지기 시작해야 하므로, 전체 리드인은 둘 중 더 큰 값이 아니라
            // 둘을 합한 값이어야 한다 — max로 잡으면 travelTime이
            // minCountdownSeconds보다 짧을 때 노트가 카운트다운 도중에 이미
            // 나타나 버린다(실제로 보고된 버그). clock.noteLeadTime에 travelTime만
            // 따로 저장해두는 이유는, CountdownUI가 "카운트다운이 끝나고 노트가
            // 떨어지기 시작하는 시점"이 정확히 언제인지 알아야 하기 때문이다.
            float travelTime = 0f;
            if (gameManager != null)
            {
                float speed = Mathf.Max(gameManager.EffectiveSpeed, 0.0001f);
                travelTime = (gameManager.spawnY - gameManager.judgmentY) / speed;
            }
            clock.noteLeadTime = travelTime;
            float minLeadIn = travelTime + minCountdownSeconds;
            clock.Play(minLeadIn);
        }
    }
}
