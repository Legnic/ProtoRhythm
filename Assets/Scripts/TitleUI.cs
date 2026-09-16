using UnityEngine;
using UnityEngine.SceneManagement;

// ============================================================
// TitleUI.cs
//   타이틀 화면의 두 버튼 — 시작(곡 선택 화면으로)과 나가기.
// ============================================================
public class TitleUI : MonoBehaviour
{
    public UnityEngine.UI.Button startButton;
    public UnityEngine.UI.Button quitButton;

    // 기능: 씬이 열릴 때마다 버튼 클릭을 코드로 연결한다 — 인스펙터의 영구
    //       리스너 대신 이 프로젝트 전체에서 쓰는 패턴(예: SongSelectUI,
    //       PlaySceneController)을 그대로 따른다.
    void Start()
    {
        if (startButton != null) startButton.onClick.AddListener(StartGame);
        if (quitButton != null) quitButton.onClick.AddListener(QuitGame);
    }

    // 기능: "시작" 버튼에 연결한다.
    public void StartGame()
    {
        SceneManager.LoadScene("SelectScene");
    }

    // 기능: "나가기" 버튼에 연결한다 — 플레이를 종료한다.
    // 원리: Application.Quit()은 실제 빌드에서만 앱을 종료하고, 유니티
    //       에디터의 Play 모드에서는 아무 효과가 없다(에디터 자체를 끄는
    //       게 아니라서). 에디터에서 눌렀을 때도 "플레이 종료"가 되도록,
    //       에디터 안에서는 대신 EditorApplication.isPlaying을 꺼서
    //       Play 모드 자체를 빠져나온다. #if UNITY_EDITOR로 감싸서
    //       빌드에는 이 코드가 아예 포함되지 않게 한다(UnityEditor는
    //       빌드에 존재하지 않는 에디터 전용 API라, 감싸지 않으면 빌드가
    //       깨진다).
    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
