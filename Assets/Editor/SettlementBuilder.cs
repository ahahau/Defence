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
/// 덮개 + 카드 1040×740(가운데에서 64 아래, 안내 카드 136~220을 피함): 왼쪽 제목·오른쪽 빚/청산일, 탭 셋(168×38),
/// 칸 하나(992×534, 오늘 정산·계약/부채·유닛 상점), 오른쪽 아래 "정산 완료" 240×46.
/// 오늘 정산(2026-10-27 한 화면 장부): 위 띠(수입·지출·이미 반영·남은 운영 자금) 72, 왼쪽 장부 560×440, 오른쪽 전과 칩 6자리,
/// 맨 아래 줄(정산 완료와 같은 높이)에 해금 한 줄과 "목록". 칸 크기는 내용과 상관없이 고정이고, 긴 목록은 칸 안에서 스크롤한다.
/// </summary>
public static class SettlementBuilder
{
    public const string PrefabPath = "Assets/03.Prefabs/UI/Settlement/Settlement.prefab";
    public const int ProductSlots = 4;
    public const int ApplicantSlots = 16;
    private const float ApplicantIconSize = 44f;
    private const float ApplicantIconInset = 10f;
    private const float CardWidth = 1040f;
    private const float CardHeight = 740f;
    private const float CardOffsetDown = 64f;
    private const float PaneX = 24f;
    private const float PaneY = 124f;
    private const float PaneWidth = CardWidth - PaneX * 2f;
    private const float PaneHeight = 534f;

    private const float LedgerWidth = 560f;
    private const float BodyTop = 86f;
    private const float BodyHeight = 440f;
    private const float FooterY = 552f;

    private static readonly string[] TabLabels = { "오늘 정산", "계약 / 부채", "유닛 상점" };

    [MenuItem("Tools/Dungeon Keeper/Build Settlement")]
    public static void BuildAll()
    {
        BuildPrefab();
        UiSceneInstaller.Place(PrefabPath, typeof(SettlementScreenView), instance =>
        {
            var settlement = Object.FindAnyObjectByType<ManagementSettlementManager>(FindObjectsInactive.Include);
            var cost = Object.FindAnyObjectByType<CostManager>(FindObjectsInactive.Include);
            var roster = Object.FindAnyObjectByType<HiredUnitRoster>(FindObjectsInactive.Include);
            var screen = instance.GetComponent<SettlementScreenView>();
            UiKit.Wire(screen, "settlement", settlement);
            UiKit.Wire(screen, "scope", Object.FindAnyObjectByType<GameScope>(FindObjectsInactive.Include));
            UiKit.Wire(screen, "dayManager", Object.FindAnyObjectByType<DayManager>(FindObjectsInactive.Include));
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

        var title = UiKit.Text("Title", card, "1일차 정산", 28f, HudPalette.AccentSoft, TextAlignmentOptions.MidlineLeft, true, true);
        UiKit.TopLeft(title.rectTransform, PaneX, 18f, 480f, 36f);
        var status = UiKit.Text("Status", card, "빚 없음 · 청산일까지 7일", 17f, HudPalette.TextDim, TextAlignmentOptions.MidlineRight, false, true);
        UiKit.TopLeft(status.rectTransform, CardWidth - PaneX - 480f, 22f, 480f, 30f);
        var divider = UiKit.TopLeft(UiKit.Rect("Divider", card), PaneX, 60f, PaneWidth, 2f);
        UiKit.Image(divider, null, HudPalette.BorderQuiet, false);

        var tabs = new Object[TabLabels.Length];
        const float tabWidth = 168f;
        const float tabGap = 8f;
        for (var i = 0; i < TabLabels.Length; i++)
        {
            var rt = UiKit.TopLeft(UiKit.Rect($"Tab{i}", card), PaneX + i * (tabWidth + tabGap), 74f, tabWidth, 38f);
            tabs[i] = UiKit.Button(rt, TabLabels[i], UiKit.ButtonKind.Secondary, 15f);
        }

        var summary = BuildSummary(card);
        var loan = BuildLoan(card);
        var shop = BuildShop(card);

        var closeRect = UiKit.Place(UiKit.Rect("Close", card), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-PaneX, 22f), new Vector2(240f, 46f));
        var close = UiKit.Button(closeRect, "정산 완료", UiKit.ButtonKind.Primary, 18f);

        UiKit.Wire(view, "root", root.gameObject);
        UiKit.Wire(view, "card", card);
        UiKit.Wire(view, "cardGroup", cardGroup);
        UiKit.Wire(view, "title", title);
        UiKit.Wire(view, "status", status);
        UiKit.WireArray(view, "tabs", tabs);
        UiKit.WireArray(view, "panes", new Object[] { summary.gameObject, loan.gameObject, shop.gameObject });
        UiKit.Wire(view, "summary", summary);
        UiKit.Wire(view, "loan", loan);
        UiKit.Wire(view, "shop", shop);
        UiKit.Wire(view, "closeButton", close);
        root.gameObject.SetActive(false);

        UiKit.EnsureFolder("Assets/03.Prefabs/UI/Settlement");
        UiKit.ApplyButtonFaces(canvas);
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

    // ── 오늘 정산 ──

    private static SettlementSummaryPane BuildSummary(RectTransform card)
    {
        var pane = Pane(card, "Summary");
        var view = pane.gameObject.AddComponent<SettlementSummaryPane>();

        // 위 띠: 수입 → 지출 → 이미 반영 → 남은 운영 자금. 칸 폭은 금액 길이와 상관없이 고정이다.
        var band = UiKit.TopLeft(UiKit.Rect("FlowBand", pane), 0f, 0f, PaneWidth, 72f);
        UiKit.Image(band, UiKit.FillSprite, HudPalette.Deep, false);
        // 칸은 가운데 정렬이고 화살표는 칸 사이 경계에 둔다(금액에 붙어 "→-7G"처럼 읽히지 않게).
        var income = FlowCell(band, "Income", "수입", 0f, 200f, HudPalette.Gain);
        Arrow(band, "ArrowIncome", 200f);
        var expense = FlowCell(band, "Expense", "지출", 220f, 200f, HudPalette.Loss);
        Arrow(band, "ArrowExpense", 420f);
        var deferred = FlowCell(band, "Deferred", "낮에 이미 반영", 440f, 200f, HudPalette.TextBody);
        Arrow(band, "ArrowDeferred", 640f);
        Place(UiKit.Text("RemainingLabel", band, "남은 운영 자금", 13f, HudPalette.TextMuted, TextAlignmentOptions.Center, false, true), 660f, 8f, 332f, 18f);
        var remaining = Place(UiKit.Text("Remaining", band, "0G", 26f, HudPalette.Accent, TextAlignmentOptions.Center, true, true), 660f, 28f, 332f, 38f);

        // 왼쪽 장부: 수입·지출을 한 장부로. 줄이 많으면 칸 안에서 스크롤한다.
        var ledgerBox = UiKit.TopLeft(UiKit.Rect("Ledger", pane), 0f, BodyTop, LedgerWidth, BodyHeight);
        UiKit.Image(ledgerBox, UiKit.FillSprite, HudPalette.SurfaceRaised, false);
        var ledgerRect = UiKit.TopLeft(UiKit.Rect("List", ledgerBox), 14f, 10f, LedgerWidth - 20f, BodyHeight - 20f);
        var ledgerScroll = UiKit.ScrollArea(ledgerRect, out var ledgerContent);
        var ledger = UiKit.ScrollText(ledgerContent, 16f, HudPalette.TextBody, TextAlignmentOptions.TopLeft);
        ledger.lineSpacing = 4f;

        // 오른쪽 전과 칩 6자리. 빈 자리는 바탕만 끄고 영역은 그대로 둔다.
        var chipsX = LedgerWidth + 20f;
        var chipsWidth = PaneWidth - chipsX;
        Heading(pane, "오늘 전과", chipsX, BodyTop, chipsWidth);
        var chipBackgrounds = new Object[ChipSlots];
        var chipTexts = new Object[ChipSlots];
        for (var i = 0; i < ChipSlots; i++)
        {
            var chip = UiKit.TopLeft(UiKit.Rect($"Chip{i}", pane), chipsX, BodyTop + 30f + i * 68f, chipsWidth, 58f);
            chipBackgrounds[i] = UiKit.Image(chip, UiKit.FillSprite, HudPalette.Chip, false);
            var text = UiKit.Text("Text", chip, string.Empty, 15f, HudPalette.TextBody, TextAlignmentOptions.MidlineLeft);
            UiKit.Fill(text.rectTransform);
            text.rectTransform.offsetMin = new Vector2(14f, 4f);
            text.rectTransform.offsetMax = new Vector2(-14f, -4f);
            chipTexts[i] = text;
        }

        // 해금 목록: 펼치면 장부·칩 위를 덮는다. 기본은 접힘.
        var unlockPanel = UiKit.TopLeft(UiKit.Rect("UnlockPanel", pane), 0f, BodyTop, PaneWidth, BodyHeight);
        // 바탕을 불투명하게 칠해 아래 장부·칩 글자가 비치지 않게 한다.
        var unlockFill = HudPalette.Deep;
        unlockFill.a = 1f;
        UiKit.Panel(unlockPanel, unlockFill, HudPalette.BorderQuiet, true);
        var unlockRect = UiKit.TopLeft(UiKit.Rect("List", unlockPanel), 18f, 14f, PaneWidth - 30f, BodyHeight - 28f);
        var unlockScroll = UiKit.ScrollArea(unlockRect, out var unlockContent);
        var unlock = UiKit.ScrollText(unlockContent, 15f, HudPalette.TextBody, TextAlignmentOptions.TopLeft);
        unlock.lineSpacing = 4f;
        unlockPanel.gameObject.SetActive(false);

        // 맨 아래 줄은 "정산 완료"와 같은 높이에 둔다.
        var unlockSummary = Place(UiKit.Text("UnlockSummary", pane, string.Empty, 15f, HudPalette.TextDim, TextAlignmentOptions.MidlineLeft, false, true),
            0f, FooterY, 560f, 40f);
        var unlockToggle = UiKit.Button(UiKit.TopLeft(UiKit.Rect("UnlockToggle", pane), 572f, FooterY + 2f, 132f, 36f), "목록", UiKit.ButtonKind.Secondary, 15f);

        UiKit.Wire(view, "incomeTotal", income);
        UiKit.Wire(view, "expenseTotal", expense);
        UiKit.Wire(view, "deferred", deferred);
        UiKit.Wire(view, "remaining", remaining);
        UiKit.Wire(view, "ledger", ledger);
        UiKit.Wire(view, "ledgerScroll", ledgerScroll);
        UiKit.WireArray(view, "chipBackgrounds", chipBackgrounds);
        UiKit.WireArray(view, "chipTexts", chipTexts);
        UiKit.Wire(view, "unlockSummary", unlockSummary);
        UiKit.Wire(view, "unlockToggle", unlockToggle);
        UiKit.Wire(view, "unlockPanel", unlockPanel.gameObject);
        UiKit.Wire(view, "unlockList", unlock);
        UiKit.Wire(view, "unlockScroll", unlockScroll);
        return view;
    }

    private const int ChipSlots = Code.UI.ViewModels.SettlementOverviewPresenter.MaxChips;

    private static TextMeshProUGUI FlowCell(RectTransform band, string name, string label, float x, float width, Color valueColor)
    {
        Place(UiKit.Text(name + "Label", band, label, 13f, HudPalette.TextMuted, TextAlignmentOptions.Center, false, true), x, 8f, width, 18f);
        return Place(UiKit.Text(name, band, "0G", 22f, valueColor, TextAlignmentOptions.Center, true, true), x, 28f, width, 34f);
    }

    private static void Arrow(RectTransform band, string name, float x) =>
        Place(UiKit.Text(name, band, "→", 18f, HudPalette.TextMuted, TextAlignmentOptions.Center, false, true), x, 26f, 20f, 34f);

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

    private static SettlementShopPane BuildShop(RectTransform card) =>
        BuildShopPane(Pane(card, "Shop"), PaneWidth, PaneHeight);

    /// <summary>지원자 목록 칸을 <paramref name="pane"/>에 채운다. 정산표와 고용 창(<c>RecruitBuilder</c>)이 함께 쓴다.</summary>
    public static SettlementShopPane BuildShopPane(RectTransform pane, float paneWidth, float paneHeight)
    {
        var view = pane.gameObject.AddComponent<SettlementShopPane>();

        Heading(pane, "이번 주 지원자", 0f, 0f, paneWidth);
        var status = Place(UiKit.Text("Status", pane, string.Empty, 15f, HudPalette.TextDim, TextAlignmentOptions.MidlineLeft, false, true), 0f, 30f, paneWidth, 22f);

        var listRect = UiKit.TopLeft(UiKit.Rect("List", pane), 0f, 60f, paneWidth, paneHeight - 60f - 62f);
        var scroll = UiKit.ScrollArea(listRect, out var content);
        var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 8f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        var slots = new Object[ApplicantSlots];
        var details = new Object[ApplicantSlots];
        var icons = new Object[ApplicantSlots];
        for (var i = 0; i < ApplicantSlots; i++)
        {
            var rt = UiKit.Rect($"Applicant{i}", content);
            rt.gameObject.AddComponent<LayoutElement>().preferredHeight = 60f;
            var button = UiKit.Button(rt, "지원자", UiKit.ButtonKind.Secondary, 16f);

            // 왼쪽 유닛 아이콘(대기 첫 프레임). 글자는 그만큼 오른쪽으로 민다.
            var iconRect = UiKit.Place(UiKit.Rect("Icon", rt), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(ApplicantIconInset, 0f), new Vector2(ApplicantIconSize, ApplicantIconSize));
            var icon = UiKit.Image(iconRect, null, Color.white, false);
            icon.type = Image.Type.Simple;
            icon.preserveAspect = true;
            var textLeft = ApplicantIconInset + ApplicantIconSize + 10f;

            var label = button.Label.rectTransform;
            button.Label.alignment = TextAlignmentOptions.MidlineLeft;
            label.anchorMin = new Vector2(0f, 0.48f);
            label.anchorMax = Vector2.one;
            label.offsetMin = new Vector2(textLeft, 0f);
            label.offsetMax = new Vector2(-14f, -4f);

            var detail = UiKit.Text("Detail", rt, string.Empty, 14f, HudPalette.TextDim, TextAlignmentOptions.MidlineLeft, false, true);
            detail.rectTransform.anchorMin = Vector2.zero;
            detail.rectTransform.anchorMax = new Vector2(1f, 0.48f);
            detail.rectTransform.offsetMin = new Vector2(textLeft, 4f);
            detail.rectTransform.offsetMax = new Vector2(-14f, 0f);

            slots[i] = button;
            details[i] = detail;
            icons[i] = icon;
            rt.gameObject.SetActive(false);
        }

        var empty = Place(UiKit.Text("Empty", pane, "이번 주에는 새 지원자가 없습니다.", 16f, HudPalette.TextMuted, TextAlignmentOptions.TopLeft, false, true), 0f, 62f, paneWidth, 24f);
        var feedback = Place(UiKit.Text("Feedback", pane, string.Empty, 15f, HudPalette.Warn, TextAlignmentOptions.TopLeft), 0f, paneHeight - 54f, paneWidth, 50f);

        UiKit.Wire(view, "status", status);
        UiKit.Wire(view, "empty", empty);
        UiKit.WireArray(view, "slots", slots);
        UiKit.WireArray(view, "details", details);
        UiKit.WireArray(view, "icons", icons);
        UiKit.Wire(view, "feedback", feedback);
        UiKit.Wire(view, "scroll", scroll);
        return view;
    }
}
