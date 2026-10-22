using Code.Manager;
using Code.UI;
using TMPro;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 전투 HUD(uGUI, A안 "장부형") 프리팹을 만들고 SampleScene에 놓아 배선한다.
/// Unity CLI: <c>GameplayHudBuilder.BuildAll()</c>. 메뉴: Tools/Dungeon Keeper/Build Gameplay HUD.
///
/// 배치(1920×1080): 왼쪽 위 장부 340×268, 오른쪽 위 배속 버튼 + 잠김 사유, 그 아래 손님 브리핑(접힘 380×164, 펼침 380×440)
/// 또는 진행 막대 380×132, 오른쪽 열 아래 기록판 380×360(위에서 568 — 침입 파티·최근 기록), 위 가운데 시작 배너 560×96,
/// 오른쪽 아래 목표 전환 320×40 + 영업 시작 320×72.
/// 지도 가운데와 아래 가운데는 비운다.
/// </summary>
public static class GameplayHudBuilder
{
    public const string PrefabPath = "Assets/03.Prefabs/UI/Hud/GameplayHud.prefab";
    private const float Margin = 24f;

    [MenuItem("Tools/Dungeon Keeper/Build Gameplay HUD")]
    public static void BuildAll()
    {
        BuildPrefab();
        UiSceneInstaller.Place(PrefabPath, typeof(TopHudView), Wire);
    }

    public static void BuildPrefab()
    {
        var root = UiKit.Canvas("GameplayHud", 10);
        var canvas = (RectTransform)root.transform;
        BuildLedger(canvas);
        BuildWaveHud(canvas);
        BuildRecordBoard(canvas);

        UiKit.EnsureFolder("Assets/03.Prefabs/UI/Hud");
        UiKit.ApplyButtonFaces(root);
        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out var saved);
        Object.DestroyImmediate(root);
        if (!saved)
            Debug.LogError($"{nameof(GameplayHudBuilder)}: {PrefabPath} 저장 실패");
    }

    private static void Wire(GameObject instance)
    {
        var top = instance.GetComponentInChildren<TopHudView>(true);
        UiKit.Wire(top, "costManager", Object.FindAnyObjectByType<CostManager>(FindObjectsInactive.Include));
        UiKit.Wire(top, "dayManager", Object.FindAnyObjectByType<DayManager>(FindObjectsInactive.Include));
        UiKit.Wire(top, "magicManager", Object.FindAnyObjectByType<MagicManager>(FindObjectsInactive.Include));
        UiKit.Wire(top, "speedController", Object.FindAnyObjectByType<GameSpeedController>(FindObjectsInactive.Include));
        UiKit.Wire(top, "waveManager", Object.FindAnyObjectByType<WaveManager>(FindObjectsInactive.Include));
        var scope = Object.FindAnyObjectByType<GameScope>(FindObjectsInactive.Include);
        if (scope != null)
            UiKit.Wire(top, "scope", scope);

        var wave = instance.GetComponentInChildren<WaveHudView>(true);
        UiKit.Wire(wave, "waveView", Object.FindAnyObjectByType<WaveView>(FindObjectsInactive.Include));

        var board = instance.GetComponentInChildren<RecordBoardView>(true);
        UiKit.Wire(board, "waveManager", Object.FindAnyObjectByType<WaveManager>(FindObjectsInactive.Include));
        UiKit.Wire(board, "dayManager", Object.FindAnyObjectByType<DayManager>(FindObjectsInactive.Include));
        UiKit.Wire(board, "enemyStatus", Object.FindAnyObjectByType<EnemyStatusPanelView>(FindObjectsInactive.Include));
    }

    // ── 장부(왼쪽 위) + 배속(오른쪽 위) ─────────────────────

    private static void BuildLedger(RectTransform canvas)
    {
        var host = UiKit.Fill(UiKit.Rect("TopHud", canvas));
        var view = host.gameObject.AddComponent<TopHudView>();

        var ledger = UiKit.TopLeft(UiKit.Rect("Ledger", host), Margin, Margin, 340f, 268f);
        UiKit.Panel(ledger, HudPalette.Surface, HudPalette.Border, true);

        var day = UiKit.Text("Day", ledger, "영업 준비", 15f, HudPalette.TextDim, TextAlignmentOptions.Left, false, true);
        UiKit.TopLeft(day.rectTransform, 18f, 14f, 304f, 20f);
        UiKit.Wire(view, "dayLabel", day);

        var gold = UiKit.Text("Gold", ledger, "0G", 38f, HudPalette.Accent, TextAlignmentOptions.Left, true, true);
        UiKit.TopLeft(gold.rectTransform, 18f, 36f, 304f, 46f);
        UiKit.Wire(view, "goldValue", gold);
        AddGoldCoin(ledger, gold);

        // 부채 줄: 누르면 설명, 파산 위험이면 바탕이 붉게 깜박인다.
        var debt = UiKit.TopLeft(UiKit.Rect("DebtRow", ledger), 10f, 88f, 320f, 46f);
        var debtBg = UiKit.Image(debt, UiKit.FillSprite, new Color(0f, 0f, 0f, 0f), true);
        var debtButton = debt.gameObject.AddComponent<UnityEngine.UI.Button>();
        debtButton.targetGraphic = debtBg;
        debtButton.transition = UnityEngine.UI.Selectable.Transition.None;
        debtButton.navigation = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.None };
        var debtLabel = UiKit.Text("Label", debt, "부채", 15f, HudPalette.TextDim, TextAlignmentOptions.Left, false, true);
        UiKit.TopLeft(debtLabel.rectTransform, 8f, 2f, 80f, 22f);
        var debtValue = UiKit.Text("Value", debt, "빚 없음", 16f, HudPalette.TextBody, TextAlignmentOptions.Right, true, true);
        UiKit.TopLeft(debtValue.rectTransform, 88f, 2f, 224f, 22f);
        var debtSub = UiKit.Text("Sub", debt, string.Empty, 13f, HudPalette.TextMuted, TextAlignmentOptions.Right, false, true);
        UiKit.TopLeft(debtSub.rectTransform, 8f, 24f, 304f, 18f);
        UiKit.Wire(view, "debtRow", debtBg);
        UiKit.Wire(view, "debtButton", debtButton);
        UiKit.Wire(view, "debtValue", debtValue);
        UiKit.Wire(view, "debtSub", debtSub);

        var magicLabel = UiKit.Text("MagicLabel", ledger, "마력", 15f, HudPalette.TextDim, TextAlignmentOptions.Left, false, true);
        UiKit.TopLeft(magicLabel.rectTransform, 18f, 140f, 80f, 22f);
        var magicValue = UiKit.Text("MagicValue", ledger, "0 / 0", 16f, HudPalette.TextBody, TextAlignmentOptions.Right, true, true);
        UiKit.TopLeft(magicValue.rectTransform, 98f, 140f, 224f, 22f);
        var magicBar = UiKit.TopLeft(UiKit.Rect("MagicBar", ledger), 18f, 166f, 304f, 8f);
        var magicFill = UiKit.Bar(magicBar, HudPalette.Track, HudPalette.Magic);
        UiKit.Wire(view, "magicValue", magicValue);
        UiKit.Wire(view, "magicFill", magicFill);

        var revival = StatusChip(ledger, "Revival", "부활", 14f, 190f);
        var grade = StatusChip(ledger, "Grade", "등급", 178f, 190f);
        var morale = StatusChip(ledger, "Morale", "민심", 14f, 228f);
        var threat = StatusChip(ledger, "Threat", "위협", 178f, 228f, out var threatBackground);
        var moraleButton = morale.gameObject.AddComponent<UnityEngine.UI.Button>();
        moraleButton.targetGraphic = morale.transform.parent.GetComponent<UnityEngine.UI.Image>();
        moraleButton.transition = UnityEngine.UI.Selectable.Transition.None;
        moraleButton.navigation = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.None };
        UiKit.Wire(view, "revivalValue", revival);
        UiKit.Wire(view, "gradeValue", grade);
        UiKit.Wire(view, "moraleValue", morale);
        UiKit.Wire(view, "moraleButton", moraleButton);
        UiKit.Wire(view, "threatValue", threat);
        UiKit.Wire(view, "threatChip", threatBackground);

        // 배속
        var speed = UiKit.TopRight(UiKit.Rect("Speed", host), Margin, Margin, 216f, 44f);
        UiKit.Wire(view, "pauseButton", SpeedButton(speed, "Pause", "II", 0f));
        UiKit.Wire(view, "normalButton", SpeedButton(speed, "Normal", "1×", 74f));
        UiKit.Wire(view, "fastButton", SpeedButton(speed, "Fast", "2×", 148f));
        // 설정 톱니: 배속 버튼 왼쪽. 설정 창 배선은 SettingsBuilder가 한다.
        var gearRect = UiKit.TopRight(UiKit.Rect("SettingsButton", host), Margin + 216f + 10f, Margin, 44f, 44f);
        var gear = UiKit.Button(gearRect, "설정", UiKit.ButtonKind.Secondary, 13f);
        UiKit.ButtonGlyph(gear, UiKit.GlyphGear, HudPalette.TextBody);
        UiKit.Wire(view, "settingsButton", gear);
        var lockLabel = UiKit.Text("PauseLock", host, string.Empty, 13f, HudPalette.Loss, TextAlignmentOptions.TopRight, false, true);
        UiKit.TopRight(lockLabel.rectTransform, Margin, 74f, 520f, 20f);
        UiKit.Wire(view, "pauseLockLabel", lockLabel);
    }

    private static UiFrameButton SpeedButton(RectTransform parent, string name, string label, float x)
    {
        var rt = UiKit.TopLeft(UiKit.Rect(name + "Button", parent), x, 0f, 68f, 44f);
        var button = UiKit.Button(rt, label, UiKit.ButtonKind.Secondary, 17f);
        AddSpeedGlyph(button);
        return button;
    }

    /// <summary>배속 버튼 글자(II·1×·2×) 대신 팩의 멈춤·재생·빨리감기 기호를 놓는다. 이미 있으면 그대로 둔다.</summary>
    public static void AddSpeedGlyph(UiFrameButton button)
    {
        if (button == null || button.transform.Find("Glyph") != null)
            return;

        var glyph = button.name switch
        {
            "PauseButton" => UiKit.GlyphPause,
            "NormalButton" => UiKit.GlyphPlay,
            "FastButton" => UiKit.GlyphFast,
            _ => -1
        };
        if (glyph >= 0)
            UiKit.ButtonGlyph(button, glyph, HudPalette.TextBody);
    }

    /// <summary>금화 숫자 앞에 금화 그림을 놓고 숫자를 그만큼 민다. 이미 있으면 그대로 둔다.</summary>
    public static void AddGoldCoin(RectTransform ledger, TMP_Text gold)
    {
        if (ledger == null || gold == null || ledger.Find("Coin") != null)
            return;

        var coin = UiKit.TopLeft(UiKit.Rect("Coin", ledger), 18f, 0f, 28f, 28f);
        UiKit.Coin(coin);
        // 숫자 줄 가운데에 맞춘다.
        var goldRect = gold.rectTransform;
        var top = -goldRect.anchoredPosition.y;
        coin.anchoredPosition = new Vector2(18f, -(top + (goldRect.sizeDelta.y - coin.sizeDelta.y) * 0.5f));
        var shift = coin.sizeDelta.x + 8f;
        goldRect.anchoredPosition += new Vector2(shift, 0f);
        goldRect.sizeDelta -= new Vector2(shift, 0f);
    }

    private static TextMeshProUGUI StatusChip(RectTransform parent, string name, string label, float x, float y)
    {
        return StatusChip(parent, name, label, x, y, out _);
    }

    private static TextMeshProUGUI StatusChip(RectTransform parent, string name, string label, float x, float y, out UnityEngine.UI.Image background)
    {
        var chip = UiKit.TopLeft(UiKit.Rect(name + "Chip", parent), x, y, 148f, 32f);
        background = UiKit.Image(chip, UiKit.FillSprite, HudPalette.Chip, true);
        var title = UiKit.Text("Label", chip, label, 12f, HudPalette.TextMuted, TextAlignmentOptions.Left, false, true);
        UiKit.TopLeft(title.rectTransform, 8f, 0f, 38f, 32f);
        var value = UiKit.Text("Value", chip, "준비 중", 12f, HudPalette.TextBody, TextAlignmentOptions.Right, false, true);
        UiKit.TopRight(value.rectTransform, 8f, 0f, 94f, 32f);
        return value;
    }

    // ── 영업 HUD ────────────────────────────────────────────

    private static void BuildWaveHud(RectTransform canvas)
    {
        var host = UiKit.Fill(UiKit.Rect("WaveHud", canvas));
        var view = host.gameObject.AddComponent<WaveHudView>();
        const float cardTop = 104f;
        const float cardWidth = 380f;

        // 손님 브리핑(접힘/펼침)
        var brief = UiKit.TopRight(UiKit.Rect("Briefing", host), Margin, cardTop, cardWidth, 164f);
        UiKit.Panel(brief, HudPalette.Surface, HudPalette.BorderQuiet, true);
        var briefTitle = UiKit.Text("Title", brief, "DAY 1 · 영업 준비", 19f, HudPalette.Accent, TextAlignmentOptions.Left, true, true);
        UiKit.TopLeft(briefTitle.rectTransform, 18f, 14f, cardWidth - 36f, 26f);
        var summary = UiKit.Text("Summary", brief, string.Empty, 14f, HudPalette.TextDim, TextAlignmentOptions.TopLeft);
        UiKit.TopLeft(summary.rectTransform, 18f, 44f, cardWidth - 36f, 30f);
        var objective = UiKit.Text("Objective", brief, string.Empty, 13f, HudPalette.AccentSoft, TextAlignmentOptions.MidlineLeft, false, true);
        UiKit.TopLeft(objective.rectTransform, 18f, 78f, cardWidth - 36f, 28f);
        var body = UiKit.Text("Body", brief, string.Empty, 14f, HudPalette.TextBody, TextAlignmentOptions.TopLeft);
        UiKit.Place(body.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 1f), new Vector2(0f, -44f), new Vector2(-36f, -92f));
        body.gameObject.SetActive(false);
        var toggleRt = UiKit.Place(UiKit.Rect("DetailToggle", brief), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-12f, 8f), new Vector2(110f, 30f));
        var toggle = UiKit.Button(toggleRt, "자세히", UiKit.ButtonKind.Quiet, 14f);
        UiKit.Wire(view, "briefCard", brief);
        UiKit.Wire(view, "briefTitle", briefTitle);
        UiKit.Wire(view, "briefSummary", summary);
        UiKit.Wire(view, "briefObjective", objective);
        UiKit.Wire(view, "briefBody", body);
        UiKit.Wire(view, "detailToggle", toggle);

        // 진행 막대
        var progress = UiKit.TopRight(UiKit.Rect("Progress", host), Margin, cardTop, cardWidth, 132f);
        UiKit.Panel(progress, HudPalette.Surface, HudPalette.BorderQuiet, true);
        var progressTitle = UiKit.Text("Title", progress, "던전 영업 중", 19f, HudPalette.Accent, TextAlignmentOptions.Left, true, true);
        UiKit.TopLeft(progressTitle.rectTransform, 18f, 14f, cardWidth - 36f, 26f);
        var bar = UiKit.TopLeft(UiKit.Rect("Bar", progress), 18f, 46f, cardWidth - 36f, 10f);
        var fill = UiKit.Bar(bar, HudPalette.Track, HudPalette.Danger);
        fill.fillAmount = 0f;
        var stats = UiKit.Text("Stats", progress, string.Empty, 14f, HudPalette.TextBody, TextAlignmentOptions.TopLeft);
        UiKit.TopLeft(stats.rectTransform, 18f, 64f, cardWidth - 36f, 56f);
        UiKit.Wire(view, "progressCard", progress.gameObject);
        UiKit.Wire(view, "progressTitle", progressTitle);
        UiKit.Wire(view, "progressFill", fill);
        UiKit.Wire(view, "progressStats", stats);

        // 목표 전환·영업 시작(오른쪽 아래)
        var start = UiKit.Button(UiKit.BottomRight(UiKit.Rect("StartButton", host), Margin, Margin, 320f, 72f), "던전 영업 시작", UiKit.ButtonKind.Primary, 18f);
        start.Label.textWrappingMode = TextWrappingModes.Normal;
        var objectiveButton = UiKit.Button(UiKit.BottomRight(UiKit.Rect("ObjectiveButton", host), Margin, Margin + 80f, 320f, 40f), "목표", UiKit.ButtonKind.Secondary, 14f);
        UiKit.Wire(view, "startButton", start);
        UiKit.Wire(view, "objectiveButton", objectiveButton);
    }

    // ── 기록판(오른쪽 열 아래): 침입 파티 + 최근 기록 ─────────

    private const float BoardWidth = 380f;
    private const float BoardHeight = 360f;
    private const int PartyRows = 8;

    private static void BuildRecordBoard(RectTransform canvas)
    {
        var host = UiKit.Fill(UiKit.Rect("RecordBoard", canvas));
        var view = host.gameObject.AddComponent<RecordBoardView>();
        var board = UiKit.TopRight(UiKit.Rect("Board", host), Margin, 568f, BoardWidth, BoardHeight);
        UiKit.Panel(board, HudPalette.Surface, HudPalette.BorderQuiet, true);
        const float inner = BoardWidth - 28f;

        var partyTitle = UiKit.Text("PartyTitle", board, "침입 파티", 16f, HudPalette.AccentSoft, TextAlignmentOptions.MidlineLeft, true, true);
        UiKit.TopLeft(partyTitle.rectTransform, 14f, 10f, 180f, 24f);
        var summary = UiKit.Text("PartySummary", board, "현재 0개", 13f, HudPalette.Warn, TextAlignmentOptions.MidlineRight, false, true);
        UiKit.TopRight(summary.rectTransform, 14f, 10f, 170f, 24f);

        var listRect = UiKit.TopLeft(UiKit.Rect("PartyList", board), 14f, 40f, inner, 156f);
        UiKit.ScrollArea(listRect, out var content, 10f);
        var layout = content.gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
        layout.spacing = 4f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        var rows = new Object[PartyRows];
        var risks = new Object[PartyRows];
        for (var i = 0; i < PartyRows; i++)
        {
            var rt = UiKit.Rect($"Party{i}", content);
            rt.gameObject.AddComponent<UnityEngine.UI.LayoutElement>().preferredHeight = 34f;
            var row = UiKit.Button(rt, "파티", UiKit.ButtonKind.Secondary, 13f);
            row.Label.alignment = TextAlignmentOptions.MidlineLeft;
            row.Label.rectTransform.offsetMin = new Vector2(10f, 0f);
            row.Label.rectTransform.offsetMax = new Vector2(-150f, 0f);
            var risk = UiKit.Text("Risk", rt, string.Empty, 12f, HudPalette.Warn, TextAlignmentOptions.MidlineRight, false, true);
            UiKit.Place(risk.rectTransform, new Vector2(1f, 0f), Vector2.one, new Vector2(1f, 0.5f), new Vector2(-10f, 0f), new Vector2(140f, 0f));
            rows[i] = row;
            risks[i] = risk;
            rt.gameObject.SetActive(false);
        }

        var divider = UiKit.TopLeft(UiKit.Rect("Divider", board), 14f, 204f, inner, 1f);
        UiKit.Image(divider, null, HudPalette.BorderQuiet, false);

        var logTitle = UiKit.Text("LogTitle", board, "최근 기록", 16f, HudPalette.AccentSoft, TextAlignmentOptions.MidlineLeft, true, true);
        UiKit.TopLeft(logTitle.rectTransform, 14f, 212f, 200f, 24f);
        var logRect = UiKit.TopLeft(UiKit.Rect("Log", board), 14f, 240f, inner, BoardHeight - 240f - 12f);
        var logScroll = UiKit.ScrollArea(logRect, out var logContent, 10f);
        var log = UiKit.ScrollText(logContent, 13f, HudPalette.TextDim, TextAlignmentOptions.TopLeft);
        log.lineSpacing = 4f;

        UiKit.Wire(view, "root", board.gameObject);
        UiKit.Wire(view, "partySummary", summary);
        UiKit.WireArray(view, "partyRows", rows);
        UiKit.WireArray(view, "partyRisks", risks);
        UiKit.Wire(view, "logText", log);
        UiKit.Wire(view, "logScroll", logScroll);
    }
}
