using UnityEngine;

// ============================================================
// LaneManager.cs
//   각 레인이 화면의 어느 X좌표에 있는지 정의한다.
//   기본값은 4키(4B) 모드 기준. 6키/8키로 늘리고 싶으면
//   laneXPositions 배열만 늘리면 된다.
// ============================================================
public class LaneManager : MonoBehaviour
{
    [Tooltip("레인 순서대로 화면 X좌표를 지정한다. 배열 길이가 곧 레인(키) 개수가 된다.")]
    public float[] laneXPositions = { -3f, -1f, 1f, 3f };

    public int LaneCount => laneXPositions.Length;

    public float GetLaneX(int lane)
    {
        if (lane < 0 || lane >= laneXPositions.Length)
        {
            Debug.LogWarning($"LaneManager: 잘못된 레인 번호 {lane}");
            return 0f;
        }
        return laneXPositions[lane];
    }
}
