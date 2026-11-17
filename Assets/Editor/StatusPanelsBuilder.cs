using Code.Manager;
using Code.UI;
using TMPro;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 부하 상태·모험가 상태·금고 창(uGUI, A안) 프리팹을 만들고 SampleScene에 놓는다. 캔버스 30(방 관리 20 위, 대화 80 아래).
/// Unity CLI: <c>StatusPanelsBuilder.BuildAll()</c>.
///
/// 부하 상태 440×330·모험가 상태 400×360은 누른 자리 옆에 뜬다(화면 밖이면 안으로 밀림).
/// 금고 380×316은 오른쪽 열 방 상세(400) 왼쪽, 위에서 104.
/// </summary>
public static class StatusPanelsBuilder
{
    public const string PrefabPath = "Assets/03.Prefabs/UI/Status/StatusPanels.prefab";

    [MenuItem("Tools/Dungeon Keeper/Build Status Panels")]
    public static void BuildAll()
    {
        BuildPrefab();
        UiSceneInstaller.Place(PrefabPath, typeof(UnitStatusView), instance =>
        {
            UiKit.Wire(instance.GetComponentInChildren<UnitStatusView>(true), "source",
                Object.FindAnyObjectByType<UnitStatusPanelView>(FindObjectsInactive.Include));
            UiKit.Wire(instance.GetComponentInChildren<EnemyStatusView>(true), "source",
                Object.FindAnyObjectByType<EnemyStatusPanelView>(FindObjectsInactive.Include));
            var treasury = instance.GetComponentInChildren<TreasuryView>(true);
            UiKit.Wire(treasury, "panel", Object.FindAnyObjectByType<NodePanelView>(FindObjectsInactive.Include));
            UiKit.Wire(treasury, "costManager", Object.FindAnyObjectByType<CostManager>(FindObjectsInactive.Include));
            UiKit.Wire(treasury, "dayManager", Object.FindAnyObjectByType<DayManager>(FindObjectsInactive.Include));
        });
    }

    public static void BuildPrefab()
    {
        var canvas = UiKit.Canvas("StatusPanels", 30);
        var root = (RectTransform)canvas.transform;
        BuildUnitStatus(root);
        BuildEnemyStatus(root);
        BuildTreasury(root);

        UiKit.EnsureFolder("Assets/03.Prefabs/UI/Status");
        UiKit.ApplyButtonFaces(canvas);
        PrefabUtility.SaveAsPrefabAsset(canvas, PrefabPath, out var saved);
        Object.DestroyImmediate(canvas);
        if (!saved)
            Debug.LogError($"{nameof(StatusPanelsBuilder)}: {PrefabPath} 저장 실패");
    }

    // ── 조각 ──

    private static RectTransform Host(RectTransform root, string name) => UiKit.Fill(UiKit.Rect(name, root));

    /// <summary>누른 자리 옆에 놓을 카드: 부모 왼쪽 아래 기준.</summary>
    private static RectTransform FloatingCard(RectTransform host, float width, float height)
    {
        var card = UiKit.Place(UiKit.Rect("Card", host), Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(200f, 200f), new Vector2(width, height));
        UiKit.Panel(card, HudPalette.Surface, HudPalette.Border, true);
        return card;
    }

    private static TextMeshProUGUI Line(RectTransform parent, string name, string value, float size, Color color,
        float x, float y, float width, float height = 20f, bool bold = false, bool singleLine = true)
    {
        var text = UiKit.Text(name, parent, value, size, color, singleLine ? TextAlignmentOptions.MidlineLeft : TextAlignmentOptions.TopLeft, bold, singleLine);
        UiKit.TopLeft(text.rectTransform, x, y, width, height);
        return text;
    }

    private static UiFrameButton Button(RectTransform parent, string name, string label, UiKit.ButtonKind kind,
        RectTransform placed, float fontSize = 15f, bool grouped = false)
    {
        var button = UiKit.Button(placed, label, kind, fontSize);
        if (grouped)
            UiKit.Wire(button, "group", placed.gameObject.AddComponent<CanvasGroup>());
        return button;
    }

    private static UnityEngine.UI.Image Bar(RectTransform parent, float x, float y, float width, Color fill) =>
        UiKit.Bar(UiKit.TopLeft(UiKit.Rect("Bar", parent), x, y, width, 8f), HudPalette.Track, fill);

    private static UiFrameButton Close(RectTransform card) =>
        Button(card, "Close", "닫기", UiKit.ButtonKind.Secondary, UiKit.TopRight(UiKit.Rect("Close", card), 14f, 12f, 64f, 28f), 14f);

    // ── 부하 상태 ──

    private static void BuildUnitStatus(RectTransform root)
    {
        const float width = 440f;
        const float inner = width - 28f;
        var host = Host(root, "UnitStatus");
        var view = host.gameObject.AddComponent<UnitStatusView>();
        var card = FloatingCard(host, width, 330f);

        var title = Line(card, "Title", "부하 Lv 1", 19f, HudPalette.Text, 14f, 10f, 340f, 32f, true);
        var close = Close(card);
        var state = Line(card, "State", "상태", 14f, HudPalette.TextDim, 14f, 46f, inner);

        var portraitFrame = UiKit.TopLeft(UiKit.Rect("Portrait", card), 14f, 72f, 72f, 72f);
        UiKit.Panel(portraitFrame, HudPalette.Deep, HudPalette.BorderQuiet, false);
        var portraitRect = UiKit.Fill(UiKit.Rect("Image", portraitFrame), 6f);
        var portrait = UiKit.Image(portraitRect, null, Color.white, false);
        portrait.type = UnityEngine.UI.Image.Type.Simple;
        portrait.preserveAspect = true;

        const float statsX = 98f;
        const float statsWidth = width - statsX - 14f;
        var hp = Line(card, "Hp", "HP 0/0", 15f, HudPalette.TextBody, statsX, 72f, statsWidth);
        var hpFill = Bar(card, statsX, 96f, statsWidth, HudPalette.Gain);
        var combat = Line(card, "Combat", "ATK 0", 14f, HudPalette.TextBody, statsX, 108f, statsWidth);
        var command = Line(card, "Command", "명령", 14f, HudPalette.TextBody, statsX, 130f, statsWidth);

        var traits = Line(card, "Traits", "특성 · 성격", 14f, HudPalette.TextBody, 14f, 152f, inner);
        traits.raycastTarget = true;
        var hover = traits.gameObject.AddComponent<UiHoverRelay>();
        var fatigue = Line(card, "Fatigue", "피로 0/100", 14f, HudPalette.TextBody, 14f, 176f, inner);
        var fatigueFill = Bar(card, 14f, 200f, inner, HudPalette.Gain);
        var hint = Line(card, "Hint", string.Empty, 14f, HudPalette.Warn, 14f, 214f, inner);

        var recover = Button(card, "Recover", "치료·휴식", UiKit.ButtonKind.Primary,
            UiKit.Place(UiKit.Rect("Recover", card), Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(14f, 14f), new Vector2(200f, 40f)), 15f, true);
        var recall = Button(card, "Recall", "회수(대기 명단)", UiKit.ButtonKind.Secondary,
            UiKit.Place(UiKit.Rect("Recall", card), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-14f, 14f), new Vector2(200f, 40f)), 15f, true);

        var tooltip = UiKit.Place(UiKit.Rect("Tooltip", host), Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(360f, 72f));
        UiKit.Panel(tooltip, HudPalette.SurfaceRaised, HudPalette.BorderAccent, false);
        var tooltipText = UiKit.Text("Text", tooltip, string.Empty, 14f, HudPalette.TextBody, TextAlignmentOptions.TopLeft);
        UiKit.Fill(tooltipText.rectTransform, 10f);
        tooltip.gameObject.SetActive(false);

        UiKit.Wire(view, "root", card.gameObject);
        UiKit.Wire(view, "card", card);
        UiKit.Wire(view, "title", title);
        UiKit.Wire(view, "state", state);
        UiKit.Wire(view, "portrait", portrait);
        UiKit.Wire(view, "hp", hp);
        UiKit.Wire(view, "hpFill", hpFill);
        UiKit.Wire(view, "combat", combat);
        UiKit.Wire(view, "command", command);
        UiKit.Wire(view, "traits", traits);
        UiKit.Wire(view, "traitsHover", hover);
        UiKit.Wire(view, "fatigue", fatigue);
        UiKit.Wire(view, "fatigueFill", fatigueFill);
        UiKit.Wire(view, "hint", hint);
        UiKit.Wire(view, "closeButton", close);
        UiKit.Wire(view, "recoverButton", recover);
        UiKit.Wire(view, "recallButton", recall);
        UiKit.Wire(view, "tooltip", tooltip);
        UiKit.Wire(view, "tooltipText", tooltipText);
    }

    // ── 모험가 상태 ──

    private static void BuildEnemyStatus(RectTransform root)
    {
        const float width = 400f;
        const float inner = width - 28f;
        var host = Host(root, "EnemyStatus");
        var view = host.gameObject.AddComponent<EnemyStatusView>();
        var card = FloatingCard(host, width, 360f);

        var title = Line(card, "Title", "모험가 Lv 1", 19f, HudPalette.Text, 14f, 10f, 300f, 32f, true);
        var close = Close(card);
        var state = Line(card, "State", "상태", 14f, HudPalette.TextDim, 14f, 46f, inner);

        var stats = UiKit.TopLeft(UiKit.Rect("Stats", card), 14f, 70f, inner, 150f);
        UiKit.Panel(stats, HudPalette.Deep, HudPalette.BorderQuiet, false);
        var names = new[] { "Hp", "Attack", "Purpose", "Party", "Mood", "Satisfaction" };
        var lines = new TextMeshProUGUI[names.Length];
        for (var i = 0; i < names.Length; i++)
            lines[i] = Line(card, names[i], string.Empty, 14f, HudPalette.TextBody, 24f, 76f + i * 23f, inner - 20f);

        var effectsTitle = Line(card, "EffectsTitle", "상태이상", 14f, HudPalette.TextDim, 14f, 226f, inner);
        var effects = new Object[4];
        for (var i = 0; i < effects.Length; i++)
            effects[i] = Line(card, $"Effect{i}", string.Empty, 14f, HudPalette.Warn, 14f, 250f + i * 22f, inner);

        UiKit.Wire(view, "root", card.gameObject);
        UiKit.Wire(view, "card", card);
        UiKit.Wire(view, "title", title);
        UiKit.Wire(view, "state", state);
        UiKit.Wire(view, "hp", lines[0]);
        UiKit.Wire(view, "attack", lines[1]);
        UiKit.Wire(view, "purpose", lines[2]);
        UiKit.Wire(view, "party", lines[3]);
        UiKit.Wire(view, "mood", lines[4]);
        UiKit.Wire(view, "satisfaction", lines[5]);
        UiKit.Wire(view, "effectsTitle", effectsTitle);
        UiKit.WireArray(view, "effects", effects);
        UiKit.Wire(view, "closeButton", close);
    }

    // ── 금고 ──

    private static void BuildTreasury(RectTransform root)
    {
        const float width = 380f;
        const float inner = width - 32f;
        var host = Host(root, "Treasury");
        var view = host.gameObject.AddComponent<TreasuryView>();
        var card = UiKit.TopRight(UiKit.Rect("Card", host), 24f + 400f + 12f, 104f, width, 316f);
        UiKit.Panel(card, HudPalette.Surface, HudPalette.Border, true);

        var iconRect = UiKit.TopLeft(UiKit.Rect("Icon", card), 16f, 14f, 24f, 24f);
        var icon = UiKit.Image(iconRect, null, Color.white, false);
        icon.type = UnityEngine.UI.Image.Type.Simple;
        icon.preserveAspect = true;
        Line(card, "Title", "금고 관리", 19f, HudPalette.Text, 48f, 10f, 230f, 32f, true);
        var close = Button(card, "Close", "닫기", UiKit.ButtonKind.Secondary, UiKit.TopRight(UiKit.Rect("Close", card), 16f, 12f, 64f, 30f), 14f);

        var status = UiKit.TopLeft(UiKit.Rect("Status", card), 16f, 56f, inner, 100f);
        UiKit.Panel(status, HudPalette.Deep, HudPalette.BorderQuiet, false);
        var storage = Line(card, "Storage", "보관 금화 0 / 0G", 15f, HudPalette.TextBody, 26f, 61f, inner - 20f);
        var interest = Line(card, "Interest", "정산 이자 0%", 14f, HudPalette.TextDim, 26f, 84f, inner - 20f);
        var risk = Line(card, "Risk", "침입자가 노리는 곳", 14f, HudPalette.TextDim, 26f, 107f, inner - 20f);
        var funds = Line(card, "Funds", "운영 자금 0G", 15f, HudPalette.TextBody, 26f, 130f, inner - 20f);
        var hint = Line(card, "Hint", string.Empty, 14f, HudPalette.TextDim, 16f, 162f, inner, 40f, false, false);

        var buttonWidth = (inner - 12f) * 0.5f;
        var deposit = Button(card, "Deposit", "25G 맡기기", UiKit.ButtonKind.Primary, UiKit.TopLeft(UiKit.Rect("Deposit", card), 16f, 210f, buttonWidth, 40f));
        var depositAll = Button(card, "DepositAll", "전부 맡기기", UiKit.ButtonKind.Secondary, UiKit.TopLeft(UiKit.Rect("DepositAll", card), 16f + buttonWidth + 12f, 210f, buttonWidth, 40f));
        var withdraw = Button(card, "Withdraw", "25G 꺼내기", UiKit.ButtonKind.Secondary, UiKit.TopLeft(UiKit.Rect("Withdraw", card), 16f, 258f, buttonWidth, 40f));
        var withdrawAll = Button(card, "WithdrawAll", "전부 꺼내기", UiKit.ButtonKind.Secondary, UiKit.TopLeft(UiKit.Rect("WithdrawAll", card), 16f + buttonWidth + 12f, 258f, buttonWidth, 40f));

        UiKit.Wire(view, "root", card.gameObject);
        UiKit.Wire(view, "icon", icon);
        UiKit.Wire(view, "storage", storage);
        UiKit.Wire(view, "interest", interest);
        UiKit.Wire(view, "risk", risk);
        UiKit.Wire(view, "funds", funds);
        UiKit.Wire(view, "hint", hint);
        UiKit.Wire(view, "closeButton", close);
        UiKit.Wire(view, "depositButton", deposit);
        UiKit.Wire(view, "depositAllButton", depositAll);
        UiKit.Wire(view, "withdrawButton", withdraw);
        UiKit.Wire(view, "withdrawAllButton", withdrawAll);
    }
}
