using Code.UI;
using Code.UI.RoomManagement;
using TMPro;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 떠돌이 상인 창(uGUI, A안) 프리팹을 만들고 SampleScene에 놓는다. 화면 가운데 카드(2026-10-21 사용자 선택), 덮개가 뒤를 막는다.
/// 방 관리(20)·상태 창(30) 위, 정산(84)·팝업(100) 아래 캔버스(40). Unity CLI: <c>MerchantBuilder.BuildAll()</c>.
///
/// 카드 780×460: 제목·방문 일정·닫기, 진열 카드 5칸(설치 카드와 같은 모양), 고른 유물 이름·설명·조합, 결과 줄, 구매 버튼.
/// </summary>
public static class MerchantBuilder
{
    public const string PrefabPath = "Assets/03.Prefabs/UI/Merchant/MerchantScreen.prefab";
    private const float CardWidth = 780f;
    private const float CardHeight = 460f;
    private const float Pad = 26f;

    [MenuItem("Tools/Dungeon Keeper/Build Merchant Screen")]
    public static void BuildAll()
    {
        BuildPrefab();
        UiSceneInstaller.Place(PrefabPath, typeof(MerchantScreenView), instance =>
            UiKit.Wire(instance.GetComponent<MerchantScreenView>(), "merchant",
                Object.FindAnyObjectByType<MerchantPanelView>(FindObjectsInactive.Include)));
    }

    public static void BuildPrefab()
    {
        var rootObject = UiKit.Canvas("MerchantScreen", 40);
        var view = rootObject.AddComponent<MerchantScreenView>();

        var panel = UiKit.Fill(UiKit.Rect("Root", rootObject.transform));
        UiKit.Image(panel, null, HudPalette.Scrim, true);

        var card = UiKit.Place(UiKit.Rect("Card", panel), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(CardWidth, CardHeight));
        UiKit.Panel(card, HudPalette.Surface, HudPalette.Border, true);

        var title = Label(card, "Title", "떠돌이 상인", 24f, HudPalette.Accent, Pad, 18f, 220f, 36f, true);
        var subtitle = Label(card, "Subtitle", string.Empty, 15f, HudPalette.TextDim, Pad + 230f, 20f, 360f, 32f);
        var closeRect = UiKit.TopRight(UiKit.Rect("Close", card), Pad, 20f, 72f, 32f);
        var close = UiKit.Button(closeRect, "닫기", UiKit.ButtonKind.Secondary, 14f);
        Divider(card, Pad, 64f, CardWidth - Pad * 2f);

        var cards = new Object[Code.UI.MerchantPanelView.MaxOffers];
        for (var i = 0; i < cards.Length; i++)
            cards[i] = BuildOfferCard(card, i);
        var empty = Label(card, "Empty", "오늘은 팔 물건이 없습니다.", 16f, HudPalette.TextDim, Pad, 78f, CardWidth - Pad * 2f, 150f, false, true, TextAlignmentOptions.Center);

        Divider(card, Pad, 242f, CardWidth - Pad * 2f);
        var detailName = Label(card, "DetailName", string.Empty, 18f, HudPalette.Text, Pad, 252f, CardWidth - Pad * 2f, 28f, true);
        var detailBody = Label(card, "DetailBody", string.Empty, 14f, HudPalette.TextBody, Pad, 284f, CardWidth - Pad * 2f, 50f, false, false, TextAlignmentOptions.TopLeft);
        var detailCombo = Label(card, "DetailCombo", string.Empty, 14f, HudPalette.Accent, Pad, 338f, CardWidth - Pad * 2f, 40f, false, false, TextAlignmentOptions.TopLeft);

        var feedback = UiKit.Text("Feedback", card, string.Empty, 14f, HudPalette.TextDim, TextAlignmentOptions.MidlineLeft);
        UiKit.Place(feedback.rectTransform, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(Pad, 20f), new Vector2(CardWidth - Pad * 3f - 160f, 48f));
        var buyRect = UiKit.Place(UiKit.Rect("Buy", card), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-Pad, 20f), new Vector2(160f, 48f));
        var buy = UiKit.Button(buyRect, "구매", UiKit.ButtonKind.Primary, 17f);

        UiKit.Wire(view, "root", panel.gameObject);
        UiKit.Wire(view, "title", title);
        UiKit.Wire(view, "subtitle", subtitle);
        UiKit.Wire(view, "closeButton", close);
        UiKit.WireArray(view, "cards", cards);
        UiKit.Wire(view, "empty", empty);
        UiKit.Wire(view, "detailName", detailName);
        UiKit.Wire(view, "detailBody", detailBody);
        UiKit.Wire(view, "detailCombo", detailCombo);
        UiKit.Wire(view, "feedback", feedback);
        UiKit.Wire(view, "buyButton", buy);
        panel.gameObject.SetActive(false);

        UiKit.EnsureFolder("Assets/03.Prefabs/UI/Merchant");
        UiKit.ApplyButtonFaces(rootObject);
        PrefabUtility.SaveAsPrefabAsset(rootObject, PrefabPath, out var saved);
        Object.DestroyImmediate(rootObject);
        if (!saved)
            Debug.LogError($"{nameof(MerchantBuilder)}: {PrefabPath} 저장 실패");
    }

    /// <summary>진열 카드 한 장. 설치 창 카드(<c>RoomManagementBuilder</c>)와 같은 모양이다.</summary>
    private static InstallCardSlot BuildOfferCard(RectTransform parent, int index)
    {
        var rt = UiKit.TopLeft(UiKit.Rect($"Offer{index}", parent), Pad + index * 148f, 80f, 140f, 150f);
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
