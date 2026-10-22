using Code.Manager;
using Code.Progression;
using Code.UI;
using TMPro;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 판 종료 결과 화면(uGUI, A안) 프리팹을 만들고 SampleScene에 놓는다. 화면 가운데 카드 1040×600(2026-10-22 사용자 승인 목업).
/// 캔버스 95 — 판이 끝나면 다른 창(방침 83·정산표 84·설정 90) 위에 뜨고 팝업(100) 아래. 덮개가 뒤를 막는다.
/// 머리: 제목(함락 빨강·사수 금색)·이유 한 줄, 오른쪽에 버틴 날·최고 기록 칩·지난 최고. 몸 왼쪽: 전과 8줄. 몸 오른쪽: 진행도 막대·다음 단계, 새로 열림 4줄.
/// 바닥: "타이틀로"·"새 판 시작". 닫기 버튼·ESC 없음(라우터 <c>runEnd</c>가 비켜 줌).
/// Unity CLI: <c>RunEndBuilder.BuildAll()</c>.
/// </summary>
public static class RunEndBuilder
{
    public const string PrefabPath = "Assets/03.Prefabs/UI/RunEnd/RunEndScreen.prefab";
    public const string UnlockCatalogPath = "Assets/07.SO/Progression/DungeonUnlockCatalog.asset";
    public const int SortingOrder = 95;
    private const float CardWidth = 1040f;
    private const float CardHeight = 600f;
    private const float Pad = 28f;
    private const float Gap = 20f;
    private const float BodyTop = 116f;
    private const float BodyHeight = 392f;
    private const float Inner = 16f;
    private const int UnlockRows = 4;
    private const float UnlockTop = 164f;
    private const float UnlockRowHeight = 54f;
    /// <summary>전과 8줄이 칸 높이(330)를 고르게 채우는 줄 간격(TMP 단위, 글자 크기의 1/100).</summary>
    private const float StatLineSpacing = 100f;
    private const float IconFrameSize = 44f;
    /// <summary>칠해진 몸을 맞출 칸(아이콘 칸 테두리 안쪽). 넘친 여백은 아이콘 칸이 자른다.</summary>
    private const float IconBoxSize = 40f;

    [MenuItem("Tools/Dungeon Keeper/Build Run End Screen")]
    public static void BuildAll()
    {
        BuildPrefab();
        UiSceneInstaller.Place(PrefabPath, typeof(RunEndScreenView), instance =>
        {
            var view = instance.GetComponent<RunEndScreenView>();
            UiKit.Wire(view, "bossWave", Object.FindAnyObjectByType<BossWavePresenter>(FindObjectsInactive.Include));
            UiKit.Wire(view, "runSummary", Object.FindAnyObjectByType<RunSummarySystem>(FindObjectsInactive.Include));
            UiKit.Wire(view, "dayManager", Object.FindAnyObjectByType<DayManager>(FindObjectsInactive.Include));
            UiKit.Wire(view, "speedController", Object.FindAnyObjectByType<GameSpeedController>(FindObjectsInactive.Include));
            UiKit.Wire(view, "unlockCatalog", AssetDatabase.LoadAssetAtPath<DungeonUnlockCatalogSO>(UnlockCatalogPath));

            var router = Object.FindAnyObjectByType<UiEscapeRouter>(FindObjectsInactive.Include);
            if (router != null)
                UiKit.Wire(router, "runEnd", view);
        });
    }

    public static void BuildPrefab()
    {
        var rootObject = UiKit.Canvas("RunEndScreen", SortingOrder);
        var view = rootObject.AddComponent<RunEndScreenView>();

        var panel = UiKit.Fill(UiKit.Rect("Root", rootObject.transform));
        UiKit.Image(panel, null, HudPalette.Scrim, true);

        var card = UiKit.Place(UiKit.Rect("Card", panel), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(CardWidth, CardHeight));
        UiKit.Panel(card, HudPalette.Surface, HudPalette.Border, true);

        var title = Label(card, "Title", "던전 함락", 30f, HudPalette.Danger, Pad, 20f, 560f, 42f, true);
        var headline = Label(card, "Headline", "던전의 주인이 쓰러졌습니다", 15f, HudPalette.TextDim, Pad, 66f, 680f, 26f);

        var days = UiKit.Text("Days", card, "14일", 30f, HudPalette.Text, TextAlignmentOptions.MidlineRight, true, true);
        UiKit.TopRight(days.rectTransform, Pad, 20f, 110f, 42f);
        var badge = UiKit.TopRight(UiKit.Rect("BestBadge", card), Pad + 118f, 28f, 104f, 28f);
        UiKit.Panel(badge, HudPalette.AlertChip, HudPalette.BorderAccent);
        var badgeText = UiKit.Text("Label", badge, "최고 기록", 13f, HudPalette.Accent, TextAlignmentOptions.Center, true, true);
        UiKit.Fill(badgeText.rectTransform, 4f);
        var bestNote = UiKit.Text("BestNote", card, "지난 최고 9일", 13f, HudPalette.TextMuted, TextAlignmentOptions.MidlineRight, false, true);
        UiKit.TopRight(bestNote.rectTransform, Pad, 66f, 300f, 26f);

        Divider(card, Pad, 102f, CardWidth - Pad * 2f);

        var columnWidth = (CardWidth - Pad * 2f - Gap) / 2f;
        var stats = UiKit.TopLeft(UiKit.Rect("Stats", card), Pad, BodyTop, columnWidth, BodyHeight);
        UiKit.Panel(stats, HudPalette.SurfaceRaised, HudPalette.BorderQuiet);
        Label(stats, "Header", "이번 판 전과", 14f, HudPalette.TextDim, Inner, 12f, 300f, 24f);
        var statLabels = Label(stats, "Labels", string.Empty, 16f, HudPalette.TextBody, Inner, 46f, 240f, 330f, false, false, TextAlignmentOptions.TopLeft);
        statLabels.lineSpacing = StatLineSpacing;
        var statValues = UiKit.Text("Values", stats, string.Empty, 16f, HudPalette.Text, TextAlignmentOptions.TopRight, true);
        UiKit.TopRight(statValues.rectTransform, Inner, 46f, 200f, 330f);
        statValues.lineSpacing = StatLineSpacing;

        var progress = UiKit.TopLeft(UiKit.Rect("Progress", card), Pad + columnWidth + Gap, BodyTop, columnWidth, BodyHeight);
        UiKit.Panel(progress, HudPalette.SurfaceRaised, HudPalette.BorderQuiet);
        var innerWidth = columnWidth - Inner * 2f;
        Label(progress, "Header", "진행도", 14f, HudPalette.TextDim, Inner, 12f, 300f, 24f);
        var gain = Label(progress, "Gain", "+0", 16f, HudPalette.Text, Inner, 42f, innerWidth - 150f, 26f);
        var range = UiKit.Text("Range", progress, "0 → 0", 16f, HudPalette.Text, TextAlignmentOptions.MidlineRight, true, true);
        UiKit.TopRight(range.rectTransform, Inner, 42f, 140f, 26f);
        // 이번 판까지 찬 몫(밝은 금색) 위에 이번 판 전까지 찬 몫(어두운 금색)을 겹쳐, 이번에 얻은 만큼이 밝게 남는다.
        var after = UiKit.Bar(UiKit.TopLeft(UiKit.Rect("ProgressBar", progress), Inner, 78f, innerWidth, 14f), HudPalette.Track, HudPalette.Accent);
        var before = UiKit.Bar(UiKit.TopLeft(UiKit.Rect("ProgressBefore", progress), Inner, 78f, innerWidth, 14f), new Color(0f, 0f, 0f, 0f), HudPalette.Border);
        var note = Label(progress, "Note", string.Empty, 13f, HudPalette.TextMuted, Inner, 100f, innerWidth, 22f);

        Label(progress, "UnlockHeader", "새로 열림", 14f, HudPalette.TextDim, Inner, 134f, 300f, 24f);
        var slots = new Object[UnlockRows];
        for (var i = 0; i < UnlockRows; i++)
            slots[i] = BuildUnlockRow(progress, i, innerWidth);
        var empty = Label(progress, "UnlockEmpty", string.Empty, 14f, HudPalette.TextMuted, Inner, UnlockTop + 8f, innerWidth, 24f);

        var titleButton = UiKit.Button(UiKit.Place(UiKit.Rect("TitleButton", card), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-100f, 24f), new Vector2(184f, 48f)),
            "타이틀로", UiKit.ButtonKind.Secondary, 17f);
        var restartButton = UiKit.Button(UiKit.Place(UiKit.Rect("RestartButton", card), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(100f, 24f), new Vector2(184f, 48f)),
            "새 판 시작", UiKit.ButtonKind.Primary, 17f);

        UiKit.Wire(view, "root", panel.gameObject);
        UiKit.Wire(view, "title", title);
        UiKit.Wire(view, "headline", headline);
        UiKit.Wire(view, "days", days);
        UiKit.Wire(view, "bestBadge", badge.gameObject);
        UiKit.Wire(view, "bestNote", bestNote);
        UiKit.Wire(view, "statLabels", statLabels);
        UiKit.Wire(view, "statValues", statValues);
        UiKit.Wire(view, "progressGain", gain);
        UiKit.Wire(view, "progressRange", range);
        UiKit.Wire(view, "progressBefore", before);
        UiKit.Wire(view, "progressAfter", after);
        UiKit.Wire(view, "progressNote", note);
        UiKit.WireArray(view, "unlockSlots", slots);
        UiKit.Wire(view, "unlockEmpty", empty);
        UiKit.Wire(view, "titleButton", titleButton);
        UiKit.Wire(view, "restartButton", restartButton);
        panel.gameObject.SetActive(false);

        UiKit.EnsureFolder("Assets/03.Prefabs/UI/RunEnd");
        UiKit.ApplyButtonFaces(rootObject);
        PrefabUtility.SaveAsPrefabAsset(rootObject, PrefabPath, out var saved);
        Object.DestroyImmediate(rootObject);
        if (!saved)
            Debug.LogError($"{nameof(RunEndBuilder)}: {PrefabPath} 저장 실패");
    }

    /// <summary>"새로 열림" 한 줄(높이 48): 아이콘 칸 44 + 이름·종류 두 줄. 아이콘은 칠해진 몸만 칸에 맞추고(<see cref="UiSpriteFit"/>) 넘친 여백은 칸이 자른다.</summary>
    private static RunEndUnlockSlot BuildUnlockRow(RectTransform parent, int index, float width)
    {
        var rt = UiKit.TopLeft(UiKit.Rect($"Unlock{index}", parent), Inner, UnlockTop + index * UnlockRowHeight, width, 48f);
        var frame = UiKit.TopLeft(UiKit.Rect("IconFrame", rt), 0f, 2f, IconFrameSize, IconFrameSize);
        // 사슬 테두리(2배)는 44칸을 거의 다 덮어 몸이 가려졌다. 아이콘 칸은 테두리 없는 둥근 바탕만 깐다.
        UiKit.Image(frame, UiKit.FillSprite, HudPalette.Chip, false);
        var clip = UiKit.Fill(UiKit.Rect("Clip", frame), (IconFrameSize - IconBoxSize) / 2f);
        clip.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
        var iconRect = UiKit.Place(UiKit.Rect("Icon", clip), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(IconBoxSize, IconBoxSize));
        var icon = UiKit.Image(iconRect, null, Color.white, false);
        icon.type = UnityEngine.UI.Image.Type.Simple;
        icon.enabled = false;

        const float textX = IconFrameSize + 12f;
        var unlockName = Label(rt, "Name", string.Empty, 16f, HudPalette.Text, textX, 2f, width - textX, 24f, true);
        var detail = Label(rt, "Detail", string.Empty, 13f, HudPalette.TextDim, textX, 26f, width - textX, 20f);

        var slot = rt.gameObject.AddComponent<RunEndUnlockSlot>();
        UiKit.Wire(slot, "iconFrame", frame.gameObject);
        UiKit.Wire(slot, "icon", icon);
        var slotWiring = new SerializedObject(slot);
        slotWiring.FindProperty("iconBox").vector2Value = new Vector2(IconBoxSize, IconBoxSize);
        slotWiring.ApplyModifiedPropertiesWithoutUndo();
        UiKit.Wire(slot, "unlockName", unlockName);
        UiKit.Wire(slot, "detail", detail);
        rt.gameObject.SetActive(false);
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
