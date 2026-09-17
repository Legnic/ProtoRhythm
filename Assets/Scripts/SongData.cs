using System.Collections.Generic;
using UnityEngine;

// ============================================================
// SongData.cs
//   한 곡(음원 하나)과 그 곡에 딸린 여러 난이도(SongDifficulty)를
//   묶어두는 애셋. 예전에는 SongData 하나 = 채보 하나였는데, 이러면
//   같은 곡에 난이도가 여러 개 생길 때마다 음원 참조를 통째로 복제한
//   "가짜 곡"을 새로 만들어야 했다. 이제는 음원/곡 이름처럼 난이도와
//   무관하게 공통인 정보는 SongData에 한 번만 두고, 난이도별로 다른
//   부분(ChartData)만 difficulties 리스트에 나눠 담는다.
// ============================================================

// 기능: 한 난이도 항목 하나 — 표시용 이름/레벨과, 그 난이도의 채보(ChartData)를 담는다.
// 원리: 별도 애셋 파일로 만들지 않고 SongData 안에 중첩된 일반 C# 클래스로
//       두었다 — NoteData가 ChartData 안에 함께 있는 것과 같은 이유로,
//       난이도 하나가 SongData 없이 단독으로 쓰일 일이 없기 때문에 굳이
//       애셋을 따로 만들 필요가 없다. [System.Serializable]을 붙여야
//       인스펙터에서 리스트로 펼쳐 편집할 수 있다.
[System.Serializable]
public class SongDifficulty
{
    [Tooltip("선택 화면에 보여줄 난이도 이름 (예: Easy, Normal, Hard)")]
    public string label = "Normal";

    [Tooltip("난이도 숫자 표기 (선택 사항, UI에서 참고용으로만 쓴다)")]
    public int level = 1;

    public ChartData chart;
}

[CreateAssetMenu(menuName = "Rhythm/Song Data", fileName = "NewSong")]
public class SongData : ScriptableObject
{
    public string songName;
    public AudioClip audioClip;

    [Tooltip("이 곡을 고르거나 플레이할 때 배경으로 보여줄 이미지 (선택 사항, 비워두면 기본 배경을 그대로 쓴다)")]
    public Sprite backgroundImage;

    [Tooltip("이 곡에 딸린 난이도들. Easy/Normal/Hard처럼 여러 개를 추가할 수 있다.")]
    public List<SongDifficulty> difficulties = new List<SongDifficulty>();
}
