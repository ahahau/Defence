using Code.Manager;
using Code.UI;
using TMPro;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 고용 창(uGUI, A안) 프리팹을 만들고 SampleScene에 놓는다. 화면 가운데 카드(상인 창과 같은 자리·층, 캔버스 40)이고,
/// 지원자 목록은 정산표 "유닛 상점" 칸(<see cref="SettlementBuilder.BuildShopPane"/>)을 그대로 쓴다.
/// Unity CLI: <c>RecruitBuilder.BuildAll()</c>. 도크 버튼 배선은 <c>RoomManagementBuilder</c>가 한다(이걸 먼저 돌린다).
/// </summary>
public static class RecruitBuilder
{
    public const string PrefabPath = "Assets/03.Prefabs/UI/Recruit/RecruitScreen.prefab";
    private const float CardWidth = 780f;
    private const float CardHeight = 560f;
    private const float Pad = 26f;
    private const float PaneTop = 76f;

    [MenuItem("Tools/Dungeon Keeper/Build Recruit Screen")]
    public static void BuildAll()
    {
        BuildPrefab();
        UiSceneInstaller.Place(PrefabPath, typeof(RecruitScreenView), instance =>
        {
            var shop = instance.GetComponentInChildren<SettlementShopPane>(true);
            UiKit.Wire(shop, "roster", Object.FindAnyObjectByType<HiredUnitRoster>(FindObjectsInactive.Include));
            UiKit.Wire(shop, "costManager", Object.FindAnyObjectByType<CostManager>(FindObjectsInactive.Include));
            UiKit.Wire(shop, "settlement", Object.FindAnyObjectByType<ManagementSettlementManager>(FindObjectsInactive.Include));
        });
    }

    public static void BuildPrefab()
    {
        var canvas = UiKit.Canvas("RecruitScreen", 40);
        var view = canvas.AddComponent<RecruitScreenView>();

        var root = UiKit.Fill(UiKit.Rect("Root", canvas.transform));
        UiKit.Image(root, null, HudPalette.Scrim, true);

        var card = UiKit.Place(UiKit.Rect("Card", root), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(CardWidth, CardHeight));
        UiKit.Panel(card, HudPalette.Surface, HudPalette.Border, true);

        var title = UiKit.Text("Title", card, "고용", 24f, HudPalette.Accent, TextAlignmentOptions.MidlineLeft, true, true);
        UiKit.TopLeft(title.rectTransform, Pad, 18f, 300f, 36f);
        var closeRect = UiKit.TopRight(UiKit.Rect("Close", card), Pad, 20f, 72f, 32f);
        var close = UiKit.Button(closeRect, "닫기", UiKit.ButtonKind.Secondary, 14f);
        var divider = UiKit.TopLeft(UiKit.Rect("Divider", card), Pad, 64f, CardWidth - Pad * 2f, 1f);
        UiKit.Image(divider, null, HudPalette.BorderQuiet, false);

        var paneWidth = CardWidth - Pad * 2f;
        var paneHeight = CardHeight - PaneTop - 18f;
        var pane = UiKit.TopLeft(UiKit.Rect("Shop", card), Pad, PaneTop, paneWidth, paneHeight);
        var shop = SettlementBuilder.BuildShopPane(pane, paneWidth, paneHeight);

        UiKit.Wire(view, "root", root.gameObject);
        UiKit.Wire(view, "shop", shop);
        UiKit.Wire(view, "closeButton", close);

        UiKit.EnsureFolder("Assets/03.Prefabs/UI/Recruit");
        UiKit.ApplyButtonFaces(canvas);
        PrefabUtility.SaveAsPrefabAsset(canvas, PrefabPath, out var saved);
        Object.DestroyImmediate(canvas);
        if (!saved)
            Debug.LogError($"{nameof(RecruitBuilder)}: {PrefabPath} 저장 실패");
    }
}
