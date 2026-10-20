using Code.Manager;
using Code.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 하루 정산표(uGUI, A안) 프리팹을 만들고 SampleScene에 놓는다. 캔버스 84(대화 80 위, 첫날 안내 덮개 85 아래 — 정산을 읽으라는 안내 카드가 보여야 한다).
/// Unity CLI: <c>SettlementBuilder.BuildAll()</c>.
///
/// 덮개 + 카드 1040×740(가운데에서 64 아래, 안내 카드 136~220을 피함): 제목, 탭 다섯(168×38), 칸 하나(992×534, 요약·수입·지출·계약/부채·유닛 상점),
/// 아래 가운데 "정산 완료" 280×46. 칸 크기는 내용과 상관없이 고정이고, 긴 목록은 칸 안에서 스크롤한다.
/// </summary>
public static class SettlementBuilder
{
    public const string PrefabPath = "Assets/03.Prefabs/UI/Settlement/Settlement.prefab";
    public const int ProductSlots = 4;
    public const int ApplicantSlots = 16;
    private const float CardWidth = 1040f;
    private const float CardHeight = 740f;
    private const float CardOffsetDown = 64f;
    private const float PaneX = 24f;
    private const float PaneY = 124f;
    private const float PaneWidth = CardWidth - PaneX * 2f;
    private const float PaneHeight = 534f;

    private static readonly string[] TabLabels = { "이번 주 요약", "수입 상세", "지출 상세", "계약 / 부채", "유닛 상점" };

    [MenuItem("Tools/Dungeon Keeper/Build Settlement")]
    public static void BuildAll()
    {
        BuildPrefab();
        UiSceneInstaller.Place(PrefabPath, typeof(SettlementScreenView), instance =>
        {
            var settlement = Object.FindAnyObjectByType<ManagementSettlementManager>(FindObjectsInactive.Include);
            var cost = Object.FindAnyObjectByType<CostManager>(FindObjectsInactive.Include);
            var roster = Object.FindAnyObjectByType<HiredUnitRoster>(FindObjectsInactive.Include);
            UiKit.Wire(instance.GetComponent<SettlementScreenView>(), "settlement", settlement);
            var loan = instance.GetComponentInChildren<SettlementLoanPane>(true);
            UiKit.Wire(loan, "costManager", cost);
            var shop = instance.GetComponentInChildren<SettlementShopPane>(true);
            UiKit.Wire(shop, "roster", roster);
            UiKit.Wire(shop, "costManager", cost);
            UiKit.Wire(shop, "settlement", settlement);
        });
    }

    public static void BuildPrefab()
    {
        var canvas = UiKit.Canvas("Settlement", 84);
        var view = canvas.AddComponent<SettlementScreenView>();

        var root = UiKit.Fill(UiKit.Rect("Root", canvas.transform));
        UiKit.Image(root, null, HudPalette.Scrim, true);

        var card = UiKit.Place(UiKit.Rect("Card", root), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -CardOffsetDown), new Vector2(CardWidth, CardHeight));
        UiKit.Panel(card, HudPalette.Surface, HudPalette.Border, true);
        var cardGroup = card.gameObject.AddComponent<CanvasGroup>();

        var title = UiKit.Text("Title", card, "1일차 정산", 28f, HudPalette.AccentSoft, TextAlignmentOptions.Center, true, true);
        UiKit.TopLeft(title.rectTransform, PaneX, 18f, PaneWidth, 36f);
        var divider = UiKit.TopLeft(UiKit.Rect("Divider", card), PaneX, 60f, PaneWidth, 2f);
        UiKit.Image(divider, null, HudPalette.BorderQuiet, false);

        var tabs = new Object[TabLabels.Length];
        const float tabWidth = 168f;
        const float tabGap = 8f;
        var tabsLeft = (CardWidth - (tabWidth * TabLabels.Length + tabGap * (TabLabels.Length - 1))) * 0.5f;
        for (var i = 0; i < TabLabels.Length; i++)
        {
            var rt = UiKit.TopLeft(UiKit.Rect($"Tab{i}", card), tabsLeft + i * (tabWidth + tabGap), 74f, tabWidth, 38f);
            tabs[i] = UiKit.Button(rt, TabLabels[i], UiKit.ButtonKind.Secondary, 15f);
        }

        var summary = BuildSummary(card);
        var income = BuildLedger(card, "Income", "획득 금화", true);
        var expense = BuildLedger(card, "Expense", "차감 내역", false);
        var loan = BuildLoan(card);
        var shop = BuildShop(card);

        var closeRect = UiKit.Place(UiKit.Rect("Close", card), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 22f), new Vector2(280f, 46f));
        var close = UiKit.Button(closeRect, "정산 완료", UiKit.ButtonKind.Primary, 18f);

        UiKit.Wire(view, "root", root.gameObject);
        UiKit.Wire(view, "card", card);
        UiKit.Wire(view, "cardGroup", cardGroup);
        UiKit.Wire(view, "title", title);
        UiKit.WireArray(view, "tabs", tabs);
        UiKit.WireArray(view, "panes", new Object[] { summary.gameObject, income.gameObject, expense.gameObject, loan.gameObject, shop.gameObject });
        UiKit.Wire(view, "summary", summary);
        UiKit.Wire(view, "income", income);
        UiKit.Wire(view, "expense", expense);
        UiKit.Wire(view, "loan", loan);
        UiKit.Wire(view, "shop", shop);
        UiKit.Wire(view, "closeButton", close);
        root.gameObject.SetActive(false);

        UiKit.EnsureFolder("Assets/03.Prefabs/UI/Settlement");
        PrefabUtility.SaveAsPrefabAsset(canvas, PrefabPath, out var saved);
        Object.DestroyImmediate(canvas);
        if (!saved)
            Debug.LogError($"{nameof(SettlementBuilder)}: {PrefabPath} 저장 실패");
    }

    private static RectTransform Pane(RectTransform card, string name) =>
        UiKit.TopLeft(UiKit.Rect(name, card), PaneX, PaneY, PaneWidth, PaneHeight);

    private static TextMeshProUGUI Heading(RectTransform parent, string text, float x, float y, float width) =>
        Place(UiKit.Text("Heading", parent, text, 17f, HudPalette.Accent, TextAlignmentOptions.MidlineLeft, true, true), x, y, width, 26f);

    private static TextMeshProUGUI Place(TextMeshProUGUI text, float x, float y, float width, float height)
    {
        UiKit.TopLeft(text.rectTransform, x, y, width, height);
        return text;
    }

    // ── 요약 ──

    private static SettlementSummaryPane BuildSummary(RectTransform card)
    {
        var pane = Pane(card, "Summary");
        var view = pane.gameObject.AddComponent<SettlementSummaryPane>();

        var net = Place(UiKit.Text("Net", pane, "운영 자금 +0G", 30f, HudPalette.Gain, TextAlignmentOptions.Center, true, true), 0f, 0f, PaneWidth, 42f);
        var deferred = Place(UiKit.Text("Deferred", pane, string.Empty, 15f, HudPalette.TextDim, TextAlignmentOptions.Center, false, true), 0f, 44f, PaneWidth, 22f);

        var notesRect = UiKit.TopLeft(UiKit.Rect("Notes", pane), 0f, 74f, PaneWidth, 210f);
        var notesScroll = UiKit.ScrollArea(notesRect, out var notesContent);
        var notes = UiKit.ScrollText(notesContent, 16f, HudPalette.TextBody, TextAlignmentOptions.Top);
        notes.lineSpacing = 6f;

        var line = UiKit.TopLeft(UiKit.Rect("Divider", pane), 0f, 294f, PaneWidth, 2f);
        UiKit.Image(line, null, HudPalette.BorderQuiet, false);

        var headerWidth = (PaneWidth - 12f) * 0.5f;
        var unitHeader = UiKit.Button(UiKit.TopLeft(UiKit.Rect("UnitUnlockHeader", pane), 0f, 306f, headerWidth, 36f), "부하 해금", UiKit.ButtonKind.Secondary, 15f);
        var buildingHeader = UiKit.Button(UiKit.TopLeft(UiKit.Rect("BuildingUnlockHeader", pane), headerWidth + 12f, 306f, headerWidth, 36f), "시설 해금", UiKit.ButtonKind.Secondary, 15f);
        unitHeader.Label.color = HudPalette.AccentSoft;
        buildingHeader.Label.color = HudPalette.AccentSoft;

        var unlockRect = UiKit.TopLeft(UiKit.Rect("UnlockList", pane), 0f, 352f, PaneWidth, PaneHeight - 352f);
        var unlockScroll = UiKit.ScrollArea(unlockRect, out var unlockContent);
        var unlock = UiKit.ScrollText(unlockContent, 15f, HudPalette.TextBody, TextAlignmentOptions.TopLeft);
        unlock.lineSpacing = 4f;

        UiKit.Wire(view, "net", net);
        UiKit.Wire(view, "deferred", deferred);
        UiKit.Wire(view, "notes", notes);
        UiKit.Wire(view, "notesScroll", notesScroll);
        UiKit.Wire(view, "unitUnlockHeader", unitHeader);
        UiKit.Wire(view, "buildingUnlockHeader", buildingHeader);
        UiKit.Wire(view, "unlockList", unlock);
        UiKit.Wire(view, "unlockScroll", unlockScroll);
        return view;
    }

    // ── 수입·지출 ──

    private static SettlementLedgerPane BuildLedger(RectTransform card, string name, string heading, bool isIncome)
    {
        var pane = Pane(card, name);
        var view = pane.gameObject.AddComponent<SettlementLedgerPane>();
        var color = isIncome ? HudPalette.Gain : HudPalette.Loss;

        Heading(pane, heading, 0f, 0f, PaneWidth);
        var listRect = UiKit.TopLeft(UiKit.Rect("List", pane), 0f, 34f, PaneWidth, PaneHeight - 34f - 52f);
        var scroll = UiKit.ScrollArea(listRect, out var content);
        var lines = UiKit.ScrollText(content, 16f, color, TextAlignmentOptions.TopLeft);
        lines.lineSpacing = 6f;

        var rule = UiKit.TopLeft(UiKit.Rect("TotalRule", pane), 0f, PaneHeight - 46f, PaneWidth - 12f, 2f);
        UiKit.Image(rule, null, HudPalette.BorderQuiet, false);
        var total = Place(UiKit.Text("Total", pane, "합계 0G", 18f, color, TextAlignmentOptions.MidlineRight, true, true), 0f, PaneHeight - 40f, PaneWidth - 12f, 30f);

        UiKit.Wire(view, "lines", lines);
        UiKit.Wire(view, "total", total);
        UiKit.Wire(view, "scroll", scroll);
        var so = new SerializedObject(view);
        so.FindProperty("isIncome").boolValue = isIncome;
        so.ApplyModifiedPropertiesWithoutUndo();
        return view;
    }

    // ── 계약 / 부채 ──

    private static SettlementLoanPane BuildLoan(RectTransform card)
    {
        var pane = Pane(card, "Loan");
        var view = pane.gameObject.AddComponent<SettlementLoanPane>();
        const float column = 476f;
        const float rightX = PaneWidth - column;

        Heading(pane, "대출", 0f, 0f, column);
        var debt = Place(UiKit.Text("Debt", pane, "빚 없음", 24f, HudPalette.Loss, TextAlignmentOptions.MidlineLeft, true, true), 0f, 30f, column, 34f);
        var due = Place(UiKit.Text("Due", pane, string.Empty, 15f, HudPalette.TextBody, TextAlignmentOptions.TopLeft), 0f, 68f, column, 42f);
        var terms = Place(UiKit.Text("Terms", pane, string.Empty, 15f, HudPalette.TextDim, TextAlignmentOptions.MidlineLeft, false, true), 0f, 112f, column, 22f);
        var wallet = Place(UiKit.Text("Wallet", pane, string.Empty, 15f, HudPalette.TextBody, TextAlignmentOptions.MidlineLeft, false, true), 0f, 136f, column, 22f);
        var windowRule = UiKit.TopLeft(UiKit.Rect("WindowRule", pane), 0f, 166f, column, 1f);
        UiKit.Image(windowRule, null, HudPalette.BorderQuiet, false);
        var window = Place(UiKit.Text("Window", pane, string.Empty, 15f, HudPalette.Warn, TextAlignmentOptions.TopLeft), 0f, 174f, column, 42f);

        Place(UiKit.Text("ProductsHeading", pane, "대출 상품", 15f, HudPalette.Accent, TextAlignmentOptions.MidlineLeft, true, true), 0f, 226f, column, 24f);
        var products = new Object[ProductSlots];
        for (var i = 0; i < ProductSlots; i++)
        {
            var rt = UiKit.TopLeft(UiKit.Rect($"Product{i}", pane), 0f, 256f + i * 68f, column, 60f);
            var button = UiKit.Button(rt, "상품", UiKit.ButtonKind.Secondary, 16f);
            button.Label.alignment = TextAlignmentOptions.MidlineLeft;
            button.Label.rectTransform.offsetMin = new Vector2(14f, 4f);
            button.Label.rectTransform.offsetMax = new Vector2(-14f, -4f);
            products[i] = button;
        }

        Heading(pane, "대출 실행", rightX, 0f, column);
        var amount = Place(UiKit.Text("BorrowAmount", pane, "빌릴 금액 0G", 20f, HudPalette.Text, TextAlignmentOptions.MidlineLeft, true, true), rightX, 34f, column, 30f);
        var slider = UiKit.Slider(UiKit.TopLeft(UiKit.Rect("BorrowSlider", pane), rightX, 74f, column, 32f));
        var borrow = UiKit.Button(UiKit.TopLeft(UiKit.Rect("Borrow", pane), rightX, 118f, column, 48f), "빌리기", UiKit.ButtonKind.Primary, 17f);
        var feedback = Place(UiKit.Text("Feedback", pane, string.Empty, 15f, HudPalette.Warn, TextAlignmentOptions.TopLeft), rightX, 178f, column, 64f);

        UiKit.Wire(view, "debt", debt);
        UiKit.Wire(view, "due", due);
        UiKit.Wire(view, "terms", terms);
        UiKit.Wire(view, "wallet", wallet);
        UiKit.Wire(view, "window", window);
        UiKit.WireArray(view, "productButtons", products);
        UiKit.Wire(view, "borrowAmount", amount);
        UiKit.Wire(view, "borrowSlider", slider);
        UiKit.Wire(view, "borrowButton", borrow);
        UiKit.Wire(view, "feedback", feedback);
        return view;
    }

    // ── 유닛 상점 ──

    private static SettlementShopPane BuildShop(RectTransform card)
    {
        var pane = Pane(card, "Shop");
        var view = pane.gameObject.AddComponent<SettlementShopPane>();

        Heading(pane, "이번 주 지원자", 0f, 0f, PaneWidth);
        var status = Place(UiKit.Text("Status", pane, string.Empty, 15f, HudPalette.TextDim, TextAlignmentOptions.MidlineLeft, false, true), 0f, 30f, PaneWidth, 22f);

        var listRect = UiKit.TopLeft(UiKit.Rect("List", pane), 0f, 60f, PaneWidth, PaneHeight - 60f - 62f);
        var scroll = UiKit.ScrollArea(listRect, out var content);
        var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 8f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        var slots = new Object[ApplicantSlots];
        var details = new Object[ApplicantSlots];
        for (var i = 0; i < ApplicantSlots; i++)
        {
            var rt = UiKit.Rect($"Applicant{i}", content);
            rt.gameObject.AddComponent<LayoutElement>().preferredHeight = 60f;
            var button = UiKit.Button(rt, "지원자", UiKit.ButtonKind.Secondary, 16f);
            var label = button.Label.rectTransform;
            button.Label.alignment = TextAlignmentOptions.MidlineLeft;
            label.anchorMin = new Vector2(0f, 0.48f);
            label.anchorMax = Vector2.one;
            label.offsetMin = new Vector2(14f, 0f);
            label.offsetMax = new Vector2(-14f, -4f);

            var detail = UiKit.Text("Detail", rt, string.Empty, 14f, HudPalette.TextDim, TextAlignmentOptions.MidlineLeft, false, true);
            detail.rectTransform.anchorMin = Vector2.zero;
            detail.rectTransform.anchorMax = new Vector2(1f, 0.48f);
            detail.rectTransform.offsetMin = new Vector2(14f, 4f);
            detail.rectTransform.offsetMax = new Vector2(-14f, 0f);

            slots[i] = button;
            details[i] = detail;
            rt.gameObject.SetActive(false);
        }

        var empty = Place(UiKit.Text("Empty", pane, "이번 주에는 새 지원자가 없습니다.", 16f, HudPalette.TextMuted, TextAlignmentOptions.TopLeft, false, true), 0f, 62f, PaneWidth, 24f);
        var feedback = Place(UiKit.Text("Feedback", pane, string.Empty, 15f, HudPalette.Warn, TextAlignmentOptions.TopLeft), 0f, PaneHeight - 54f, PaneWidth, 50f);

        UiKit.Wire(view, "status", status);
        UiKit.Wire(view, "empty", empty);
        UiKit.WireArray(view, "slots", slots);
        UiKit.WireArray(view, "details", details);
        UiKit.Wire(view, "feedback", feedback);
        UiKit.Wire(view, "scroll", scroll);
        return view;
    }
}
