using Code.Manager;
using Code.UI;
using TMPro;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 운영 방침 창(uGUI, A안) 프리팹을 만들고 SampleScene에 놓는다. 화면 가운데 카드 1120×660(2026-10-22 사용자 승인 목업).
/// 캔버스 83 — 정산표(84)가 닫힌 뒤 뜨고, 첫날 안내 덮개(85)·설정(90)·팝업(100) 아래. 덮개가 뒤를 막는다.
/// 머리: 제목·안내, 오른쪽에 민심 막대와 시행 중인 방침. 몸: 방침 카드 3장(이름·기간·설명·효과 5줄·조합 칸·공포 버튼).
/// 바닥: "이번에는 공포하지 않음"(셋 다 금화 부족일 때만 보임, 자리는 늘 있음)과 안내 한 줄.
/// Unity CLI: <c>PolicyChoiceBuilder.BuildAll()</c>. ESC 라우터가 이 창을 가리키므로 <c>SettingsBuilder</c>보다 먼저 돌리거나, 이 생성기가 라우터도 잇는다.
/// </summary>
public static class PolicyChoiceBuilder
{
    public const string PrefabPath = "Assets/03.Prefabs/UI/Policy/PolicyChoiceScreen.prefab";
    public const int SortingOrder = 83;
    private const float CardWidth = 1120f;
    private const float CardHeight = 660f;
    private const float Pad = 28f;
    private const float Gap = 16f;
    private const float CardsTop = 108f;
    private const float PolicyCardHeight = 450f;
    private const int CardCount = 3;

    [MenuItem("Tools/Dungeon Keeper/Build Policy Choice Screen")]
    public static void BuildAll()
    {
        BuildPrefab();
        UiSceneInstaller.Place(PrefabPath, typeof(PolicyChoiceScreenView), instance =>
        {
            var view = instance.GetComponent<PolicyChoiceScreenView>();
            UiKit.Wire(view, "policies", Object.FindAnyObjectByType<MoralePolicyManager>(FindObjectsInactive.Include));
            UiKit.Wire(view, "costManager", Object.FindAnyObjectByType<CostManager>(FindObjectsInactive.Include));
            UiKit.Wire(view, "speedController", Object.FindAnyObjectByType<GameSpeedController>(FindObjectsInactive.Include));
            UiKit.Wire(view, "recordBoard", Object.FindAnyObjectByType<RecordBoardView>(FindObjectsInactive.Include));

            var router = Object.FindAnyObjectByType<UiEscapeRouter>(FindObjectsInactive.Include);
            if (router != null)
                UiKit.Wire(router, "policyChoice", view);
        });
    }

    public static void BuildPrefab()
    {
        var rootObject = UiKit.Canvas("PolicyChoiceScreen", SortingOrder);
        var view = rootObject.AddComponent<PolicyChoiceScreenView>();

        var panel = UiKit.Fill(UiKit.Rect("Root", rootObject.transform));
        UiKit.Image(panel, null, HudPalette.Scrim, true);

        var card = UiKit.Place(UiKit.Rect("Card", panel), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(CardWidth, CardHeight));
        UiKit.Panel(card, HudPalette.Surface, HudPalette.Border, true);

        var title = Label(card, "Title", "운영 방침 · 1일차", 26f, HudPalette.Accent, Pad, 20f, 560f, 38f, true);
        var subtitle = Label(card, "Subtitle", "하나를 골라 공포합니다 · 고를 때까지 시간이 멈춥니다", 15f, HudPalette.TextDim, Pad, 60f, 620f, 24f);

        var moraleCaption = UiKit.Text("MoraleCaption", card, "민심", 15f, HudPalette.TextDim, TextAlignmentOptions.MidlineLeft);
        UiKit.TopRight(moraleCaption.rectTransform, Pad + 220f, 20f, 100f, 24f);
        var moraleLabel = UiKit.Text("MoraleValue", card, "50 / 100", 15f, HudPalette.Text, TextAlignmentOptions.MidlineRight, true, true);
        UiKit.TopRight(moraleLabel.rectTransform, Pad, 20f, 200f, 24f);
        var moraleFill = UiKit.Bar(UiKit.TopRight(UiKit.Rect("MoraleBar", card), Pad, 48f, 320f, 12f), HudPalette.Track, HudPalette.Gain);
        var active = UiKit.Text("Active", card, "시행 중인 방침 없음", 13f, HudPalette.TextMuted, TextAlignmentOptions.MidlineRight, false, true);
        UiKit.TopRight(active.rectTransform, Pad, 66f, 520f, 22f);

        Divider(card, Pad, 96f, CardWidth - Pad * 2f);

        var cardWidth = (CardWidth - Pad * 2f - Gap * (CardCount - 1)) / CardCount;
        var slots = new Object[CardCount];
        for (var i = 0; i < CardCount; i++)
            slots[i] = BuildPolicyCard(card, i, Pad + i * (cardWidth + Gap), cardWidth);

        var declineRect = UiKit.Place(UiKit.Rect("Decline", card), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(320f, 44f));
        var decline = UiKit.Button(declineRect, "이번에는 공포하지 않음", UiKit.ButtonKind.Secondary, 15f);
        var footer = UiKit.Text("Footer", card, "방침은 기간이 끝날 때까지 이어집니다", 13f, HudPalette.TextMuted, TextAlignmentOptions.Center, false, true);
        UiKit.Place(footer.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 12f), new Vector2(CardWidth - Pad * 2f, 24f));

        UiKit.Wire(view, "root", panel.gameObject);
        UiKit.Wire(view, "title", title);
        UiKit.Wire(view, "subtitle", subtitle);
        UiKit.Wire(view, "moraleLabel", moraleLabel);
        UiKit.Wire(view, "moraleFill", moraleFill);
        UiKit.Wire(view, "activePolicies", active);
        UiKit.WireArray(view, "cards", slots);
        UiKit.Wire(view, "footer", footer);
        UiKit.Wire(view, "declineButton", decline);
        panel.gameObject.SetActive(false);

        UiKit.EnsureFolder("Assets/03.Prefabs/UI/Policy");
        UiKit.ApplyButtonFaces(rootObject);
        PrefabUtility.SaveAsPrefabAsset(rootObject, PrefabPath, out var saved);
        Object.DestroyImmediate(rootObject);
        if (!saved)
            Debug.LogError($"{nameof(PolicyChoiceBuilder)}: {PrefabPath} 저장 실패");
    }

    /// <summary>방침 카드 한 장(폭 344, 높이 450). 칸 크기는 내용과 상관없이 고정이다(ui-rules 5).</summary>
    private static PolicyCardSlot BuildPolicyCard(RectTransform parent, int index, float x, float width)
    {
        const float inner = 16f;
        var rt = UiKit.TopLeft(UiKit.Rect($"Policy{index}", parent), x, CardsTop, width, PolicyCardHeight);
        var outline = UiKit.Panel(rt, HudPalette.SurfaceRaised, HudPalette.Border, true);
        var group = rt.gameObject.AddComponent<CanvasGroup>();

        var cardName = Label(rt, "Name", "방침", 20f, HudPalette.Text, inner, 14f, width - inner * 2f - 96f, 28f, true);
        var duration = UiKit.Text("Duration", rt, "3일간", 14f, HudPalette.TextDim, TextAlignmentOptions.MidlineRight, false, true);
        UiKit.TopRight(duration.rectTransform, inner, 16f, 92f, 26f);
        var description = Label(rt, "Description", string.Empty, 14f, HudPalette.TextDim, inner, 48f, width - inner * 2f, 66f, false, false, TextAlignmentOptions.TopLeft);
        Divider(rt, inner, 122f, width - inner * 2f);

        var effectLabels = Label(rt, "EffectLabels", string.Empty, 15f, HudPalette.TextBody, inner, 132f, width * 0.5f, 136f, false, false, TextAlignmentOptions.TopLeft);
        effectLabels.lineSpacing = 12f;
        var effectValues = UiKit.Text("EffectValues", rt, string.Empty, 15f, HudPalette.TextBody, TextAlignmentOptions.TopRight, true);
        UiKit.TopRight(effectValues.rectTransform, inner, 132f, width * 0.5f - inner, 136f);
        effectValues.lineSpacing = 12f;

        var comboRect = UiKit.TopLeft(UiKit.Rect("Combo", rt), inner - 4f, 278f, width - inner * 2f + 8f, 94f);
        var comboOutline = UiKit.Panel(comboRect, HudPalette.Chip, HudPalette.BorderQuiet);
        var comboBack = comboRect.GetComponent<UnityEngine.UI.Image>();
        var comboTitle = Label(comboRect, "ComboTitle", string.Empty, 14f, HudPalette.TextDim, 12f, 8f, width - inner * 2f - 16f, 22f, true);
        var comboBody = Label(comboRect, "ComboBody", string.Empty, 13f, HudPalette.TextBody, 12f, 32f, width - inner * 2f - 16f, 56f, false, false, TextAlignmentOptions.TopLeft);

        var chooseRect = UiKit.Place(UiKit.Rect("Choose", rt), Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(inner - 4f, 14f), new Vector2(width - inner * 2f + 8f, 50f));
        var choose = UiKit.Button(chooseRect, "이 방침으로", UiKit.ButtonKind.Secondary, 16f);

        var slot = rt.gameObject.AddComponent<PolicyCardSlot>();
        UiKit.Wire(slot, "group", group);
        UiKit.Wire(slot, "outline", outline);
        UiKit.Wire(slot, "cardName", cardName);
        UiKit.Wire(slot, "duration", duration);
        UiKit.Wire(slot, "description", description);
        UiKit.Wire(slot, "effectLabels", effectLabels);
        UiKit.Wire(slot, "effectValues", effectValues);
        UiKit.Wire(slot, "comboBack", comboBack);
        UiKit.Wire(slot, "comboOutline", comboOutline);
        UiKit.Wire(slot, "comboTitle", comboTitle);
        UiKit.Wire(slot, "comboBody", comboBody);
        UiKit.Wire(slot, "choose", choose);
        return slot;
    }

    private static TextMeshProUGUI Label(RectTransform parent, string name, string value, float size, Color color,
        float x, float y, float width, float height, bool bold = false, bool singleLine = true,
        TextAlignmentOptions alignment = TextAlignmentOptions.MidlineLeft)
    {
        var text = UiKit.Text(name, parent, value, size, color, alignment, bold, singleLine);
        UiKit.TopLeft(text.rectTransform, x, y, width, height);
        return text;
    }

    private static void Divider(RectTransform parent, float x, float y, float width)
    {
        var line = UiKit.TopLeft(UiKit.Rect("Divider", parent), x, y, width, 1f);
        UiKit.Image(line, null, HudPalette.BorderQuiet, false);
    }
}
