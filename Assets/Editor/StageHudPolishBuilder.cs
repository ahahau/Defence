using System;
using System.Linq;
using Code.Manager;
using Code.Stages;
using Code.UI;
using Code.UI.Stages;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 함정 본편 씬 정리와 HUD(2026-11-03~04 사용자 요청·목업 확정). 여러 번 돌려도 같은 결과가 나온다.
/// - 최상위 오브젝트를 Systems / World / UI 아래로 묶고, 편집 중 칠한 문 그림 복제본을 지운다(실행 중 다시 만든다).
/// - 위 한 띠·아래 단축칸(바닥/벽/도구, 왼쪽 아래 부하)·오른쪽 아래 버튼, 놓은 함정 둘레 메뉴, 시작 전 편성 창을 만든다.
/// - 왼쪽 편성 패널은 숨기고 그 요소 일부(스테이지 이동·편성 수·유물)를 편성 창으로 옮긴다.
/// </summary>
public static class StageHudPolishBuilder
{
    public const string TrapCampaignScene = "Assets/00.Scenes/TrapCampaign.unity";

    [MenuItem("Tools/Dungeon Keeper/Polish Trap Campaign Scene")]
    private static void ApplyFromMenu() => Debug.Log(Apply(TrapCampaignScene));

    public static string Apply(string scenePath)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            throw new InvalidOperationException("편집 모드에서 컴파일이 끝난 뒤 실행하세요");
        Scene scene = SceneManager.GetSceneByPath(scenePath);
        bool opened = !scene.IsValid() || !scene.isLoaded;
        if (!opened && scene.isDirty)
            throw new InvalidOperationException(scenePath + " 씬에 저장하지 않은 변경이 있어 멈춥니다");
        string rows = StageDefenseBuilder.RebuildOptionList(scenePath, true);
        scene = SceneManager.GetSceneByPath(scenePath);
        opened = !scene.IsValid() || !scene.isLoaded;
        if (opened)
            scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
        // 새 오브젝트는 활성 씬에 먼저 생긴다. 다른 씬이 활성이면 옮겨도 그 씬이 dirty가 되어 대상 씬을 잠시 활성으로 둔다.
        Scene previous = SceneManager.GetActiveScene();
        SceneManager.SetActiveScene(scene);
        try
        {
            string organized = Organize(scene);
            string hud = PolishHud(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException(scenePath + " 씬 저장 실패");
            return $"{rows} · {organized} · {hud}";
        }
        finally
        {
            if (previous.IsValid() && previous.isLoaded && previous != scene)
                SceneManager.SetActiveScene(previous);
            if (opened)
                EditorSceneManager.CloseScene(scene, true);
        }
    }

    /// <summary>최상위를 역할별로 묶는다. 참조는 컴포넌트로 걸려 있어 부모만 바뀐다.</summary>
    private static string Organize(Scene scene)
    {
        Transform systems = Group(scene, "Systems");
        Transform world = Group(scene, "World");
        Transform ui = Group(scene, "UI");
        int moved = 0;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.transform == systems || root.transform == world || root.transform == ui)
                continue;
            Transform target = root.GetComponent<Canvas>() != null ? ui
                : root.GetComponent<StageSession>() != null || root.GetComponent<GameSpeedController>() != null
                    || root.GetComponent<UnityEngine.EventSystems.EventSystem>() != null
                    || root.GetComponent<Code.Core.RuntimeAssetInstaller>() != null ? systems
                : world;
            root.transform.SetParent(target, true);
            moved++;
        }
        StageMapView map = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<StageMapView>(true)).Single();
        Transform doors = (Transform)new SerializedObject(map).FindProperty("doorsRoot").objectReferenceValue;
        int removed = ClearEditorDoors(doors);
        return $"묶음 {moved}개 이동 · 문 복제본 {removed}개 제거";
    }

    /// <summary>편집 중 지형을 칠할 때 생긴 문 그림. StageMapView가 실행 중에 다시 만든다.</summary>
    public static int ClearEditorDoors(Transform doors)
    {
        if (doors == null)
            return 0;
        int removed = doors.childCount;
        for (int i = doors.childCount - 1; i >= 0; i--)
            UnityEngine.Object.DestroyImmediate(doors.GetChild(i).gameObject);
        return removed;
    }

    private static Transform Group(Scene scene, string name)
    {
        // 같은 이름이라도 컴포넌트가 붙은 오브젝트는 묶음이 아니다
        GameObject existing = scene.GetRootGameObjects().FirstOrDefault(root => root.name == name
            && root.GetComponents<Component>().Length == 1);
        if (existing != null)
            return existing.transform;
        GameObject created = new(name);
        SceneManager.MoveGameObjectToScene(created, scene);
        return created.transform;
    }

    private static string PolishHud(Scene scene)
    {
        StageHudView view = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<StageHudView>(true)).Single();
        RectTransform hud = (RectTransform)view.transform;
        RectTransform side = (RectTransform)hud.Find("Preparation");
        if (side == null)
            throw new InvalidOperationException("편성 패널(Preparation)을 찾지 못했습니다");
        foreach (string name in new[] { "StatusBar", "Briefing", "Guide", "TrapCard", "TrapRing", "TopBand", "Hotbar", "Notice", "Units",
                     "Loadout", "Portraits", "Devices" })
            Remove(hud, name);
        // 앞서 편성 창으로 옮긴 버튼·글을 패널로 되돌린 뒤 창을 다시 만든다(여러 번 돌려도 같은 결과)
        RestoreFromLoadoutWindow(hud, side);
        Remove(side, "OptionTabs");
        SerializedObject wiring = new(view);

        // 위 한 띠(12~68). 편성 패널은 그 아래(80)로 내리고, 바리케이드·철거는 단축칸 도구 묶음이 맡는다.
        BuildTopBand(view, hud);
        UiKit.TopLeft(side, 20, 80, 410, 990);
        Hide(wiring, "barricade");
        Hide(wiring, "demolish");
        UiKit.TopLeft(((TMP_Text)wiring.FindProperty("message").objectReferenceValue).rectTransform, 18, 864, 374, 116);
        MoveTopRight(hud, "Settings", 28, 20, 84, 40);
        MoveTopRight(hud, "FastSpeed", 120, 20, 64, 40);
        MoveTopRight(hud, "NormalSpeed", 192, 20, 64, 40);
        MoveTopRight(hud, "Pause", 264, 20, 100, 40);

        // 준비 단계 제목·적 예고(패널 오른쪽), 전투 중 알림(가운데 위), 상태 한 줄은 띠가 대신한다
        // 왼쪽 패널은 편성 창으로 바뀌어 쓰지 않는다. 제목·적 예고는 띠 아래 가운데, 알림 한 줄은 그 아래 가운데.
        Vector2 topCenter = new(0.5f, 1f);
        TMP_Text headline = (TMP_Text)wiring.FindProperty("headline").objectReferenceValue;
        UiKit.Place(headline.rectTransform, topCenter, topCenter, topCenter, new Vector2(0, -78), new Vector2(1200, 64));
        headline.alignment = TextAlignmentOptions.Top;
        Hide(wiring, "status");
        TMP_Text notice = UiKit.Text("Notice", hud, string.Empty, 20, HudPalette.Warn, TextAlignmentOptions.Top, false, true);
        UiKit.Place(notice.rectTransform, topCenter, topCenter, topCenter, new Vector2(0, -146), new Vector2(1200, 32));
        // 글꼴에 말줄임표가 없어 넘치면 자른다(ui-rules)
        notice.overflowMode = TextOverflowModes.Truncate;
        UiKit.Wire(view, "notice", notice);

        // 아래: 부하 명령(840) · 도구 줄(단축칸 위) · 단축칸 · 오른쪽 아래 상인·웨이브 시작
        Move(hud, "CommandBar", 456, 840, 640, 48);
        TMP_Text tool = (TMP_Text)wiring.FindProperty("tool").objectReferenceValue;
        UiKit.Place(tool.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 150), new Vector2(1200, 34));
        tool.alignment = TextAlignmentOptions.Center;
        BuildHotbar(view, hud);
        MoveBottomRight(wiring, "primary", 24, 24, 260, 64);
        MoveBottomRight(wiring, "merchantButton", 296, 24, 160, 64);
        UiFrameButton loadoutButton = UiKit.Button(UiKit.BottomRight(UiKit.Rect("Loadout", hud), 468, 24, 160, 64), "편성",
            UiKit.ButtonKind.Secondary, 18);
        UiKit.Wire(view, "loadoutButton", loadoutButton);
        MoveBottomRight(wiring, "supplyButton", 640, 24, 160, 64);
        BuildLoadoutWindow(view, hud, wiring);
        side.gameObject.SetActive(false);
        string retry = MoveRetryIntoSettings(scene, wiring);
        Hide(wiring, "title");

        BuildTrapRing(view, hud);
        BuildPortraits(view, hud);
        BuildDeviceButtons(view, hud);

        // 편성 목록 종류 탭(목록은 280부터)
        RectTransform tabs = UiKit.TopLeft(UiKit.Rect("OptionTabs", side), 18, 240, 374, 34);
        StageOptionTabs optionTabs = tabs.gameObject.AddComponent<StageOptionTabs>();
        UiKit.Wire(optionTabs, "floorTraps", Tab(tabs, "FloorTraps", "바닥 함정", 0));
        UiKit.Wire(optionTabs, "wallTraps", Tab(tabs, "WallTraps", "벽 함정", 127));
        UiKit.Wire(optionTabs, "units", Tab(tabs, "Units", "부하", 254));
        UiKit.Wire(view, "optionTabs", optionTabs);

        // 편성 패널을 여닫고 맵을 넓히는 대상, 숫자키를 단축칸에 넘기는 배속 키, 유물 창을 맨 위로
        UiKit.Wire(view, "preparationPanel", side.gameObject);
        StageMapView map = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<StageMapView>(true)).Single();
        UiKit.Wire(view, "mapView", map);
        SetBool(map, "hudFraming", true);
        GameSpeedController speed = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<GameSpeedController>(true)).Single();
        SetBool(speed, "functionKeySpeeds", true);
        StageArtifactWindow artifacts = hud.GetComponentInChildren<StageArtifactWindow>(true);
        if (artifacts != null && artifacts.transform.parent == hud)
            artifacts.transform.SetAsLastSibling();
        return $"위 띠·단축칸·알림·함정 메뉴·편성 창 배선 · {retry}";
    }

    /// <summary>
    /// 화면 너비로 늘어나는 위 한 띠: 왼쪽 코어 막대(빨강)·금화, 가운데 웨이브·진행 막대·남은 적.
    /// 배속·설정 버튼은 띠 오른쪽 끝 위에 따로 놓인다(화면 비율이 바뀌어도 세 덩어리가 양끝·가운데에 남는다).
    /// </summary>
    private static void BuildTopBand(StageHudView view, RectTransform hud)
    {
        RectTransform band = UiKit.Place(UiKit.Rect("TopBand", hud), new Vector2(0f, 1f), new Vector2(1f, 1f),
            new Vector2(0.5f, 1f), new Vector2(0, -12), new Vector2(-32, 56));
        band.SetAsFirstSibling();
        UiKit.Panel(band, HudPalette.Surface, HudPalette.Border, true);
        StageTopBand top = band.gameObject.AddComponent<StageTopBand>();
        BandText(band, "CoreLabel", "코어", 0f, 16, 56, 18, HudPalette.TextDim, false, TextAlignmentOptions.MidlineLeft);
        UiKit.Wire(top, "coreFill", UiKit.Bar(BandRect(band, "CoreBar", 0f, 76, 20, 200, 16), HudPalette.Track, HudPalette.Loss));
        UiKit.Wire(top, "core", BandText(band, "Core", string.Empty, 0f, 288, 100, 22, HudPalette.Text, true, TextAlignmentOptions.MidlineLeft));
        UiKit.Coin(BandRect(band, "Coin", 0f, 410, 13, 30, 30));
        UiKit.Wire(top, "gold", BandText(band, "Gold", string.Empty, 0f, 448, 140, 24, HudPalette.Accent, true, TextAlignmentOptions.MidlineLeft));
        // 가운데 덩어리: 웨이브 글(가운데 왼쪽, 오른쪽 맞춤) · 막대(가운데 오른쪽) · 남은 적(막대 오른쪽)
        UiKit.Wire(top, "wave", BandText(band, "Wave", string.Empty, 0.5f, -318, 300, 20, HudPalette.Text, true, TextAlignmentOptions.MidlineRight));
        UiKit.Wire(top, "waveFill", UiKit.Bar(BandRect(band, "WaveBar", 0.5f, -6, 23, 220, 10), HudPalette.Track, HudPalette.Accent));
        UiKit.Wire(top, "remaining", BandText(band, "Remaining", string.Empty, 0.5f, 230, 330, 18, HudPalette.TextBody, false, TextAlignmentOptions.MidlineLeft));
        UiKit.Wire(view, "topBand", top);
    }

    /// <summary>띠 안 칸. <paramref name="anchorX"/> 0이면 띠 왼쪽 끝, 0.5면 띠 가운데에서 x만큼(왼쪽 위 기준).</summary>
    private static RectTransform BandRect(RectTransform band, string name, float anchorX, float x, float y, float width, float height) =>
        UiKit.Place(UiKit.Rect(name, band), new Vector2(anchorX, 1f), new Vector2(anchorX, 1f), new Vector2(0f, 1f),
            new Vector2(x, -y), new Vector2(width, height));

    private static TMP_Text BandText(RectTransform band, string name, string value, float anchorX, float x, float width, float size,
        Color color, bool bold, TextAlignmentOptions alignment)
    {
        TMP_Text text = UiKit.Text(name, band, value, size, color, alignment, bold, true);
        UiKit.Place(text.rectTransform, new Vector2(anchorX, 1f), new Vector2(anchorX, 1f), new Vector2(0f, 1f),
            new Vector2(x, 0), new Vector2(width, 56));
        return text;
    }

    /// <summary>
    /// 아래 가운데 단축칸: 바닥 함정 8칸 / 벽 함정 8칸 / 도구 2칸 묶음. 쓰지 않는 칸은 꺼지고 묶음이 줄어든다.
    /// 편성은 첫 웨이브 뒤 고정이라 전투 중에는 칸 수가 바뀌지 않는다.
    /// </summary>
    private static void BuildHotbar(StageHudView view, RectTransform hud)
    {
        RectTransform bar = UiKit.Place(UiKit.Rect("Hotbar", hud), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f), new Vector2(0, 16), new Vector2(0, 92));
        UnityEngine.UI.HorizontalLayoutGroup row = bar.gameObject.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
        row.spacing = 18;
        row.childAlignment = TextAnchor.LowerCenter;
        row.childControlWidth = true;
        row.childControlHeight = false;
        row.childForceExpandWidth = false;
        row.childForceExpandHeight = false;
        bar.gameObject.AddComponent<UnityEngine.UI.ContentSizeFitter>().horizontalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
        StageHotbar hotbar = bar.gameObject.AddComponent<StageHotbar>();
        UiKit.WireArray(hotbar, "floorSlots", Group(bar, "Floor", "바닥 함정", 8, out _));
        UiKit.WireArray(hotbar, "wallSlots", Group(bar, "Wall", "벽 함정", 8, out RectTransform wall));
        UiKit.Wire(hotbar, "wallGroup", wall.gameObject);
        StageHotbarSlot[] tools = Group(bar, "Tools", "도구", 2, out _);
        UiKit.Wire(hotbar, "barricadeSlot", tools[0]);
        UiKit.Wire(hotbar, "demolishSlot", tools[1]);

        // 부하 묶음은 왼쪽 아래 따로(4단계에서 초상·체력 칸으로 바뀐다)
        RectTransform units = UiKit.Place(UiKit.Rect("Units", hud), Vector2.zero, Vector2.zero, Vector2.zero,
            new Vector2(24, 16), new Vector2(0, 92));
        UnityEngine.UI.HorizontalLayoutGroup unitRow = units.gameObject.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
        unitRow.childAlignment = TextAnchor.LowerLeft;
        unitRow.childControlWidth = true;
        unitRow.childControlHeight = false;
        unitRow.childForceExpandWidth = false;
        unitRow.childForceExpandHeight = false;
        units.gameObject.AddComponent<UnityEngine.UI.ContentSizeFitter>().horizontalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
        UiKit.WireArray(hotbar, "unitSlots", Group(units, "UnitSlots", "부하", 6, out RectTransform unitGroup));
        UiKit.Wire(hotbar, "unitGroup", unitGroup.gameObject);
        UiKit.Wire(view, "hotbar", hotbar);
    }

    /// <summary>
    /// 놓은 함정 둘레 메뉴(가운데 기준): 이름·단계(위 112), 강화(위 64), 판매(오른쪽 112), 회전(왼쪽 112), 이유(아래 56).
    /// 바탕은 없고 버튼만 클릭을 받는다. 이름·이유 글은 함정 그림을 가리지 않게 어두운 띠 위에 쓴다.
    /// </summary>
    private static void BuildTrapRing(StageHudView view, RectTransform hud)
    {
        RectTransform holder = UiKit.Fill(UiKit.Rect("TrapRing", hud));
        StageTrapRing ring = holder.gameObject.AddComponent<StageTrapRing>();
        Vector2 center = new(0.5f, 0.5f);
        RectTransform root = UiKit.Place(UiKit.Rect("Root", holder), center, center, center, Vector2.zero, new Vector2(10, 10));
        RectTransform titleBack = UiKit.Place(UiKit.Rect("TitleBack", root), center, center, center, new Vector2(0, 112), new Vector2(260, 34));
        UiKit.Panel(titleBack, HudPalette.Deep, HudPalette.Border, false);
        TMP_Text title = UiKit.Text("Title", titleBack, string.Empty, 17, HudPalette.Text, TextAlignmentOptions.Center, true, true);
        title.overflowMode = TextOverflowModes.Truncate;
        UiKit.Fill(title.rectTransform, 4);
        UiKit.Wire(ring, "title", title);
        TMP_Text reason = UiKit.Text("Reason", root, string.Empty, 15, HudPalette.Warn, TextAlignmentOptions.Center, false, true);
        reason.overflowMode = TextOverflowModes.Truncate;
        UiKit.Place(reason.rectTransform, center, center, center, new Vector2(0, -56), new Vector2(320, 24));
        UiKit.Wire(ring, "reason", reason);
        UiKit.Wire(ring, "upgrade", UiKit.Button(UiKit.Place(UiKit.Rect("Upgrade", root), center, center, center, new Vector2(0, 64), new Vector2(150, 44)),
            "강화", UiKit.ButtonKind.Primary, 17));
        UiKit.Wire(ring, "sell", UiKit.Button(UiKit.Place(UiKit.Rect("Sell", root), center, center, center, new Vector2(120, 0), new Vector2(150, 44)),
            "판매", UiKit.ButtonKind.Secondary, 17));
        UiKit.Wire(ring, "rotate", UiKit.Button(UiKit.Place(UiKit.Rect("Rotate", root), center, center, center, new Vector2(-100, 0), new Vector2(110, 44)),
            "회전 R", UiKit.ButtonKind.Secondary, 17));
        UiKit.Wire(ring, "root", root);
        root.gameObject.SetActive(false);
        UiKit.Wire(view, "trapRing", ring);
    }

    /// <summary>전투 중 왼쪽 아래 부하 초상 줄(준비 단계 부하 단축칸 자리). 칸 84×104: 초상·체력 막대·궁극기 막대.</summary>
    private static void BuildPortraits(StageHudView view, RectTransform hud)
    {
        RectTransform holder = UiKit.Fill(UiKit.Rect("Portraits", hud));
        StageUnitPortraits portraits = holder.gameObject.AddComponent<StageUnitPortraits>();
        RectTransform row = UiKit.Place(UiKit.Rect("Row", holder), Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(24, 16), new Vector2(0, 104));
        UnityEngine.UI.HorizontalLayoutGroup layout = row.gameObject.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
        layout.spacing = 6;
        layout.childAlignment = TextAnchor.LowerLeft;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        row.gameObject.AddComponent<UnityEngine.UI.ContentSizeFitter>().horizontalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
        StageUnitPortrait[] slots = new StageUnitPortrait[6];
        for (int i = 0; i < slots.Length; i++)
        {
            RectTransform slot = UiKit.Rect("Portrait_" + i, row);
            slot.sizeDelta = new Vector2(84, 104);
            UiKit.Panel(slot, HudPalette.Deep, HudPalette.Border, false);
            RectTransform iconRect = UiKit.TopLeft(UiKit.Rect("Icon", slot), 14, 8, 56, 56);
            UnityEngine.UI.Image icon = UiKit.Image(iconRect, null, Color.white, false);
            icon.type = UnityEngine.UI.Image.Type.Simple;
            UnityEngine.UI.Image health = UiKit.Bar(UiKit.TopLeft(UiKit.Rect("Health", slot), 8, 72, 68, 9), HudPalette.Track, HudPalette.Loss);
            RectTransform ultimateRect = UiKit.TopLeft(UiKit.Rect("Ultimate", slot), 8, 86, 68, 7);
            UnityEngine.UI.Image ultimate = UiKit.Bar(ultimateRect, HudPalette.Track, HudPalette.Info);
            UnityEngine.UI.Image shade = UiKit.Image(UiKit.Fill(UiKit.Rect("Shade", slot), 3), null, new Color(0f, 0f, 0f, 0.6f), false);
            TMP_Text status = UiKit.Text("Status", slot, string.Empty, 16, HudPalette.TextBody, TextAlignmentOptions.Center, true, true);
            UiKit.TopLeft(status.rectTransform, 0, 24, 84, 24);
            StageUnitPortrait portrait = slot.gameObject.AddComponent<StageUnitPortrait>();
            UiKit.Wire(portrait, "icon", icon);
            UiKit.Wire(portrait, "health", health);
            UiKit.Wire(portrait, "ultimate", ultimate);
            UiKit.Wire(portrait, "ultimateTrack", ultimateRect.gameObject);
            UiKit.Wire(portrait, "shade", shade);
            UiKit.Wire(portrait, "status", status);
            slots[i] = portrait;
        }
        UiKit.Wire(portraits, "root", row.gameObject);
        UiKit.WireArray(portraits, "portraits", slots);
        row.gameObject.SetActive(false);
        UiKit.Wire(view, "unitPortraits", portraits);
    }

    /// <summary>전투 중 오른쪽 아래 수동 장치 버튼 줄(준비 단계 웨이브 시작 자리). 칸 140×72: 이름·F·재사용 막대·남은 초.</summary>
    private static void BuildDeviceButtons(StageHudView view, RectTransform hud)
    {
        RectTransform holder = UiKit.Fill(UiKit.Rect("Devices", hud));
        StageDeviceButtons devices = holder.gameObject.AddComponent<StageDeviceButtons>();
        RectTransform row = UiKit.Place(UiKit.Rect("Row", holder), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f),
            new Vector2(-24, 24), new Vector2(0, 72));
        UnityEngine.UI.HorizontalLayoutGroup layout = row.gameObject.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
        layout.spacing = 10;
        layout.childAlignment = TextAnchor.LowerRight;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        row.gameObject.AddComponent<UnityEngine.UI.ContentSizeFitter>().horizontalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
        StageDeviceButton[] buttons = new StageDeviceButton[4];
        for (int i = 0; i < buttons.Length; i++)
        {
            RectTransform slot = UiKit.Rect("Device_" + i, row);
            slot.sizeDelta = new Vector2(140, 72);
            UiFrameButton button = UiKit.Button(slot, "밀치기", UiKit.ButtonKind.Secondary, 20);
            TMP_Text key = UiKit.Text("Key", slot, "F", 15, HudPalette.Accent, TextAlignmentOptions.TopLeft, true, true);
            UiKit.TopLeft(key.rectTransform, 8, 4, 30, 20);
            TMP_Text seconds = UiKit.Text("Seconds", slot, string.Empty, 14, HudPalette.Info, TextAlignmentOptions.TopRight, false, true);
            UiKit.TopRight(seconds.rectTransform, 8, 4, 60, 20);
            UnityEngine.UI.Image cooldown = UiKit.Bar(UiKit.Place(UiKit.Rect("Cooldown", slot), new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(0.5f, 0f), new Vector2(0, 8), new Vector2(-24, 6)), HudPalette.Track, HudPalette.Accent);
            StageDeviceButton device = slot.gameObject.AddComponent<StageDeviceButton>();
            UiKit.Wire(device, "button", button);
            UiKit.Wire(device, "key", key);
            UiKit.Wire(device, "cooldown", cooldown);
            UiKit.Wire(device, "seconds", seconds);
            buttons[i] = device;
        }
        UiKit.Wire(devices, "root", row.gameObject);
        UiKit.WireArray(devices, "buttons", buttons);
        row.gameObject.SetActive(false);
        UiKit.Wire(view, "deviceButtons", devices);
    }

    private const string LoadoutWindowName = "LoadoutWindow";
    private static readonly string[] MovedIntoLoadoutWindow = { "PreviousStage", "NextStage", "Loadout", "ArtifactSummary", "Artifacts" };

    private static void RestoreFromLoadoutWindow(RectTransform hud, RectTransform side)
    {
        Transform window = hud.Find(LoadoutWindowName);
        if (window == null)
            return;
        foreach (string name in MovedIntoLoadoutWindow)
        {
            Transform moved = window.GetComponentsInChildren<Transform>(true).FirstOrDefault(child => child.name == name);
            if (moved != null)
                moved.SetParent(side, false);
        }
        UnityEngine.Object.DestroyImmediate(window.gameObject);
    }

    /// <summary>
    /// 가운데 편성 창(1240×760, 뒤를 어둡게 덮어 맵 클릭을 막는다). 격자 5열·설명·가져갈 칸·출격.
    /// 스테이지 이동·편성 수·유물 요약/버튼은 패널의 기존 요소를 옮겨 HUD 배선을 그대로 쓴다.
    /// </summary>
    private static void BuildLoadoutWindow(StageHudView view, RectTransform hud, SerializedObject wiring)
    {
        StageSession session = (StageSession)wiring.FindProperty("session").objectReferenceValue;
        RectTransform holder = UiKit.Fill(UiKit.Rect(LoadoutWindowName, hud));
        StageLoadoutWindow window = holder.gameObject.AddComponent<StageLoadoutWindow>();
        RectTransform root = UiKit.Fill(UiKit.Rect("Root", holder));
        UiKit.Image(root, null, HudPalette.Scrim, true);
        RectTransform panel = UiKit.Place(UiKit.Rect("Panel", root), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f), new Vector2(0, -10), new Vector2(1240, 760));
        UiKit.Panel(panel, HudPalette.Surface, HudPalette.Border, true);
        UiKit.Wire(window, "root", root.gameObject);

        TMP_Text title = UiKit.Text("Title", panel, string.Empty, 26, HudPalette.Accent, TextAlignmentOptions.MidlineLeft, true, true);
        UiKit.TopLeft(title.rectTransform, 24, 18, 900, 40);
        UiKit.Wire(window, "title", title);
        MoveInto(wiring, "previousStage", panel, rt => UiKit.TopRight(rt, 92, 20, 54, 40));
        MoveInto(wiring, "nextStage", panel, rt => UiKit.TopRight(rt, 28, 20, 54, 40));
        MoveInto(wiring, "loadout", panel, rt => UiKit.TopLeft(rt, 24, 62, 760, 30));

        // 격자: 5열 136×150
        RectTransform area = UiKit.TopLeft(UiKit.Rect("Grid", panel), 24, 104, 760, 486);
        UiKit.ScrollArea(area, out RectTransform content);
        UnityEngine.UI.GridLayoutGroup grid = content.gameObject.AddComponent<UnityEngine.UI.GridLayoutGroup>();
        grid.cellSize = new Vector2(136, 150);
        grid.spacing = new Vector2(12, 12);
        grid.constraint = UnityEngine.UI.GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 5;
        // 후보가 조금 늘어도 생성기를 다시 돌리지 않게 5열 단위로 여유 칸을 둔다(남는 칸은 실행 중 숨긴다)
        int options = session != null && session.Catalog != null ? session.Catalog.Options.Length : 0;
        int count = Mathf.Max(25, (options + 5 + 4) / 5 * 5);
        StageLoadoutCell[] cells = new StageLoadoutCell[count];
        for (int i = 0; i < count; i++)
            cells[i] = Cell(content, "Cell_" + i);
        UiKit.WireArray(window, "cells", cells);

        // 설명
        RectTransform detail = UiKit.TopLeft(UiKit.Rect("Detail", panel), 808, 104, 408, 486);
        UiKit.Panel(detail, HudPalette.Deep, HudPalette.BorderQuiet, false);
        TMP_Text detailName = UiKit.Text("Name", detail, string.Empty, 22, HudPalette.Text, TextAlignmentOptions.MidlineLeft, true, true);
        UiKit.TopLeft(detailName.rectTransform, 18, 14, 372, 36);
        TMP_Text detailBody = UiKit.Text("Body", detail, string.Empty, 16, HudPalette.TextBody, TextAlignmentOptions.TopLeft);
        UiKit.TopLeft(detailBody.rectTransform, 18, 58, 372, 330);
        detailBody.enableAutoSizing = true;
        detailBody.fontSizeMin = 13;
        detailBody.fontSizeMax = 16;
        UiKit.Wire(window, "detailName", detailName);
        UiKit.Wire(window, "detailBody", detailBody);
        UiKit.Wire(window, "toggle", UiKit.Button(UiKit.TopLeft(UiKit.Rect("Toggle", detail), 18, 404, 372, 32), "편성에 넣기", UiKit.ButtonKind.Primary, 17));
        UiKit.Wire(window, "upgrade", UiKit.Button(UiKit.TopLeft(UiKit.Rect("Upgrade", detail), 18, 444, 372, 30), "영구 강화", UiKit.ButtonKind.Secondary, 15));

        // 아래: 가져갈 칸 · 유물 · 상태 줄 · 출격
        TMP_Text carryLabel = UiKit.Text("CarryLabel", panel, "가져갈 칸", 16, HudPalette.AccentSoft, TextAlignmentOptions.MidlineLeft, false, true);
        UiKit.TopLeft(carryLabel.rectTransform, 24, 604, 200, 26);
        RectTransform carry = UiKit.TopLeft(UiKit.Rect("Carry", panel), 24, 632, 560, 76);
        UnityEngine.UI.HorizontalLayoutGroup carryRow = carry.gameObject.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
        carryRow.spacing = 8;
        carryRow.childControlWidth = false;
        carryRow.childControlHeight = false;
        carryRow.childForceExpandWidth = false;
        carryRow.childForceExpandHeight = false;
        StageHotbarSlot[] carrySlots = new StageHotbarSlot[6];
        for (int i = 0; i < carrySlots.Length; i++)
            carrySlots[i] = Slot(carry, "Carry_" + i);
        UiKit.WireArray(window, "carrySlots", carrySlots);
        MoveInto(wiring, "artifactSummary", panel, rt => UiKit.TopLeft(rt, 600, 604, 360, 56));
        MoveInto(wiring, "artifactButton", panel, rt => UiKit.TopLeft(rt, 600, 664, 240, 44));
        TMP_Text status = UiKit.Text("Status", panel, string.Empty, 15, HudPalette.Warn, TextAlignmentOptions.MidlineLeft, false, true);
        status.overflowMode = TextOverflowModes.Truncate;
        UiKit.TopLeft(status.rectTransform, 24, 718, 900, 28);
        UiKit.Wire(window, "status", status);
        UiKit.Wire(window, "start", UiKit.Button(UiKit.BottomRight(UiKit.Rect("Start", panel), 24, 24, 240, 72), "출격", UiKit.ButtonKind.Primary, 24));
        root.gameObject.SetActive(false);
        UiKit.Wire(view, "loadoutWindow", window);
    }

    private static StageLoadoutCell Cell(RectTransform content, string name)
    {
        RectTransform rect = UiKit.Rect(name, content);
        UiFrameButton button = UiKit.Button(rect, string.Empty, UiKit.ButtonKind.Secondary, 15);
        RectTransform iconRect = UiKit.Place(UiKit.Rect("Icon", rect), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f), new Vector2(0, -12), new Vector2(72, 72));
        UnityEngine.UI.Image icon = UiKit.Image(iconRect, null, Color.white, false);
        icon.type = UnityEngine.UI.Image.Type.Simple;
        TMP_Text shortName = UiKit.Text("Short", iconRect, string.Empty, 22, HudPalette.TextBody, TextAlignmentOptions.Center, true, true);
        UiKit.Fill(shortName.rectTransform);
        TMP_Text label = UiKit.Text("Label", rect, string.Empty, 15, HudPalette.TextBody, TextAlignmentOptions.Center, false, true);
        label.overflowMode = TextOverflowModes.Truncate;
        UiKit.TopLeft(label.rectTransform, 6, 92, 124, 24);
        TMP_Text badge = UiKit.Text("Badge", rect, string.Empty, 14, HudPalette.Accent, TextAlignmentOptions.Center, true, true);
        badge.overflowMode = TextOverflowModes.Truncate;
        UiKit.TopLeft(badge.rectTransform, 6, 118, 124, 22);
        UnityEngine.UI.Image shade = UiKit.Image(UiKit.Fill(UiKit.Rect("Shade", rect), 3), null, new Color(0f, 0f, 0f, 0.55f), false);
        UnityEngine.UI.Image focus = UiKit.Outline(rect, HudPalette.Info);
        focus.name = "Focus";
        StageLoadoutCell cell = rect.gameObject.AddComponent<StageLoadoutCell>();
        UiKit.Wire(cell, "button", button);
        UiKit.Wire(cell, "icon", icon);
        UiKit.Wire(cell, "shortName", shortName);
        UiKit.Wire(cell, "label", label);
        UiKit.Wire(cell, "badge", badge);
        UiKit.Wire(cell, "shade", shade);
        UiKit.Wire(cell, "focus", focus);
        return cell;
    }

    private static void MoveInto(SerializedObject wiring, string field, RectTransform parent, Action<RectTransform> place)
    {
        Component target = (Component)wiring.FindProperty(field).objectReferenceValue;
        if (target == null)
            throw new InvalidOperationException(field + " 배선이 없습니다");
        RectTransform rect = (RectTransform)target.transform;
        rect.SetParent(parent, false);
        place(rect);
    }

    private static StageHotbarSlot[] Group(RectTransform bar, string name, string title, int count, out RectTransform group)
    {
        group = UiKit.Rect(name, bar);
        group.sizeDelta = new Vector2(0, 92);
        UiKit.Panel(group, HudPalette.Deep, HudPalette.Border, true);
        UnityEngine.UI.HorizontalLayoutGroup row = group.gameObject.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
        row.padding = new RectOffset(8, 8, 8, 8);
        row.spacing = 6;
        row.childAlignment = TextAnchor.MiddleLeft;
        row.childControlWidth = false;
        row.childControlHeight = false;
        row.childForceExpandWidth = false;
        row.childForceExpandHeight = false;
        // 묶음 이름표는 테두리 위에 걸친다(배치 계산에서 뺀다)
        TMP_Text label = UiKit.Text("Title", group, title, 15, HudPalette.AccentSoft, TextAlignmentOptions.BottomLeft, false, true);
        label.gameObject.AddComponent<UnityEngine.UI.LayoutElement>().ignoreLayout = true;
        UiKit.Place(label.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 0f), new Vector2(10, 2), new Vector2(160, 22));
        Transform outline = group.Find("Outline");
        if (outline != null)
            outline.gameObject.AddComponent<UnityEngine.UI.LayoutElement>().ignoreLayout = true;
        StageHotbarSlot[] slots = new StageHotbarSlot[count];
        for (int i = 0; i < count; i++)
            slots[i] = Slot(group, "Slot_" + i);
        return slots;
    }

    private static StageHotbarSlot Slot(RectTransform group, string name)
    {
        RectTransform rect = UiKit.Rect(name, group);
        rect.sizeDelta = new Vector2(76, 76);
        UiFrameButton button = UiKit.Button(rect, string.Empty, UiKit.ButtonKind.Secondary, 15);
        RectTransform iconRect = UiKit.Place(UiKit.Rect("Icon", rect), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f), new Vector2(0, 2), new Vector2(52, 52));
        UnityEngine.UI.Image icon = UiKit.Image(iconRect, null, Color.white, false);
        icon.type = UnityEngine.UI.Image.Type.Simple;
        TMP_Text shortName = UiKit.Text("Name", rect, string.Empty, 18, HudPalette.TextBody, TextAlignmentOptions.Center, true, true);
        UiKit.Fill(shortName.rectTransform, 6);
        TMP_Text key = UiKit.Text("Key", rect, string.Empty, 14, HudPalette.TextMuted, TextAlignmentOptions.TopLeft, false, true);
        UiKit.TopLeft(key.rectTransform, 6, 4, 30, 20);
        TMP_Text cost = UiKit.Text("Cost", rect, string.Empty, 15, HudPalette.Accent, TextAlignmentOptions.BottomRight, true, true);
        UiKit.Place(cost.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-6, 4), new Vector2(50, 20));
        UnityEngine.UI.Image shade = UiKit.Image(UiKit.Fill(UiKit.Rect("Shade", rect), 3), null, new Color(0f, 0f, 0f, 0.55f), false);
        StageHotbarSlot slot = rect.gameObject.AddComponent<StageHotbarSlot>();
        UiKit.Wire(slot, "button", button);
        UiKit.Wire(slot, "icon", icon);
        UiKit.Wire(slot, "shortName", shortName);
        UiKit.Wire(slot, "key", key);
        UiKit.Wire(slot, "cost", cost);
        UiKit.Wire(slot, "shade", shade);
        return slot;
    }

    /// <summary>재도전 버튼을 설정 창의 (스테이지 씬에서 숨기는) 다시 시작 자리로 옮긴다. 클릭 배선은 HUD 그대로다.</summary>
    private static string MoveRetryIntoSettings(Scene scene, SerializedObject wiring)
    {
        Component retryButton = wiring.FindProperty("retry").objectReferenceValue as Component;
        if (retryButton == null)
            throw new InvalidOperationException("HUD 재도전 버튼 배선이 없습니다(설정 창을 다시 만들었다면 StageDefense 생성기부터 다시 돌리세요)");
        RectTransform retry = (RectTransform)retryButton.transform;
        SettingsScreenView settings = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<SettingsScreenView>(true)).Single();
        Component restartButton = new SerializedObject(settings).FindProperty("restartButton").objectReferenceValue as Component;
        if (restartButton == null)
            throw new InvalidOperationException("설정 창의 다시 시작 버튼 배선이 없습니다");
        RectTransform restart = (RectTransform)restartButton.transform;
        retry.SetParent(restart.parent, false);
        retry.anchorMin = restart.anchorMin;
        retry.anchorMax = restart.anchorMax;
        retry.pivot = restart.pivot;
        retry.sizeDelta = restart.sizeDelta;
        retry.anchoredPosition = restart.anchoredPosition;
        retry.SetSiblingIndex(restart.GetSiblingIndex() + 1);
        return "재도전 → 설정 창";
    }

    private static void Hide(SerializedObject wiring, string field)
    {
        Component target = (Component)wiring.FindProperty(field).objectReferenceValue;
        if (target == null)
            throw new InvalidOperationException(field + " 배선이 없습니다");
        target.gameObject.SetActive(false);
    }

    private static void MoveBottomRight(SerializedObject wiring, string field, float x, float y, float width, float height)
    {
        Component target = (Component)wiring.FindProperty(field).objectReferenceValue;
        if (target == null)
            throw new InvalidOperationException(field + " 배선이 없습니다");
        UiKit.BottomRight((RectTransform)target.transform, x, y, width, height);
    }

    private static void MoveTopRight(RectTransform parent, string name, float x, float y, float width, float height)
    {
        RectTransform child = parent.Find(name) as RectTransform;
        if (child == null)
            throw new InvalidOperationException(name + "을 찾지 못했습니다");
        UiKit.TopRight(child, x, y, width, height);
    }

    private static void SetBool(UnityEngine.Object target, string field, bool value)
    {
        SerializedObject so = new(target);
        SerializedProperty property = so.FindProperty(field) ?? throw new InvalidOperationException(field + " 필드가 없습니다");
        property.boolValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static UiFrameButton Tab(RectTransform parent, string name, string label, float x) =>
        UiKit.Button(UiKit.TopLeft(UiKit.Rect(name, parent), x, 0, 120, 34), label, UiKit.ButtonKind.Secondary, 15);

    private static void Move(RectTransform parent, string name, float x, float y, float width, float height)
    {
        RectTransform child = parent.Find(name) as RectTransform;
        if (child == null)
            throw new InvalidOperationException(name + "을 찾지 못했습니다");
        UiKit.TopLeft(child, x, y, width, height);
    }

    private static void Remove(Transform parent, string name)
    {
        Transform child = parent.Find(name);
        if (child != null)
            UnityEngine.Object.DestroyImmediate(child.gameObject);
    }
}
