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

    private const string CacheFolder = "Assets/Editor/AudioPreviewCache";

    // 기능: source와 같은 소리를 speedMultiplier배 속도로 재생할 수 있는 클립을 만들어(또는
    //       캐시/디스크에서 찾아) 돌려준다. speedMultiplier가 1이면 source를 그대로 돌려준다.
    // 원리: AudioUtil의 미리듣기에는 재생 속도/피치를 바꾸는 API가 전혀 없다(리플렉션으로
    //       뒤져봐도 없다) — 그래서 "같은 샘플 데이터인데 재생 주파수(frequency)만
    //       다르게 선언한 클립"을 새로 만드는 우회로를 쓴다. 오디오는 결국 "1초에 몇
    //       샘플을 재생하는가"로 속도가 정해지므로, frequency를 낮추면(원본의
    //       speedMultiplier배) 같은 샘플들을 그만큼 느리게(그리고 낮은 음으로) 재생한다 —
    //       카세트테이프를 천천히 돌리는 것과 같은 원리다. 피치가 같이 바뀌는 건
    //       의도된 트레이드오프다(피치 보정까지 하려면 별도의 타임스트레치 알고리즘이
    //       필요한데, 여기서는 "정확한 타이밍만 느리게 확인"하는 용도라 필요 없다).
    //
    //       처음엔 AudioClip.Create + SetData로 메모리에서만 클립을 만들었는데, 실제로는
    //       소리가 전혀 안 나는 문제가 있었다 — AudioUtil.HasPreview()로 확인해보니
    //       그렇게 즉석에서 만든 클립은 "미리듣기 가능"으로 인식되지 않았다(재생 위치는
    //       그럴듯하게 흘러가지만 실제 오디오 출력은 없었다). AudioUtil.PlayPreviewClip은
    //       임포트된(디스크의 애셋으로 등록된) 클립이어야 실제로 소리를 낸다. 그래서
    //       지금은 변형된 샘플을 임시 WAV 파일로 저장하고 AssetDatabase.ImportAsset으로
    //       실제 임포트해서, "진짜 애셋"이 된 클립을 재생한다. 파일이 이미 있으면
    //       (지난 세션에서 만들어둔 것) 다시 만들지 않고 그대로 불러와 쓴다.
    public static AudioClip GetSpeedVariant(AudioClip source, float speedMultiplier)
    {
        if (source == null) return null;
        if (Mathf.Approximately(speedMultiplier, 1f)) return source;

        var key = (source, Mathf.RoundToInt(speedMultiplier * 1000f));
        if (speedVariantCache.TryGetValue(key, out AudioClip cached) && cached != null)
        {
            return cached;
        }

        string safeName = string.Join("_", source.name.Split(System.IO.Path.GetInvalidFileNameChars()));
        string assetPath = $"{CacheFolder}/{safeName}_x{speedMultiplier:0.##}.wav";

        AudioClip variant = AssetDatabase.LoadAssetAtPath<AudioClip>(assetPath);
        if (variant == null)
        {
            if (!AssetDatabase.IsValidFolder(CacheFolder))
            {
                AssetDatabase.CreateFolder("Assets/Editor", "AudioPreviewCache");
            }

            var samples = new float[source.samples * source.channels];
            source.GetData(samples, 0);
            int newFrequency = Mathf.Max(1, Mathf.RoundToInt(source.frequency * speedMultiplier));

            string projectRoot = System.IO.Path.GetDirectoryName(Application.dataPath);
            string absolutePath = System.IO.Path.Combine(projectRoot, assetPath);
            WriteWav(absolutePath, samples, source.channels, newFrequency);

            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
            variant = AssetDatabase.LoadAssetAtPath<AudioClip>(assetPath);
        }

        speedVariantCache[key] = variant;
        return variant;
    }

    // 기능: float 샘플 배열을 16비트 PCM WAV 파일로 저장한다.
    // 원리: WAV는 RIFF 컨테이너 형식이라, "RIFF/WAVE" 헤더 + "fmt " 청크(채널 수,
    //       샘플레이트, 비트심도 등) + "data" 청크(실제 샘플)만 정확한 순서와 크기로
    //       쓰면 된다. Unity의 float 샘플(-1~1 범위)을 16비트 정수로 양자화해서 쓴다 —
    //       미리듣기용이라 원본보다 살짝 거칠어지는 건 문제가 되지 않는다.
    private static void WriteWav(string path, float[] samples, int channels, int frequency)
    {
        int byteRate = frequency * channels * 2;
        int dataSize = samples.Length * 2;

        using (var stream = new System.IO.FileStream(path, System.IO.FileMode.Create))
        using (var writer = new System.IO.BinaryWriter(stream))
        {
            writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
            writer.Write(36 + dataSize);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));

            writer.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
            writer.Write(16);
            writer.Write((short)1); // PCM
            writer.Write((short)channels);
            writer.Write(frequency);
            writer.Write(byteRate);
            writer.Write((short)(channels * 2));
            writer.Write((short)16);

            writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));
            writer.Write(dataSize);

            foreach (float sample in samples)
            {
                short pcm = (short)Mathf.Clamp(Mathf.RoundToInt(sample * 32767f), short.MinValue, short.MaxValue);
                writer.Write(pcm);
            }
        }
    }
}
