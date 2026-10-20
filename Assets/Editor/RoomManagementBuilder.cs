using Code.Manager;
using Code.UI;
using Code.UI.RoomManagement;
using TMPro;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 방 관리 화면(uGUI, A안) 프리팹을 만들고 SampleScene에 놓는다. 캔버스 20(HUD 10 위, 대화 80·튜토리얼 덮개 85 아래).
/// Unity CLI: <c>RoomManagementBuilder.BuildAll()</c>.
///
/// 왼쪽 아래 행동 도크 760×132(관리 안내 · 건설·유닛 배치·함정·시설 | 상인·정산 정보·메뉴 · 보유 유닛·부활 대기·목록),
/// 도크 위 설치 창 760×360(접히면 60)·보유 목록 760×140, 오른쪽 열 방 상세 400×640(위에서 104, 열린 동안 브리핑을 비켜 둠).
/// </summary>
public static class RoomManagementBuilder
{
    public const string PrefabPath = "Assets/03.Prefabs/UI/RoomManagement/RoomManagement.prefab";
    private const float Margin = 24f;
    private const float DockWidth = 760f;
    private const float DockHeight = 132f;
    private const float AboveDock = Margin + DockHeight + 8f;
    private const float DetailWidth = 400f;
    private const float DetailHeight = 640f;
    private const float InstallHeight = 360f;

    private static readonly string[] KindLabels = { "건설", "유닛 배치", "함정", "시설" };

    [MenuItem("Tools/Dungeon Keeper/Build Room Management")]
    public static void BuildAll()
    {
        BuildPrefab();
        var instance = UiSceneInstaller.Place(PrefabPath, typeof(RoomManagementScreen), Wire);
        var tutorial = Object.FindAnyObjectByType<PlayTutorialView>(FindObjectsInactive.Include);
        if (tutorial != null)
        {
            UiKit.Wire(tutorial, "roomScreen", instance.GetComponent<RoomManagementScreen>());
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(tutorial.gameObject.scene);
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(tutorial.gameObject.scene);
        }
    }

    private static void Wire(GameObject instance)
    {
        var screen = instance.GetComponent<RoomManagementScreen>();
        UiKit.Wire(screen, "panel", Object.FindAnyObjectByType<NodePanelView>(FindObjectsInactive.Include));
        UiKit.Wire(screen, "waveHud", Object.FindAnyObjectByType<WaveHudView>(FindObjectsInactive.Include));
        UiKit.Wire(screen, "recordBoard", Object.FindAnyObjectByType<RecordBoardView>(FindObjectsInactive.Include));
        UiKit.Wire(screen, "dayManager", Object.FindAnyObjectByType<DayManager>(FindObjectsInactive.Include));
        UiKit.Wire(screen, "speedController", Object.FindAnyObjectByType<GameSpeedController>(FindObjectsInactive.Include));
        UiKit.Wire(screen, "roster", Object.FindAnyObjectByType<HiredUnitRoster>(FindObjectsInactive.Include));
        UiKit.Wire(screen, "merchant", Object.FindAnyObjectByType<MerchantPanelView>(FindObjectsInactive.Include));
        var scope = Object.FindAnyObjectByType<GameScope>(FindObjectsInactive.Include);
        if (scope != null)
            UiKit.Wire(screen, "scope", scope);
    }

    public static void BuildPrefab()
    {
        var canvas = UiKit.Canvas("RoomManagement", 20);
        var screen = canvas.AddComponent<RoomManagementScreen>();
        var root = (RectTransform)canvas.transform;

        var dock = BuildDock(root);
        var install = BuildInstall(root);
        var detail = BuildDetail(root);

        UiKit.Wire(screen, "dock", dock);
        UiKit.Wire(screen, "install", install);
        UiKit.Wire(screen, "detail", detail);

        UiKit.EnsureFolder("Assets/03.Prefabs/UI/RoomManagement");
        PrefabUtility.SaveAsPrefabAsset(canvas, PrefabPath, out var saved);
        Object.DestroyImmediate(canvas);
        if (!saved)
            Debug.LogError($"{nameof(RoomManagementBuilder)}: {PrefabPath} 저장 실패");
    }

    // ── 조각 ──

    private static RectTransform BottomLeft(RectTransform rt, float x, float y, float width, float height) =>
        UiKit.Place(rt, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(x, y), new Vector2(width, height));

    private static TextMeshProUGUI Label(RectTransform parent, string name, string value, float size, Color color,
        float x, float y, float width, float height, bool bold = false, bool singleLine = true,
        TextAlignmentOptions alignment = TextAlignmentOptions.MidlineLeft)
    {
        var text = UiKit.Text(name, parent, value, size, color, alignment, bold, singleLine);
        UiKit.TopLeft(text.rectTransform, x, y, width, height);
        return text;
    }

    /// <summary>자리를 지킨 채 숨길 수 있는 버튼(CanvasGroup 포함).</summary>
    private static UiFrameButton Button(RectTransform parent, string name, string label, UiKit.ButtonKind kind,
        float x, float y, float width, float height, float fontSize = 15f, bool right = false)
    {
        var rt = UiKit.Rect(name, parent);
        if (right)
            UiKit.TopRight(rt, x, y, width, height);
        else
            UiKit.TopLeft(rt, x, y, width, height);
        var button = UiKit.Button(rt, label, kind, fontSize);
        UiKit.Wire(button, "group", rt.gameObject.AddComponent<CanvasGroup>());
        return button;
    }

    private static void Divider(RectTransform parent, float x, float y, float width, float height = 1f)
    {
        var line = UiKit.TopLeft(UiKit.Rect("Divider", parent), x, y, width, height);
        UiKit.Image(line, null, HudPalette.BorderQuiet, false);
    }

    // ── 왼쪽 아래 도크 ──

    private static ActionDockView BuildDock(RectTransform root)
    {
        var dock = BottomLeft(UiKit.Rect("Dock", root), Margin, Margin, DockWidth, DockHeight);
        UiKit.Panel(dock, HudPalette.Surface, HudPalette.Border, true);
        var view = dock.gameObject.AddComponent<ActionDockView>();

        var management = Label(dock, "Management", "관리 중", 15f, HudPalette.TextDim, 16f, 8f, DockWidth - 32f, 24f);

        var kinds = new Object[KindLabels.Length];
        for (var i = 0; i < KindLabels.Length; i++)
            kinds[i] = Button(dock, $"Kind{i}", KindLabels[i], UiKit.ButtonKind.Secondary, 14f + i * 102f, 38f, 96f, 48f, 16f);

        Divider(dock, 430f, 40f, 2f, 44f);
        var merchant = Button(dock, "Merchant", "상인", UiKit.ButtonKind.Secondary, 446f, 38f, 96f, 48f, 16f);
        var settlement = Button(dock, "SettlementInfo", "정산 정보", UiKit.ButtonKind.Secondary, 548f, 38f, 96f, 48f, 16f);
        var menu = Button(dock, "Menu", "메뉴", UiKit.ButtonKind.Secondary, 650f, 38f, 96f, 48f, 16f);

        var roster = Label(dock, "Roster", "보유 0명", 14f, HudPalette.TextBody, 16f, 96f, 330f, 26f);
        var revival = Label(dock, "Revival", "부활 대기열 없음", 14f, HudPalette.Warn, 352f, 96f, 280f, 26f);
        var rosterToggle = Button(dock, "RosterToggle", "목록 열기", UiKit.ButtonKind.Secondary, 14f, 96f, 100f, 28f, 13f, true);

        // 보유 목록: 도크 바로 위(설치 창과 같은 자리).
        var rosterPanel = BottomLeft(UiKit.Rect("RosterPanel", root), Margin, AboveDock, DockWidth, 140f);
        UiKit.Panel(rosterPanel, HudPalette.Surface, HudPalette.Border, true);
        var listRect = UiKit.Fill(UiKit.Rect("List", rosterPanel), 12f);
        UiKit.ScrollArea(listRect, out var content);
        var rosterList = UiKit.ScrollText(content, 14f, HudPalette.TextBody, TextAlignmentOptions.TopLeft);
        rosterList.lineSpacing = 4f;

        // 잠깐 띄우는 안내 줄: 도크 바로 위.
        var messageRoot = BottomLeft(UiKit.Rect("Message", root), Margin, AboveDock, DockWidth, 34f);
        UiKit.Panel(messageRoot, HudPalette.AlertChip, HudPalette.Border, false);
        var message = UiKit.Text("Text", messageRoot, string.Empty, 15f, HudPalette.Warn, TextAlignmentOptions.MidlineLeft, false, true);
        UiKit.Fill(message.rectTransform);
        message.rectTransform.offsetMin = new Vector2(14f, 0f);
        message.rectTransform.offsetMax = new Vector2(-14f, 0f);

        UiKit.Wire(view, "managementLabel", management);
        UiKit.WireArray(view, "kindButtons", kinds);
        UiKit.Wire(view, "merchantButton", merchant);
        UiKit.Wire(view, "settlementButton", settlement);
        UiKit.Wire(view, "menuButton", menu);
        UiKit.Wire(view, "rosterSummary", roster);
        UiKit.Wire(view, "revivalQueue", revival);
        UiKit.Wire(view, "rosterToggle", rosterToggle);
        UiKit.Wire(view, "rosterPanel", rosterPanel.gameObject);
        UiKit.Wire(view, "rosterList", rosterList);
        UiKit.Wire(view, "message", message);
        UiKit.Wire(view, "messageRoot", messageRoot.gameObject);
        return view;
    }

    // ── 설치 창 ──

    private static InstallPanel BuildInstall(RectTransform root)
    {
        var host = UiKit.Fill(UiKit.Rect("Install", root));
        var view = host.gameObject.AddComponent<InstallPanel>();
        var card = BottomLeft(UiKit.Rect("Card", host), Margin, AboveDock, DockWidth, InstallHeight);
        UiKit.Panel(card, HudPalette.Surface, HudPalette.Border, true);

        var title = Label(card, "Title", "건설", 20f, HudPalette.Text, 14f, 12f, 120f, 36f, true);
        var subtitle = Label(card, "Subtitle", string.Empty, 14f, HudPalette.TextDim, 140f, 12f, 360f, 36f);
        var close = Button(card, "Close", "닫기", UiKit.ButtonKind.Secondary, 14f, 16f, 64f, 28f, 14f, true);
        var next = Button(card, "Next", "다음", UiKit.ButtonKind.Secondary, 14f + 64f + 12f, 16f, 52f, 28f, 14f, true);
        var page = UiKit.Text("Page", card, "1 / 1", 14f, HudPalette.TextDim, TextAlignmentOptions.Center, false, true);
        UiKit.TopRight(page.rectTransform, 14f + 64f + 12f + 52f, 16f, 56f, 28f);
        var prev = Button(card, "Prev", "이전", UiKit.ButtonKind.Secondary, 14f + 64f + 12f + 52f + 56f, 16f, 52f, 28f, 14f, true);

        var body = UiKit.TopLeft(UiKit.Rect("Body", card), 0f, 56f, DockWidth, InstallHeight - 56f);
        var cards = new Object[InstallPanelPresenter.CardsPerPage];
        for (var i = 0; i < cards.Length; i++)
            cards[i] = BuildCard(body, i);

        var empty = Label(body, "Empty", "이 갈래에 고를 것이 없습니다.", 15f, HudPalette.TextDim, 14f, 0f, DockWidth - 28f, 150f, false, true, TextAlignmentOptions.Center);
        Divider(body, 14f, 158f, DockWidth - 28f);
        var detailName = Label(body, "DetailName", string.Empty, 16f, HudPalette.Text, 14f, 166f, 560f, 24f, true);
        var detailSummary = Label(body, "DetailSummary", string.Empty, 14f, HudPalette.TextBody, 14f, 192f, 560f, 20f);
        var detailEffect = Label(body, "DetailEffect", string.Empty, 13f, HudPalette.TextDim, 14f, 214f, 560f, 54f, false, false, TextAlignmentOptions.TopLeft);
        var detailReason = Label(body, "DetailReason", string.Empty, 14f, HudPalette.Loss, 14f, 272f, 560f, 20f);
        var confirm = Button(body, "Confirm", "설치", UiKit.ButtonKind.Primary, 14f, 230f, 150f, 48f, 17f, true);

        UiKit.Wire(view, "root", host.gameObject);
        UiKit.Wire(view, "card", card);
        UiKit.Wire(view, "body", body.gameObject);
        UiKit.Wire(view, "title", title);
        UiKit.Wire(view, "subtitle", subtitle);
        UiKit.Wire(view, "prevButton", prev);
        UiKit.Wire(view, "pageLabel", page);
        UiKit.Wire(view, "nextButton", next);
        UiKit.Wire(view, "closeButton", close);
        UiKit.WireArray(view, "cards", cards);
        UiKit.Wire(view, "empty", empty);
        UiKit.Wire(view, "detailName", detailName);
        UiKit.Wire(view, "detailSummary", detailSummary);
        UiKit.Wire(view, "detailEffect", detailEffect);
        UiKit.Wire(view, "detailReason", detailReason);
        UiKit.Wire(view, "confirmButton", confirm);
        host.gameObject.SetActive(false);
        return view;
    }

    private static InstallCardSlot BuildCard(RectTransform body, int index)
    {
        var rt = UiKit.TopLeft(UiKit.Rect($"Card{index}", body), 14f + index * 148f, 0f, 140f, 150f);
        var frame = UiKit.Button(rt, string.Empty, UiKit.ButtonKind.Secondary, 14f);
        var group = rt.gameObject.AddComponent<CanvasGroup>();

        var iconRect = UiKit.Place(UiKit.Rect("Icon", rt), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -8f), new Vector2(64f, 64f));
        var icon = UiKit.Image(iconRect, null, Color.white, false);
        icon.type = UnityEngine.UI.Image.Type.Simple;
        icon.preserveAspect = true;

        var cardName = Label(rt, "Name", "이름", 14f, HudPalette.Text, 6f, 76f, 128f, 20f, true, true, TextAlignmentOptions.Center);
        var price = Label(rt, "Price", "0G", 13f, HudPalette.Accent, 6f, 98f, 128f, 18f, false, true, TextAlignmentOptions.Center);
        var note = Label(rt, "Note", string.Empty, 12f, HudPalette.TextDim, 6f, 120f, 128f, 18f, false, true, TextAlignmentOptions.Center);

        var slot = rt.gameObject.AddComponent<InstallCardSlot>();
        UiKit.Wire(slot, "frame", frame);
        UiKit.Wire(slot, "group", group);
        UiKit.Wire(slot, "icon", icon);
        UiKit.Wire(slot, "cardName", cardName);
        UiKit.Wire(slot, "price", price);
        UiKit.Wire(slot, "note", note);
        return slot;
    }

    // ── 오른쪽 방 상세 ──

    private static RoomDetailPanel BuildDetail(RectTransform root)
    {
        var host = UiKit.Fill(UiKit.Rect("Detail", root));
        var view = host.gameObject.AddComponent<RoomDetailPanel>();
        var card = UiKit.TopRight(UiKit.Rect("Card", host), Margin, 104f, DetailWidth, DetailHeight);
        UiKit.Panel(card, HudPalette.Surface, HudPalette.Border, true);
        const float inner = DetailWidth - 28f;

        var title = Label(card, "Title", "통로 · 중형", 19f, HudPalette.Text, 14f, 12f, 290f, 32f, true);
        var close = Button(card, "Close", "닫기", UiKit.ButtonKind.Secondary, 14f, 14f, 64f, 28f, 14f, true);
        var state = Label(card, "State", "빈 방", 14f, HudPalette.TextDim, 14f, 46f, inner, 20f);
        Divider(card, 14f, 70f, inner);

        Label(card, "FacilityHeading", "시설", 14f, HudPalette.AccentSoft, 14f, 76f, inner, 20f, true);
        var facilityName = Label(card, "FacilityName", "중앙 시설 없음", 15f, HudPalette.TextBody, 14f, 98f, inner, 20f);
        var barRect = UiKit.TopLeft(UiKit.Rect("FacilityBar", card), 14f, 122f, inner, 8f);
        var barFill = UiKit.Bar(barRect, HudPalette.Track, HudPalette.Gain);
        var facilityStatus = Label(card, "FacilityStatus", string.Empty, 14f, HudPalette.TextBody, 14f, 134f, inner, 20f);
        var facilityUsage = Label(card, "FacilityUsage", string.Empty, 13f, HudPalette.TextDim, 14f, 156f, inner, 20f);
        var repair = Button(card, "Repair", "수리", UiKit.ButtonKind.Secondary, 14f, 180f, 118f, 32f, 14f);
        var closure = Button(card, "Closure", "휴업", UiKit.ButtonKind.Secondary, 141f, 180f, 118f, 32f, 14f);
        var demolish = Button(card, "Demolish", "철거", UiKit.ButtonKind.Danger, 268f, 180f, 118f, 32f, 14f);
        Divider(card, 14f, 220f, inner);

        var unitCount = Label(card, "UnitCount", "유닛 0 / 0", 14f, HudPalette.AccentSoft, 14f, 228f, 280f, 28f, true);
        var deploy = Button(card, "Deploy", "배치", UiKit.ButtonKind.Primary, 14f, 228f, 84f, 28f, 14f, true);
        var rows = new Object[RoomDetailPresenter.MaxUnitRows];
        for (var i = 0; i < rows.Length; i++)
        {
            var row = Button(card, $"Unit{i}", string.Empty, UiKit.ButtonKind.Secondary, 14f, 260f + i * 30f, inner, 28f, 13f);
            row.Label.alignment = TextAlignmentOptions.MidlineLeft;
            row.Label.rectTransform.offsetMin = new Vector2(10f, 0f);
            row.Label.rectTransform.offsetMax = new Vector2(-10f, 0f);
            rows[i] = row;
        }

        var commands = new Object[NodePanelView.CommandOrder.Count];
        for (var i = 0; i < commands.Length; i++)
            commands[i] = Button(card, $"Command{i}", string.Empty, UiKit.ButtonKind.Secondary, 14f + i * 94f, 384f, 88f, 30f, 13f);
        var move = Button(card, "Move", "이동", UiKit.ButtonKind.Secondary, 14f, 420f, 120f, 30f, 14f);
        var recall = Button(card, "Recall", "회수", UiKit.ButtonKind.Secondary, 142f, 420f, 120f, 30f, 14f);
        Divider(card, 14f, 458f, inner);

        Label(card, "InstallHeading", "설치물", 14f, HudPalette.AccentSoft, 14f, 464f, inner, 20f, true);
        var installations = Label(card, "Installations", "없음", 14f, HudPalette.TextBody, 14f, 486f, inner, 20f);
        var rumor = Label(card, "Rumor", "소문 없음", 13f, HudPalette.TextBody, 14f, 508f, inner, 20f);
        var hint = Label(card, "Hint", string.Empty, 13f, HudPalette.Warn, 14f, 536f, inner, 90f, false, false, TextAlignmentOptions.BottomLeft);

        UiKit.Wire(view, "root", host.gameObject);
        UiKit.Wire(view, "title", title);
        UiKit.Wire(view, "state", state);
        UiKit.Wire(view, "closeButton", close);
        UiKit.Wire(view, "facilityName", facilityName);
        UiKit.Wire(view, "facilityBar", barRect.gameObject);
        UiKit.Wire(view, "facilityBarFill", barFill);
        UiKit.Wire(view, "facilityStatus", facilityStatus);
        UiKit.Wire(view, "facilityUsage", facilityUsage);
        UiKit.Wire(view, "repairButton", repair);
        UiKit.Wire(view, "closureButton", closure);
        UiKit.Wire(view, "demolishButton", demolish);
        UiKit.Wire(view, "unitCount", unitCount);
        UiKit.Wire(view, "deployButton", deploy);
        UiKit.WireArray(view, "unitRows", rows);
        UiKit.WireArray(view, "commandButtons", commands);
        UiKit.Wire(view, "moveButton", move);
        UiKit.Wire(view, "recallButton", recall);
        UiKit.Wire(view, "installations", installations);
        UiKit.Wire(view, "rumor", rumor);
        UiKit.Wire(view, "hint", hint);
        host.gameObject.SetActive(false);
        return view;
    }
}
