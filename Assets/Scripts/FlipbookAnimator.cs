using UnityEngine;
using UnityEngine.UI;

// ============================================================
// FlipbookAnimator.cs
//   GIF는 유니티가 네이티브로 재생하지 못한다 — Texture2D에는 애니메이션
//   개념이 없어서, .gif 파일을 그대로 임포트하면 첫 프레임 한 장만 남는
//   정적 이미지가 된다. 그래서 GIF의 각 프레임을 미리 PNG로 뽑아 Sprite
//   배열로 만들고, 이 컴포넌트가 그 배열을 정해진 속도로 순환시켜
//   애니메이션처럼 보이게 한다(플립북).
//
//   이 방식이 안전한 이유: 프레임 압축 해제가 전부 "임포트 시점"(에디터에서
//   PNG를 텍스처로 불러올 때)에 끝나 있다. 런타임에는 매 프레임 이미
//   메모리에 있는 Sprite로 갈아끼우기만 하므로, 이번 시즌에 겪었던
//   "처음 쓸 때 CPU가 무겁게 압축을 푼다"는 종류의 끊김과 완전히 무관하다.
// ============================================================
public class FlipbookAnimator : MonoBehaviour
{
    public Image targetImage;
    public Sprite[] frames;

    [Tooltip("초당 프레임 수. 원본 GIF가 프레임당 70ms 간격이었다면 약 14.29.")]
    public float frameRate = 14.2857f;

    public bool loop = true;

    private float timer;
    private int currentFrame;

    // 기능: 켜질 때마다 처음 프레임으로 되돌아가 재생을 시작한다.
    void OnEnable()
    {
        timer = 0f;
        currentFrame = 0;
        ApplyFrame();
    }

    // 기능: 경과 시간을 누적해서, 프레임 하나의 재생 시간(1/frameRate)을
    //       넘길 때마다 다음 프레임으로 넘어간다.
    // 원리: 프레임 레이트가 실제 화면 주사율보다 낮으므로(14.29fps), 매
    //       Update마다 넘기는 대신 누적된 시간이 프레임 길이를 넘었을
    //       때만 넘긴다. while로 도는 이유는 한 프레임 사이 간격이
    //       벌어지면(랙 등) 한 번에 여러 프레임을 건너뛰어야 할 수도
    //       있기 때문이다.
    void Update()
    {
        if (targetImage == null || frames == null || frames.Length == 0)
        {
            return;
        }

        timer += Time.deltaTime;
        float frameDuration = 1f / frameRate;

        while (timer >= frameDuration)
        {
            timer -= frameDuration;
            currentFrame++;

            if (currentFrame >= frames.Length)
            {
                if (loop)
                {
                    currentFrame = 0;
                }
                else
                {
                    currentFrame = frames.Length - 1;
                    timer = 0f;
                    break;
                }
            }

            ApplyFrame();
        }
    }

    private void ApplyFrame()
    {
        if (targetImage != null && frames != null && currentFrame < frames.Length)
        {
            targetImage.sprite = frames[currentFrame];
        }
    }
}
