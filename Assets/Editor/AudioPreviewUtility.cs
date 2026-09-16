using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

// ============================================================
// AudioPreviewUtility.cs
//   UnityEditor.AudioUtil은 공개 API가 아니다(internal) — Unity 에디터
//   자체가 AudioClip 인스펙터의 미리듣기/파형 스크러빙에 쓰는 클래스인데,
//   외부에는 리플렉션으로만 열려 있다. 이 프로젝트를 시작한 인계 문서에서
//   "버전마다 시그니처가 바뀌기 쉬워서 범위 밖으로 뺐다"고 적어뒀던 바로
//   그 API다.
//
//   그 위험을 이 파일 하나로 몰아서, 나중에 Unity 버전이 올라가 시그니처가
//   달라지면 여기 한 곳만 고치면 되게 만들어뒀다. ChartEditorWindow는
//   이 클래스의 Play/Stop/Seek/GetPosition만 알고, 리플렉션이 있다는
//   사실 자체를 모른다.
// ============================================================
public static class AudioPreviewUtility
{
    private const BindingFlags Flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

    private static readonly System.Type AudioUtilType = typeof(Editor).Assembly.GetType("UnityEditor.AudioUtil");
    private static readonly MethodInfo PlayMethod = AudioUtilType.GetMethod("PlayPreviewClip", Flags);
    private static readonly MethodInfo StopMethod = AudioUtilType.GetMethod("StopAllPreviewClips", Flags);
    private static readonly MethodInfo IsPlayingMethod = AudioUtilType.GetMethod("IsPreviewClipPlaying", Flags);
    private static readonly MethodInfo GetPositionMethod = AudioUtilType.GetMethod("GetPreviewClipPosition", Flags);
    private static readonly MethodInfo SetSamplePositionMethod = AudioUtilType.GetMethod("SetPreviewClipSamplePosition", Flags);

    // 기능: 클립을 지정한 시각(초)부터 재생한다.
    // 원리: AudioUtil.PlayPreviewClip은 시작 위치를 "샘플 단위" 정수로만 받기
    //       때문에, 초 -> 샘플 변환(시각 × clip.frequency)을 여기서 대신 해준다.
    //       범위를 벗어난 시각(음수, 클립 길이 초과)이 들어와도 죽지 않게
    //       0~samples-1로 잘라낸다.
    public static void Play(AudioClip clip, float startTime, bool loop = false)
    {
        if (clip == null) return;
        PlaySamples(clip, Mathf.RoundToInt(startTime * clip.frequency), loop);
    }

    // 기능: 샘플 인덱스를 직접 지정해서 재생한다(속도 변형 클립처럼 declared
    //       frequency가 원본과 달라서, "시각 × frequency" 계산을 호출부가
    //       직접 해야 하는 경우에 쓴다).
    public static void PlaySamples(AudioClip clip, int startSample, bool loop = false)
    {
        if (clip == null) return;
        int clamped = Mathf.Clamp(startSample, 0, clip.samples - 1);
        PlayMethod.Invoke(null, new object[] { clip, clamped, loop });
    }

    // 기능: 재생 중인 클립의 위치를 다른 시각(초)으로 즉시 옮긴다 — 스크러빙.
    public static void Seek(AudioClip clip, float time)
    {
        if (clip == null) return;
        int sample = Mathf.Clamp(Mathf.RoundToInt(time * clip.frequency), 0, clip.samples - 1);
        SetSamplePositionMethod.Invoke(null, new object[] { clip, sample });
    }

    public static void Stop()
    {
        StopMethod.Invoke(null, null);
    }

    public static bool IsPlaying()
    {
        return (bool)IsPlayingMethod.Invoke(null, null);
    }

    // 기능: 현재 재생 위치를 초 단위로 돌려준다(에디터 자체가 이 단위를 그대로 씀).
    public static float GetPosition()
    {
        return (float)GetPositionMethod.Invoke(null, null);
    }

    // (원본 클립, 배속을 정수화한 키) -> 그 배속으로 재생하기 위해 만들어둔 클립.
    // 세션 중 같은 배속을 다시 고를 때마다 매번 새로 만들지 않도록 캐시해둔다.
    private static readonly Dictionary<(AudioClip clip, int speedKey), AudioClip> speedVariantCache = new();

    // 기능: source와 같은 소리를 speedMultiplier배 속도로 재생할 수 있는 클립을 만들어(또는
    //       캐시에서 찾아) 돌려준다. speedMultiplier가 1이면 source를 그대로 돌려준다.
    // 원리: AudioUtil의 미리듣기에는 재생 속도/피치를 바꾸는 API가 전혀 없다(리플렉션으로
    //       뒤져봐도 없다) — 그래서 "같은 샘플 데이터인데 재생 주파수(frequency)만
    //       다르게 선언한 클립"을 새로 만드는 우회로를 쓴다. 오디오는 결국 "1초에 몇
    //       샘플을 재생하는가"로 속도가 정해지므로, frequency를 낮추면(원본의
    //       speedMultiplier배) 같은 샘플들을 그만큼 느리게(그리고 낮은 음으로) 재생한다 —
    //       카세트테이프를 천천히 돌리는 것과 같은 원리다. 피치가 같이 바뀌는 건
    //       의도된 트레이드오프다(피치 보정까지 하려면 별도의 타임스트레치 알고리즘이
    //       필요한데, 여기서는 "정확한 타이밍만 느리게 확인"하는 용도라 필요 없다).
    //       GetData/SetData로 전체 샘플을 한 번 복사해야 해서 곡이 길수록 잠깐 시간이
    //       걸릴 수 있는데, 배속을 바꿀 때 한 번만 만들고 캐시해두므로 반복 재생에는
    //       영향이 없다.
    public static AudioClip GetSpeedVariant(AudioClip source, float speedMultiplier)
    {
        if (source == null) return null;
        if (Mathf.Approximately(speedMultiplier, 1f)) return source;

        var key = (source, Mathf.RoundToInt(speedMultiplier * 1000f));
        if (speedVariantCache.TryGetValue(key, out AudioClip cached) && cached != null)
        {
            return cached;
        }

        var samples = new float[source.samples * source.channels];
        source.GetData(samples, 0);

        int newFrequency = Mathf.Max(1, Mathf.RoundToInt(source.frequency * speedMultiplier));
        var variant = AudioClip.Create($"{source.name}_x{speedMultiplier:0.##}", source.samples, source.channels, newFrequency, false);
        variant.SetData(samples, 0);

        speedVariantCache[key] = variant;
        return variant;
    }
}
