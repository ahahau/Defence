using Code.UI;
using Code.UI.Run;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 던전 FTL 시연 런의 상인 좌판(uGUI, 장부형) 프리팹을 만들고 DungeonRun 씬에 놓아 <see cref="RunSessionController"/>에 잇는다.
/// 구역 지도(50) 위, 팝업(100) 아래 캔버스(60). Unity CLI: <c>RunMerchantBuilder.BuildAll()</c>.
///
/// 카드 1040×680(2026-11-11 사용자 목업 승인 "B 전용 좌판"): 제목·금화, 안내 줄, 항목 일곱 줄(이름·효과·가격·사기),
/// 경고 줄과 떠나기. 새 부하 고르기 판은 같은 자리에 네 종류 카드와 뒤로·고용 버튼을 둔다.
/// </summary>
public static class RunMerchantBuilder
{
    public const string PrefabPath = "Assets/03.Prefabs/UI/Run/RunMerchant.prefab";
    private const string ScenePath = "Assets/00.Scenes/DungeonRun.unity";
    private const int SortingOrder = 60;
    private const float CardWidth = 1040f;
    private const float CardHeight = 680f;
    private const float Pad = 28f;
    private const float BodyTop = 108f;
    private const int RowCount = 7;
    private const float RowHeight = 56f;
    private const float RowGap = 6f;
    private const int PickCount = 4;
    private const float PickGap = 16f;
    private const float PickHeight = 300f;

    [MenuItem("Tools/Dungeon Keeper/Build Run Merchant")]
    public static void BuildAll()
    {
        BuildPrefab();
        Place();
    }

    public static void BuildPrefab()
    {
        var rootObject = UiKit.Canvas("RunMerchant", SortingOrder);
        var view = rootObject.AddComponent<RunMerchantView>();

        var panel = UiKit.Fill(UiKit.Rect("Root", rootObject.transform));
        UiKit.Image(panel, null, HudPalette.Scrim, true);

        var card = UiKit.Place(UiKit.Rect("Card", panel), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(CardWidth, CardHeight));
        UiKit.Panel(card, HudPalette.Surface, HudPalette.Border, true);

        float inner = CardWidth - Pad * 2f;
        Label(card, "Title", "상인", 24f, HudPalette.Accent, Pad, 20f, 300f, 36f, true);
        var gold = Label(card, "Gold", string.Empty, 17f, HudPalette.AccentSoft, 0f, 24f, 360f, 30f, false, true, TextAlignmentOptions.MidlineRight);
        UiKit.TopRight(gold.rectTransform, Pad, 24f, 360f, 30f);
        var summary = Label(card, "Summary", string.Empty, 15f, HudPalette.TextDim, Pad, 60f, inner, 26f);
        Divider(card, Pad, 94f, inner);

        float bodyHeight = CardHeight - BodyTop - 20f;

        // ── 항목 목록 ──
        var list = UiKit.TopLeft(UiKit.Rect("List", card), Pad, BodyTop, inner, bodyHeight);
        var names = new Object[RowCount];
        var details = new Object[RowCount];
        var prices = new Object[RowCount];
        var buttons = new Object[RowCount];
        for (int i = 0; i < RowCount; i++)
        {
            var row = UiKit.TopLeft(UiKit.Rect($"Row{i}", list), 0f, i * (RowHeight + RowGap), inner, RowHeight);
            UiKit.Panel(row, HudPalette.Deep, HudPalette.BorderQuiet);
            names[i] = Label(row, "Name", string.Empty, 18f, HudPalette.Text, 16f, 5f, 560f, 26f, true);
            details[i] = Label(row, "Detail", string.Empty, 14f, HudPalette.TextDim, 16f, 30f, 640f, 22f);
            var price = Label(row, "Price", string.Empty, 17f, HudPalette.Accent, 0f, 0f, 120f, RowHeight, false, true, TextAlignmentOptions.MidlineRight);
            UiKit.TopRight(price.rectTransform, 166f, 0f, 120f, RowHeight);
            prices[i] = price;
            var buttonRect = UiKit.TopRight(UiKit.Rect("Buy", row), 12f, 8f, 130f, 40f);
            buttons[i] = UiKit.Button(buttonRect, "사기", UiKit.ButtonKind.Primary, 16f);
        }
        float listBottom = RowCount * (RowHeight + RowGap);
        var warning = Label(list, "Warning", string.Empty, 15f, HudPalette.Warn, 0f, listBottom + 18f, inner - 200f, 34f);
        var leaveRect = UiKit.TopRight(UiKit.Rect("Leave", list), 0f, listBottom + 12f, 170f, 48f);
        var leave = UiKit.Button(leaveRect, "떠나기", UiKit.ButtonKind.Secondary, 17f);

        // ── 새 부하 고르기 ──
        var pick = UiKit.TopLeft(UiKit.Rect("Pick", card), Pad, BodyTop, inner, bodyHeight);
        var pickHeading = Label(pick, "Heading", string.Empty, 15f, HudPalette.TextDim, 0f, 0f, inner, 28f);
        float pickWidth = (inner - PickGap * (PickCount - 1)) / PickCount;
        var cards = new Object[PickCount];
        var bodies = new Object[PickCount];
        for (int i = 0; i < PickCount; i++)
        {
            var rt = UiKit.TopLeft(UiKit.Rect($"Kind{i}", pick), i * (pickWidth + PickGap), 44f, pickWidth, PickHeight);
            var kind = UiKit.Button(rt, string.Empty, UiKit.ButtonKind.Secondary, 19f);
            UiKit.Place(kind.Label.rectTransform, new Vector2(0f, 1f), Vector2.one, new Vector2(0.5f, 1f), new Vector2(0f, -16f), new Vector2(-24f, 32f));
            var body = UiKit.Text("Body", rt, string.Empty, 15f, HudPalette.TextBody, TextAlignmentOptions.TopLeft);
            UiKit.Place(body.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), new Vector2(0f, -24f), new Vector2(-32f, -96f));
            cards[i] = kind;
            bodies[i] = body;
        }
        var confirmRect = UiKit.TopRight(UiKit.Rect("Confirm", pick), 0f, 44f + PickHeight + 24f, 300f, 48f);
        var confirm = UiKit.Button(confirmRect, "부하를 고르세요", UiKit.ButtonKind.Primary, 17f);
        var backRect = UiKit.TopRight(UiKit.Rect("Back", pick), 316f, 44f + PickHeight + 24f, 130f, 48f);
        var back = UiKit.Button(backRect, "뒤로", UiKit.ButtonKind.Secondary, 16f);
        pick.gameObject.SetActive(false);

        UiKit.Wire(view, "root", panel.gameObject);
        UiKit.Wire(view, "gold", gold);
        UiKit.Wire(view, "summary", summary);
        UiKit.Wire(view, "listPanel", list.gameObject);
        UiKit.WireArray(view, "rowNames", names);
        UiKit.WireArray(view, "rowDetails", details);
        UiKit.WireArray(view, "rowPrices", prices);
        UiKit.WireArray(view, "rowButtons", buttons);
        UiKit.Wire(view, "warning", warning);
        UiKit.Wire(view, "leaveButton", leave);
        UiKit.Wire(view, "pickPanel", pick.gameObject);
        UiKit.Wire(view, "pickHeading", pickHeading);
        UiKit.WireArray(view, "pickCards", cards);
        UiKit.WireArray(view, "pickBodies", bodies);
        UiKit.Wire(view, "pickBackButton", back);
        UiKit.Wire(view, "pickConfirmButton", confirm);
        panel.gameObject.SetActive(false);

        UiKit.EnsureFolder("Assets/03.Prefabs/UI/Run");
        UiKit.ApplyButtonFaces(rootObject);
        PrefabUtility.SaveAsPrefabAsset(rootObject, PrefabPath, out var saved);
        Object.DestroyImmediate(rootObject);
        if (!saved)
            Debug.LogError($"{nameof(RunMerchantBuilder)}: {PrefabPath} 저장 실패");
    }

    /// <summary>DungeonRun에 놓고 세션 컨트롤러에 잇는다. 이 프리팹으로 만든 옛 인스턴스만 지운다.</summary>
    private static void Place()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.path != ScenePath)
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        foreach (var existing in Object.FindObjectsByType<RunMerchantView>(FindObjectsInactive.Include))
        {
            var instanceRoot = PrefabUtility.GetOutermostPrefabInstanceRoot(existing.gameObject);
            if (instanceRoot != null && PrefabUtility.GetCorrespondingObjectFromSource(instanceRoot) == prefab)
                Object.DestroyImmediate(instanceRoot);
        }

        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        var session = Object.FindAnyObjectByType<RunSessionController>(FindObjectsInactive.Include);
        if (session == null)
            Debug.LogError($"{nameof(RunMerchantBuilder)}: DungeonRun에 RunSessionController가 없습니다.");
        else
            UiKit.Wire(session, "merchantWindow", instance.GetComponent<RunMerchantView>());

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static void Divider(RectTransform parent, float x, float y, float width)
    {
        var line = UiKit.TopLeft(UiKit.Rect("Divider", parent), x, y, width, 2f);
        UiKit.Image(line, null, HudPalette.BorderQuiet, false);
    }

    private static TextMeshProUGUI Label(RectTransform parent, string name, string value, float size, Color color,
        float x, float y, float width, float height, bool bold = false, bool singleLine = true,
        TextAlignmentOptions alignment = TextAlignmentOptions.MidlineLeft)
    {
        var text = UiKit.Text(name, parent, value, size, color, alignment, bold, singleLine);
        UiKit.TopLeft(text.rectTransform, x, y, width, height);
        return text;
    }
}
