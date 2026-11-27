using UnityEngine;

// ============================================================
// LaneKeyBeam.cs
//   키를 누르면 그 레인의 판정선(레인 아래쪽)에서 위로 그라데이션
//   빛줄기(키 빔)가 솟아올랐다가 사라지는 연출이다(DJMAX의 키 빔과 같은 방식).
//
//   - 탭: 누르는 순간 아래에서 위로 빠르게 차오른 뒤 곧바로 옅어지며 사라진다.
//   - 롱노트: 머리를 정확히 눌러 홀드가 시작되면 키를 뗄 때까지 최대 밝기로
//     유지되고, 떼는 순간(또는 꼬리를 놓쳐 Miss가 되는 순간)부터 옅어진다.
//
//   InputJudge가 Press / BeginHold / Release를 호출해 알려준다.
//
//   성능 메모
//   - 빔 텍스처(세로 알파 그라데이션)는 실행 중에 한 번만 만들어 모든 레인이
//     같은 스프라이트·머티리얼을 공유한다 — 레인이 몇 개든 같은 배치로 그려진다.
//   - 빔이 보이지 않는 레인은 SpriteRenderer를 꺼둬서 그리기 호출 자체가 없고,
//     모든 레인이 꺼져 있으면 Update가 바로 빠져나간다.
//   - 매 프레임 할당(GC)이 없다 — 상태는 미리 만든 배열에만 담고, 색/스케일은
//     값 타입으로만 갱신한다.
//   - 홀드 유지 여부는 홀드를 시작한 노트 참조 하나만 보고 판단한다(레인의 노트
//     목록을 매 프레임 훑지 않는다).
// ============================================================
public class LaneKeyBeam : MonoBehaviour
{
    [Header("참조")]
    public LaneManager laneManager;
    public GameManager gameManager;

    [Header("모양")]
    [Tooltip("빔 색. 알파는 빔 맨 아래(가장 밝은 곳)의 불투명도다.")]
    public Color beamColor = new Color(0.4f, 0.8f, 1f, 0.7f);

    [Tooltip("판정선부터 위로 뻗는 빔 높이(월드 유닛).")]
    public float beamHeight = 7f;

    [Tooltip("판정선 기준으로 빔 아래 끝을 얼마나 내릴지(월드 유닛). 0이면 판정선에서 시작한다.")]
    public float bottomOffset = 0.05f;

    [Tooltip("레인 폭에서 양옆으로 비워둘 여백(월드 유닛). 레인 구분선과 겹치지 않게 한다.")]
    public float sidePadding = 0.06f;

    [Tooltip("그라데이션이 위로 갈수록 옅어지는 정도. 클수록 아래쪽에 빛이 몰린다.")]
    [Range(0.5f, 4f)] public float gradientPower = 1.6f;

    [Tooltip("SpriteRenderer 정렬 순서. 노트(0)·롱노트 몸통(-1)·레인 구분선(-1)보다 뒤에 그린다.")]
    public int sortingOrder = -2;

    [Header("타이밍(초)")]
    [Tooltip("누른 순간 빔이 아래에서 위로 다 차오르기까지 걸리는 시간.")]
    public float riseTime = 0.06f;

    [Tooltip("다 차오른(또는 홀드가 끝난) 뒤 완전히 사라지기까지 걸리는 시간.")]
    public float fadeTime = 0.25f;

    [Tooltip("누른 순간의 빔 높이 비율(0~1). 여기서 1까지 차오른다.")]
    [Range(0f, 1f)] public float riseStartScale = 0.3f;

    [Tooltip("사라지는 동안 빔 높이가 줄어드는 비율(1이면 줄지 않음).")]
    [Range(0.5f, 1f)] public float fadeEndScale = 0.85f;

    const int GradientResolution = 64;

    // 레인 하나의 빔 상태. class 대신 struct 배열로 들고 있어 참조 추적이 없다.
    struct LaneState
    {
        public Transform tr;
        public SpriteRenderer sr;
        public bool visible;      // 렌더러가 켜져 있는지(= 아직 다 사라지지 않았는지)
        public float rise;        // 누른 뒤 지난 시간(차오르기용)
        public float riseFrom;    // 이번에 차오르기 시작한 높이 비율
        public float fade;        // 옅어지기 시작한 뒤 지난 시간
        public float height;      // 현재 높이 비율(다시 누를 때 이어서 차오르기용)
        public float alpha;       // 마지막으로 적용한 밝기(값이 같으면 렌더러를 다시 건드리지 않는다)
        public Note holdNote;     // 홀드 중인 롱노트(없으면 null)
    }

    LaneState[] lanes;
    int visibleCount;
    float spriteHeight = 1f;
    float laneScaleX = 1f;

    Texture2D gradientTex;
    Sprite gradientSprite;

    void Awake()
    {
        BuildGradientSprite();
        BuildLanes();
    }

    void OnDestroy()
    {
        // 실행 중에 만든 텍스처/스프라이트는 씬이 바뀌어도 자동으로 풀리지 않으므로 직접 정리한다.
        if (gradientSprite != null) Destroy(gradientSprite);
        if (gradientTex != null) Destroy(gradientTex);
    }

    // 기능: 아래(알파 1)에서 위(알파 0)로 옅어지는 1×64 흰색 그라데이션 스프라이트를 만든다.
    // 원리: 색은 SpriteRenderer.color로 입히므로 텍스처는 흰색 + 알파만 담는다. 만든 뒤에는
    //       CPU 쪽 사본을 버리도록(makeNoLongerReadable) 올려 메모리를 아낀다. FullRect 메시를
    //       써서 투명 영역을 깎는 타이트 메시 계산을 건너뛴다(사각형 하나 = 정점 4개).
    void BuildGradientSprite()
    {
        gradientTex = new Texture2D(1, GradientResolution, TextureFormat.RGBA32, false)
        {
            name = "LaneKeyBeamGradient",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            hideFlags = HideFlags.DontSave,
        };

        var pixels = new Color32[GradientResolution];
        for (int y = 0; y < GradientResolution; y++)
        {
            float t = y / (float)(GradientResolution - 1);           // 0 = 아래, 1 = 위
            float a = Mathf.Pow(1f - t, gradientPower);
            pixels[y] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
        }
        gradientTex.SetPixels32(pixels);
        gradientTex.Apply(false, true);

        // 피벗을 아래 가운데(0.5, 0)에 둬서, 세로 스케일을 바꾸면 아래 끝은 고정된 채 위로만 늘어난다.
        gradientSprite = Sprite.Create(gradientTex, new Rect(0f, 0f, 1f, GradientResolution),
            new Vector2(0.5f, 0f), GradientResolution, 0, SpriteMeshType.FullRect);
        gradientSprite.name = "LaneKeyBeamGradient";
        gradientSprite.hideFlags = HideFlags.DontSave;
    }

    // 기능: 레인마다 빔 오브젝트를 하나씩 만들어 레인 X좌표·판정선 높이에 배치한다.
    void BuildLanes()
    {
        int count = laneManager != null ? laneManager.LaneCount : 4;
        lanes = new LaneState[count];

        // 레인 폭 = 이웃 레인 사이 간격(레인이 하나뿐이면 2유닛).
        float laneWidth = 2f;
        if (laneManager != null && count > 1)
        {
            laneWidth = Mathf.Abs(laneManager.GetLaneX(1) - laneManager.GetLaneX(0));
        }
        Vector3 spriteSize = gradientSprite.bounds.size;
        spriteHeight = spriteSize.y;
        laneScaleX = Mathf.Max(0.01f, laneWidth - sidePadding * 2f) / spriteSize.x;

        float bottomY = (gameManager != null ? gameManager.judgmentY : -4f) - bottomOffset;

        for (int i = 0; i < count; i++)
        {
            var go = new GameObject("KeyBeam_" + i);
            go.transform.SetParent(transform, false);
            float x = laneManager != null ? laneManager.GetLaneX(i) : 0f;
            go.transform.position = new Vector3(x, bottomY, 0f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = gradientSprite;
            sr.sortingOrder = sortingOrder;
            sr.color = beamColor;
            sr.enabled = false;

            lanes[i].tr = go.transform;
            lanes[i].sr = sr;
        }
    }

    // 기능: 레인의 키가 눌린 순간 호출된다 — 빔을 다시 최대 밝기로 켜고 아래에서부터 차오르게 한다.
    // 원리: 이미 옅어지던 중에 다시 누르면 현재 높이에서 이어서 차오른다(뚝 끊겨 보이지 않게).
    public void Press(int lane)
    {
        if (lanes == null || (uint)lane >= (uint)lanes.Length) return;

        ref LaneState s = ref lanes[lane];
        s.riseFrom = s.visible ? Mathf.Max(riseStartScale, s.height) : riseStartScale;
        s.rise = 0f;
        s.fade = 0f;
        s.holdNote = null;
        if (!s.visible)
        {
            s.visible = true;
            s.sr.enabled = true;
            visibleCount++;
        }
        Apply(ref s, s.riseFrom, 1f);
    }

    // 기능: 롱노트 머리 판정으로 홀드가 시작됐을 때 호출된다 — 그 노트가 홀드 중인 동안 빔을 유지한다.
    public void BeginHold(int lane, Note note)
    {
        if (lanes == null || (uint)lane >= (uint)lanes.Length) return;
        lanes[lane].holdNote = note;
    }

    // 기능: 레인의 키를 뗀 순간 호출된다 — 홀드 유지를 풀어서 그때부터 옅어지게 한다.
    public void Release(int lane)
    {
        if (lanes == null || (uint)lane >= (uint)lanes.Length) return;
        lanes[lane].holdNote = null;
    }

    void Update()
    {
        if (visibleCount == 0) return;

        float dt = Time.deltaTime;
        for (int i = 0; i < lanes.Length; i++)
        {
            ref LaneState s = ref lanes[i];
            if (!s.visible) continue;

            // 홀드가 아직 이어지는지: 그 노트가 여전히 이 레인에서 Holding이고 풀로 돌아가지 않았어야 한다
            // (꼬리 판정이 끝나거나, 꼬리를 놓쳐 Miss가 되거나, 다시하기로 정리되면 여기서 풀린다).
            Note held = s.holdNote;
            bool holding = held != null && held.State == NoteState.Holding && held.lane == i && held.isActiveAndEnabled;
            if (!holding) s.holdNote = null;

            s.rise += dt;
            float r = riseTime > 0f ? Mathf.Clamp01(s.rise / riseTime) : 1f;
            r = 1f - (1f - r) * (1f - r);                         // ease-out: 처음에 빠르게 솟는다
            float height = Mathf.LerpUnclamped(s.riseFrom, 1f, r);

            float alpha = 1f;
            if (holding)
            {
                s.fade = 0f;
            }
            else if (r >= 1f)
            {
                s.fade += dt;
                float f = fadeTime > 0f ? Mathf.Clamp01(s.fade / fadeTime) : 1f;
                if (f >= 1f)
                {
                    s.visible = false;
                    s.sr.enabled = false;
                    s.height = 0f;
                    s.alpha = -1f;
                    visibleCount--;
                    continue;
                }
                float inv = 1f - f;
                alpha = inv * inv;                                 // 빠르게 꺼지고 끝은 부드럽게
                height *= Mathf.LerpUnclamped(fadeEndScale, 1f, inv); // 사라지며 아래로 살짝 가라앉는다
            }

            Apply(ref s, height, alpha);
        }
    }

    // 기능: 계산한 높이·밝기를 Transform/SpriteRenderer에 반영한다.
    // 원리: 롱노트를 오래 누르고 있는 동안처럼 값이 그대로면(최대 높이·최대 밝기)
    //       아무것도 다시 쓰지 않는다 — Transform 변경 알림과 렌더러 갱신을 아낀다.
    void Apply(ref LaneState s, float height, float alpha)
    {
        if (height == s.height && alpha == s.alpha && s.sr.enabled) return;
        s.height = height;
        s.alpha = alpha;
        s.tr.localScale = new Vector3(laneScaleX, beamHeight * height / spriteHeight, 1f);
        Color c = beamColor;
        c.a = beamColor.a * alpha;
        s.sr.color = c;
    }
}
