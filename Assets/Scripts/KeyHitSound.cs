using UnityEngine;
using UnityEngine.AddressableAssets;

// ============================================================
// KeyHitSound.cs
//   플레이 중 레인 키를 누를 때마다 울리는 효과음(bells). 판정과 무관하게
//   InputJudge가 키 입력을 감지하는 즉시 Play()를 부른다.
//
//   연타 대응: AudioSource.PlayOneShot은 호출할 때마다 엔진 내부 보이스를
//   하나씩 새로 잡아 같은 소스에서도 소리가 겹쳐 재생된다 — 그래서 소스를
//   여러 개 돌려 쓰는 별도 풀은 필요 없다(엔진이 이미 보이스를 풀링한다).
//
//   클립은 곡 음원과 같은 방식으로 Addressables에서 비동기 로드해서
//   씬 로딩 중 동기 로드로 프레임이 튀지 않게 한다. 로드가 끝나기 전에
//   눌린 키는 그냥 소리 없이 넘어간다.
// ============================================================
public class KeyHitSound : MonoBehaviour
{
    [Tooltip("키를 누를 때마다 재생할 효과음(Addressables).")]
    public AssetReferenceT<AudioClip> clipReference;

    [Range(0f, 1f)]
    public float volume = 1f;

    private AudioSource source;
    private AudioClip clip;
    private readonly AddressableAssetSlot<AudioClip> slot = new AddressableAssetSlot<AudioClip>();

    private bool enabledByOption;
    private float optionVolume;

    // 옵션(사운드 탭)의 효과음 on/off와 음량은 PlayScene 진입 시 한 번만 읽는다 —
    // 옵션은 다른 씬이라 플레이 중에는 바뀔 수 없고, 키를 누를 때마다
    // PlayerPrefs를 읽는 비용도 피한다.
    void Awake()
    {
        source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;

        enabledByOption = GameSettings.SfxEnabled;
        optionVolume = GameSettings.SfxVolume;
    }

    void Start()
    {
        slot.Load(clipReference, loaded => clip = loaded);
    }

    void OnDestroy()
    {
        slot.Release();
    }

    // 기능: 효과음을 한 번 재생한다. 연타하면 앞 소리가 끝나기 전에 겹쳐서 울린다.
    public void Play()
    {
        if (enabledByOption && clip != null)
        {
            source.PlayOneShot(clip, volume * optionVolume);
        }
    }
}
