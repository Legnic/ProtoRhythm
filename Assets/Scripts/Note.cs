using UnityEngine;

// ============================================================
// Note.cs
//   DJMAX 방식 노트 하나. 머리 위치 공식은 그대로다.
//
//     y = judgmentY + (hitTime - currentTime) * effectiveSpeed
//
//   롱노트(holdDuration > 0)는 상태를 하나 더 가진다.
//     Waiting -> (머리를 누르면) Holding -> (떼거나 판정 구간을 넘기면) Judged
//   즉 누르는 순간과 떼는 순간, 두 번 판정이 생긴다. 머리 판정은
//   InputJudge가 직접 계산해서 넣어주고(diff 계산에 note.hitTime이 필요하므로),
//   꼬리 판정은 Note 스스로 계산한다 — 판정 구간(MissWindow) 안에 떼면
//   ReleaseHold가 오차에 따라 등급을 매기고, 떼지 못한 채 구간을 넘기면
//   Update가 자동으로 Miss 처리한다.
// ============================================================
public enum NoteState
{
    Waiting,  // 아직 누르지 않음 (탭 노트거나, 롱노트의 머리를 기다리는 중)
    Holding,  // 롱노트를 누르고 있는 중 (머리는 이미 판정됨, 꼬리 판정 대기)
    Judged,   // 판정이 끝나 곧 사라짐
}

public class Note : MonoBehaviour
{
    public int lane;
    public float hitTime;

    [Tooltip("0이면 탭 노트. 0보다 크면 이 길이(초)만큼 눌러서 유지해야 하는 롱노트.")]
    public float holdDuration = 0f;

    public bool IsLongNote => holdDuration > 0f;
    public NoteState State { get; private set; } = NoteState.Waiting;

    private GameManager gameManager;
    private float laneX;

    // 롱노트일 때만 만들어지는, 머리(판정 지점)부터 꼬리까지를 표시하는 몸통 스프라이트.
    private Transform holdBody;
    private Vector2 holdBodyNativeSize = Vector2.one;

    // NoteSpawner가 생성 직후 호출해서 이 노트의 정보를 채운다.
    // (등록도 여기서 함께 한다 — OnEnable에서 등록하면 lane 값이
    //  아직 설정되기 전이라 잘못된 레인에 등록되는 문제가 생기기 때문)
    public void Setup(GameManager manager, float x, int laneIndex, float hitTimeValue, float holdDurationValue = 0f)
    {
        gameManager = manager;
        laneX = x;
        lane = laneIndex;
        hitTime = hitTimeValue;
        holdDuration = holdDurationValue;
        State = NoteState.Waiting;

        if (IsLongNote)
        {
            CreateHoldBody();
        }

        NoteRegistry.Register(this);
    }

    void OnDestroy()
    {
        NoteRegistry.Unregister(this);
        if (holdBody != null)
        {
            Destroy(holdBody.gameObject);
        }
    }

    void Update()
    {
        if (gameManager == null || gameManager.clock == null)
        {
            return;
        }

        float currentTime = gameManager.clock.CurrentTime;

        if (State == NoteState.Holding)
        {
            // 홀드 중에는 머리를 판정선에 고정해서 보여주고, 몸통만 꼬리 쪽에서 줄어든다.
            transform.position = new Vector3(laneX, gameManager.judgmentY, transform.position.z);
            UpdateHoldBody(currentTime);

            float tailTime = hitTime + holdDuration;
            if (currentTime - tailTime > JudgmentWindows.MissWindow)
            {
                // 꼬리 판정 구간(MissWindow) 안에 떼지 못했다 — Miss로 처리한다.
                // (제때 떼면 InputJudge -> ReleaseHold가 먼저 판정하므로 여기까지 오지 않는다)
                Finish(Grade.Miss);
            }
            return;
        }

        // ==== DJMAX 방식의 핵심 공식 (머리 위치) ====
        float y = gameManager.judgmentY + (hitTime - currentTime) * gameManager.EffectiveSpeed;
        transform.position = new Vector3(laneX, y, transform.position.z);

        if (IsLongNote)
        {
            UpdateHoldBody(currentTime);
        }

        // 판정 가능 구간(MissWindow)을 넘어설 때까지 누르지 않았다면 자동으로 Miss 처리한다.
        // (롱노트라면 "머리"조차 못 누른 경우다 — 이러면 꼬리 판정 기회 자체가 없다)
        if (currentTime - hitTime > JudgmentWindows.MissWindow)
        {
            Finish(Grade.Miss);
        }
    }

    // InputJudge가 이 레인의 키를 처음 눌렀을 때(머리 판정을 끝낸 뒤) 호출한다.
    // 머리 판정 자체(점수 반영)는 InputJudge가 처리하고, 여기서는 상태만 넘긴다.
    public void BeginHold()
    {
        State = NoteState.Holding;
    }

    // InputJudge가 홀드 중 키를 뗐을 때(꼬리 판정) 호출한다.
    public void ReleaseHold(float currentTime)
    {
        if (State != NoteState.Holding)
        {
            return;
        }

        float tailTime = hitTime + holdDuration;
        float diff = Mathf.Abs(currentTime - tailTime);

        // 너무 일찍 떼면(MissWindow 밖) 꼬리는 Miss, 그 안이면 오차에 따라 등급을 매긴다.
        Grade grade = diff <= JudgmentWindows.MissWindow ? JudgmentWindows.Evaluate(diff) : Grade.Miss;
        Finish(grade);
    }

    // 탭 노트를 판정할 때 InputJudge가 호출한다.
    public void Judge(Grade grade)
    {
        Finish(grade);
    }

    void Finish(Grade grade)
    {
        State = NoteState.Judged;
        ScoreManager.Instance.ReportHit(grade);
        Destroy(gameObject);
    }

    void CreateHoldBody()
    {
        var bodyObj = new GameObject("HoldBody");
        var sr = bodyObj.AddComponent<SpriteRenderer>();

        SpriteRenderer headSprite = GetComponent<SpriteRenderer>();
        sr.sprite = headSprite.sprite;
        sr.color = new Color(headSprite.color.r, headSprite.color.g, headSprite.color.b, 0.5f);
        sr.sortingOrder = headSprite.sortingOrder - 1;

        // 스프라이트의 "원래" 크기(로컬 스케일 1일 때 월드 유닛 크기)를 알아야
        // 몸통 길이를 원하는 월드 유닛 길이에 맞게 정확히 스케일링할 수 있다.
        if (headSprite.sprite != null)
        {
            holdBodyNativeSize = headSprite.sprite.bounds.size;
        }

        holdBody = bodyObj.transform;
    }

    // 머리(headY)부터 꼬리(tailY = judgmentY + (hitTime+holdDuration-currentTime)*speed)까지를
    // 잇는 직사각형으로 몸통을 그린다. 홀드 중에는 머리가 판정선에 고정되므로
    // 몸통이 꼬리 쪽에서부터 줄어드는 것처럼 보인다.
    void UpdateHoldBody(float currentTime)
    {
        if (holdBody == null)
        {
            return;
        }

        float headY = transform.position.y;
        float tailY = gameManager.judgmentY + (hitTime + holdDuration - currentTime) * gameManager.EffectiveSpeed;
        float length = Mathf.Max(0f, tailY - headY);

        holdBody.position = new Vector3(laneX, headY + length * 0.5f, transform.position.z + 0.01f);

        float scaleX = holdBodyNativeSize.x > 0f ? transform.localScale.x : 1f;
        float scaleY = holdBodyNativeSize.y > 0f ? length / holdBodyNativeSize.y : length;
        holdBody.localScale = new Vector3(scaleX, scaleY, 1f);
    }
}
