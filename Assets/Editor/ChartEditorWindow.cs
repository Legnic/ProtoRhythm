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
//     2. BPM, 시작 오프셋, 마디 수, 박자 세분화(4분~64분음표)를 정한다
//     3. 노트 격자: 클릭하면 탭 노트, 클릭한 채로 아래로 드래그하면
//        그 길이만큼의 롱노트가 된다 (StepMania류 표준 방식). 클릭하기
//        전 커서를 올리면 반투명 미리보기가 뜬다. 이미 있는 노트(머리든
//        몸통이든) 위를 클릭하면 통째로 지워진다.
//     4. 변속 열: 그 칸을 클릭하면 그 시각부터 적용되는 배속(기본 1.5배)이
//        추가되고, 숫자를 직접 입력해 원하는 배율로 바꿀 수 있다. "x"를
//        누르면 지운다. 다음 변속 지점이 나오기 전까지 그대로 유지된다
//        (계단식). 짧은 간격으로 여러 개 찍으면 부드러운 가감속처럼 보인다.
//     5. "채보 애셋에 저장"을 누르면 ChartData 애셋(노트+변속)에 실제로 기록된다
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
    private class EditorNote
    {
        public int lane;
        public int startStep;
        public int lengthSteps;
    }

    // 변속 열의 항목 하나. 이 스텝부터 새 배속(multiplier)이 적용된다.
    private class EditorSpeedChange
    {
        public int step;
        public float multiplier;
    }

    public ChartData targetChart;

    private float bpm = 120f;
    private float offsetSeconds = 1.0f;
    private int laneCount = 4;
    private int beatsPerMeasure = 4;
    private int subdivision = 4; // 1=4분, 2=8분, 4=16분, 8=32분, 16=64분음표
    private int measureCount = 8;

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

    private static readonly Color EmptyColor = new Color(0.22f, 0.22f, 0.22f);
    private static readonly Color HeadColor = new Color(0.3f, 0.65f, 1f);
    private static readonly Color BodyColor = new Color(0.3f, 0.65f, 1f, 0.45f);
    private static readonly Color HoverPreviewColor = new Color(0.3f, 0.65f, 1f, 0.25f);

    private const float CellWidth = 50f;
    private const float CellHeight = 18f;
    private const float SpeedColumnWidth = 64f;
    private const float DefaultSpeedMultiplier = 1.5f;

    // 기능: 창이 열릴 때 한 번 호출된다. 마우스를 누르지 않고 움직이기만 해도
    //       OnGUI가 MouseMove 이벤트를 받도록 설정한다.
    // 원리: IMGUI는 기본적으로 마우스 버튼을 누르고 있을 때만 이벤트를 준다.
    //       클릭하기 전 커서 위치를 미리 알아야(반투명 미리보기) 하므로,
    //       wantsMouseMove를 켜서 단순 이동만으로도 이벤트가 오게 만든다.
    private void OnEnable()
    {
        wantsMouseMove = true;
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

        DrawSettings();
        EditorGUILayout.Space();
        DrawToolbar();
        EditorGUILayout.Space();
        DrawGrid();
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
        bpm = Mathf.Max(1f, EditorGUILayout.FloatField("BPM", bpm));
        offsetSeconds = EditorGUILayout.FloatField("시작 오프셋(초)", offsetSeconds);
        laneCount = EditorGUILayout.IntSlider("레인 수", laneCount, 1, 8);
        beatsPerMeasure = EditorGUILayout.IntSlider("한 마디당 박자 수", beatsPerMeasure, 1, 8);
        subdivision = EditorGUILayout.IntPopup("박자 세분화", subdivision,
            new[] { "4분음표", "8분음표", "16분음표", "32분음표", "64분음표" },
            new[] { 1, 2, 4, 8, 16 });
        measureCount = EditorGUILayout.IntSlider("마디 수", measureCount, 1, 64);
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

        EditorGUILayout.EndHorizontal();
        EditorGUILayout.HelpBox(
            "노트 칸: 클릭 = 탭 노트, 클릭한 채로 드래그 = 롱노트, 노트 위 클릭 = 삭제.\n" +
            "변속 칸: '+' 클릭 = 그 시각부터 배속 추가(숫자 직접 입력 가능), 'x' = 삭제.",
            MessageType.None);
    }

    // 기능: BPM/마디 수/세분화 설정에 맞춰 전체 격자(시간 레이블 + 변속 열 +
    //       레인별 노트 칸)를 그린다.
    // 원리: secondsPerStep = (60/bpm)/subdivision으로 "칸 하나가 몇 초인지"를
    //       구하고, 그 값으로 각 칸의 실제 시각(offsetSeconds + step*secondsPerStep)을
    //       계산해 왼쪽에 표시한다. 매 프레임 RebuildStepLookup()/
    //       RebuildSpeedLookup()을 먼저 호출해서 "이 칸에 무엇이 있는지"를
    //       조회 가능한 상태로 만들어둔 뒤, 스크롤 뷰 안에서 스텝(행) 단위로
    //       시간 레이블 -> 변속 칸 -> 레인별 노트 칸 순서로 그린다.
    private void DrawGrid()
    {
        int totalSteps = measureCount * beatsPerMeasure * subdivision;
        float secondsPerStep = (60f / bpm) / subdivision;

        RebuildStepLookup();
        RebuildSpeedLookup();

        scrollPos = EditorGUILayout.BeginScrollView(scrollPos, GUILayout.Height(500));

        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("", GUILayout.Width(70));
        GUILayout.Label("변속", GUILayout.Width(SpeedColumnWidth));
        for (int lane = 0; lane < laneCount; lane++)
        {
            GUILayout.Label($"레인 {lane}", GUILayout.Width(CellWidth));
        }
        EditorGUILayout.EndHorizontal();

        for (int step = 0; step < totalSteps; step++)
        {
            EditorGUILayout.BeginHorizontal();

            float time = offsetSeconds + step * secondsPerStep;
            bool isMeasureStart = (step % (beatsPerMeasure * subdivision)) == 0;
            string timeLabel = isMeasureStart ? $"▶ {time:0.00}" : $"{time:0.00}";
            GUILayout.Label(timeLabel, GUILayout.Width(70));

            DrawSpeedCell(step);

            for (int lane = 0; lane < laneCount; lane++)
            {
                Rect cellRect = GUILayoutUtility.GetRect(CellWidth, CellHeight, GUILayout.Width(CellWidth));
                DrawCell(cellRect, lane, step);
            }

            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.EndScrollView();
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
            fill = isHead ? HeadColor : BodyColor;
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
    //       (StepMania류: 클릭=탭, 클릭+드래그=롱노트 길이 지정).
    // 원리: GUILayout.Toggle 같은 표준 컨트롤로는 "누른 채로 드래그"나
    //       "누르지 않고 지나가기"를 감지할 수 없어서, Event.current로 이
    //       칸의 사각형 위에서 일어난 원시 마우스 이벤트를 직접 읽는다.
    //       MouseMove는 클릭/드래그와 무관하게 hoverLane/hoverStep만
    //       갱신하고 끝낸다(다른 칸이 이미 hover 상태를 차지하고 있었다면
    //       바뀐 경우에만 Repaint해서 불필요한 다시그리기를 줄인다).
    //       MouseDown 시점에 이미 노트가 있으면(머리든 몸통이든) 그 노트를
    //       통째로 지우고, 없으면 길이 1짜리 새 노트를 만들며 동시에
    //       "지금부터 이 레인에서 드래그로 늘리는 중"이라는 상태
    //       (isDragging/dragLane/dragNote)를 기록한다. 이후 MouseDrag가
    //       같은 레인 안에서 들어오면, "시작 칸부터 지금 칸까지의 거리 + 1"을
    //       새 길이로 갱신한다. e.Use()는 이 이벤트를 자신이 처리했다고 Unity에
    //       알려서 다른 컨트롤이 같은 이벤트를 중복 처리하지 않게 막는다.
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
            if (note != null)
            {
                // 머리든 몸통이든, 노트 위를 클릭하면 그 노트를 통째로 지운다.
                notes.Remove(note);
            }
            else
            {
                var newNote = new EditorNote { lane = lane, startStep = step, lengthSteps = 1 };
                notes.Add(newNote);
                isDragging = true;
                dragLane = lane;
                dragNote = newNote;
            }
            e.Use();
            Repaint();
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
    private void RebuildStepLookup()
    {
        stepLookup.Clear();
        foreach (EditorNote note in notes)
        {
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
        foreach (EditorSpeedChange change in speedChanges)
        {
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

    // 기능: 대상 ChartData의 notes/speedChanges(초 단위)를 읽어서, 현재
    //       BPM/오프셋/세분화 설정 기준의 격자 좌표(EditorNote/EditorSpeedChange
    //       리스트)로 되살린다.
    // 원리: NoteData.hitTime과 SpeedChange.time을 secondsPerStep으로 나눠서
    //       몇 번째 칸(step)인지 역산한다(반올림하므로, 지금 설정한 BPM/오프셋이
    //       그 채보를 만들 때 쓴 값과 다르면 칸 위치가 어긋날 수 있다). 홀드
    //       길이(holdDuration)는 같은 방식으로 "몇 칸을 더 차지하는지"로
    //       환산해서 lengthSteps에 반영한다(+1은 시작 칸 자신도 포함하기 위함).
    private void LoadFromChart()
    {
        notes.Clear();
        speedChanges.Clear();
        if (targetChart == null)
        {
            EditorUtility.DisplayDialog("불러오기 실패", "대상 ChartData를 먼저 지정하세요.", "확인");
            return;
        }

        float secondsPerStep = (60f / bpm) / subdivision;
        foreach (NoteData data in targetChart.notes)
        {
            int startStep = Mathf.RoundToInt((data.hitTime - offsetSeconds) / secondsPerStep);
            if (startStep < 0)
            {
                continue;
            }

            // 탭 노트는 1칸, 롱노트는 홀드 길이를 스텝 수로 환산해 그만큼 칸을 차지한다.
            int lengthSteps = data.holdDuration > 0f
                ? Mathf.Max(1, Mathf.RoundToInt(data.holdDuration / secondsPerStep) + 1)
                : 1;

            notes.Add(new EditorNote { lane = data.lane, startStep = startStep, lengthSteps = lengthSteps });
        }

        foreach (SpeedChange sc in targetChart.speedChanges)
        {
            int step = Mathf.RoundToInt((sc.time - offsetSeconds) / secondsPerStep);
            if (step < 0)
            {
                continue;
            }
            speedChanges.Add(new EditorSpeedChange { step = step, multiplier = sc.multiplier });
        }
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

        float secondsPerStep = (60f / bpm) / subdivision;
        Undo.RecordObject(targetChart, "채보 편집");

        var noteResult = new List<NoteData>();
        foreach (EditorNote note in notes)
        {
            float time = offsetSeconds + note.startStep * secondsPerStep;
            float holdDuration = (note.lengthSteps - 1) * secondsPerStep;
            noteResult.Add(new NoteData(note.lane, time, holdDuration));
        }
        noteResult.Sort((a, b) => a.hitTime.CompareTo(b.hitTime));
        targetChart.notes = noteResult;

        var speedResult = new List<SpeedChange>();
        foreach (EditorSpeedChange change in speedChanges)
        {
            float time = offsetSeconds + change.step * secondsPerStep;
            speedResult.Add(new SpeedChange(time, change.multiplier));
        }
        speedResult.Sort((a, b) => a.time.CompareTo(b.time));
        targetChart.speedChanges = speedResult;

        EditorUtility.SetDirty(targetChart);
        AssetDatabase.SaveAssets();

        Debug.Log($"채보 저장 완료: 노트 {noteResult.Count}개, 변속 {speedResult.Count}개");
    }
}
