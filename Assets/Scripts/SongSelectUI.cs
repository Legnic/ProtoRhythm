using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

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
    public TMP_Text titleText;
    [Tooltip("난이도 선택 단계에서만 보이는 뒤로가기 버튼(곡 목록으로) — 선택 사항.")]
    public Button backButton;
    [Tooltip("곡 목록 단계에서만 보이는 돌아가기 버튼(타이틀로) — 선택 사항.")]
    public Button quitToTitleButton;

    [Tooltip("난이도 선택 화면에서 곡 미리듣기를 재생하는 컴포넌트 — 선택 사항.")]
    public SongPreviewPlayer previewPlayer;

    [Header("배경")]
    [Tooltip("곡별 배경 이미지를 보여줄 SpriteRenderer — 선택 사항.")]
    public SpriteRenderer backgroundRenderer;
    [Tooltip("곡 목록 단계(아직 곡을 안 골랐을 때)나, 고른 곡에 backgroundImage가 없을 때 대신 보여줄 기본 배경.")]
    public Sprite defaultBackground;

    // 난이도 목록을 보여주는 중일 때, 어떤 곡을 골랐었는지 기억해둔다.
    private SongData selectedSong;

    // song.audioClip(미리듣기용)은 Addressable 참조라 난이도 화면으로 들어갈
    // 때마다 비동기로 로드해야 한다. Load를 다시 부르면 이전 곡의 핸들이
    // 자동으로 해제되므로, 곡을 여러 번 오가며 눌러도 메모리가 쌓이지 않는다.
    // 배경 이미지는 SongBackgroundCache(정적, 씬이 바뀌어도 유지)를 쓴다 —
    // 여기서 인스턴스 슬롯으로 들고 있다가 씬 전환으로 파괴되며 해제해버리면,
    // 다음 씬(PlayScene)이 방금 화면에 보여주고 있던 바로 그 배경을 처음부터
    // 다시 로드하게 된다(실제로 "플레이 화면 진입 시 배경이 한 박자 늦게
    // 뜬다"로 보고된 버그).
    private readonly AddressableAssetSlot<AudioClip> previewAudioSlot = new AddressableAssetSlot<AudioClip>();

    // 곡 목록이 뜨자마자 모든 곡의 배경 이미지를 미리 로드해두는 슬롯들이다 —
    // 곡을 클릭하는 시점엔 이미 다 로드돼 있어 배경이 살짝 늦게 뜨는 지연이
    // 사라진다. 오디오는 파일이 훨씬 커서 미리 로드하면 초기 진입이 오히려
    // 느려지므로 배경만 미리 로드한다. 이 화면을 떠나면(OnDestroy) 다른 곡들
    // 것은 더 필요 없으니 해제하지만, 실제로 고른 곡의 배경은
    // SongBackgroundCache가 별도로 들고 있어서 이 해제와 무관하게 이어진다.
    private readonly List<AddressableAssetSlot<Sprite>> backgroundPreloadSlots = new List<AddressableAssetSlot<Sprite>>();

    // 기능: 씬이 시작될 때 뒤로가기/타이틀로 버튼 리스너를 걸어두고, 곡 목록 화면으로 시작한다.
    void Start()
    {
        if (backButton != null)
        {
            backButton.onClick.AddListener(ShowSongList);
        }
        if (quitToTitleButton != null)
        {
            quitToTitleButton.onClick.AddListener(() => SceneLoader.Load(this, "TitleScene"));
        }

        ShowSongList();
        PreloadBackgrounds();
    }

    // 기능: 라이브러리에 있는 모든 곡의 배경 이미지를 한 번에 로드 걸어둔다.
    private void PreloadBackgrounds()
    {
        if (library == null) return;

        foreach (SongData song in library.songs)
        {
            if (song == null) continue;

            var slot = new AddressableAssetSlot<Sprite>();
            slot.Load(song.backgroundImage, _ => { });
            backgroundPreloadSlots.Add(slot);
        }
    }

    // 기능: 목록 영역을 SongLibrary의 곡들로 채운다 (선택의 1단계).
    // 원리: selectedSong을 비우고, 이 단계에서만 의미 있는 quitToTitleButton을
    //       보이고 backButton은 숨긴다. ClearList()로 이전 목록(난이도 버튼이었을
    //       수도 있는)을 지우고 곡마다 버튼을 새로 만든다. 버튼을 누르면 그 곡을
    //       인자로 ShowDifficultyList를 호출하도록 연결한다.
    void ShowSongList()
    {
        selectedSong = null;

        // 곡 목록 단계(아직 곡을 고르기 전 + 난이도 화면에서 뒤로 돌아온 경우
        // 모두 포함)에서는 미리듣기가 나오면 안 된다. 로드해뒀던 이전 곡의
        // 미리듣기 오디오 핸들도 이제 필요 없으니 해제한다(배경은
        // SongBackgroundCache가 별도로 관리하므로 여기서 건드리지 않는다).
        if (previewPlayer != null)
        {
            previewPlayer.Stop();
        }
        previewAudioSlot.Release();

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

        if (backgroundRenderer != null)
        {
            backgroundRenderer.sprite = defaultBackground;
        }

        if (AmbientLoopPlayer.Instance != null) AmbientLoopPlayer.Instance.EnsurePlaying();

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
            TMP_Text label = button.GetComponentInChildren<TMP_Text>();
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

        // 난이도 화면으로 넘어온 시점에 환경음을 멈추고 이 곡의 미리듣기를 시작한다.
        // 음원은 Addressable 참조라 즉시 쓸 수 없으므로 로드가 끝나는 대로 재생한다.
        if (AmbientLoopPlayer.Instance != null) AmbientLoopPlayer.Instance.Stop();

        previewAudioSlot.Load(song.audioClip, clip =>
        {
            if (previewPlayer != null)
            {
                previewPlayer.Play(clip);
            }
        });

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

        SongBackgroundCache.Load(song.backgroundImage, sprite =>
        {
            if (backgroundRenderer != null)
            {
                backgroundRenderer.sprite = sprite != null ? sprite : defaultBackground;
            }
        });

        ClearList();

        foreach (SongDifficulty difficulty in song.difficulties)
        {
            if (difficulty == null) continue;

            Button button = Instantiate(buttonPrefab, listParent);
            TMP_Text label = button.GetComponentInChildren<TMP_Text>();
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
        // 플레이 화면으로 넘어가면 미리듣기는 반드시 꺼져 있어야 한다 —
        // 그대로 두면 다음 씬에서도 계속 흘러나오거나(오디오는 씬을 넘어도
        // 저절로 멈추지 않는다), 곧이어 시작될 실제 곡 재생과 겹쳐 들린다.
        if (previewPlayer != null)
        {
            previewPlayer.Stop();
        }

        GameSession.SelectedSong = selectedSong;
        GameSession.SelectedDifficulty = difficulty;
        SceneLoader.Load(this, "PlayScene");
    }

    // 기능: 씬이 바뀌어 이 컴포넌트가 파괴될 때 로드해둔 미리듣기/미리로드용
    //       Addressable 핸들을 해제한다. 실제로 고른 곡의 배경(SongBackgroundCache)은
    //       여기서 해제하지 않는다 — 다음 씬(PlayScene)이 이어서 써야 한다.
    void OnDestroy()
    {
        previewAudioSlot.Release();
        foreach (var slot in backgroundPreloadSlots)
        {
            slot.Release();
        }
    }

    // 기능: listParent 밑의 버튼들을 전부 지운다.
    // 원리: 곡 목록 <-> 난이도 목록을 오갈 때마다 이전 단계의 버튼이 남아있으면
    //       안 되므로, 자식을 뒤에서부터 순서대로 Destroy한다(앞에서부터 지우면
    //       인덱스가 밀려서 하나씩 건너뛰는 실수가 생기기 쉽다).
    // 반드시 DestroyImmediate가 아니라 Destroy를 써야 한다 — 이 메서드는 곡
    //       버튼의 onClick(ShowDifficultyList)에서 호출되는데, 그 순간 지우는
    //       대상 중에는 "지금 클릭 이벤트를 처리하고 있는 바로 그 버튼 자신"도
    //       들어있다. DestroyImmediate로 자기 자신을 즉시 파괴하면 UGUI
    //       이벤트 시스템이 그 클릭을 마저 처리하는 도중에 상태가 꼬여서,
    //       실제로 "곡을 처음 클릭하면 난이도 화면이 아니라 곡 목록이 그대로
    //       남고, 뒤로 갔다가 다시 눌러야 정상적으로 넘어가는" 버그가 있었다.
    //       Destroy()는 파괴를 이번 프레임 끝으로 미루므로, 클릭 이벤트 처리가
    //       완전히 끝난 뒤에 안전하게 지워진다.
    void ClearList()
    {
        for (int i = listParent.childCount - 1; i >= 0; i--)
        {
            Destroy(listParent.GetChild(i).gameObject);
        }
    }
}
