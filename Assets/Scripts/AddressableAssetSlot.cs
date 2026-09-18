using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

// ============================================================
// AddressableAssetSlot.cs
//   AssetReference로 로드한 에셋 핸들 "하나"의 수명을 관리하는 헬퍼.
//   곡을 바꿀 때마다 이전 곡의 오디오/배경 이미지 핸들을 반드시
//   Release해야 메모리가 계속 쌓이지 않는데, 이 로직을 SongLoader/
//   SongSelectUI/ResultUI 세 곳에 따로따로 작성하면 해제를 빠뜨리기
//   쉽다. Load를 호출하면 이 슬롯이 들고 있던 이전 핸들을 먼저 자동
//   으로 해제하고 새로 로드하므로, 호출하는 쪽은 "곡이 바뀌면 Load만
//   다시 부르면 된다"는 것만 신경 쓰면 된다.
// ============================================================
public class AddressableAssetSlot<T> where T : Object
{
    private AsyncOperationHandle<T> handle;
    private bool hasHandle;
    private int version;

    // 기능: reference가 가리키는 에셋을 비동기로 로드하고, 끝나면 onLoaded로 결과를 전달한다.
    // 원리: 이전에 이 슬롯이 들고 있던 핸들을 먼저 Release한 뒤 새로 로드한다.
    //       reference가 비어있으면(선택 사항 필드라 null일 수 있다) onLoaded를
    //       null 결과로 즉시 호출하고 끝낸다. version은 "이 Load 호출이 아직
    //       가장 최신 요청인지"를 판별한다 — 로드가 끝나기 전에 Load가 다시
    //       호출되면(예: 사용자가 곡을 빠르게 여러 번 바꿔 누름) 먼저 보낸
    //       요청의 콜백이 나중에 도착해도 낡은 결과이므로 무시해야, 화면에
    //       이전 곡 정보가 잠깐 덮어써지는 일이 없다.
    //
    //       reference.LoadAssetAsync<T>() 대신 Addressables.LoadAssetAsync<T>(reference.RuntimeKey)를
    //       쓰는 이유: 같은 SongData 에셋의 audioClip/backgroundImage 참조를 곡
    //       선택 화면(미리듣기)과 플레이 화면(실제 재생)이 함께 참조하는데,
    //       AssetReference.LoadAssetAsync는 로드 상태를 참조 "객체 자신"에
    //       저장한다. 두 화면이 같은 참조 객체로 각자 로드를 걸면 "이미 로드된
    //       참조를 또 로드하려 한다"는 예외가 난다(실제로 겪은 버그). 키(GUID)
    //       기준으로 로드하면 호출마다 독립된 핸들을 받아 서로 간섭하지 않는다.
    public void Load(AssetReference reference, System.Action<T> onLoaded)
    {
        Release();
        int myVersion = ++version;

        if (reference == null || !reference.RuntimeKeyIsValid())
        {
            onLoaded?.Invoke(null);
            return;
        }

        handle = Addressables.LoadAssetAsync<T>(reference.RuntimeKey);
        hasHandle = true;
        handle.Completed += op =>
        {
            if (myVersion != version) return;

            if (op.Status != AsyncOperationStatus.Succeeded)
            {
                Debug.LogWarning($"AddressableAssetSlot: '{reference.RuntimeKey}' 로드에 실패했습니다.");
                onLoaded?.Invoke(null);
                return;
            }

            onLoaded?.Invoke(op.Result);
        };
    }

    // 기능: 들고 있는 핸들이 있으면 해제한다. 곡을 바꾸기 직전이나, 이 슬롯을
    //       쓰던 오브젝트가 파괴될 때(씬 전환 등) 호출한다.
    // 원리: version도 함께 올려서, 아직 완료되지 않은 채 남아있던 이전 Load의
    //       콜백이 이 Release 이후에 도착하더라도 무시되게 한다 — 그렇지 않으면
    //       "뒤로 가기를 눌러 화면을 떠난 뒤"에도 늦게 도착한 콜백이 이미 지운
    //       화면을 예전 곡 정보로 다시 덮어쓸 수 있다.
    public void Release()
    {
        version++;

        if (hasHandle)
        {
            Addressables.Release(handle);
            hasHandle = false;
        }
    }
}
