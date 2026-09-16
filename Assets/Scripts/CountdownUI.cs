using UnityEngine;
using UnityEngine.UI;

// ============================================================
// CountdownUI.cs
//   재생 시작 전 리드인(lead-in) 동안 화면 가운데에 큰 숫자로
//   카운트다운을 보여준다.
// 원리: 새로운 타이머를 따로 만들지 않고 RhythmClock.CurrentTime을
//   그대로 읽는다 — 리드인 중에는 CurrentTime이 음수(-남은 시간)이므로,
//   그 값을 뒤집어 올림하기만 하면 그게 곧 "몇 초 남았는지"다. 그래서
//   일시정지/재개로 리드인이 중간에 멈췄다 풀려도 따로 맞춰줄 게
//   없다 — 어차피 같은 CurrentTime을 보고 있어서 자동으로 맞다.
//   CurrentTime이 0 이상이 되는 순간(=노트가 판정선에 닿기 시작하는
//   순간과 정확히 같다) 자동으로 사라진다.
// ============================================================
public class CountdownUI : MonoBehaviour
{
    public RhythmClock clock;
    public Text countdownText;

    void Update()
    {
        if (clock == null || countdownText == null) return;

        if (clock.IsActive && clock.CurrentTime < 0f)
        {
            countdownText.gameObject.SetActive(true);
            countdownText.text = Mathf.CeilToInt(-clock.CurrentTime).ToString();
        }
        else
        {
            countdownText.gameObject.SetActive(false);
        }
    }
}
