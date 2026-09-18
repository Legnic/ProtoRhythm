using System.Collections.Generic;
using UnityEngine;

// ============================================================
// ObjectPool.cs
//   Instantiate/Destroy를 반복하는 대신, 한 번 만든 오브젝트를
//   비활성화해뒀다가 다시 꺼내 쓰는 범용 오브젝트 풀.
//
//   왜 필요한가: 노트/히트 이펙트처럼 짧은 생애주기로 자주 생성-파괴되는
//   오브젝트를 매번 Instantiate/Destroy하면, 그때그때 메모리 할당과 GC,
//   그리고 (특히 그 프리팹의 머티리얼이 그 세션에서 처음 그려지는 순간
//   일어나는) 셰이더 변형 컴파일까지 겹쳐서 첫 사용 시점에 심한 프레임
//   드랍이 생긴다. 미리 만들어(Prewarm) 비활성 상태로 쌓아두면, 실제
//   플레이 중에는 이미 존재하는 오브젝트를 꺼내 위치만 옮기고 켜기만
//   하면 되므로 이 비용이 로딩/준비 시점으로 옮겨진다.
// ============================================================
public class ObjectPool : MonoBehaviour
{
    private GameObject prefab;
    private readonly Queue<GameObject> free = new Queue<GameObject>();

    // 기능: 풀을 담을 컨테이너 오브젝트를 만들고, 미리 count개만큼 채워둔다.
    public static ObjectPool Create(string name, GameObject prefab, int prewarmCount)
    {
        var go = new GameObject(name);
        var pool = go.AddComponent<ObjectPool>();
        pool.prefab = prefab;
        pool.Prewarm(prewarmCount);
        return pool;
    }

    // 기능: 비활성 상태의 인스턴스를 count개 미리 만들어 풀에 쌓아둔다.
    public void Prewarm(int count)
    {
        for (int i = 0; i < count; i++)
        {
            GameObject obj = Instantiate(prefab, transform);
            obj.SetActive(false);
            free.Enqueue(obj);
        }
    }

    // 기능: 풀에서 오브젝트 하나를 꺼내 지정된 위치/회전으로 활성화한다.
    // 원리: 풀이 비어있으면(동시에 필요한 개수가 prewarm 수를 넘어서면)
    //       그 자리에서 새로 Instantiate해서 부족분을 메운다 — 개수가
    //       모자라 노트가 아예 안 나오는 것보다는, 드물게 한 번 더
    //       Instantiate 비용을 치르는 쪽이 안전하다.
    public GameObject Get(Vector3 position, Quaternion rotation)
    {
        GameObject obj = free.Count > 0 ? free.Dequeue() : Instantiate(prefab);
        obj.transform.SetParent(null);
        obj.transform.SetPositionAndRotation(position, rotation);
        obj.SetActive(true);
        return obj;
    }

    // 기능: 다 쓴 오브젝트를 비활성화해서 풀로 돌려보낸다.
    public void Release(GameObject obj)
    {
        obj.SetActive(false);
        obj.transform.SetParent(transform);
        free.Enqueue(obj);
    }
}
