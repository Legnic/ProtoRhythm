using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// ============================================================
// ChartEditorWindow.cs
//   인스펙터에서 (레인, 시각) 쌍을 하나하나 입력하는 대신,
//   BPM 기반 그리드에 클릭해서 노트를 찍는 채보 편집기.
//
//   사용법: 메뉴에서 Rhythm > Chart Editor 를 연다.
//     1. 대상 ChartData 애셋을 지정한다
//     2. BPM(외부 사이트 등에서 실제 곡의 BPM을 측정해 입력), 시작 오프셋,
//        마디 수, 박자 세분화(4분~64분음표)를 정한다 — "불러오기"를 하면
//        그 채보에 저장돼 있던 BPM/오프셋을 자동으로 채워준다
//     3. 노트 격자: 좌클릭하면 탭 노트, 좌클릭한 채로 아래로 드래그하면
//        그 길이만큼의 롱노트가 된다 (StepMania류 표준 방식). 클릭하기
//        전 커서를 올리면 반투명 미리보기가 뜬다. 이미 있는 노트(머리든
//        몸통이든) 위를 우클릭하면 통째로 지워진다 — 좌클릭과 분리해둔
//        이유는, 드래그하다 실수로 다른 노트 위에서 손을 떼는 것만으로
//        멀쩡한 노트가 지워지는 사고를 막기 위해서다.
//     4. 변속 열: 그 칸을 클릭하면 그 시각부터 적용되는 배속(기본 1.5배)이
//        추가되고, 숫자를 직접 입력해 원하는 배율로 바꿀 수 있다. "x"를
//        누르면 지운다. 다음 변속 지점이 나오기 전까지 그대로 유지된다
//        (계단식). 짧은 간격으로 여러 개 찍으면 부드러운 가감속처럼 보인다.
//     5. "채보 애셋에 저장"을 누르면 ChartData 애셋(노트+변속)에 실제로 기록된다
//     6. 재생 바: 미리듣기용 AudioClip을 지정하면 ▶로 재생/스페이스바로
//        토글할 수 있고, 시간 레이블을 클릭하면 그 지점부터 재생한다(스크러빙).
//        재생 중에는 격자에 재생 위치선이 표시되고 화면이 자동으로 따라간다.
//        마디 시작 줄을 지날 때마다 그 줄이 잠깐 밝아진다(메트로놈 강조).
//        1x/0.5x로 재생 속도를 바꿀 수 있다(0.5x는 피치도 낮아짐 — 정확한
//        타이밍만 느리게 확인하는 용도).
//
//   이 파일은 반드시 Assets/Editor 폴더 밑에 둬야 한다.
// ============================================================
public class ChartEditorWindow : EditorWindow
{
    // 기능: 상단 메뉴 "Rhythm > Chart Editor"를 눌렀을 때 창을 연다.
    // 원리: [MenuItem] 특성이 붙은 정적 메서드는 Unity가 에디터를 켤 때 자동으로
    //       스캔해서 메뉴에 등록해준다. GetWindow<T>()는 이미 같은 타입의 창이
    //       열려 있으면 그 창을 앞으로 가져오고, 없으면 새로 만든다.
    [MenuItem("Rhythm/Chart Editor")]
    public static void Open()
    {
        GetWindow<ChartEditorWindow>("Chart Editor");
    }

    // 격자 위의 노트 하나. startStep에서 시작해서 lengthSteps칸을 차지한다.
    // lengthSteps == 1이면 탭 노트, 그보다 크면 롱노트다.
    //
    // 시각 보존: 채보 애셋에서 불러온 노트는 원래의 정확한 시각(exactTime)과
    // 홀드 길이(exactHold)를 그대로 기억한다(keepsExactTime). 격자 칸
    // (startStep/lengthSteps)은 화면 표시용으로만 매번 다시 계산하고, 저장할
    // 때는 기억해둔 원래 시각을 그대로 쓴다 — 그래서 불러와서 아무것도 안 하고
    // 저장해도, BPM/세분화를 바꾼 뒤 저장해도, 격자에서 벗어나 있던 노트가 격자로
    // 끌려가 타이밍이 틀어지는 일이 없다. 편집기에서 새로 찍은 노트만 격자 시각으로
    // 저장된다.
    private class EditorNote
    {
        public int lane;
        public int startStep;
        public int lengthSteps;
        public bool keepsExactTime;
        public float exactTime;
        public float exactHold;
        public bool offGrid; // 표시용: 원래 시각이 현재 격자 칸에서 OffGridToleranceSeconds 넘게 벗어나 있음
    }

    // 변속 열의 항목 하나. 이 스텝부터 새 배속(multiplier)이 적용된다.
    // 노트와 마찬가지로, 불러온 항목은 원래 시각을 그대로 기억한다.
    private class EditorSpeedChange
    {
        public int step;
        public float multiplier;
        public bool keepsExactTime;
        public float exactTime;
    }

    public ChartData targetChart;

    private float bpm = 120f;
    private float offsetSeconds = 1.0f;
    private int laneCount = 4;
    private int beatsPerMeasure = 4;
    private int subdivision = 4; // 1=4분, 2=8분, 4=16분, 8=32분, 16=64분음표
    private int measureCount = 8;

    // 템포 맵(박마다의 실제 시각). 비어 있으면 bpm/offsetSeconds의 고정 격자를 쓴다.
    // 채보 애셋의 beatTimes에서 불러오고, 저장할 때 그대로 되돌려 쓴다.
    private readonly List<float> beatTimes = new List<float>();

    // 같은 레인의 같은 칸에 겹쳐서 화면에 하나만 보이는 노트 수(세분화가 너무 굵을 때 생김).
    private int hiddenOverlapCount;
    private int offGridCount;

    // 기능: 현재 설정(bpm/오프셋/세분화/템포 맵)으로 칸<->시각 변환기를 만든다.
    // 원리: 설정 필드가 매 프레임 바뀔 수 있으므로 캐시하지 않고 필요할 때마다
    //       만든다 — 가벼운 객체라 비용이 거의 없다.
    private ChartTimeGrid Grid => new ChartTimeGrid(bpm, offsetSeconds, subdivision, beatTimes);
    private float StepToTime(int step) => (float)Grid.StepToTime(step);

    private readonly List<EditorNote> notes = new List<EditorNote>();
    private readonly Dictionary<(int lane, int step), EditorNote> stepLookup = new Dictionary<(int, int), EditorNote>();

    private readonly List<EditorSpeedChange> speedChanges = new List<EditorSpeedChange>();
    private readonly Dictionary<int, EditorSpeedChange> speedStepLookup = new Dictionary<int, EditorSpeedChange>();

    // 드래그로 롱노트 길이를 늘리는 중일 때의 상태.
    private bool isDragging;
    private int dragLane;
    private EditorNote dragNote;

    // 아직 클릭하지 않고 커서만 올려둔 칸(반투명 미리보기용). 없으면 -1.
    private int hoverLane = -1;
    private int hoverStep = -1;

    private Vector2 scrollPos;

    // 미리듣기용 오디오. ChartData가 아니라 창 자체가 들고 있는 값이라 채보에 저장되지 않는다.
    public AudioClip previewClip;
    // 마지막으로 알고 있는 재생 위치(초, 채보 시각 기준). 재생을 멈춰도 유지돼서, 다시 누르면 그 자리에서 이어진다.
    private float previewTime;
    // previewTime이 한 번이라도 재생/탐색으로 채워졌는지 — false면 재생 위치선을 아직 그리지 않는다.
    private bool hasPreviewTime;
    // 재생 속도 배율. 0.5면 절반 속도(피치도 같이 낮아진다) — 정확한 타이밍만 느리게 확인하는 용도.
    private float playbackSpeed = 1f;

    // 메트로놈 강조: 재생 중 마디 시작 줄(▶ 표시)을 지나간 순간 그 줄을 잠깐 밝힌다.
    private int lastMetronomeStep = -1;
    private double metronomeFlashUntil;
    private const double MetronomeFlashDuration = 0.15;

    private static readonly Color EmptyColor = new Color(0.22f, 0.22f, 0.22f);
    private static readonly Color HeadColor = new Color(0.3f, 0.65f, 1f);
    private static readonly Color OffGridHeadColor = new Color(0.35f, 0.85f, 0.6f);
    private static readonly Color BodyColor = new Color(0.3f, 0.65f, 1f, 0.45f);
    private static readonly Color HoverPreviewColor = new Color(0.3f, 0.65f, 1f, 0.25f);
    private static readonly Color PlayheadColor = new Color(1f, 0.7f, 0.2f);
    private static readonly Color MetronomeFlashColor = new Color(1f, 0.95f, 0.4f, 0.35f);

    // 이보다 작은 차이는 사람이 느낄 수 없는 수준이라 "격자 위"로 본다(초록 표시 기준).
    private const double OffGridToleranceSeconds = 0.005;

    private const float CellWidth = 50f;
    private const float CellHeight = 18f;
    private const float SpeedColumnWidth = 64f;
    private const float DefaultSpeedMultiplier = 1.5f;
    private const float TimeColumnWidth = 70f;
    private const float RowHeight = CellHeight + 2f;   // EditorGUILayout이 칸 사이에 두는 여백까지 어림한 값
    private const float HeaderHeight = CellHeight + 20f;

    // 기능: 창이 열릴 때 한 번 호출된다. 마우스를 누르지 않고 움직이기만 해도
    //       OnGUI가 MouseMove 이벤트를 받도록 설정한다.
    // 원리: IMGUI는 기본적으로 마우스 버튼을 누르고 있을 때만 이벤트를 준다.
    //       클릭하기 전 커서 위치를 미리 알아야(반투명 미리보기) 하므로,
    //       wantsMouseMove를 켜서 단순 이동만으로도 이벤트가 오게 만든다.
    private void OnEnable()
    {
        wantsMouseMove = true;
    }

    // 기능: 창이 닫히거나 다른 창으로 교체될 때 호출된다. 재생 중이던 미리듣기를 멈춘다.
    // 원리: AudioUtil의 미리듣기는 창이 사라져도 스스로 멈추지 않는다 — 여기서 정리하지
    //       않으면 창을 닫아도 음악이 계속 흘러나오는 문제가 생긴다. EditorApplication.update
    //       구독도 함께 해지해서, 사라진 창을 계속 Repaint하려는 시도가 남지 않게 한다.
    private void OnDisable()
    {
        StopPreview();
    }

    // 기능: 이 창이 다시 그려질 때마다(IMGUI는 즉시 모드라 매 프레임/입력마다
    //       통째로 다시 실행된다) 설정 영역 -> 툴바 -> 격자 순서로 그린다.
    // 원리: 그리드 칸 밖에서 마우스를 놓아도(MouseUp) 드래그 상태가 확실히
    //       풀리도록, 개별 칸의 이벤트 처리와 별개로 여기서 한 번 더 검사한다
    //       — 그렇지 않으면 창 경계 밖에서 마우스를 떼는 경우 isDragging이
    //       true로 남아 다음 클릭 때 엉뚱하게 이어붙는 문제가 생길 수 있다.
    //       마우스가 창 밖으로 완전히 나가면(MouseLeaveWindow) 미리보기도
    //       꺼서, 창 밖 마지막 위치에 미리보기가 남아있는 것을 막는다.
    private void OnGUI()
    {
        if (Event.current.type == EventType.MouseUp)
        {
            isDragging = false;
            dragNote = null;
        }
        else if (Event.current.type == EventType.MouseLeaveWindow)
        {
            hoverLane = -1;
            hoverStep = -1;
            Repaint();
        }
        else if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Space)
        {
            // 스페이스바로 재생/정지를 토글한다. 텍스트 필드에 포커스가 있을 때도
            // 동작하도록 키 자체만 검사한다(스페이스는 어차피 텍스트 입력에선 문자로 소비되므로 충돌이 적다).
            if (AudioPreviewUtility.IsPlaying())
            {
                StopPreview();
            }
            else
            {
                PlayFrom(previewTime);
            }
            Event.current.Use();
        }

        DrawSettings();
        EditorGUILayout.Space();
        DrawPreviewBar();
        EditorGUILayout.Space();
        DrawToolbar();
        EditorGUILayout.Space();
        DrawGrid();
    }

    // 기능: 미리듣기 클립 지정 필드, 재생/정지 버튼, 현재 위치 표시를 그린다.
    // 원리: 재생 중인지는 매번 AudioPreviewUtility.IsPlaying()으로 직접 물어봐서
    //       판단한다 — 별도의 "재생 중" bool을 직접 들고 있으면 클립이 자연히
    //       끝났을 때(OnEditorUpdate가 감지하기 전 프레임) 상태가 잠깐 어긋날 수
    //       있기 때문이다.
    private void DrawPreviewBar()
    {
        bool playing = AudioPreviewUtility.IsPlaying();

        EditorGUILayout.BeginHorizontal();
        previewClip = (AudioClip)EditorGUILayout.ObjectField("미리듣기 오디오", previewClip, typeof(AudioClip), false);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();

        using (new EditorGUI.DisabledScope(previewClip == null))
        {
            if (GUILayout.Button(playing ? "⏸ 정지" : "▶ 재생 (Space)", GUILayout.Width(140)))
            {
                if (playing) StopPreview();
                else PlayFrom(previewTime);
            }

            // 격자는 오프셋(첫 박자)부터 시작하므로, 그 앞에 있을 수 있는
            // 도입부(인트로)는 격자 칸을 클릭해서는 절대 되돌아갈 수 없다.
            // 격자에 0초 줄을 추가하는 대신, 오디오 클립 자체의 맨 처음으로
            // 보내는 버튼을 따로 둔다 — 오프셋 앞쪽은 "채보 칸"이 아니라
            // "클립의 진짜 처음"이라는 뜻이 더 명확하기 때문이다.
            if (GUILayout.Button("⏮ 0:00", GUILayout.Width(60)))
            {
                SeekTo(0f);
            }
        }

        string posLabel = hasPreviewTime ? FormatTime(previewTime) : "--:--.-";
        string lenLabel = previewClip != null ? FormatTime(previewClip.length) : "--:--.-";
        GUILayout.Label($"{posLabel} / {lenLabel}", GUILayout.Width(140));

        GUILayout.Label("속도", GUILayout.Width(32));
        float previousSpeed = playbackSpeed;
        if (GUILayout.Toggle(Mathf.Approximately(playbackSpeed, 1f), "1x", "Button", GUILayout.Width(44)))
        {
            playbackSpeed = 1f;
        }
        if (GUILayout.Toggle(Mathf.Approximately(playbackSpeed, 0.5f), "0.5x", "Button", GUILayout.Width(44)))
        {
            playbackSpeed = 0.5f;
        }
        if (!Mathf.Approximately(previousSpeed, playbackSpeed) && playing)
        {
            // 재생 중에 속도를 바꾸면, 지금 위치에서 새 속도로 바로 이어 듣는다.
            PlayFrom(previewTime);
        }

        EditorGUILayout.EndHorizontal();
        EditorGUILayout.HelpBox(
            "시간 레이블(맨 왼쪽 칸)을 클릭하면 그 지점부터 재생된다. 0.5x는 피치도 같이 낮아지는 " +
            "대신(테이프를 천천히 돌리는 것과 같은 원리) 정확한 타이밍을 확인하기 좋다.",
            MessageType.None);
    }

    // 기능: 초 단위 시간을 "분:초.소수" 문자열로 바꾼다(예: 83.4 -> "1:23.4").
    private static string FormatTime(float seconds)
    {
        seconds = Mathf.Max(0f, seconds);
        int minutes = (int)(seconds / 60f);
        float rem = seconds - minutes * 60f;
        return $"{minutes}:{rem:00.0}";
    }

    // 기능: 지정한 시각(초, 채보 기준)부터 previewClip을 현재 playbackSpeed로
    //       재생을 시작하고, 매 에디터 틱마다 위치를 갱신하도록
    //       EditorApplication.update에 구독한다.
    // 원리: playbackSpeed가 1이 아니면 AudioPreviewUtility.GetSpeedVariant가
    //       실제로 재생할, 주파수만 다르게 선언한 클립을 만들어(또는 캐시에서
    //       찾아) 돌려준다. 이 변형 클립은 declared frequency가 원본과 다르므로,
    //       시작 샘플 위치는 반드시 "원본 클립의 frequency"로 계산해야 한다
    //       (같은 샘플 데이터를 그대로 재사용했을 뿐, 샘플 순서 자체는 안 바뀌었기
    //       때문). EditorWindow는 Play 모드의 MonoBehaviour처럼 매 프레임 Update가
    //       오지 않기 때문에, 재생 위치를 계속 읽어와 화면(재생 위치선/자동 스크롤)을
    //       갱신하려면 EditorApplication.update 콜백으로 직접 틱을 받아야 한다.
    //       중복 구독을 막기 위해 먼저 해지하고 다시 구독한다.
    private void PlayFrom(float time)
    {
        if (previewClip == null) return;

        AudioClip clipToPlay = AudioPreviewUtility.GetSpeedVariant(previewClip, playbackSpeed);
        int startSample = Mathf.RoundToInt(time * previewClip.frequency);

        AudioPreviewUtility.Stop();
        AudioPreviewUtility.PlaySamples(clipToPlay, startSample);
        previewTime = time;
        hasPreviewTime = true;
        lastMetronomeStep = -1;

        EditorApplication.update -= OnEditorUpdate;
        EditorApplication.update += OnEditorUpdate;
        Repaint();
    }

    // 기능: 실제로 재생을 시작하지 않고, "다음에 재생을 누르면 여기서부터
    //       시작한다"는 위치(previewTime)만 옮긴다. 재생 중이었다면 멈춘다.
    // 원리: 예전에는 시간 레이블을 클릭하면 그 자리에서 곧바로 재생까지
    //       시작해버렸는데(PlayFrom), 그러면 위치만 확인하고 싶어도 매번
    //       소리가 나버리고, 클릭할 때마다 Stop 직후 곧바로 Play가 실행되어
    //       재생 시작 부분이 유실되는 문제(AudioPreviewUtility 참고)도 같이
    //       겪게 된다. 위치 이동과 재생 시작을 분리해서, 사용자가 ▶ 재생
    //       버튼(또는 스페이스바)을 눌렀을 때만 실제로 소리가 나게 한다.
    private void SeekTo(float time)
    {
        if (AudioPreviewUtility.IsPlaying())
        {
            StopPreview();
        }

        previewTime = time;
        hasPreviewTime = true;
        lastMetronomeStep = -1;
        Repaint();
    }

    // 기능: 재생을 멈추고 EditorApplication.update 구독을 해지한다.
    private void StopPreview()
    {
        AudioPreviewUtility.Stop();
        EditorApplication.update -= OnEditorUpdate;
    }

    // 기능: 재생 중일 때 매 틱마다 현재 위치를 읽어와 화면을 갱신하고, 마디
    //       시작 줄을 막 지났다면 메트로놈 강조를 켠다.
    // 원리: AudioUtil의 미리듣기는 클립이 끝나면 스스로 멈추는데, 그걸 InputJudge처럼
    //       이벤트로 알려주지 않으므로 IsPlaying()을 계속 물어봐서 "이미 멈췄다"를
    //       스스로 감지해야 한다. 멈췄으면 구독을 해지해서 더 이상 불필요하게
    //       Repaint를 요청하지 않는다.
    //       GetPosition()은 "지금 실제로 재생 중인 클립"(속도 변형본일 수 있다)의
    //       자기 시간이므로, 채보 시각으로 되돌리려면 playbackSpeed를 곱해야 한다
    //       — 절반 속도로 재생 중이면 그 클립 기준 2초가 지나도 채보상으로는
    //       1초만 지난 것이기 때문이다.
    private void OnEditorUpdate()
    {
        if (!AudioPreviewUtility.IsPlaying())
        {
            EditorApplication.update -= OnEditorUpdate;
            return;
        }

        previewTime = AudioPreviewUtility.GetPosition() * playbackSpeed;

        int currentStep = (int)System.Math.Round(Grid.TimeToStep(previewTime));
        int stepsPerMeasure = beatsPerMeasure * subdivision;
        if (currentStep >= 0 && currentStep > lastMetronomeStep && currentStep % stepsPerMeasure == 0)
        {
            lastMetronomeStep = currentStep;
            metronomeFlashUntil = EditorApplication.timeSinceStartup + MetronomeFlashDuration;
        }

        Repaint();
    }

    // 기능: 대상 ChartData, BPM, 오프셋, 레인 수, 박자 설정 등 채보 편집에
    //       필요한 값들을 입력받는 필드들을 그린다.
    // 원리: EditorGUILayout의 각 필드 메서드는 "현재 값을 보여주고, 사용자가
    //       바꾸면 그 즉시 새 값을 반환"하는 방식으로 동작한다(IMGUI 특성).
    //       그래서 `bpm = EditorGUILayout.FloatField("BPM", bpm)`처럼 반환값을
    //       바로 같은 필드에 대입하는 패턴이 반복된다. bpm은 0 이하가 되면
    //       나눗셈(secondsPerStep 계산)에서 문제가 생기므로 Mathf.Max(1f, ...)로
    //       최소값을 보장한다. 세분화 옵션은 4분음표(1)부터 64분음표(16)까지
    //       두 배씩 촘촘해지도록 늘려뒀다 — 변속을 짧은 간격으로 여러 개
    //       찍으려면 그만큼 잘게 쪼갤 수 있어야 하기 때문이다.
    private void DrawSettings()
    {
        EditorGUILayout.LabelField("채보 설정", EditorStyles.boldLabel);
        targetChart = (ChartData)EditorGUILayout.ObjectField("대상 ChartData", targetChart, typeof(ChartData), false);

        // 템포 맵이 있으면 격자는 박 시각 목록을 따르므로 BPM/오프셋 입력은 쓰이지 않는다 —
        // 헷갈리지 않도록 잠가두고, 맵의 평균 BPM과 첫 박 시각을 안내한다.
        bool usesTempoMap = beatTimes.Count >= 2;
        if (usesTempoMap)
        {
            EditorGUILayout.HelpBox(
                $"템포 맵 사용 중: {beatTimes.Count}박, 평균 BPM {Grid.AverageBpm():0.###}, 첫 박 {beatTimes[0]:0.###}초. " +
                "격자 칸이 곡의 실제 박 위치를 따라갑니다(템포가 조금씩 변하는 곡용).",
                MessageType.Info);
            if (GUILayout.Button("템포 맵 해제 (고정 BPM 격자로 전환)", GUILayout.Width(240)))
            {
                if (EditorUtility.DisplayDialog("템포 맵 해제",
                        "고정 BPM 격자로 바꿉니다. 이미 있는 노트의 시각은 그대로 보존되지만, 이후 새로 찍는 노트는 " +
                        "곡의 실제 박에서 어긋날 수 있습니다. 저장해야 채보 애셋에 반영됩니다.", "해제", "취소"))
                {
                    bpm = (float)Grid.AverageBpm();
                    // 오프셋 입력은 0 이상만 허용하므로, 첫 박이 0초보다 앞이면(곡 시작 직전) 그다음 박을 쓴다.
                    offsetSeconds = beatTimes.Find(t => t >= 0f);
                    beatTimes.Clear();
                    usesTempoMap = false;
                }
            }
        }
        using (new EditorGUI.DisabledScope(usesTempoMap))
        {
            bpm = Mathf.Max(1f, EditorGUILayout.FloatField("BPM", bpm));
            // 음수 오프셋은 격자/시간 계산에서 의미가 없다(첫 박자가 곡 시작보다도
            // 앞선다는 뜻이 되어버린다) — bpm과 같은 이유로 여기서 0 이상으로 막는다.
            offsetSeconds = Mathf.Max(0f, EditorGUILayout.FloatField("시작 오프셋(초)", offsetSeconds));
        }
        laneCount = EditorGUILayout.IntSlider("레인 수", laneCount, 1, 8);
        beatsPerMeasure = EditorGUILayout.IntSlider("한 마디당 박자 수", beatsPerMeasure, 1, 8);
        subdivision = EditorGUILayout.IntPopup("박자 세분화", subdivision,
            new[] { "4분음표", "8분음표", "16분음표", "32분음표", "64분음표" },
            new[] { 1, 2, 4, 8, 16 });

        EditorGUILayout.BeginHorizontal();
        measureCount = Mathf.Max(1, EditorGUILayout.IntField("마디 수", measureCount));
        using (new EditorGUI.DisabledScope(previewClip == null))
        {
            if (GUILayout.Button("곡 길이에 맞추기", GUILayout.Width(110)))
            {
                FitMeasureCountToPreviewClip();
            }
        }
        EditorGUILayout.EndHorizontal();
    }

    // 기능: "곡 길이에 맞추기" 버튼에 연결된다. 격자가 previewClip 전체 길이를
    //       덮기에 충분한 마디 수를 계산해서 measureCount에 채워 넣는다.
    // 원리: 마디 수는 슬라이더(최대 64)로 제한되어 있었는데, BPM/세분화에 따라
    //       64마디가 곡 전체 길이(특히 3~4분대 곡)를 못 덮는 경우가 있어
    //       "20초 정도밖에 채보를 못 찍는다"는 문제가 생겼다 — 사용자가 매번
    //       마디 수를 감으로 늘려야 했던 것도 번거로웠다. 여기서는 곡 길이
    //       (previewClip.length)에서 오프셋을 뺀 만큼을 secondsPerStep으로
    //       나눠 필요한 스텝 수를 구하고, 한 마디의 스텝 수(beatsPerMeasure *
    //       subdivision)로 올림 나눗셈해서 정확히 곡 끝까지(또는 그 이상)
    //       덮는 마디 수를 얻는다.
    private void FitMeasureCountToPreviewClip()
    {
        if (previewClip == null) return;

        int stepsPerMeasure = Mathf.Max(1, beatsPerMeasure * subdivision);
        int neededSteps = Mathf.Max(0, Mathf.CeilToInt((float)Grid.TimeToStep(previewClip.length)));

        measureCount = Mathf.Max(1, Mathf.CeilToInt((float)neededSteps / stepsPerMeasure));
        Repaint();
    }

    // 기능: 불러오기/저장/전체 지우기 버튼과 조작 안내 문구를 그린다.
    // 원리: GUILayout.Button(...)은 그 프레임에 버튼이 "눌렸는지"를 bool로
    //       돌려주는 즉시 모드 방식이라, if문 안에서 바로 해당 동작을 호출하는
    //       식으로 쓴다. "전체 지우기"는 되돌릴 수 없는 동작이라 실수 방지를
    //       위해 DisplayDialog로 한 번 더 확인을 받은 뒤에만 지운다(노트/변속 둘 다).
    private void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("채보 애셋에서 불러오기"))
        {
            LoadFromChart();
        }
        if (GUILayout.Button("채보 애셋에 저장"))
        {
            SaveToChart();
        }
        if (GUILayout.Button("전체 지우기"))
        {
            if (EditorUtility.DisplayDialog("전체 지우기", "편집 중인 노트와 변속을 모두 지울까요?", "지우기", "취소"))
            {
                notes.Clear();
                speedChanges.Clear();
            }
        }
        if (GUILayout.Button("미리듣기 캐시 비우기", GUILayout.Width(130)))
        {
            AudioPreviewUtility.ClearDiskCache();
            Debug.Log("Chart Editor: 배속 미리듣기 캐시를 비웠습니다.");
        }

        EditorGUILayout.EndHorizontal();
        EditorGUILayout.HelpBox(
            "노트 칸: 좌클릭 = 탭 노트, 좌클릭한 채로 드래그 = 롱노트, 노트 위 우클릭 = 삭제.\n" +
            "변속 칸: '+' 클릭 = 그 시각부터 배속 추가(숫자 직접 입력 가능), 'x' = 삭제.\n" +
            "\"미리듣기 캐시 비우기\"는 배속 변경 미리듣기용으로 만들어둔 디스크 파일만 지운다 — 채보 데이터와는 무관하다.\n" +
            "불러온 노트는 원래 시각 그대로 저장된다. 초록색 노트 = 격자 칸 사이에 있는 노트(가장 가까운 칸에 표시됨).",
            MessageType.None);

        if (hiddenOverlapCount > 0)
        {
            EditorGUILayout.HelpBox(
                $"같은 칸에 겹쳐서 하나만 보이는 노트가 {hiddenOverlapCount}개 있습니다. 저장해도 지워지지 않지만, " +
                "보고 편집하려면 '박자 세분화'를 더 촘촘하게 바꾸세요.",
                MessageType.Warning);
        }
    }

    // 기능: BPM/마디 수/세분화 설정에 맞춰 전체 격자(시간 레이블 + 변속 열 +
    //       레인별 노트 칸)를 그린다.
    // 원리: secondsPerStep = (60/bpm)/subdivision으로 "칸 하나가 몇 초인지"를
    //       구하고, 그 값으로 각 칸의 실제 시각(offsetSeconds + step*secondsPerStep)을
    //       계산해 왼쪽에 표시한다. 매 프레임 RebuildStepLookup()/
    //       RebuildSpeedLookup()을 먼저 호출해서 "이 칸에 무엇이 있는지"를
    //       조회 가능한 상태로 만들어둔 뒤, 스크롤 뷰 안에서 스텝(행) 단위로
    //       시간 레이블 -> 변속 칸 -> 레인별 노트 칸 순서로 그린다.
    //
    //       마디 수가 많고 세분화가 촘촘하면(예: 64마디 x 64분음표) 행이
    //       수천 개에 이를 수 있는데, wantsMouseMove 때문에 마우스만 움직여도
    //       OnGUI가 다시 실행되므로 매번 전체 행을 그대로 레이아웃하면 편집기
    //       자체가 버벅인다. 그래서 실제로 스크롤 뷰에 보이는 범위(위아래로
    //       몇 줄 여유 포함)만 실제로 그리고, 화면 밖 위/아래는 그 높이만큼의
    //       빈 자리(GetRect)로만 채운다 — 스크롤바 길이는 기존과 동일하게
    //       전체 행 수 기준으로 유지되면서, 화면에 보이지 않는 행은 애초에
    //       그려지지 않으니 결과는 이전과 똑같다.
    private void DrawGrid()
    {
        int totalSteps = measureCount * beatsPerMeasure * subdivision;
        ChartTimeGrid grid = Grid;

        // 오프셋(offsetSeconds) 이전, 즉 곡의 진짜 도입부(0초~오프셋)도 격자에
        // 표시하고 그 구간에 노트를 놓을 수 있어야 한다 — 실제 게임에서는
        // 음악이 오디오 클립의 0초 지점부터 재생되므로(RhythmClock 참고),
        // 오프셋 이전이라고 노트를 못 놓을 이유가 없다. 이 구간은 step을
        // 음수로 확장해서 표현한다: step -preSteps가 대략 0초, step 0이
        // 오프셋(첫 박자)이 되도록 한다.
        int preSteps = Mathf.Max(0, Mathf.CeilToInt((float)-grid.TimeToStep(0)));

        RebuildStepLookup();
        RebuildSpeedLookup();

        bool playing = AudioPreviewUtility.IsPlaying();
        int playheadStep = hasPreviewTime ? (int)System.Math.Round(grid.TimeToStep(previewTime)) : int.MinValue;

        int firstStep = -preSteps;
        int rowCount = totalSteps - firstStep;

        // 재생 중일 때만 재생 위치선을 따라 자동으로 스크롤한다. 멈춰 있을 땐
        // 사용자가 자유롭게 격자를 둘러볼 수 있어야 하므로 건드리지 않는다.
        // 화면상 행 위치는 맨 위에 그려지는 step(firstStep)을 0으로 놓고
        // 세어야 하므로 playheadStep에서 firstStep을 빼서 픽셀로 환산한다.
        if (playing && playheadStep >= firstStep)
        {
            float rowY = HeaderHeight + (playheadStep - firstStep) * RowHeight;
            scrollPos.y = Mathf.Max(0f, rowY - 150f);
        }

        const float ViewportHeight = 500f;
        scrollPos = EditorGUILayout.BeginScrollView(scrollPos, GUILayout.Height(ViewportHeight));

        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("", GUILayout.Width(TimeColumnWidth));
        GUILayout.Label("변속", GUILayout.Width(SpeedColumnWidth));
        for (int lane = 0; lane < laneCount; lane++)
        {
            GUILayout.Label($"레인 {lane}", GUILayout.Width(CellWidth));
        }
        EditorGUILayout.EndHorizontal();

        // 지금 스크롤 위치(scrollPos.y)를 기준으로, 실제로 화면에 보이는 행
        // 인덱스 범위(0-based, firstStep로부터 몇 번째 행인지)만 계산한다.
        // 위아래로 4줄씩 여유를 둬서, 스크롤 도중 경계 부분이 한 프레임
        // 비어 보이는 것을 막는다.
        int firstVisibleIndex = Mathf.Max(0, Mathf.FloorToInt((scrollPos.y - HeaderHeight) / RowHeight) - 4);
        int lastVisibleIndex = Mathf.Min(rowCount - 1, Mathf.CeilToInt((scrollPos.y - HeaderHeight + ViewportHeight) / RowHeight) + 4);

        if (firstVisibleIndex > 0)
        {
            // 화면 위쪽에 가려진 행들은 그리지 않고, 그만큼의 빈 높이만 예약해둔다.
            GUILayoutUtility.GetRect(1f, firstVisibleIndex * RowHeight);
        }

        for (int i = firstVisibleIndex; i <= lastVisibleIndex; i++)
        {
            int step = firstStep + i;
            EditorGUILayout.BeginHorizontal();

            float time = (float)grid.StepToTime(step);
            DrawTimeCell(time, step);
            DrawSpeedCell(step);

            Rect rowRect = default;
            for (int lane = 0; lane < laneCount; lane++)
            {
                Rect cellRect = GUILayoutUtility.GetRect(CellWidth, CellHeight, GUILayout.Width(CellWidth));
                if (lane == 0) rowRect = cellRect;
                DrawCell(cellRect, lane, step);
            }

            EditorGUILayout.EndHorizontal();

            if (step == lastMetronomeStep && EditorApplication.timeSinceStartup < metronomeFlashUntil)
            {
                // 메트로놈 강조: 마디 시작 줄을 막 지난 순간 그 줄 전체를 잠깐 밝힌다.
                // 재생 위치선보다 먼저 그려서, 위치선이 그 위에 덮이도록 한다.
                EditorGUI.DrawRect(new Rect(0f, rowRect.y, 2000f, rowRect.height), MetronomeFlashColor);
            }

            if (step == playheadStep)
            {
                // 라벨/변속 칸까지 포함해서 폭 전체에 걸치도록 넉넉한 너비로 그린다
                // (ScrollView가 어차피 넘치는 부분은 잘라서 보여주므로 안전하다).
                EditorGUI.DrawRect(new Rect(0f, rowRect.y, 2000f, 2f), PlayheadColor);
            }
        }

        int remainingRows = rowCount - 1 - lastVisibleIndex;
        if (remainingRows > 0)
        {
            // 화면 아래쪽에 가려진 행들도 마찬가지로 빈 높이만 예약해둔다.
            GUILayoutUtility.GetRect(1f, remainingRows * RowHeight);
        }

        EditorGUILayout.EndScrollView();
    }

    // 기능: 격자 왼쪽의 시간 레이블 칸을 그리고, 클릭하면 그 시각부터 미리듣기를 시작한다(스크러빙).
    // 원리: 표준 GUILayout.Label은 클릭을 감지하지 못하므로, 노트 칸과 똑같은 방식으로
    //       GUILayoutUtility.GetRect로 직접 사각형을 받아 Event.current로 클릭을 잡는다.
    private void DrawTimeCell(float time, int step)
    {
        // 마디 시작 표시(▶)는 채보의 첫 박자(step 0, 오프셋)부터 셈한다 — 그
        // 이전(도입부, 음수 step)은 아직 "마디"라는 개념이 없는 구간이므로
        // 항상 일반 칸으로 표시한다.
        bool isMeasureStart = step >= 0 && (step % (beatsPerMeasure * subdivision)) == 0;
        string timeLabel = isMeasureStart ? $"▶ {time:0.00}" : $"{time:0.00}";

        Rect rect = GUILayoutUtility.GetRect(TimeColumnWidth, CellHeight, GUILayout.Width(TimeColumnWidth));
        GUI.Label(rect, timeLabel);

        Event e = Event.current;
        if (e.type == EventType.MouseDown && rect.Contains(e.mousePosition) && previewClip != null)
        {
            SeekTo(time);
            e.Use();
        }
    }

    // 기능: 격자 칸 하나를 실제로 색칠하고, 그 칸의 마우스 입력 처리를 맡긴다.
    // 원리: 이 칸에 노트가 있으면 HeadColor/BodyColor로 칠한다. 노트가 없고
    //       지금 이 칸이 hoverLane/hoverStep과 일치한다면(클릭 전 커서만
    //       올려둔 상태) 반투명 HoverPreviewColor로 "여기 찍으면 이렇게
    //       된다"를 미리 보여준다. 드래그 중에는 미리보기를 끄는데, 드래그
    //       자체가 이미 시각적 피드백(늘어나는 노트)을 주고 있어서 겹쳐
    //       보이면 오히려 헷갈리기 때문이다. 실제 클릭/드래그 판정은
    //       HandleCellInput에 위임한다.
    private void DrawCell(Rect rect, int lane, int step)
    {
        EditorNote note = GetNoteAt(lane, step);
        bool isHead = note != null && note.startStep == step;

        Color fill;
        if (note != null)
        {
            fill = isHead ? (note.offGrid ? OffGridHeadColor : HeadColor) : BodyColor;
        }
        else if (!isDragging && lane == hoverLane && step == hoverStep)
        {
            fill = HoverPreviewColor;
        }
        else
        {
            fill = EmptyColor;
        }

        EditorGUI.DrawRect(new Rect(rect.x + 1, rect.y + 1, rect.width - 2, rect.height - 2), fill);

        HandleCellInput(rect, lane, step, note);
    }

    // 기능: 격자 칸 하나의 마우스 이동/클릭/드래그를 감지해서, 커서 위치를
    //       기억하거나(미리보기) 노트를 만들거나, 늘리거나, 지운다
    //       (StepMania류: 좌클릭=탭, 좌클릭+드래그=롱노트 길이 지정,
    //       우클릭=삭제).
    // 원리: GUILayout.Toggle 같은 표준 컨트롤로는 "누른 채로 드래그"나
    //       "누르지 않고 지나가기"를 감지할 수 없어서, Event.current로 이
    //       칸의 사각형 위에서 일어난 원시 마우스 이벤트를 직접 읽는다.
    //       MouseMove는 클릭/드래그와 무관하게 hoverLane/hoverStep만
    //       갱신하고 끝낸다(다른 칸이 이미 hover 상태를 차지하고 있었다면
    //       바뀐 경우에만 Repaint해서 불필요한 다시그리기를 줄인다).
    //
    //       삭제를 좌클릭이 아니라 우클릭(e.button == 1)에서만 하는 이유:
    //       예전에는 좌클릭 한 번으로 "빈 칸이면 생성, 노트가 있으면 삭제"를
    //       같이 처리했는데, 그러면 드래그로 롱노트를 늘이다가 실수로 다른
    //       노트 위에서 손을 떼는 것만으로 그 노트가 지워지는 사고가 날 수
    //       있었다. 지금은 좌클릭(button 0)은 항상 "생성 + 드래그 시작"만
    //       담당하고(이미 노트가 있는 칸의 좌클릭은 그냥 무시), 삭제는
    //       button 1(우클릭)에서만 일어나므로 실수로 지워질 일이 없다.
    //       MouseDown 이후 같은 레인 안에서 MouseDrag가 들어오면, "시작
    //       칸부터 지금 칸까지의 거리 + 1"을 새 길이로 갱신한다. e.Use()는
    //       이 이벤트를 자신이 처리했다고 Unity에 알려서 다른 컨트롤(우클릭
    //       기본 컨텍스트 메뉴 등)이 같은 이벤트를 중복 처리하지 않게 막는다.
    private void HandleCellInput(Rect rect, int lane, int step, EditorNote note)
    {
        Event e = Event.current;

        if (e.type == EventType.MouseMove)
        {
            if (rect.Contains(e.mousePosition) && (hoverLane != lane || hoverStep != step))
            {
                hoverLane = lane;
                hoverStep = step;
                Repaint();
            }
            return;
        }

        if (!rect.Contains(e.mousePosition))
        {
            return;
        }

        if (e.type == EventType.MouseDown)
        {
            if (e.button == 1)
            {
                // 우클릭 = 삭제. 머리든 몸통이든, 노트 위에서 우클릭하면 그 노트를 통째로 지운다.
                if (note != null)
                {
                    notes.Remove(note);
                    Repaint();
                }
                e.Use();
                return;
            }

            if (e.button != 0)
            {
                // 가운데 버튼 등은 이 격자에서 다루지 않는다.
                return;
            }

            if (note == null)
            {
                var newNote = new EditorNote { lane = lane, startStep = step, lengthSteps = 1 };
                notes.Add(newNote);
                isDragging = true;
                dragLane = lane;
                dragNote = newNote;
                Repaint();
            }
            // 이미 노트가 있는 칸의 좌클릭은 아무 일도 하지 않는다(삭제는 우클릭 전용).
            e.Use();
        }
        else if (e.type == EventType.MouseDrag && isDragging && lane == dragLane)
        {
            // 시작 칸보다 아래로 드래그한 만큼 롱노트 길이를 늘린다 (StepMania류).
            int newLength = step - dragNote.startStep + 1;
            dragNote.lengthSteps = Mathf.Max(1, newLength);
            e.Use();
            Repaint();
        }
    }

    // 기능: 변속 열의 칸 하나를 그린다. 그 스텝에 변속 항목이 있으면 배속을
    //       직접 입력할 수 있는 숫자 필드와 삭제 버튼을, 없으면 추가 버튼을 보여준다.
    // 원리: RebuildSpeedLookup이 미리 채워둔 조회 테이블에서 이 스텝에
    //       해당하는 항목을 찾는다. 있으면 FloatField로 즉시 편집 가능하게
    //       하고("x"로 삭제), 없으면 "+" 버튼 하나로 기본 배속
    //       (DefaultSpeedMultiplier)을 가진 새 항목을 추가한다 — 추가 직후
    //       바로 값을 조정할 수 있도록 다음 프레임부터 그 칸이 숫자 필드로
    //       바뀐다.
    private void DrawSpeedCell(int step)
    {
        EditorGUILayout.BeginHorizontal(GUILayout.Width(SpeedColumnWidth));

        speedStepLookup.TryGetValue(step, out EditorSpeedChange change);
        if (change != null)
        {
            float newMultiplier = EditorGUILayout.FloatField(change.multiplier, GUILayout.Width(SpeedColumnWidth - 20));
            if (!Mathf.Approximately(newMultiplier, change.multiplier))
            {
                change.multiplier = newMultiplier;
            }
            if (GUILayout.Button("x", GUILayout.Width(18)))
            {
                speedChanges.Remove(change);
                Repaint();
            }
        }
        else
        {
            if (GUILayout.Button("+", GUILayout.Width(SpeedColumnWidth)))
            {
                speedChanges.Add(new EditorSpeedChange { step = step, multiplier = DefaultSpeedMultiplier });
                Repaint();
            }
        }

        EditorGUILayout.EndHorizontal();
    }

    // 기능: notes 리스트를 훑어서 "(레인, 스텝) -> 그 칸을 차지하는 노트"
    //       조회 테이블(stepLookup)을 다시 만든다.
    // 원리: 롱노트 하나가 여러 칸(startStep ~ startStep+lengthSteps-1)을
    //       차지하므로, 그 모든 칸에 같은 노트 참조를 채워 넣는다. 매 프레임
    //       DrawGrid 시작 시 호출해서, 격자를 그리는 동안에는 "이 칸에 노트가
    //       있는지"를 리스트 전체를 훑지 않고 O(1) 조회로 바로 알 수 있게 한다.
    //       시각을 보존하는 노트(keepsExactTime)는 지금의 격자 설정(BPM/세분화/
    //       템포 맵)으로 가장 가까운 칸을 매번 다시 계산한다 — 설정을 바꾸면 화면
    //       위치는 따라 바뀌지만 저장되는 시각은 그대로다. 같은 칸을 이미 다른
    //       노트가 차지하고 있으면 화면에는 하나만 보이므로 그 수를 세어 경고한다.
    private void RebuildStepLookup()
    {
        stepLookup.Clear();
        hiddenOverlapCount = 0;
        offGridCount = 0;
        ChartTimeGrid grid = Grid;
        foreach (EditorNote note in notes)
        {
            if (note.keepsExactTime)
            {
                note.startStep = (int)System.Math.Round(grid.TimeToStep(note.exactTime));
                int endStep = note.exactHold > 0f
                    ? (int)System.Math.Round(grid.TimeToStep(note.exactTime + note.exactHold))
                    : note.startStep;
                note.lengthSteps = Mathf.Max(1, endStep - note.startStep + 1);
                note.offGrid = grid.DistanceFromGrid(note.exactTime) > OffGridToleranceSeconds;
                if (note.offGrid) offGridCount++;
            }

            if (stepLookup.ContainsKey((note.lane, note.startStep)))
            {
                hiddenOverlapCount++;
            }
            for (int s = note.startStep; s < note.startStep + note.lengthSteps; s++)
            {
                stepLookup[(note.lane, s)] = note;
            }
        }
    }

    // 기능: speedChanges 리스트를 훑어서 "스텝 -> 그 스텝의 변속 항목"
    //       조회 테이블(speedStepLookup)을 다시 만든다.
    // 원리: 변속은 레인 구분이 없어 노트보다 단순하다 — 스텝 하나당 항목이
    //       최대 하나뿐이라 그대로 채워 넣기만 하면 된다.
    private void RebuildSpeedLookup()
    {
        speedStepLookup.Clear();
        ChartTimeGrid grid = Grid;
        foreach (EditorSpeedChange change in speedChanges)
        {
            if (change.keepsExactTime)
            {
                change.step = (int)System.Math.Round(grid.TimeToStep(change.exactTime));
            }
            speedStepLookup[change.step] = change;
        }
    }

    // 기능: 특정 (레인, 스텝) 칸을 차지하는 노트를 찾는다(없으면 null).
    // 원리: RebuildStepLookup이 미리 채워둔 딕셔너리에서 그대로 조회한다.
    private EditorNote GetNoteAt(int lane, int step)
    {
        stepLookup.TryGetValue((lane, step), out EditorNote note);
        return note;
    }

    // 기능: 대상 ChartData의 notes/speedChanges(초 단위)를 읽어서, 그 채보에
    //       저장되어 있던 BPM/오프셋/레인 수/박자/마디 수를 먼저 전부 되살린
    //       뒤(항상 같은 기준으로 역산되게), 그 기준의 격자 좌표(EditorNote/
    //       EditorSpeedChange 리스트)로 되살린다.
    // 원리: ChartData에 이 채보를 만들 때 쓴 격자 설정이 전부 저장되어 있으므로,
    //       사용자가 이전에 다른 채보를 만지면서 남겨둔 값을 그대로 쓰는 대신
    //       항상 정확한 값으로 먼저 덮어쓴다. 특히 subdivision을 다른 채보를
    //       편집하던 값 그대로 두면, secondsPerStep이 그 채보를 저장할 때와
    //       달라져서 노트가 다른 칸으로 잘못 역산되어 보인다 — 그래서 BPM과
    //       마찬가지로 반드시 먼저 복원해야 한다. NoteData.hitTime과
    //       SpeedChange.time을 secondsPerStep으로 나눠서 몇 번째 칸(step)인지
    //       역산하고, 홀드 길이(holdDuration)는 같은 방식으로 "몇 칸을 더
    //       차지하는지"로 환산해서 lengthSteps에 반영한다(+1은 시작 칸 자신도
    //       포함하기 위함).
    private void LoadFromChart()
    {
        notes.Clear();
        speedChanges.Clear();
        if (targetChart == null)
        {
            EditorUtility.DisplayDialog("불러오기 실패", "대상 ChartData를 먼저 지정하세요.", "확인");
            return;
        }

        bpm = Mathf.Max(1f, targetChart.bpm);
        offsetSeconds = targetChart.offsetSeconds;
        laneCount = targetChart.laneCount;
        beatsPerMeasure = targetChart.beatsPerMeasure;
        subdivision = targetChart.subdivision;
        measureCount = targetChart.measureCount;

        beatTimes.Clear();
        if (targetChart.beatTimes != null) beatTimes.AddRange(targetChart.beatTimes);

        // 오프셋(첫 박자) 이전 시각(도입부 구간)의 노트도 음수 step으로 되살린다 —
        // 격자가 그 구간까지 표시한다(DrawGrid 참고). 칸 위치(startStep/lengthSteps)는
        // RebuildStepLookup이 exactTime에서 매번 다시 계산하므로 여기서는 시각만 기억한다.
        foreach (NoteData data in targetChart.notes)
        {
            notes.Add(new EditorNote
            {
                lane = data.lane,
                keepsExactTime = true,
                exactTime = data.hitTime,
                exactHold = Mathf.Max(0f, data.holdDuration),
            });
        }

        foreach (SpeedChange sc in targetChart.speedChanges)
        {
            speedChanges.Add(new EditorSpeedChange { multiplier = sc.multiplier, keepsExactTime = true, exactTime = sc.time });
        }

        RebuildStepLookup();
        RebuildSpeedLookup();
        Debug.Log($"채보 불러오기: 노트 {notes.Count}개, 변속 {speedChanges.Count}개" +
                  (beatTimes.Count >= 2 ? $", 템포 맵 {beatTimes.Count}박" : "") +
                  (offGridCount > 0 ? $" — 격자에서 벗어난 노트 {offGridCount}개는 원래 시각 그대로 보존됩니다." : ""));
    }

    // 기능: 현재 격자 위의 EditorNote/EditorSpeedChange 리스트를 초 단위
    //       NoteData/SpeedChange로 변환해서 ChartData 애셋에 실제로 기록한다.
    // 원리: 각 항목의 step을 secondsPerStep으로 곱해 실제 시각으로 바꾼 뒤,
    //       시각 순으로 정렬해 targetChart에 대입한다. ScriptableObject는
    //       메모리 위 데이터일 뿐이라, Undo.RecordObject로 되돌리기(Ctrl+Z)
    //       기록을 남기고 EditorUtility.SetDirty로 "저장되지 않은 변경사항이
    //       있다"고 표시한 뒤 AssetDatabase.SaveAssets()를 호출해야 비로소
    //       디스크의 .asset 파일에 실제로 기록된다.
    private void SaveToChart()
    {
        if (targetChart == null)
        {
            EditorUtility.DisplayDialog("저장 실패", "대상 ChartData를 먼저 지정하세요.", "확인");
            return;
        }

        Undo.RecordObject(targetChart, "채보 편집");

        // 불러온 노트/변속은 기억해둔 원래 시각을, 편집기에서 새로 만든 것만
        // 격자 칸 시각을 쓴다(EditorNote 주석 참고).
        var noteResult = new List<NoteData>();
        foreach (EditorNote note in notes)
        {
            if (note.keepsExactTime)
            {
                noteResult.Add(new NoteData(note.lane, note.exactTime, note.exactHold));
                continue;
            }
            float time = StepToTime(note.startStep);
            float holdDuration = note.lengthSteps > 1 ? StepToTime(note.startStep + note.lengthSteps - 1) - time : 0f;
            noteResult.Add(new NoteData(note.lane, time, holdDuration));
        }
        noteResult.Sort((a, b) => a.hitTime.CompareTo(b.hitTime));
        targetChart.notes = noteResult;

        var speedResult = new List<SpeedChange>();
        foreach (EditorSpeedChange change in speedChanges)
        {
            float time = change.keepsExactTime ? change.exactTime : StepToTime(change.step);
            speedResult.Add(new SpeedChange(time, change.multiplier));
        }
        speedResult.Sort((a, b) => a.time.CompareTo(b.time));
        targetChart.speedChanges = speedResult;
        targetChart.beatTimes = new List<float>(beatTimes);

        targetChart.bpm = bpm;
        targetChart.offsetSeconds = offsetSeconds;
        targetChart.laneCount = laneCount;
        targetChart.beatsPerMeasure = beatsPerMeasure;
        targetChart.subdivision = subdivision;
        targetChart.measureCount = measureCount;

        EditorUtility.SetDirty(targetChart);
        AssetDatabase.SaveAssets();

        Debug.Log($"채보 저장 완료: 노트 {noteResult.Count}개, 변속 {speedResult.Count}개");
    }
}
