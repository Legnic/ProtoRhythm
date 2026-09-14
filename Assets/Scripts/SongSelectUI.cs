using UnityEngine;
using UnityEngine.UI;

// ============================================================
// SongSelectUI.cs
//   SongLibrary에 담긴 곡마다 버튼을 하나씩 만들어 목록으로 보여준다.
//   버튼을 누르면 SongLoader.Load(song)을 호출해 그 곡을 배선하고
//   재생을 시작한 뒤, 선택 화면 자체를 숨긴다.
// ============================================================
public class SongSelectUI : MonoBehaviour
{
    [Header("참조")]
    public SongLibrary library;
    public SongLoader songLoader;

    [Header("UI")]
    [Tooltip("곡 버튼들이 생성될 부모 (Vertical Layout Group이 붙어있는 오브젝트)")]
    public RectTransform listParent;
    [Tooltip("곡 하나당 만들어질 버튼 프리팹. 자식에 Text가 있어야 한다.")]
    public Button buttonPrefab;

    // 기능: 씬이 시작될 때 library.songs 목록을 훑으며 곡마다 버튼을 하나씩 만든다.
    // 원리: buttonPrefab을 listParent 밑에 Instantiate하고(Vertical Layout Group이
    //       자동으로 세로 줄 세우기를 해준다), 버튼 자식의 Text 컴포넌트를 찾아
    //       곡 이름을 채워 넣는다. onClick에 리스너를 등록할 때 foreach의 song을
    //       그대로 캡처하지 않고 capturedSong이라는 지역 변수에 한 번 담아서
    //       넘기는 이유는, 클로저가 "변수 자체"를 참조하기 때문이다 — 지역 변수로
    //       한 번 더 감싸두면 각 버튼이 자기 차례에 만들어진 song 값을 확실히
    //       기억한다(참고: 최신 C#의 foreach는 매 반복마다 새 변수를 만들어서
    //       원래도 안전하지만, 의도를 명확히 하기 위해 명시적으로 분리했다).
    void Start()
    {
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

            // song은 foreach 변수라 클로저에 그대로 캡처하면 매번 같은 값을 참조하는
            // 문제가 생길 수 있어(C# 최신 버전에서는 안전하지만) 명시적으로 지역 변수에 담아 넘긴다.
            SongData capturedSong = song;
            button.onClick.AddListener(() => SelectSong(capturedSong));
        }
    }

    // 기능: 버튼 클릭으로 곡이 선택됐을 때, 그 곡을 실제로 로드/재생하고 선택 화면을 닫는다.
    // 원리: 판정/스폰/재생 배선은 전부 SongLoader.Load에 위임하고, 여기서는
    //       "선택 화면 UI를 더 이상 보여줄 필요가 없다"는 것만 처리한다
    //       (gameObject.SetActive(false)로 이 컴포넌트가 붙은 패널 전체를 숨긴다).
    void SelectSong(SongData song)
    {
        songLoader.Load(song);
        gameObject.SetActive(false);
    }
}
