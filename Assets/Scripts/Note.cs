using UnityEngine;

// ============================================================
// Note.cs
//   DJMAX 방식 노트 하나. 머리 위치 공식의 뼈대는 그대로다.
//
//     y = judgmentY + (hitTime부터 currentTime까지 이동해야 할 거리)
//
//   변속 구간이 없던 시절엔 이 거리가 그냥 (hitTime - currentTime) *
//   effectiveSpeed 였지만, 지금은 GameManager.GetDistance(currentTime,
//   hitTime)에 위임한다 — 그 사이에 변속 지점이 몇 개 있든 없든 항상
//   정확한 값을 돌려준다(GameManager.cs 참고). 변속이 없는 채보라면
//   결과는 예전 공식과 완전히 같다.
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
    private ObjectPool pool;

    // 롱노트일 때만 보여주는, 머리(판정 지점)부터 꼬리까지를 표시하는 몸통 스프라이트.
    // 풀링을 위해 한 번 만들면 파괴하지 않고 이 노트의 자식으로 두고 재사용한다
    // (Setup에서 매번 IsLongNote 여부에 맞춰 활성/비활성만 바꾼다).
    private Transform holdBody;
    private Vector2 holdBodyNativeSize = Vector2.one;

    // 기능: NoteSpawner가 이 노트를 풀에서 꺼낸 직후 호출해서, 어느 풀로
    //       돌아가야 하는지 기억해둔다.
    public void SetPool(ObjectPool notePool)
    {
        pool = notePool;
    }

    // 기능: NoteSpawner가 노트를 꺼낸 직후 호출해서, 이 노트가 어떤 레인/시각의
    //       노트인지 채워 넣고 NoteRegistry에 등록한다.
    // 원리: 풀에서 재사용된 오브젝트는 이전 판정의 lane/hitTime 등 값이 그대로
    //       남아있을 수 있으므로, 매번 이 메서드에서 전부 새로 채워 넣는다.
    //       값 설정과 등록(NoteRegistry.Register)을 이 메서드 안에서 순서대로,
    //       명시적으로 한 번에 처리한다.
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
            EnsureHoldBody();
        }
        else if (holdBody != null)
        {
            holdBody.gameObject.SetActive(false);
        }

        NoteRegistry.Register(this);
    }

    // 기능: 이 노트 오브젝트가 실제로 파괴될 때(씬이 바뀌는 등, 풀 반환과는 별개로)
    //       명부에서 빠지지 못한 채 남는 일을 막는 안전망이다.
    // 원리: 평소에는 ReturnToPool()이 이미 Unregister까지 끝내두므로 여기서 할
    //       일이 없지만(List.Remove는 없는 항목에 대해 안전하게 아무 일도 안 한다),
    //       씬 언로드처럼 풀을 거치지 않고 바로 파괴되는 경우에 대비한 안전망이다.
    //       holdBody는 이제 이 노트의 자식이라 별도로 챙기지 않아도 함께 파괴된다.
    void OnDestroy()
    {
        NoteRegistry.Unregister(this);
    }

    // 기능: 판정이 끝난(또는 강제로 정리해야 하는) 노트를 명부에서 빼고 풀로 돌려보낸다.
    // 원리: Finish()와 NoteRegistry.ClearAll()(다시하기 시 화면에 남은 노트 정리)이
    //       공통으로 거쳐가는 지점이다. pool이 없다면(예: 풀 없이 직접 테스트하는 경우)
    //       기존처럼 Destroy로 안전하게 대체한다.
    public void ReturnToPool()
    {
        NoteRegistry.Unregister(this);
        if (pool != null)
        {
            pool.Release(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // 기능: 매 프레임 노트의 화면 위치를 갱신하고, 판정 시간을 넘겼는지 검사한다.
    // 원리: 상태가 Holding이면 "머리를 판정선에 고정한 채 꼬리 쪽 길이만 줄어드는"
    //       홀드 중 표시로 전환하고, 판정 구간(MissWindow)을 넘기도록 떼지 못했으면
    //       자동으로 Miss 처리한다(반대로 제때 떼면 InputJudge -> ReleaseHold가 먼저
    //       Finish를 호출하므로 여기까지 오지 않는다). Holding이 아니면(Waiting)
    //       DJMAX 방식의 핵심 공식으로 머리 위치를 계산하고, 역시 MissWindow를
    //       넘도록 누르지 못했으면 Miss로 처리한다(롱노트라면 머리조차 못 누른 것이므로
    //       꼬리 판정 기회 자체가 사라진다).
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
        float y = gameManager.judgmentY + gameManager.GetDistance(currentTime, hitTime);
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

    // 기능: 롱노트의 머리가 판정된 직후 InputJudge가 호출해서, 노트를 "홀드 중"
    //       상태로 전환한다.
    // 원리: 머리 판정 자체(점수 반영)는 InputJudge가 diff를 계산해 이미 처리했으므로,
    //       여기서는 상태만 Holding으로 바꿔서 Update()가 "판정선에 고정 + 몸통 축소"
    //       모드로 넘어가게 만든다. 이 노트는 아직 Destroy되지 않고 살아있는 채로
    //       꼬리 판정(ReleaseHold 또는 Update의 자동 Miss)을 기다린다.
    public void BeginHold()
    {
        State = NoteState.Holding;
    }

    // 기능: 홀드 중인 롱노트의 키를 뗐을 때(InputJudge가 호출) 꼬리 판정을 매긴다.
    // 원리: 이미 Judged/Waiting 상태라면(중복 호출 등) 아무 일도 하지 않는다.
    //       Holding 상태라면 꼬리 목표 시각(hitTime + holdDuration)과 현재 시각의
    //       오차를 구해서, MissWindow 밖이면 Miss, 안이면 JudgmentWindows.Evaluate로
    //       등급을 매긴 뒤 Finish로 마무리한다.
    public void ReleaseHold(float currentTime)
    {
        if (State != NoteState.Holding)
        {
            return;
        }

        float tailTime = hitTime + holdDuration;
        float signedDiff = currentTime - tailTime;
        float diff = Mathf.Abs(signedDiff);

        // 실제로 뗀 시각이 있으므로(자동 타임아웃이 아니므로) 결과 등급과
        // 무관하게 FAST/LATE 피드백을 보고한다 — InputJudge.TryPress와 같은 이유.
        TimingFeedback.Report(signedDiff, lane);

        // 너무 일찍 떼면(MissWindow 밖) 꼬리는 Miss, 그 안이면 오차에 따라 등급을 매긴다.
        Grade grade = diff <= JudgmentWindows.MissWindow ? JudgmentWindows.Evaluate(diff) : Grade.Miss;
        Finish(grade);
    }

    // 기능: 탭 노트를 판정할 때 InputJudge가 호출한다.
    // 원리: 탭 노트는 머리/꼬리 구분이 없어 판정이 한 번뿐이므로, 등급을 받은
    //       즉시 Finish로 넘겨 점수 반영과 파괴까지 한 번에 끝낸다.
    public void Judge(Grade grade)
    {
        Finish(grade);
    }

    // 기능: 이 노트의 판정을 최종 확정하고, 점수에 반영한 뒤 오브젝트를 없앤다.
    // 원리: 탭 노트의 단일 판정, 롱노트의 꼬리 판정(정상 릴리즈/미스 모두)이
    //       공통으로 거쳐가는 종료 지점이다. State를 Judged로 표시해 두면 이번
    //       프레임 안에서 NoteRegistry.GetEarliestNote/GetHoldingNote가 이미
    //       끝난 이 노트를 다시 대상으로 고르지 않는다.
    void Finish(Grade grade)
    {
        State = NoteState.Judged;
        ScoreManager.Instance.ReportHit(grade, lane);
        ReturnToPool();
    }

    // 기능: 롱노트의 몸통(머리~꼬리를 잇는 스프라이트)이 아직 없으면 만들고, 있으면
    //       재사용한다.
    // 원리: 풀링 도입 전에는 노트마다 매번 새로 만들고 OnDestroy에서 같이 파괴했지만,
    //       이제 노트 오브젝트 자체가 재사용되므로 몸통도 한 번만 만들어 이 노트의
    //       자식으로 붙여두고 계속 재사용한다(activate/deactivate만 반복). 머리 노트와
    //       같은 스프라이트를 재사용하되 반투명하게 표시한다. 스프라이트의 "원래" 크기
    //       (sprite.bounds.size, 로컬 스케일이 1일 때의 월드 유닛 크기)를 매번 다시
    //       기억해두는 이유는, 재사용 시점의 스프라이트가 바뀌어 있을 수도 있기 때문이다.
    void EnsureHoldBody()
    {
        SpriteRenderer headSprite = GetComponent<SpriteRenderer>();

        if (holdBody == null)
        {
            var bodyObj = new GameObject("HoldBody");
            bodyObj.transform.SetParent(transform, false);
            bodyObj.AddComponent<SpriteRenderer>();
            holdBody = bodyObj.transform;
        }

        SpriteRenderer sr = holdBody.GetComponent<SpriteRenderer>();
        sr.sprite = headSprite.sprite;
        sr.color = new Color(headSprite.color.r, headSprite.color.g, headSprite.color.b, 0.8f);
        sr.sortingOrder = headSprite.sortingOrder - 1;

        // 스프라이트의 "원래" 크기(로컬 스케일 1일 때 월드 유닛 크기)를 알아야
        // 몸통 길이를 원하는 월드 유닛 길이에 맞게 정확히 스케일링할 수 있다.
        holdBodyNativeSize = headSprite.sprite != null ? headSprite.sprite.bounds.size : Vector2.one;

        holdBody.gameObject.SetActive(true);
    }

    // 기능: 머리(headY)부터 꼬리(tailY)까지를 잇는 직사각형으로 몸통 위치/크기를
    //       매 프레임 다시 그린다.
    // 원리: tailY는 머리와 같은 GetDistance 공식에 hitTime 대신 hitTime+holdDuration을
    //       넘겨서 구한다 — 꼬리도 "그 시각에 판정선에 도달해야 하는 지점"이라는
    //       점은 머리와 똑같기 때문이다. 홀드 중에는 머리가 판정선에 고정되어
    //       있으므로, tailY가 시간이 지날수록 judgmentY에 가까워지면서 몸통이
    //       꼬리 쪽부터 줄어드는 것처럼 보인다. length(월드 유닛)를
    //       holdBodyNativeSize.y로 나눠서 localScale.y로 변환하는 이유는,
    //       스프라이트의 실제 크기가 스케일 1일 때 몇 유닛인지 모르면
    //       "길이 length만큼 늘리기"를 정확한 배율로 환산할 수 없기 때문이다.
    //       여기에 다시 transform.lossyScale.y로 나누는 이유는, holdBody가
    //       이 노트(head)의 자식이라 localScale이 부모의 lossyScale과
    //       곱해져서 화면에 나타나기 때문이다 — Note 프리팹 자체가 Y축을
    //       0.5로 눌러둔 모양이라, 이 나눗셈이 없으면 몸통이 의도한
    //       length의 절반 높이로만 그려져 머리와 몸통 사이에 빈틈이
    //       생긴다(실제로 보고된 "헤드가 분리되어 보이는" 버그의 원인).
    void UpdateHoldBody(float currentTime)
    {
        if (holdBody == null)
        {
            return;
        }

        float headY = transform.position.y;
        float tailY = gameManager.judgmentY + gameManager.GetDistance(currentTime, hitTime + holdDuration);
        float length = Mathf.Max(0f, tailY - headY);

        holdBody.position = new Vector3(laneX, headY + length * 0.5f, transform.position.z + 0.01f);

        // 너비는 머리와 같은 화면 폭으로 보이면 되므로 로컬 스케일 1이면 충분하다
        // (부모의 lossyScale.x가 그대로 곱해져 머리와 같은 폭이 된다).
        float scaleX = 1f;
        float lossyScaleY = transform.lossyScale.y;
        float scaleY = (holdBodyNativeSize.y > 0f && lossyScaleY != 0f)
            ? length / (holdBodyNativeSize.y * lossyScaleY)
            : length;
        holdBody.localScale = new Vector3(scaleX, scaleY, 1f);
    }
}
