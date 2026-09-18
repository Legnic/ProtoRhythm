using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// ============================================================
// SceneLoader.cs
//   SceneManager.LoadScene(동기)을 그대로 쓰면 씬 전환 순간 필요한
//   에셋을 전부 한 프레임 안에 불러오느라 화면이 한 번에 멎어버린다.
//   대신 LoadSceneAsync로 불러오면 유니티가 로딩 작업을 여러 프레임에
//   나눠 처리하므로, 버튼을 눌렀을 때 통째로 얼어붙는 대신 전환이
//   한결 매끄럽게 느껴진다. 프로젝트 전체의 씬 전환 버튼(TitleUI,
//   SongSelectUI, PlaySceneController, ResultUI, OptionsUI)이 전부
//   이 헬퍼 하나를 공유해서, 매번 코루틴을 따로 작성하지 않게 한다.
//
//   비동기로 불러오는 것 자체만으로는 "로딩 중"이라는 게 눈에 보이지
//   않는다 — 화면은 그냥 이전 화면을 계속 보여주다가 갑자기 다음 화면으로
//   바뀔 뿐이다. 그래서 씬을 불러오는 동안 화면 전체를 덮는 "로딩 중"
//   오버레이를 함께 띄운다.
//
//   이 오버레이는 앱이 켜지자마자 기본으로 "켜진" 채로 시작한다 —
//   타이틀 화면이 뜨기 전, 유니티 자체의 초기화나 그 화면의 첫 프레임
//   준비(예: TextGlyphWarmup의 폰트 글리프 워밍업)로 생기는 대기 시간도
//   함께 가려주기 위해서다. 그 준비를 담당하는 쪽(TextGlyphWarmup 등)이
//   다 끝나면 HideOverlay()를 직접 불러서 오버레이를 걷어낸다.
//
//   overlayRoot(코루틴을 돌리는 오브젝트)와 visualRoot(실제로 껐다 켰다
//   하는, 검은 배경+글자만 담은 오브젝트)를 반드시 분리해야 한다 — 처음엔
//   하나로 합쳐뒀다가, "너무 오래 떠 있으면 강제로 끈다"는 안전장치가
//   그 오브젝트 자체를 SetActive(false)하면서 그 위에서 돌고 있던 씬
//   로딩 코루틴까지 함께 멈춰버리는 문제가 실제로 있었다(비활성화된
//   오브젝트의 코루틴은 다시 켜져도 저절로 이어서 실행되지 않는다) —
//   그러면 씬 전환 자체가 영원히 끝나지 않는다. 코루틴은 항상 켜져 있는
//   overlayRoot에서 돌리고, 화면 표시는 그 자식인 visualRoot만 건드리므로
//   안전장치가 몇 번을 켜고 꺼도 코루틴에는 영향이 없다.
// ============================================================
public static class SceneLoader
{
    private static GameObject overlayRoot;
    private static GameObject visualRoot;
    private static SceneLoaderRunner runner;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (overlayRoot != null) return;

        overlayRoot = new GameObject("SceneLoaderOverlay");
        Object.DontDestroyOnLoad(overlayRoot);

        var canvas = overlayRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000; // 다른 모든 Canvas(전부 0) 위에 항상 뜨도록

        var scaler = overlayRoot.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);

        // 코루틴 실행 전용 — 절대 SetActive(false)하지 않는다.
        runner = overlayRoot.AddComponent<SceneLoaderRunner>();

        // 실제로 화면에 보이는 부분(배경+글자)만 따로 묶어서, 이것만 껐다 켰다 한다.
        visualRoot = new GameObject("Visual", typeof(RectTransform));
        visualRoot.transform.SetParent(overlayRoot.transform, false);
        var visualRt = visualRoot.GetComponent<RectTransform>();
        visualRt.anchorMin = Vector2.zero;
        visualRt.anchorMax = Vector2.one;
        visualRt.offsetMin = Vector2.zero;
        visualRt.offsetMax = Vector2.zero;

        var backgroundGO = new GameObject("Background", typeof(RectTransform));
        backgroundGO.transform.SetParent(visualRoot.transform, false);
        var backgroundRt = backgroundGO.GetComponent<RectTransform>();
        backgroundRt.anchorMin = Vector2.zero;
        backgroundRt.anchorMax = Vector2.one;
        backgroundRt.offsetMin = Vector2.zero;
        backgroundRt.offsetMax = Vector2.zero;
        backgroundGO.AddComponent<Image>().color = Color.black;

        // 로딩 텍스트는 영어("Loading")로 둔다 — 이 프로젝트 한글 폰트
        // (MalgunGothic SDF)는 에셋 참조라 여기서 코드로 즉시 물려줄 수
        // 없고, TMP 기본 폰트는 한글 글리프가 없어 한글을 쓰면 네모만
        // 보인다. "로딩 중"이라는 뜻만 전달하면 되는 자리라 영어로 충분하다.
        var labelGO = new GameObject("LoadingLabel", typeof(RectTransform));
        labelGO.transform.SetParent(visualRoot.transform, false);
        var labelRt = labelGO.GetComponent<RectTransform>();
        labelRt.anchorMin = new Vector2(0.5f, 0.5f);
        labelRt.anchorMax = new Vector2(0.5f, 0.5f);
        labelRt.sizeDelta = new Vector2(600f, 100f);
        var label = labelGO.AddComponent<TextMeshProUGUI>();
        label.alignment = TextAlignmentOptions.Center;
        label.fontSize = 48f;
        label.color = Color.white;
        label.text = "Loading";

        visualRoot.AddComponent<LoadingOverlayVisual>().label = label;

        // 앱이 시작하자마자 이미 켜진 채로 시작한다 — 타이틀 화면의 첫
        // 프레임 준비가 끝날 때까지(TextGlyphWarmup.HideOverlay 호출 전까지)
        // 화면을 가린다.
        visualRoot.SetActive(true);
    }

    // 기능: 지정한 씬으로 전환한다 — 로딩 오버레이를 띄운 채 비동기로 불러온다.
    public static void Load(MonoBehaviour host, string sceneName)
    {
        if (runner == null)
        {
            // Bootstrap이 무슨 이유로든 아직 안 됐다면(있을 수 없지만 방어적으로)
            // 로딩 화면 없이라도 씬 전환 자체는 되게 한다.
            host.StartCoroutine(LoadAsync(sceneName));
            return;
        }

        ShowOverlay();
        runner.StartCoroutine(LoadRoutine(sceneName));
    }

    // 기능: 오버레이를 켠다. 씬 전환(Load)뿐 아니라, 앱 시작 시점의 준비 작업
    //       동안 다른 코드가 직접 켜두고 싶을 때도 쓸 수 있다.
    public static void ShowOverlay()
    {
        if (visualRoot != null)
        {
            visualRoot.SetActive(true);
        }
    }

    // 기능: 오버레이를 끈다. TextGlyphWarmup처럼 앱 시작 시점의 준비 작업이
    //       끝났을 때 직접 호출해서 화면을 드러낸다.
    public static void HideOverlay()
    {
        if (visualRoot != null)
        {
            visualRoot.SetActive(false);
        }
    }

    private static IEnumerator LoadRoutine(string sceneName)
    {
        // 오버레이가 실제로 한 프레임 그려질 시간을 줘야, 로딩으로 메인
        // 스레드가 멈추기 직전에 "로딩 중" 화면이 먼저 눈에 들어온다.
        yield return null;

        AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);
        while (op != null && !op.isDone)
        {
            yield return null;
        }

        HideOverlay();
    }

    // Bootstrap이 실패했을 때만 쓰이는 대체 경로 — 오버레이 없이 그냥 비동기 로드만 한다.
    private static IEnumerator LoadAsync(string sceneName)
    {
        yield return SceneManager.LoadSceneAsync(sceneName);
    }
}

// 기능: SceneLoader는 정적 클래스라 코루틴을 직접 돌릴 수 없어서, 씬 로딩
//       코루틴 실행만 전담하는 작은 MonoBehaviour를 하나 붙여둔다. 이
//       오브젝트(overlayRoot)는 절대 SetActive(false)되지 않는다 —
//       그래야 코루틴이 중간에 멈추지 않는다.
public class SceneLoaderRunner : MonoBehaviour
{
}

// 기능: 오버레이의 "보이는 부분"(배경+글자)에 붙어서, 켜져 있는 동안
//       "Loading" 뒤에 점을 늘렸다 줄였다 하며 애니메이션을 주고, 안전장치
//       역할도 한다.
// 원리: OnEnable마다(=ShowOverlay로 켜질 때마다) 타이머를 리셋한다. 무슨
//       이유로든(예: TextGlyphWarmup이 오버레이를 끄는 걸 잊거나 실패하는
//       경우) 너무 오래 떠 있으면 스스로를 SetActive(false)해서 게임이
//       검은 화면에 영원히 갇히는 최악의 상황을 막는다 — 이 오브젝트는
//       SceneLoaderRunner와 분리되어 있으므로, 여기서 SetActive(false)를
//       해도 씬 로딩 코루틴에는 영향이 없다.
public class LoadingOverlayVisual : MonoBehaviour
{
    public TMP_Text label;

    private const float MaxShownSeconds = 8f;
    private const float DotIntervalSeconds = 0.4f;

    private float shownDuration;
    private float dotTimer;
    private int dotCount;

    void OnEnable()
    {
        shownDuration = 0f;
        dotTimer = 0f;
        dotCount = 0;
        if (label != null)
        {
            label.text = "Loading";
        }
    }

    void Update()
    {
        shownDuration += Time.unscaledDeltaTime;
        if (shownDuration > MaxShownSeconds)
        {
            gameObject.SetActive(false);
            return;
        }

        if (label == null) return;

        dotTimer += Time.unscaledDeltaTime;
        if (dotTimer >= DotIntervalSeconds)
        {
            dotTimer = 0f;
            dotCount = (dotCount + 1) % 4;
            label.text = "Loading" + new string('.', dotCount);
        }
    }
}
