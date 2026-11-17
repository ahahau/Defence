using Code.UI;
using TMPro;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 첫날 안내 덮개(uGUI, A안) 프리팹을 만들고 SampleScene에 놓는다. 캔버스 85(대화 80 위, 팝업 100 아래).
/// Unity CLI: <c>TutorialOverlayBuilder.BuildAll()</c>.
///
/// 네 조각 어둠(클릭을 삼킴) + 위 가운데 안내 카드 760×84(위에서 136, 배너 24~120 아래): 머리글, 본문 두 줄, 오른쪽 건너뛰기.
/// </summary>
public static class TutorialOverlayBuilder
{
    public const string PrefabPath = "Assets/03.Prefabs/UI/Tutorial/TutorialOverlay.prefab";
    private const float HintWidth = 760f;
    private const float HintHeight = 84f;
    private const float SkipWidth = 118f;

    [MenuItem("Tools/Dungeon Keeper/Build Tutorial Overlay")]
    public static void BuildAll()
    {
        BuildPrefab();
        UiSceneInstaller.Place(PrefabPath, typeof(TutorialOverlayView), instance =>
        {
            var view = instance.GetComponent<TutorialOverlayView>();
            var source = Object.FindAnyObjectByType<PlayTutorialView>(FindObjectsInactive.Include);
            UiKit.Wire(view, "tutorial", source);
        });
    }

    public static void BuildPrefab()
    {
        var canvas = UiKit.Canvas("TutorialOverlay", 85);
        var view = canvas.AddComponent<TutorialOverlayView>();

        var spotlight = UiKit.SpotlightDim(canvas.transform);

        var hint = UiKit.Place(UiKit.Rect("Hint", canvas.transform), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -136f), new Vector2(HintWidth, HintHeight));
        UiKit.Panel(hint, HudPalette.Surface, HudPalette.BorderAccent, true);

        var textWidth = HintWidth - 24f - 16f - SkipWidth - 12f;
        var header = UiKit.Text("Header", hint, "처음 시작하기 · 1/5", 15f, HudPalette.Accent, TextAlignmentOptions.Center, true, true);
        UiKit.TopLeft(header.rectTransform, 24f, 10f, textWidth, 22f);
        var body = UiKit.Text("Body", hint, "밝은 타일을 눌러 첫 방을 파세요", 16f, HudPalette.TextBody, TextAlignmentOptions.Center);
        UiKit.TopLeft(body.rectTransform, 24f, 34f, textWidth, 42f);

        var skipRect = UiKit.Place(UiKit.Rect("Skip", hint), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-16f, 0f), new Vector2(SkipWidth, 36f));
        var skip = UiKit.Button(skipRect, "건너뛰기", UiKit.ButtonKind.Secondary, 14f);

        UiKit.Wire(view, "hint", hint.gameObject);
        UiKit.Wire(view, "header", header);
        UiKit.Wire(view, "body", body);
        UiKit.Wire(view, "skipButton", skip);
        UiKit.Wire(view, "spotlight", spotlight);
        hint.gameObject.SetActive(false);

        UiKit.EnsureFolder("Assets/03.Prefabs/UI/Tutorial");
        UiKit.ApplyButtonFaces(canvas);
        PrefabUtility.SaveAsPrefabAsset(canvas, PrefabPath, out var saved);
        Object.DestroyImmediate(canvas);
        if (!saved)
            Debug.LogError($"{nameof(TutorialOverlayBuilder)}: {PrefabPath} 저장 실패");
    }
}
