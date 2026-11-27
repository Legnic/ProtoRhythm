using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// ============================================================
// ChartAutoPlayTestMenu.cs
//   메뉴 "Rhythm/Tests/Auto Play All Charts"(모든 채보 자동 플레이 테스트) — 현재 배속/판정 보정 설정을
//   기억해두고, PlayScene을 플레이 모드로 열어 ChartAutoPlayTester가 모든 곡/난이도를
//   자동 플레이하게 한다. 결과는 Temp/ChartAutoPlay/results.md에 남는다.
//   끝나면(또는 도중에 플레이 모드를 멈추면) 설정을 되돌리고 원래 열려 있던 씬을 다시 연다.
// ============================================================
public static class ChartAutoPlayTestMenu
{
    const string ReturnSceneKey = "ChartAutoPlay.ReturnScene";

    [MenuItem("Rhythm/Tests/Auto Play All Charts")]
    public static void Run()
    {
        if (EditorApplication.isPlaying) { Debug.LogWarning("ChartAutoPlay: 플레이 모드를 먼저 끝내세요."); return; }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        string outDir = Path.Combine(Path.GetDirectoryName(Application.dataPath), "Temp", "ChartAutoPlay");
        Directory.CreateDirectory(outDir);
        File.WriteAllText(Path.Combine(outDir, "progress.log"), "");
        File.WriteAllText(Path.Combine(outDir, "results.md"), "");

        SessionState.SetString(ReturnSceneKey, EditorSceneManager.GetActiveScene().path);
        SessionState.SetFloat(ChartAutoPlayTester.OrigSpeedKey, GameSettings.ScrollSpeed);
        SessionState.SetFloat(ChartAutoPlayTester.OrigOffsetKey, GameSettings.AudioOffset);
        SessionState.SetBool(ChartAutoPlayTester.ActiveKey, true);

        EditorSceneManager.OpenScene("Assets/Scenes/PlayScene.unity");
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        EditorApplication.isPlaying = true;
    }

    // 메뉴 "Rhythm/Tests/Key Input And SFX Test" — 가상 키보드로 실제 입력 경로를 거쳐
    // 효과음 지연과 동시치기 입력을 확인한다(KeyInputAutoTester). 결과: Temp/ChartAutoPlay/keyinput.md
    [MenuItem("Rhythm/Tests/Key Input And SFX Test")]
    public static void RunKeyInput()
    {
        if (EditorApplication.isPlaying) { Debug.LogWarning("KeyInputTest: 플레이 모드를 먼저 끝내세요."); return; }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        string outDir = Path.Combine(Path.GetDirectoryName(Application.dataPath), "Temp", "ChartAutoPlay");
        Directory.CreateDirectory(outDir);
        File.WriteAllText(Path.Combine(outDir, "keyinput.md"), "");

        SessionState.SetString(ReturnSceneKey, EditorSceneManager.GetActiveScene().path);
        SessionState.SetBool(KeyInputAutoTester.OrigSfxOnKey, GameSettings.SfxEnabled);
        SessionState.SetFloat(KeyInputAutoTester.OrigSfxVolKey, GameSettings.SfxVolume);
        SessionState.SetFloat(KeyInputAutoTester.OrigSpeedKey, GameSettings.ScrollSpeed);
        SessionState.SetBool(KeyInputAutoTester.ActiveKey, true);

        EditorSceneManager.OpenScene("Assets/Scenes/PlayScene.unity");
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        EditorApplication.isPlaying = true;
    }

    // 플레이 모드가 끝나면 설정 복원을 한 번 더 보장하고 원래 씬으로 돌아간다.
    static void OnPlayModeChanged(PlayModeStateChange change)
    {
        if (change != PlayModeStateChange.EnteredEditMode) return;
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        if (SessionState.GetBool(ChartAutoPlayTester.ActiveKey, false))
        {
            GameSettings.ScrollSpeed = SessionState.GetFloat(ChartAutoPlayTester.OrigSpeedKey, GameSettings.ScrollSpeedDefault);
            GameSettings.AudioOffset = SessionState.GetFloat(ChartAutoPlayTester.OrigOffsetKey, 0f);
            GameSettings.Save();
            SessionState.SetBool(ChartAutoPlayTester.ActiveKey, false);
        }
        if (SessionState.GetBool(KeyInputAutoTester.ActiveKey, false))
        {
            GameSettings.SfxEnabled = SessionState.GetBool(KeyInputAutoTester.OrigSfxOnKey, true);
            GameSettings.SfxVolume = SessionState.GetFloat(KeyInputAutoTester.OrigSfxVolKey, 1f);
            GameSettings.ScrollSpeed = SessionState.GetFloat(KeyInputAutoTester.OrigSpeedKey, GameSettings.ScrollSpeedDefault);
            GameSettings.Save();
            SessionState.SetBool(KeyInputAutoTester.ActiveKey, false);
        }
        string back = SessionState.GetString(ReturnSceneKey, "");
        if (!string.IsNullOrEmpty(back) && File.Exists(Path.Combine(Path.GetDirectoryName(Application.dataPath), back)))
            EditorSceneManager.OpenScene(back);
    }
}
