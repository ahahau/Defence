using Code.UI;
using Code.UI.Run;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 던전 FTL 시연 런의 원정 파견 판(uGUI, 장부형) 프리팹을 만들고 DungeonRun 씬에 놓아 <see cref="RunSessionController"/>에 잇는다.
/// 구역 지도(50) 위, 팝업(100) 아래 캔버스(60, 상인 좌판과 같은 층이고 둘이 함께 뜨지 않는다). Unity CLI: <c>RunRaidSelectBuilder.BuildAll()</c>.
///
/// 카드 1040×484(2026-11-16 사용자 A안 승인): 제목·골라 둔 수, 안내 줄, 부하 카드 다섯 장(이름·그림·체력·사거리·상성·큰 기술),
/// 아래에 파견 명단과 닫기·진입 경로 고르기. 카드 안 그림·본문 자리는 상인 고르기 카드와 같다.
/// </summary>
public static class RunRaidSelectBuilder
{
    public const string PrefabPath = "Assets/03.Prefabs/UI/Run/RunRaidSelect.prefab";
    private const string ScenePath = "Assets/00.Scenes/DungeonRun.unity";
    private const int SortingOrder = 60;
    private const float CardWidth = 1040f;
    private const float CardHeight = 484f;
    private const float Pad = 28f;
    private const float BodyTop = 108f;
    private const int Count = 5;
    private const float Gap = 12f;
    private const float KindHeight = 280f;
    private const float FooterGap = 20f;
    private const float ButtonHeight = 48f;

    [MenuItem("Tools/Dungeon Keeper/Build Run Raid Select")]
    public static void BuildAll()
    {
        BuildPrefab();
        Place();
    }

    public static void BuildPrefab()
    {
        var rootObject = UiKit.Canvas("RunRaidSelect", SortingOrder);
        var view = rootObject.AddComponent<RunRaidSelectView>();

        var panel = UiKit.Fill(UiKit.Rect("Root", rootObject.transform));
        UiKit.Image(panel, null, HudPalette.Scrim, true);

        var card = UiKit.Place(UiKit.Rect("Card", panel), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(CardWidth, CardHeight));
        UiKit.Panel(card, HudPalette.Surface, HudPalette.Border, true);

        float inner = CardWidth - Pad * 2f;
        Label(card, "Title", "원정 · 파견할 부하", 24f, HudPalette.Accent, Pad, 20f, 400f, 36f, true);
        var count = Label(card, "Count", string.Empty, 17f, HudPalette.AccentSoft, 0f, 24f, 360f, 30f, false, true, TextAlignmentOptions.MidlineRight);
        UiKit.TopRight(count.rectTransform, Pad, 24f, 360f, 30f);
        var summary = Label(card, "Summary", string.Empty, 15f, HudPalette.TextDim, Pad, 60f, inner, 26f);
        Divider(card, Pad, 94f, inner);

        float width = (inner - Gap * (Count - 1)) / Count;
        var cards = new Object[Count];
        var bodies = new Object[Count];
        var portraits = new Object[Count];
        for (int i = 0; i < Count; i++)
        {
            var rt = UiKit.TopLeft(UiKit.Rect($"Kind{i}", card), Pad + i * (width + Gap), BodyTop, width, KindHeight);
            var kind = UiKit.Button(rt, string.Empty, UiKit.ButtonKind.Secondary, 18f);
            UiKit.Place(kind.Label.rectTransform, new Vector2(0f, 1f), Vector2.one, new Vector2(0.5f, 1f), new Vector2(0f, -16f), new Vector2(-20f, 32f));
            portraits[i] = RunMerchantBuilder.PickPortrait(rt);
            var body = UiKit.Text("Body", rt, string.Empty, 15f, HudPalette.TextBody, TextAlignmentOptions.TopLeft);
            RunMerchantBuilder.PlacePickBody(body.rectTransform);
            cards[i] = kind;
            bodies[i] = body;
        }

        float footerTop = BodyTop + KindHeight + FooterGap;
        var footer = Label(card, "Footer", string.Empty, 16f, HudPalette.TextBody, Pad, footerTop, inner - 440f, ButtonHeight);
        var confirmRect = UiKit.TopRight(UiKit.Rect("Confirm", card), Pad, footerTop, 260f, ButtonHeight);
        var confirm = UiKit.Button(confirmRect, "진입 경로 고르기", UiKit.ButtonKind.Primary, 17f);
        var closeRect = UiKit.TopRight(UiKit.Rect("Close", card), Pad + 276f, footerTop, 130f, ButtonHeight);
        var close = UiKit.Button(closeRect, "닫기", UiKit.ButtonKind.Secondary, 16f);

        UiKit.Wire(view, "root", panel.gameObject);
        UiKit.Wire(view, "count", count);
        UiKit.Wire(view, "summary", summary);
        UiKit.WireArray(view, "cards", cards);
        UiKit.WireArray(view, "bodies", bodies);
        UiKit.WireArray(view, "portraits", portraits);
        UiKit.Wire(view, "footer", footer);
        UiKit.Wire(view, "closeButton", close);
        UiKit.Wire(view, "confirmButton", confirm);
        panel.gameObject.SetActive(false);

        UiKit.EnsureFolder("Assets/03.Prefabs/UI/Run");
        UiKit.ApplyButtonFaces(rootObject);
        PrefabUtility.SaveAsPrefabAsset(rootObject, PrefabPath, out var saved);
        Object.DestroyImmediate(rootObject);
        if (!saved)
            Debug.LogError($"{nameof(RunRaidSelectBuilder)}: {PrefabPath} 저장 실패");
    }

    /// <summary>DungeonRun에 놓고 세션 컨트롤러·ESC 라우터에 잇는다. 이 프리팹으로 만든 옛 인스턴스만 지운다.</summary>
    public static void Place()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.path != ScenePath)
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        foreach (var existing in Object.FindObjectsByType<RunRaidSelectView>(FindObjectsInactive.Include))
        {
            var instanceRoot = PrefabUtility.GetOutermostPrefabInstanceRoot(existing.gameObject);
            if (instanceRoot != null && PrefabUtility.GetCorrespondingObjectFromSource(instanceRoot) == prefab)
                Object.DestroyImmediate(instanceRoot);
        }

        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        var session = Object.FindAnyObjectByType<RunSessionController>(FindObjectsInactive.Include);
        if (session == null)
            Debug.LogError($"{nameof(RunRaidSelectBuilder)}: DungeonRun에 RunSessionController가 없습니다.");
        else
            UiKit.Wire(session, "raidSelectWindow", instance.GetComponent<RunRaidSelectView>());

        var settings = Object.FindAnyObjectByType<SettingsScreenView>(FindObjectsInactive.Include);
        if (settings != null) SettingsBuilder.WireRunEscape(settings);

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
