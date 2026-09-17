using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

// ============================================================
// SceneLoader.cs
//   SceneManager.LoadScene(동기)을 그대로 쓰면 씬 전환 순간 필요한
//   에셋을 전부 한 프레임 안에 불러오느라 화면이 한 번에 멎어버린다.
//   대신 LoadSceneAsync로 불러오면 유니티가 로딩 작업을 여러 프레임에
//   나눠 처리하므로, 버튼을 눌렀을 때 통째로 얼어붙는 대신 전환이
//   한결 매끄럽게 느껴진다. 프로젝트 전체의 씬 전환 버튼(TitleUI,
//   SongSelectUI, PlaySceneController, ResultUI, OptionsUI)이 전부
//   이 헬퍼 하나를 공유해서, 매번 코루틴을 따로 작성하지 않게 한다.
// ============================================================
public static class SceneLoader
{
    public static void Load(MonoBehaviour host, string sceneName)
    {
        host.StartCoroutine(LoadAsync(sceneName));
    }

    private static IEnumerator LoadAsync(string sceneName)
    {
        yield return SceneManager.LoadSceneAsync(sceneName);
    }
}
