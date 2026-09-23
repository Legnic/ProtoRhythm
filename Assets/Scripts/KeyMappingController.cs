using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using TMPro;

// ============================================================
// KeyMappingController.cs
//   옵션 화면의 "키 매핑" 탭 — 레인별로 지금 배정된 키를 보여주고,
//   버튼을 누른 뒤 원하는 키를 누르면 그 키로 바로 바꿔준다.
//
//   InputJudge는 PlayScene에 있어서 이 씬에서 직접 참조할 수 없다 —
//   대신 GameSettings(PlayerPrefs)를 거쳐서만 값을 주고받는다. 레인
//   버튼을 누르는 즉시 GameSettings에 저장하므로(별도 "저장" 버튼 없음),
//   PlayScene이 열릴 때 InputJudge.Start()가 그 값을 그대로 읽어간다.
//
//   레인 수(laneCount)는 InputJudge.laneKeys의 길이와 수동으로 맞춰야
//   한다 — LaneManager.laneXPositions와 InputJudge.laneKeys를 항상 같은
//   길이로 맞춰야 하는 것과 같은 이유(이 프로젝트가 레인 수를 다루는
//   기존 방식)다. 나중에 레인 수를 바꾸면 여기 laneCount와 defaultKeys도
//   함께 늘려야 한다.
// ============================================================
public class KeyMappingController : MonoBehaviour
{
    [Header("설정")]
    [Tooltip("InputJudge.laneKeys 길이와 반드시 같아야 한다.")]
    public int laneCount = 4;
    [Tooltip("한 번도 바꾼 적 없는 레인에 보여줄 기본 키. InputJudge의 인스펙터 기본값과 맞춰둔다.")]
    public Key[] defaultKeys = { Key.D, Key.F, Key.J, Key.K };

    [Header("참조")]
    [Tooltip("레인 행(row) 하나를 만드는 프리팹 — 레인 이름 텍스트 + 리바인드 버튼.")]
    public GameObject rowPrefab;
    [Tooltip("레인 행들이 세로로 쌓일 부모(Vertical Layout Group이 붙어있는 오브젝트).")]
    public Transform rowContainer;

    private TMP_Text[] rowLabels;
    private int waitingLane = -1;

    // 기능: 저장된(또는 기본) 키로 레인 수만큼 행을 만든다.
    void Start()
    {
        rowLabels = new TMP_Text[laneCount];

        for (int lane = 0; lane < laneCount; lane++)
        {
            int capturedLane = lane;

            GameObject row = Instantiate(rowPrefab, rowContainer);
            TMP_Text[] texts = row.GetComponentsInChildren<TMP_Text>();
            TMP_Text laneNameText = texts[0];
            TMP_Text keyText = texts[1];
            Button rebindButton = row.GetComponentInChildren<Button>();

            laneNameText.text = $"레인 {capturedLane}";

            Key fallback = capturedLane < defaultKeys.Length ? defaultKeys[capturedLane] : Key.None;
            Key current = GameSettings.GetLaneKey(capturedLane, fallback);
            keyText.text = current.ToString();

            rowLabels[lane] = keyText;
            rebindButton.onClick.AddListener(() => BeginRebind(capturedLane, keyText));
        }
    }

    // 기능: 리바인드 버튼을 누르면 호출된다 — "다음에 누르는 키"를 기다리는 상태로 들어간다.
    // 원리: 이미 다른 레인이 키 입력을 기다리는 중이었다면(그 레인은 아무 키도
    //       배정받지 못한 채 취소된 것이다), 새 레인으로 넘어가기 전에 먼저
    //       그 레인을 취소 처리해서 원래 키 텍스트로 되돌려야 한다 — 그렇지
    //       않으면 이전 레인의 라벨이 "키 입력..."에 계속 멈춰있게 된다.
    private void BeginRebind(int lane, TMP_Text keyText)
    {
        if (waitingLane >= 0 && waitingLane != lane)
        {
            CancelRebind(waitingLane);
        }

        waitingLane = lane;
        keyText.text = "키 입력...";
    }

    // 기능: 지정한 레인의 리바인드를 취소하고, 저장된(또는 기본) 키 텍스트로 되돌린다.
    private void CancelRebind(int lane)
    {
        Key fallback = lane < defaultKeys.Length ? defaultKeys[lane] : Key.None;
        rowLabels[lane].text = GameSettings.GetLaneKey(lane, fallback).ToString();
    }

    // 기능: 리바인드를 기다리는 중이면, 이번 프레임에 새로 눌린 키를 찾아 그
    //       레인에 배정한다. Escape는 취소로 취급해서 이전 표시로 되돌린다.
    // 원리: Keyboard.current.allKeys를 훑어 wasPressedThisFrame인 첫 키를
    //       채택한다 — 어떤 키를 누를지 미리 알 수 없으므로 특정 키 하나만
    //       검사하는 방식(InputJudge처럼)으로는 안 되고, 눌린 모든 키 후보를
    //       매 프레임 스캔해야 한다.
    void Update()
    {
        if (waitingLane < 0)
        {
            return;
        }

        var keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return;
        }

        if (keyboard[Key.Escape].wasPressedThisFrame)
        {
            CancelRebind(waitingLane);
            waitingLane = -1;
            return;
        }

        foreach (KeyControl control in keyboard.allKeys)
        {
            if (control.wasPressedThisFrame)
            {
                GameSettings.SetLaneKey(waitingLane, control.keyCode);
                GameSettings.Save();
                rowLabels[waitingLane].text = control.keyCode.ToString();
                waitingLane = -1;
                break;
            }
        }
    }
}
