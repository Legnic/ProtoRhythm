using UnityEngine;
using UnityEngine.AddressableAssets;

// ============================================================
// SongBackgroundCache.cs
//   곡 배경 이미지 하나를 씬이 바뀌어도 살아있는 곳에 캐싱해둔다.
//   SongSelectUI(난이도 화면) -> SongLoader(플레이 화면) -> ResultUI
//   (결과 화면)가 전부 "지금 고른 곡의 배경"을 각자 로드하는데, 예전엔
//   각 컴포넌트가 자기 자신의 슬롯으로 따로 로드하고 OnDestroy에서
//   해제했다 — 그러면 SongSelectUI가 씬 전환으로 파괴되며 방금 화면에
//   보여주고 있던 배경까지 같이 해제해버려서, 다음 씬(PlayScene)이
//   화면에 이미 나와있던 바로 그 배경을 처음부터 다시 로드하는 낭비가
//   생겼다(실제로 "플레이 화면에서 이미지가 한 박자 늦게 뜬다"는 형태로
//   보고된 버그). 이 클래스에 한 곳에만 캐시를 두고 세 화면이 전부 여길
//   같이 쓰면, 같은 곡을 보여주는 동안은 한 번만 로드하고 그대로 이어서
//   쓴다.
//
//   MonoBehaviour가 아닌 정적 클래스라 어떤 씬이 파괴되어도 캐시가
//   유지된다(GameSession과 같은 이유). 곡이 바뀌면 이전 곡의 핸들을
//   해제하고 새로 로드하므로, 한 번에 배경 이미지 하나만큼의 메모리만
//   차지한다 — 앱이 켜져있는 동안 계속 들고 있어도 될 만큼 가볍다.
// ============================================================
public static class SongBackgroundCache
{
    private static readonly AddressableAssetSlot<Sprite> slot = new AddressableAssetSlot<Sprite>();
    private static AssetReference loadedReference;
    private static Sprite loadedSprite;

    // 기능: reference가 가리키는 배경을 로드하고 onLoaded로 전달한다.
    // 원리: 직전에 로드한 참조와 이번 참조가 같으면(=같은 곡을 계속 보여주는
    //       중이면) 이미 캐시된 결과를 그 즉시 돌려준다 — 다시 로드를 걸지
    //       않으므로 씬이 바뀌어도 지연이 없다. 참조가 다르면(곡이 바뀌면)
    //       slot.Load가 이전 곡의 핸들을 자동으로 해제하고 새로 로드한다.
    public static void Load(AssetReference reference, System.Action<Sprite> onLoaded)
    {
        if (reference == loadedReference)
        {
            onLoaded?.Invoke(loadedSprite);
            return;
        }

        loadedReference = reference;
        loadedSprite = null;
        slot.Load(reference, sprite =>
        {
            loadedSprite = sprite;
            onLoaded?.Invoke(sprite);
        });
    }
}
