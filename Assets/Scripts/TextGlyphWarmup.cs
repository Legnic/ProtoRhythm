using System.Collections;
using TMPro;
using UnityEngine;

// ============================================================
// TextGlyphWarmup.cs
//   MalgunGothic SDF 폰트는 Dynamic OS 모드다 — 폰트 파일 자체를 빌드에
//   포함하지 않는 대신, 화면에 그 글자가 "처음" 등장하는 순간 OS에 설치된
//   폰트에서 그 글리프를 읽어와 아틀라스 텍스처에 그 자리에서 새로
//   래스터라이즈해 넣는다. 한글은 알파벳과 달리 글자 하나하나가 전부
//   다른 글리프라서, 그동안 한 번도 안 나온 문구가 많은 화면(예: 옵션
//   화면)에 처음 들어가면 그 많은 글리프를 한꺼번에 그려 넣느라 눈에
//   띄는 멈춤이 생긴다.
//
//   문구를 딱 두 그룹으로 나눠서 예열한다:
//     criticalWarmupText — 타이틀 화면 자체에 곧바로 보이는 문구만. 로딩
//       오버레이가 떠 있는 "동안" 그리고, 다 끝나면 곧장 오버레이를 걷는다.
//       여기 목록을 필요 이상으로 늘리면 그만큼 로딩 화면이 길어진다 —
//       "빌드에서 첫 로딩 화면이 너무 길다"로 실제 보고된 문제의 원인이
//       바로 이 목록에 다른 화면들 문구까지 전부 욱여넣었던 것이었다.
//     backgroundWarmupText — 나머지 화면(선택/플레이/결과/옵션 등)에서
//       쓰이는 문구. 오버레이를 이미 걷어낸 뒤, 타이틀 화면에 떠 있는
//       동안 여러 프레임에 걸쳐 조금씩(chunk 단위) 그린다 — 한 프레임에
//       다 그리면 그 프레임만 길어지는 건 매한가지라, 조금씩 나눠서
//       플레이어가 타이틀 화면을 보고 있는 몇 초 사이에 눈치채지 못하게
//       끝마친다. 플레이어가 그 화면에 실제로 들어가기 전에 이 백그라운드
//       예열이 다 끝나 있으면 그 화면도 멈춤 없이 뜬다 — 다만 타이틀이
//       뜨자마자 몇 프레임 만에 바로 그 화면으로 넘어가면(예: 아주 빠르게
//       연타) 아직 예열이 안 끝났을 수 있다.
//
//   주의: 여기 목록에 없는 새 문구를 화면에 추가하면 그 문구를 쓰는
//   화면의 첫 진입이 다시 느려질 수 있다 — 새 UI 문구를 추가할 때마다
//   (타이틀 자체 문구가 아니라면 backgroundWarmupText에) 같이 추가해야
//   한다. 실제로 볼륨/FAST-LATE 옵션 탭을 추가하면서 이 목록 갱신을
//   깜빡해 "옵션 화면 첫 진입이 느리다"가 다시 보고된 적이 있다.
// ============================================================
public class TextGlyphWarmup : MonoBehaviour
{
    [Tooltip("실제 게임에서 한글 UI에 쓰는 폰트(예: MalgunGothic SDF)와 반드시 같아야 한다.")]
    public TMP_FontAsset font;

    [Tooltip("타이틀 화면 자체에 곧바로 보이는 문구만. 로딩 화면이 떠 있는 동안 그려지므로 최소한으로 유지한다.")]
    [TextArea(2, 4)]
    public string criticalWarmupText = "ProtoRythm 시작 옵션 나가기 0123456789";

    [Tooltip("다른 화면들(선택/플레이/결과/옵션)에서 쓰이는 문구. 로딩 화면을 가리지 않고 백그라운드에서 나눠 예열한다.")]
    [TextArea(6, 15)]
    public string backgroundWarmupText =
        "돌아가기 곡 선택 뒤로\n" +
        "계속하기 일시정지 다시하기 선택 화면으로 처음으로 결과 저장 초기화\n" +
        "키 매핑 판정 보정 오프셋: ms Score: Max Combo: Combo:\n" +
        "메트로놈 소리와 깜빡임이 동시에 느껴지도록 슬라이더로 조절하세요.\n" +
        "바꾸고 싶은 레인의 버튼을 누른 뒤, 원하는 키를 누르세요. (Esc: 취소)\n" +
        "Happy Core Normal Hard Glitch Hop Chip Tune Super Hard Test Nomal\n" +
        "Perfect Great Good Miss FAST LATE ms\n" +
        "볼륨 전체 볼륨을 조절하세요. 100%\n" +
        "FAST/LATE 표시 끔 밀리초까지 표시";

    [Tooltip("백그라운드 예열 시 한 프레임에 처리할 글자 수. 작을수록 한 프레임당 부담은 줄지만 전체 예열은 오래 걸린다.")]
    public int backgroundCharsPerFrame = 20;

    // 기능: 한 프레임 뒤에 화면 밖에서 critical 문구만 먼저 그려 로딩 화면을
    //       걷어내고, 이어서 나머지 문구를 백그라운드로 나눠 예열한다.
    IEnumerator Start()
    {
        yield return null;

        var probeObj = new GameObject("TextGlyphWarmupProbe");
        probeObj.transform.position = new Vector3(9999f, 9999f, 9999f);

        var text = probeObj.AddComponent<TextMeshPro>();
        if (font != null)
        {
            text.font = font;
        }

        // 1) 타이틀 화면 자체에 필요한 최소 문구만 먼저 예열하고 즉시 로딩 화면을 걷는다.
        text.text = criticalWarmupText;
        text.ForceMeshUpdate();

        yield return null;

        SceneLoader.HideOverlay();

        // 2) 나머지 문구는 로딩 화면 없이, 여러 프레임에 걸쳐 조금씩 예열한다.
        yield return StartCoroutine(WarmupInBackground(text, backgroundWarmupText));

        Destroy(probeObj);
    }

    // 기능: text를 chunk 단위로 잘라 한 프레임에 하나씩 그린다 — 문구 전체를
    //       한 번에 그리면 그 한 프레임만 길어지는 문제가 그대로 반복되므로,
    //       여러 프레임에 나눠서 매 프레임 부담을 작게 유지한다. 표시 내용이
    //       사람이 읽기에 맞는 문장일 필요는 없다 — 그 문자가 한 번이라도
    //       그려져 글리프가 아틀라스에 들어가기만 하면 목적을 달성한다.
    private IEnumerator WarmupInBackground(TextMeshPro probe, string fullText)
    {
        for (int i = 0; i < fullText.Length; i += backgroundCharsPerFrame)
        {
            int length = Mathf.Min(backgroundCharsPerFrame, fullText.Length - i);
            probe.text = fullText.Substring(i, length);
            probe.ForceMeshUpdate();
            yield return null;
        }
    }
}
