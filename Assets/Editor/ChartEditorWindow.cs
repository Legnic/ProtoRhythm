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
//     2. BPM, 시작 오프셋, 마디 수, 박자 세분화를 정한다
//     3. 격자를 클릭하면 탭 노트, 클릭한 채로 아래로 드래그하면
//        그 길이만큼의 롱노트가 된다 (StepMania류 표준 방식).
//        이미 있는 노트(머리든 몸통이든) 위를 클릭하면 통째로 지워진다.
//     4. "채보 애셋에 저장"을 누르면 ChartData 애셋에 실제로 기록된다
//
//   이 파일은 반드시 Assets/Editor 폴더 밑에 둬야 한다.
// ============================================================
public class ChartEditorWindow : EditorWindow
{
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

    public ChartData targetChart;

    private float bpm = 120f;
    private float offsetSeconds = 1.0f;
    private int laneCount = 4;
    private int beatsPerMeasure = 4;
    private int subdivision = 4; // 1=4분, 2=8분, 4=16분음표
    private int measureCount = 8;

    private readonly List<EditorNote> notes = new List<EditorNote>();
    private readonly Dictionary<(int lane, int step), EditorNote> stepLookup = new Dictionary<(int, int), EditorNote>();

    // 드래그로 롱노트 길이를 늘리는 중일 때의 상태.
    private bool isDragging;
    private int dragLane;
    private EditorNote dragNote;

    private Vector2 scrollPos;

    private static readonly Color EmptyColor = new Color(0.22f, 0.22f, 0.22f);
    private static readonly Color HeadColor = new Color(0.3f, 0.65f, 1f);
    private static readonly Color BodyColor = new Color(0.3f, 0.65f, 1f, 0.45f);

    private const float CellWidth = 50f;
    private const float CellHeight = 18f;

    private void OnGUI()
    {
        // 그리드 밖에서 마우스를 놓아도 드래그 상태가 확실히 풀리도록 한다.
        if (Event.current.type == EventType.MouseUp)
        {
            isDragging = false;
            dragNote = null;
        }

        DrawSettings();
        EditorGUILayout.Space();
        DrawToolbar();
        EditorGUILayout.Space();
        DrawGrid();
    }

    private void DrawSettings()
    {
        EditorGUILayout.LabelField("채보 설정", EditorStyles.boldLabel);
        targetChart = (ChartData)EditorGUILayout.ObjectField("대상 ChartData", targetChart, typeof(ChartData), false);
        bpm = Mathf.Max(1f, EditorGUILayout.FloatField("BPM", bpm));
        offsetSeconds = EditorGUILayout.FloatField("시작 오프셋(초)", offsetSeconds);
        laneCount = EditorGUILayout.IntSlider("레인 수", laneCount, 1, 8);
        beatsPerMeasure = EditorGUILayout.IntSlider("한 마디당 박자 수", beatsPerMeasure, 1, 8);
        subdivision = EditorGUILayout.IntPopup("박자 세분화", subdivision,
            new[] { "4분음표", "8분음표", "16분음표" }, new[] { 1, 2, 4 });
        measureCount = EditorGUILayout.IntSlider("마디 수", measureCount, 1, 64);
    }

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
            if (EditorUtility.DisplayDialog("전체 지우기", "편집 중인 노트를 모두 지울까요?", "지우기", "취소"))
            {
                notes.Clear();
            }
        }

        EditorGUILayout.EndHorizontal();
        EditorGUILayout.HelpBox("클릭 = 탭 노트. 클릭한 채로 아래로 드래그 = 롱노트 길이 지정. 노트 위 클릭 = 삭제.", MessageType.None);
    }

    private void DrawGrid()
    {
        int totalSteps = measureCount * beatsPerMeasure * subdivision;
        float secondsPerStep = (60f / bpm) / subdivision;

        RebuildStepLookup();

        scrollPos = EditorGUILayout.BeginScrollView(scrollPos, GUILayout.Height(500));

        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("", GUILayout.Width(70));
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

            for (int lane = 0; lane < laneCount; lane++)
            {
                Rect cellRect = GUILayoutUtility.GetRect(CellWidth, CellHeight, GUILayout.Width(CellWidth));
                DrawCell(cellRect, lane, step);
            }

            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawCell(Rect rect, int lane, int step)
    {
        EditorNote note = GetNoteAt(lane, step);
        bool isHead = note != null && note.startStep == step;

        Color fill = note == null ? EmptyColor : (isHead ? HeadColor : BodyColor);
        EditorGUI.DrawRect(new Rect(rect.x + 1, rect.y + 1, rect.width - 2, rect.height - 2), fill);

        HandleCellInput(rect, lane, step, note);
    }

    private void HandleCellInput(Rect rect, int lane, int step, EditorNote note)
    {
        Event e = Event.current;
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

    private EditorNote GetNoteAt(int lane, int step)
    {
        stepLookup.TryGetValue((lane, step), out EditorNote note);
        return note;
    }

    private void LoadFromChart()
    {
        notes.Clear();
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
    }

    private void SaveToChart()
    {
        if (targetChart == null)
        {
            EditorUtility.DisplayDialog("저장 실패", "대상 ChartData를 먼저 지정하세요.", "확인");
            return;
        }

        float secondsPerStep = (60f / bpm) / subdivision;
        Undo.RecordObject(targetChart, "채보 편집");

        var result = new List<NoteData>();
        foreach (EditorNote note in notes)
        {
            float time = offsetSeconds + note.startStep * secondsPerStep;
            float holdDuration = (note.lengthSteps - 1) * secondsPerStep;
            result.Add(new NoteData(note.lane, time, holdDuration));
        }
        result.Sort((a, b) => a.hitTime.CompareTo(b.hitTime));

        targetChart.notes = result;

        EditorUtility.SetDirty(targetChart);
        AssetDatabase.SaveAssets();

        Debug.Log($"채보 저장 완료: 노트 {result.Count}개");
    }
}
