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
//   띄는 멈춤이 생긴다(실제로 보고된 "옵션 화면 첫 진입이 느리다" 증상의
//   유력한 원인).
//
//   타이틀 화면이 뜨자마자 게임에 등장하는 모든 문구를 화면 밖에서 한 번
//   그려서(ForceMeshUpdate) 필요한 글리프를 전부 미리 아틀라스에 채워
//   두면, 이후 실제로 그 화면에 들어갔을 때는 이미 있는 글리프를 그대로
//   가져다 쓰기만 하면 되어 멈춤이 없다.
//
//   주의: 여기 목록에 없는 새 문구를 화면에 추가하면 그 문구를 쓰는
//   화면의 첫 진입이 다시 느려질 수 있다 — 새 UI 문구를 추가할 때마다
//   warmupText에도 그 문구를 같이 추가해야 한다.
//
//   SceneLoader의 로딩 오버레이는 앱이 켜지자마자 기본으로 켜진 채로
//   시작한다 — 이 워밍업이 끝나기 전까지 타이틀 화면을 가려서, 플레이어가
//   준비 안 된 화면(글리프가 빠진 텍스트 등)을 보지 않게 한다. 그래서 이
//   컴포넌트가 워밍업을 다 마치면 반드시 SceneLoader.HideOverlay()를
//   불러 오버레이를 걷어내야 한다.
// ============================================================
public class TextGlyphWarmup : MonoBehaviour
{
    [Tooltip("실제 게임에서 한글 UI에 쓰는 폰트(예: MalgunGothic SDF)와 반드시 같아야 한다.")]
    public TMP_FontAsset font;

    [Tooltip("게임 전체에서 쓰이는 문구를 모아둔 텍스트. 새 화면/문구를 추가하면 여기에도 추가한다.")]
    [TextArea(5, 15)]
    public string warmupText =
        "ProtoRythm 시작 옵션 나가기 돌아가기 곡 선택 뒤로 0123456789\n" +
        "계속하기 일시정지 다시하기 선택 화면으로 처음으로 결과 저장 초기화\n" +
        "키 매핑 판정 보정 오프셋: ms Score: Max Combo:\n" +
        "메트로놈 소리와 깜빡임이 동시에 느껴지도록 슬라이더로 조절하세요.\n" +
        "바꾸고 싶은 레인의 버튼을 누른 뒤, 원하는 키를 누르세요. (Esc: 취소)\n" +
        "Happy Core Normal Hard Glitch Hop Chip Tune Super Hard Test Nomal\n" +
        "Perfect Great Good Miss";

    // 기능: 씬이 시작되면 한 프레임 뒤에 화면 밖에서 warmupText를 한 번 그려
    //       글리프 아틀라스를 채우고, 다 채웠으면 정리한 뒤 로딩 오버레이를 걷어낸다.
    IEnumerator Start()
    {
        yield return null;

        var probe = new GameObject("TextGlyphWarmupProbe");
        probe.transform.position = new Vector3(9999f, 9999f, 9999f);

        var text = probe.AddComponent<TextMeshPro>();
        if (font != null)
        {
            text.font = font;
        }
        text.text = warmupText;
        text.ForceMeshUpdate();

        yield return null;

        Destroy(probe);

        SceneLoader.HideOverlay();
    }
}
