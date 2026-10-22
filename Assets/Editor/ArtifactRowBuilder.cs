using Code.UI;
using TMPro;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 유물 줄(uGUI, A안) 프리팹을 만들고 SampleScene에 놓는다(2026-10-22 사용자 승인 목업).
/// 왼쪽 위 장부(24, 24, 340×268) 바로 아래 340×152 패널: 머리("유물 N"·"조합 N 성립") + 6칸 2줄 고정 칸(44, 간격 9).
/// 칸에 마우스를 올리면 패널 오른쪽에 설명 카드 340×220(이름·대상·설명·조합). 캔버스 11 — HUD(10) 바로 위, 상태 창(30) 아래.
/// Unity CLI: <c>ArtifactRowBuilder.BuildAll()</c>.
/// </summary>
public static class ArtifactRowBuilder
{
    public const string PrefabPath = "Assets/03.Prefabs/UI/Hud/ArtifactRow.prefab";
    public const int SortingOrder = 11;
    private const float Left = 24f;
    private const float Top = 304f;
    private const float Width = 340f;
    private const float Height = 152f;
    private const float Inner = 14f;
    private const int Columns = 6;
    private const float SlotSize = 44f;
    private const float SlotGap = 9f;
    private const float SlotsTop = 42f;
    private const float DetailWidth = 340f;
    private const float DetailHeight = 220f;

    [MenuItem("Tools/Dungeon Keeper/Build Artifact Row")]
    public static void BuildAll()
    {
        BuildPrefab();
        UiSceneInstaller.Place(PrefabPath, typeof(ArtifactRowView), instance =>
        {
            var view = instance.GetComponent<ArtifactRowView>();
            UiKit.Wire(view, "panel", Object.FindAnyObjectByType<ArtifactPanelView>(FindObjectsInactive.Include));
        });
    }

    public static void BuildPrefab()
    {
        var rootObject = UiKit.Canvas("ArtifactRow", SortingOrder);
        var view = rootObject.AddComponent<ArtifactRowView>();
        // 바탕 그림이 없는 묶음이라 빈 자리는 지도 클릭을 막지 않는다.
        var root = UiKit.Fill(UiKit.Rect("Root", rootObject.transform));

        var panel = UiKit.TopLeft(UiKit.Rect("Panel", root), Left, Top, Width, Height);
        UiKit.Panel(panel, HudPalette.Surface, HudPalette.BorderQuiet, true);
        var header = Label(panel, "Header", "유물 0", 15f, HudPalette.TextDim, Inner, 10f, 160f, 24f, false);
        var combo = UiKit.Text("ComboSummary", panel, string.Empty, 15f, HudPalette.Accent, TextAlignmentOptions.MidlineRight, true, true);
        UiKit.TopRight(combo.rectTransform, Inner, 10f, 160f, 24f);

        var slots = new Object[Code.UI.ViewModels.ArtifactRowPresenter.SlotCount];
        for (var i = 0; i < slots.Length; i++)
            slots[i] = BuildSlot(panel, i);

        var gridHeight = SlotSize * 2f + SlotGap;
        var empty = UiKit.Text("EmptyHint", panel, string.Empty, 14f, HudPalette.TextMuted, TextAlignmentOptions.Center, false, true);
        UiKit.TopLeft(empty.rectTransform, Inner, SlotsTop, Width - Inner * 2f, gridHeight);

        var detail = UiKit.TopLeft(UiKit.Rect("Detail", root), Left + Width + 10f, Top, DetailWidth, DetailHeight);
        UiKit.Panel(detail, HudPalette.SurfaceRaised, HudPalette.Border);
        const float detailInner = 16f;
        var detailName = Label(detail, "Name", "유물", 18f, HudPalette.Text, detailInner, 12f, DetailWidth - detailInner * 2f - 120f, 28f, true);
        var detailTarget = UiKit.Text("Target", detail, "모든 유닛", 13f, HudPalette.TextDim, TextAlignmentOptions.MidlineRight, false, true);
        UiKit.TopRight(detailTarget.rectTransform, detailInner, 14f, 116f, 24f);
        var detailDescription = Label(detail, "Description", string.Empty, 14f, HudPalette.TextBody, detailInner, 46f, DetailWidth - detailInner * 2f, 64f, false, false, TextAlignmentOptions.TopLeft);
        var line = UiKit.TopLeft(UiKit.Rect("Divider", detail), detailInner, 116f, DetailWidth - detailInner * 2f, 1f);
        UiKit.Image(line, null, HudPalette.BorderQuiet, false);
        var detailCombos = Label(detail, "Combos", string.Empty, 13f, HudPalette.TextBody, detailInner, 124f, DetailWidth - detailInner * 2f, 84f, false, false, TextAlignmentOptions.TopLeft);

        UiKit.Wire(view, "root", root.gameObject);
        UiKit.Wire(view, "header", header);
        UiKit.Wire(view, "comboSummary", combo);
        UiKit.Wire(view, "emptyHint", empty);
        UiKit.WireArray(view, "slots", slots);
        UiKit.Wire(view, "detailRoot", detail.gameObject);
        UiKit.Wire(view, "detailName", detailName);
        UiKit.Wire(view, "detailTarget", detailTarget);
        UiKit.Wire(view, "detailDescription", detailDescription);
        UiKit.Wire(view, "detailCombos", detailCombos);
        detail.gameObject.SetActive(false);
        root.gameObject.SetActive(false);

        UiKit.EnsureFolder("Assets/03.Prefabs/UI/Hud");
        PrefabUtility.SaveAsPrefabAsset(rootObject, PrefabPath, out var saved);
        Object.DestroyImmediate(rootObject);
        if (!saved)
            Debug.LogError($"{nameof(ArtifactRowBuilder)}: {PrefabPath} 저장 실패");
    }

    /// <summary>칸 하나(44): 바탕·테두리(마우스를 받는다) + 가운데 아이콘(32, 픽셀 그대로).</summary>
    private static ArtifactSlotView BuildSlot(RectTransform parent, int index)
    {
        var column = index % Columns;
        var row = index / Columns;
        var rt = UiKit.TopLeft(UiKit.Rect($"Slot{index}", parent), Inner + column * (SlotSize + SlotGap), SlotsTop + row * (SlotSize + SlotGap), SlotSize, SlotSize);
        var back = UiKit.Image(rt, UiKit.FillSprite, HudPalette.Deep, true);
        var outline = UiKit.Outline(rt, HudPalette.Track);
        var iconRect = UiKit.Place(UiKit.Rect("Icon", rt), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(32f, 32f));
        var icon = UiKit.Image(iconRect, null, Color.white, false);
        icon.type = UnityEngine.UI.Image.Type.Simple;
        icon.preserveAspect = true;
        icon.enabled = false;

        var slot = rt.gameObject.AddComponent<ArtifactSlotView>();
        UiKit.Wire(slot, "back", back);
        UiKit.Wire(slot, "outline", outline);
        UiKit.Wire(slot, "icon", icon);
        return slot;
    }

    private static TextMeshProUGUI Label(RectTransform parent, string name, string value, float size, Color color,
        float x, float y, float width, float height, bool bold, bool singleLine = true,
        TextAlignmentOptions alignment = TextAlignmentOptions.MidlineLeft)
    {
        var text = UiKit.Text(name, parent, value, size, color, alignment, bold, singleLine);
        UiKit.TopLeft(text.rectTransform, x, y, width, height);
        return text;
    }
}
