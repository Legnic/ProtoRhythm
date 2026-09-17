using UnityEngine;
using TMPro;

// ============================================================
// CountdownUI.cs
//   재생 시작 전 리드인(lead-in) 동안 화면 가운데에 큰 숫자로
//   카운트다운을 보여준다.
// 원리: 새로운 타이머를 따로 만들지 않고 RhythmClock.CurrentTime을
//   그대로 읽는다. 다만 리드인 전체(음수 CurrentTime 구간)를 그대로
//   카운트다운으로 보여주면 안 된다 — 리드인은 "카운트다운" 구간과
//   그 뒤에 이어지는 "노트가 실제로 떨어지는"(noteLeadTime) 구간,
//   두 개로 나뉘어 있다(SongLoader.Load 참고). 그래서 CurrentTime에
//   noteLeadTime을 더해 "카운트다운 구간 기준 남은 시간"으로 바꾼 뒤
//   그 값을 뒤집어 올림한다. 이렇게 하면 카운트다운 숫자는 정확히
//   카운트다운 구간 동안만 보이고, 그 구간이 끝나는 순간(=노트가
//   spawnY에서 나타나 떨어지기 시작하는 순간) 정확히 사라진다.
//   일시정지/재개로 리드인이 중간에 멈췄다 풀려도 따로 맞춰줄 게
//   없다 — 어차피 같은 CurrentTime을 보고 있어서 자동으로 맞다.
// ============================================================
public class CountdownUI : MonoBehaviour
{
    public RhythmClock clock;
    public TMP_Text countdownText;

    void Update()
    {
        if (clock == null || countdownText == null) return;

        float countdownRemaining = -(clock.CurrentTime + clock.noteLeadTime);

        if (clock.IsActive && countdownRemaining > 0f)
        {
            countdownText.gameObject.SetActive(true);
            countdownText.text = Mathf.CeilToInt(countdownRemaining).ToString();
        }
        else
        {
            countdownText.gameObject.SetActive(false);
        }
    }
}
