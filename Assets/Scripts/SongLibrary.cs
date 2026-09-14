using System.Collections.Generic;
using UnityEngine;

// ============================================================
// SongLibrary.cs
//   곡 선택 UI가 보여줄 SongData 목록. 곡이 늘어나면 여기에
//   SongData 애셋을 끌어다 추가하기만 하면 선택 화면에 나타난다.
// ============================================================
[CreateAssetMenu(menuName = "Rhythm/Song Library", fileName = "SongLibrary")]
public class SongLibrary : ScriptableObject
{
    public List<SongData> songs = new List<SongData>();
}
