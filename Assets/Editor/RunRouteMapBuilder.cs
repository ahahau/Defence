using Code.UI;
using Code.UI.Run;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 던전 FTL 시연 런의 구역 지도 창(uGUI, 장부형) 프리팹을 만들고 DungeonRun 씬에 놓아 <see cref="RunSessionController"/>에 잇는다.
/// RunHud(10) 위, 팝업(100) 아래 캔버스(50). Unity CLI: <c>RunRouteMapBuilder.BuildAll()</c>.
///
/// 카드 1500×580(2026-11-12 설명칸 빈 곳 줄임): 제목·자원 줄, 칸 줄(여섯 열, 둘째·다섯째 열은 상인·글 사건 두 칸), 색 설명 한 줄, 고른 칸 설명(이름·종류·본문·닫기·들어가기).
/// 칸 순서는 RunDemoRoute.Node 순서(도적·상인·글 사건·공병·원정·정예) 뒤에 원정 뒤 갈래(상인·글 사건)를 붙인다(2026-11-11 원정 뒤 갈래).
/// </summary>
public static class RunRouteMapBuilder
{
    public const string PrefabPath = "Assets/03.Prefabs/UI/Run/RunRouteMap.prefab";
    private const string ScenePath = "Assets/00.Scenes/DungeonRun.unity";
    private const int SortingOrder = 50;
    private const float CardWidth = 1500f;
    private const float CardHeight = 580f;
    private const float Pad = 28f;
    private const float StripTop = 84f;
    private const float StripHeight = 220f;
    private const float NodeWidth = 190f;
    private const float NodeHeight = 76f;
    private const float BranchGap = 12f;
    private const int Columns = 6;
    private const float DetailTop = 348f;
    // 본문은 많아야 3줄이라(상인 칸 부족분 줄까지) 본문 칸을 3줄 높이로 두고 버튼을 그 아래에 붙인다(2026-11-12 사용자 A안 승인).
    private const float DetailHeight = 204f;
    private const float DetailBodyHeight = 72f;

    // 칸 순서 → 열. 둘째 열(1)과 다섯째 열(4)에 상인·글 사건이 위아래로 선다.
    private static readonly int[] NodeColumns = { 0, 1, 1, 2, 3, 5, 4, 4 };
    private static readonly string[] NodeNames = { "도적 침입", "상인", "글 사건", "공병 침입", "진지 원정", "정예 침입", "상인", "글 사건" };

    [MenuItem("Tools/Dungeon Keeper/Build Run Route Map")]
    public static void BuildAll()
    {
        BuildPrefab();
        Place();
    }

    public static void BuildPrefab()
    {
        var rootObject = UiKit.Canvas("RunRouteMap", SortingOrder);
        var view = rootObject.AddComponent<RunRouteMapView>();

        var panel = UiKit.Fill(UiKit.Rect("Root", rootObject.transform));
        UiKit.Image(panel, null, HudPalette.Scrim, true);

        var card = UiKit.Place(UiKit.Rect("Card", panel), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(CardWidth, CardHeight));
        UiKit.Panel(card, HudPalette.Surface, HudPalette.Border, true);

        Label(card, "Title", "1구역 지도", 24f, HudPalette.Accent, Pad, 20f, 300f, 36f, true);
        var resources = Label(card, "Resources", string.Empty, 16f, HudPalette.TextBody, 0f, 24f, 1100f, 30f, false, true, TextAlignmentOptions.MidlineRight);
        UiKit.TopRight(resources.rectTransform, Pad, 24f, 1100f, 30f);
        Divider(card, Pad, 66f, CardWidth - Pad * 2f);

        var strip = UiKit.TopLeft(UiKit.Rect("Strip", card), Pad, StripTop, CardWidth - Pad * 2f, StripHeight);
        UiKit.Panel(strip, HudPalette.Deep, HudPalette.BorderQuiet);
        float columnGap = (CardWidth - Pad * 2f - NodeWidth * Columns) / (Columns - 1);
        float center = StripHeight * 0.5f;

        var nodes = new Object[NodeColumns.Length];
        var states = new Object[NodeColumns.Length];
        var branchIndex = new int[Columns];
        for (int i = 0; i < NodeColumns.Length; i++)
        {
            int column = NodeColumns[i];
            float x = column * (NodeWidth + columnGap);
            float y = center - NodeHeight * 0.5f;
            if (column is 1 or 4)
            {
                y = center - NodeHeight - BranchGap * 0.5f + branchIndex[column] * (NodeHeight + BranchGap);
                branchIndex[column]++;
            }
            nodes[i] = BuildNode(strip, i, x, y, out var state);
            states[i] = state;
        }
        for (int column = 0; column < Columns - 1; column++)
        {
            float x = column * (NodeWidth + columnGap) + NodeWidth;
            Label(strip, $"Arrow{column}", "→", 26f, HudPalette.TextDim, x, center - 20f, columnGap, 40f, false, true, TextAlignmentOptions.Center);
        }

        // 칸 밝기 범례 줄은 뺐다. 칸 아래 상태 글(고를 수 있음·지나옴)이 같은 뜻이다(2026-11-12 사용자 목업 승인).

        var detail = UiKit.TopLeft(UiKit.Rect("Detail", card), Pad, DetailTop, CardWidth - Pad * 2f, DetailHeight);
        UiKit.Panel(detail, HudPalette.SurfaceRaised, HudPalette.BorderQuiet);
        float detailWidth = CardWidth - Pad * 2f;
        var detailName = Label(detail, "Name", string.Empty, 22f, HudPalette.Text, 20f, 14f, 420f, 34f, true);
        var detailKind = Label(detail, "Kind", string.Empty, 15f, HudPalette.TextDim, 0f, 18f, 400f, 28f, false, true, TextAlignmentOptions.MidlineRight);
        UiKit.TopRight(detailKind.rectTransform, 20f, 18f, 400f, 28f);
        var detailBody = Label(detail, "Body", string.Empty, 16f, HudPalette.TextBody, 20f, 56f, detailWidth - 40f, DetailBodyHeight, false, false, TextAlignmentOptions.TopLeft);

        var goRect = UiKit.BottomRight(UiKit.Rect("Go", detail), 20f, 16f, 280f, 48f);
        var go = UiKit.Button(goRect, "들어가기", UiKit.ButtonKind.Primary, 17f);
        var closeRect = UiKit.BottomRight(UiKit.Rect("Close", detail), 316f, 16f, 120f, 48f);
        var close = UiKit.Button(closeRect, "닫기", UiKit.ButtonKind.Secondary, 16f);

        UiKit.Wire(view, "root", panel.gameObject);
        UiKit.Wire(view, "resources", resources);
        UiKit.WireArray(view, "nodes", nodes);
        UiKit.WireArray(view, "nodeStates", states);
        UiKit.Wire(view, "detailName", detailName);
        UiKit.Wire(view, "detailKind", detailKind);
        UiKit.Wire(view, "detailBody", detailBody);
        UiKit.Wire(view, "closeButton", close);
        UiKit.Wire(view, "goButton", go);
        panel.gameObject.SetActive(false);

        UiKit.EnsureFolder("Assets/03.Prefabs/UI/Run");
        UiKit.ApplyButtonFaces(rootObject);
        PrefabUtility.SaveAsPrefabAsset(rootObject, PrefabPath, out var saved);
        Object.DestroyImmediate(rootObject);
        if (!saved)
            Debug.LogError($"{nameof(RunRouteMapBuilder)}: {PrefabPath} 저장 실패");
    }

    /// <summary>DungeonRun에 놓고 세션 컨트롤러에 잇는다. 이 프리팹으로 만든 옛 인스턴스만 지운다.</summary>
    private static void Place()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.path != ScenePath)
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        foreach (var existing in Object.FindObjectsByType<RunRouteMapView>(FindObjectsInactive.Include))
        {
            var instanceRoot = PrefabUtility.GetOutermostPrefabInstanceRoot(existing.gameObject);
            if (instanceRoot != null && PrefabUtility.GetCorrespondingObjectFromSource(instanceRoot) == prefab)
                Object.DestroyImmediate(instanceRoot);
        }

        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        var session = Object.FindAnyObjectByType<RunSessionController>(FindObjectsInactive.Include);
        if (session == null)
            Debug.LogError($"{nameof(RunRouteMapBuilder)}: DungeonRun에 RunSessionController가 없습니다.");
        else
            UiKit.Wire(session, "routeMap", instance.GetComponent<RunRouteMapView>());

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    /// <summary>칸 하나: 이름은 버튼 글자(위쪽), 상태는 아래 작은 글자. 글자가 둘이라 팩 버튼 면 대신 패널 테두리를 쓴다.</summary>
    private static UiFrameButton BuildNode(RectTransform strip, int index, float x, float y, out TextMeshProUGUI state)
    {
        var rt = UiKit.TopLeft(UiKit.Rect($"Node{index}", strip), x, y, NodeWidth, NodeHeight);
        var node = UiKit.Button(rt, NodeNames[index], UiKit.ButtonKind.Secondary, 18f);
        UiKit.Place(node.Label.rectTransform, new Vector2(0f, 1f), Vector2.one, new Vector2(0.5f, 1f), new Vector2(0f, -10f), new Vector2(-16f, 30f));
        state = UiKit.Text("State", rt, string.Empty, 14f, HudPalette.TextDim, TextAlignmentOptions.Center, false, true);
        UiKit.Place(state.rectTransform, Vector2.zero, new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 10f), new Vector2(-16f, 24f));
        return node;
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
