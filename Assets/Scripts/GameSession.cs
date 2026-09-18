// ============================================================
// GameSession.cs
//   씬을 넘나들며 유지되어야 하는 값들을 담는 정적 클래스.
//   SceneManager.LoadScene은 이전 씬의 모든 오브젝트를 파괴하기
//   때문에, "곡 선택 화면에서 고른 곡/난이도"를 "플레이 화면"에
//   전달하거나 "플레이가 끝난 점수"를 "결과 화면"에 전달하려면
//   씬에 속하지 않는 곳에 잠깐 담아둬야 한다. NoteRegistry와 같은
//   이유로 MonoBehaviour가 아닌 순수 정적 클래스로 만들었다 —
//   씬이 몇 번을 바뀌어도 이 값들은 그대로 남아있는다(단, 앱을
//   완전히 재시작하면 당연히 초기화된다).
// ============================================================
public static class GameSession
{
    public static SongData SelectedSong;
    public static SongDifficulty SelectedDifficulty;

    public static int FinalScore;
    public static int FinalCombo;
    public static int FinalMaxCombo;
    public static int FinalFastCount;
    public static int FinalLateCount;
}
