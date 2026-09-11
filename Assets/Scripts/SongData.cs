using UnityEngine;

// ============================================================
// SongData.cs
//   한 곡을 이루는 음원(AudioClip)과 그 곡의 채보(ChartData)를
//   한 세트로 묶어두는 애셋. 곡이 여러 개가 되면 SongLoader가
//   이 세트를 통째로 갈아끼워서 "음원과 채보가 서로 다른 곡을
//   가리키는" 실수를 막는다.
// ============================================================
[CreateAssetMenu(menuName = "Rhythm/Song Data", fileName = "NewSong")]
public class SongData : ScriptableObject
{
    public string songName;
    public AudioClip audioClip;
    public ChartData chart;
}
