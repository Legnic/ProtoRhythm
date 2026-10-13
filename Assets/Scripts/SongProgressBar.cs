using UnityEngine;
using UnityEngine.UI;

// ============================================================
// SongProgressBar.cs
//   플레이 화면 상단에서 지금까지 재생된 구간을 왼쪽부터 오른쪽으로
//   채워 보여주는 곡 진행도 게이지.
//
//   진행도는 별도 타이머 없이 RhythmClock.CurrentTime을 곡 길이로
//   나눈 값을 그대로 쓴다 — PlaySceneController가 곡이 끝났는지
//   판단할 때 쓰는 기준(clock.CurrentTime >= clip.length)과 같은
//   값이라, 게이지가 다 차는 시점과 실제로 결과 화면으로 넘어가는
//   시점이 항상 일치한다. 리드인(카운트다운) 동안은 CurrentTime이
//   음수라 자동으로 0으로 클램프되어 게이지가 비어있는 채로
//   시작하고, 일시정지 중에는 CurrentTime 자체가 얼어있으므로
//   게이지도 따로 처리할 것 없이 같이 멈춘다.
//
//   색은 막대 길이를 따라 고정된 무늬를 그리는 게 아니라, 막대
//   전체가 한 순간엔 하나의 색이고 그 색 자체가 시간에 따라
//   분홍<->파랑으로 서서히 넘어가는 "무드등" 방식이다. 사인파로
//   섞는 비율을 계산하므로(선형 왕복과 달리 양 끝에서 방향이
//   꺾이는 순간에도 속도가 0으로 부드럽게 줄어든다) 번쩍이거나
//   뚝뚝 끊기지 않고 항상 매끄럽게 이어진다.
// ============================================================
public class SongProgressBar : MonoBehaviour
{
    [Header("참조")]
    public RhythmClock clock;
    [Tooltip("실제로 채워지는 막대. Image.type은 Filled/Horizontal로 코드에서 맞춘다.")]
    public Image fillImage;
    [Tooltip("게이지가 차오르는 머리(오른쪽 끝) 위치를 따라다니는 엠블럼. 왼쪽 끝을 기준으로 anchorMin/Max=(0, 0.5)로 앵커링돼 있어야 한다.")]
    public RectTransform emblemRect;

    [Header("무드등 색상 순환")]
    public Color colorA = new Color(1f, 0.45f, 0.75f); // 분홍
    public Color colorB = new Color(0.35f, 0.65f, 1f); // 파랑
    [Tooltip("분홍 -> 파랑 -> 분홍, 한 바퀴를 도는 데 걸리는 시간(초).")]
    public float colorCycleSeconds = 4f;

    // Image는 sprite가 비어있으면 Filled 타입이어도 fillAmount를 무시하고
    // 그냥 꽉 찬 사각형을 그려버린다(스프라이트가 없을 때는 UGUI가 Filled
    // 전용 메시 생성 경로 자체를 건너뛰기 때문) — 그래서 "게이지가 처음부터
    // 꽉 차서 시작하는" 증상이 생겼다. 색은 흰 스프라이트를 UpdateMoodColor가
    // 틴트하는 색으로 그대로 보여주면 되므로, 엔진 내장 1x1 흰 텍스처로
    // 만든 스프라이트 하나만 물려주면 충분하다(별도 에셋 불필요).
    private static Sprite flatSprite;

    private static Sprite GetFlatSprite()
    {
        if (flatSprite == null)
        {
            Texture2D tex = Texture2D.whiteTexture;
            flatSprite = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
        }
        return flatSprite;
    }

    // 기능: Image.type을 Filled/Horizontal/Left로 맞추고, fillAmount가 실제로
    //       반영되도록 흰 스프라이트를 물려둔다.
    void Awake()
    {
        if (fillImage == null) return;

        fillImage.sprite = GetFlatSprite();
        fillImage.type = Image.Type.Filled;
        fillImage.fillMethod = Image.FillMethod.Horizontal;
        fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
        fillImage.fillAmount = 0f;
    }

    void Update()
    {
        if (fillImage == null) return;

        UpdateMoodColor();

        float progress = 0f;
        if (clock != null && clock.musicSource != null && clock.musicSource.clip != null)
        {
            float duration = clock.musicSource.clip.length;
            progress = duration > 0f ? Mathf.Clamp01(clock.CurrentTime / duration) : 0f;
        }

        fillImage.fillAmount = progress;
        UpdateEmblem(progress);
    }

    // 기능: 엠블럼을 게이지가 차오른 끝(머리) 위치로 옮긴다.
    // 원리: emblemRect는 왼쪽 끝 기준으로 앵커링돼 있으므로, anchoredPosition.x가
    //       곧 "왼쪽 끝에서부터의 거리"가 된다. 막대 전체 너비(이 오브젝트 자신의
    //       RectTransform.rect.width)에 진행도를 곱하면 정확히 채워진 끝 지점의
    //       x좌표가 나온다.
    private void UpdateEmblem(float progress)
    {
        if (emblemRect == null) return;

        float width = ((RectTransform)transform).rect.width;
        Vector2 pos = emblemRect.anchoredPosition;
        pos.x = width * progress;
        emblemRect.anchoredPosition = pos;
    }

    // 기능: 막대 전체의 색을 분홍<->파랑 사이에서 사인파로 서서히 오간다.
    private void UpdateMoodColor()
    {
        if (colorCycleSeconds <= 0f)
        {
            fillImage.color = colorA;
            return;
        }

        float phase = (Mathf.Sin(Time.time * (Mathf.PI * 2f / colorCycleSeconds)) + 1f) * 0.5f;
        fillImage.color = Color.Lerp(colorA, colorB, phase);
    }
}
