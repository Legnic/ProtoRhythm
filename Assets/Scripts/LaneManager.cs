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

    // 기능: 현재 레인(키) 개수를 알려준다.
    // 원리: 별도로 값을 저장하지 않고 laneXPositions 배열의 길이를 그대로
    //       돌려준다 — 배열 길이 하나로 "레인이 몇 개인지"까지 함께
    //       표현하도록 해서, 값이 서로 어긋날 일이 없게 한다.
    public int LaneCount => laneXPositions.Length;

    // 기능: 레인 번호를 받아 그 레인의 화면 X좌표를 돌려준다.
    // 원리: 배열 인덱스로 바로 접근하되, 범위를 벗어난 레인 번호가 들어오면
    //       (설정 실수 등으로) 예외를 던지는 대신 경고 로그를 남기고 0을
    //       돌려주는 방어적 처리를 한다 — 게임 도중 크래시로 이어지지 않게 하기 위함이다.
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
