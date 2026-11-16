using Code.Dialogue;
using Code.Manager;
using Code.UI;
using Code.UI.RoomManagement;
using TMPro;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 게임 화면 설정 창(uGUI, A안)과 ESC 라우터를 만들어 SampleScene에 놓는다. 화면 가운데 카드(캔버스 90 — 정산표 84 위, 팝업 100 아래).
/// 라우터(<see cref="UiEscapeRouter"/>)는 같은 오브젝트에 붙고, 씬의 창들을 위에서부터 배선한다. 오른쪽 위 톱니(<c>GameplayHudBuilder</c>)도 여기서 잇는다.
/// Unity CLI: <c>SettingsBuilder.BuildAll()</c>. HUD·방 관리 화면을 다시 놓았으면 그 뒤에 돌린다(그 인스턴스를 가리켜야 한다).
/// </summary>
public static class SettingsBuilder
{
    public const string PrefabPath = "Assets/03.Prefabs/UI/Settings/SettingsScreen.prefab";
    private const float CardWidth = 520f;
    private const float CardHeight = 344f;
    private const float Pad = 26f;

    [MenuItem("Tools/Dungeon Keeper/Build Settings Screen")]
    public static void BuildAll()
    {
        BuildPrefab();
        UiSceneInstaller.Place(PrefabPath, typeof(SettingsScreenView), Wire);
    }

    private const string TitleScenePath = "Assets/00.Scenes/Start.unity";
    private const string RunScenePath = "Assets/00.Scenes/DungeonRun.unity";
    private const int DemoSortingOrder = 110;

    /// <summary>
    /// 던전 FTL 시연의 타이틀(Start)과 DungeonRun에 같은 설정 창을 놓는다(2026-11-12 사용자 결정 "새 설정 창으로 바꾸기").
    /// 타이틀의 옛 설정 창(<c>SettingsPanelView</c>, 크기 0으로 놓여 보이지 않았다)은 꺼서 ESC를 둘이 받지 않게 한다.
    /// "다시 시작"은 예전 경영 판 저장을 지우는 버튼이라 두 씬 모두 숨긴다. Unity CLI: <c>SettingsBuilder.BuildRunScenes()</c>.
    /// </summary>
    [MenuItem("Tools/Dungeon Keeper/Build Settings Screen (Run Demo)")]
    public static string BuildRunScenes()
    {
        BuildPrefab();
        UiSceneInstaller.Place(PrefabPath, typeof(SettingsScreenView), WireTitle, TitleScenePath);
        UiSceneInstaller.Place(PrefabPath, typeof(SettingsScreenView), WireRun, RunScenePath);
        return "settings placed in Start and DungeonRun";
    }

    private static void WireTitle(GameObject instance)
    {
        var settings = WireDemo(instance);
        UiKit.Wire(Object.FindAnyObjectByType<TitleMenuActions>(FindObjectsInactive.Include), "settings", settings);
        var old = Object.FindAnyObjectByType<SettingsPanelView>(FindObjectsInactive.Include);
        if (old != null) old.gameObject.SetActive(false);
    }

    private static void WireRun(GameObject instance)
    {
        var settings = WireDemo(instance);
        UiKit.Wire(Object.FindAnyObjectByType<Code.Run.RunBattleDirector>(FindObjectsInactive.Include), "settings", settings);
        WireRunEscape(settings);
    }

    /// <summary>
    /// DungeonRun 라우터 목록: 설정(110) → 상인 고르기 판·원정 파견 판(60, 함께 뜨지 않음) → 구역 지도(50). 위에 그려지는 창부터다.
    /// 상인 좌판 자체는 "떠나기"로만 끝나고 ESC는 고르기 판만 닫는다(<c>RunMerchantView</c>).
    /// 이미 놓인 씬에는 이 함수만 불러 다시 잇는다(설정 프리팹을 다시 만들면 내부 ID가 바뀐다).
    /// </summary>
    public static void WireRunEscape(SettingsScreenView settings)
    {
        var router = settings.GetComponent<UiEscapeRouter>();
        UiKit.WireArray(router, "windows", new Object[]
        {
            settings,
            Object.FindAnyObjectByType<Code.UI.Run.RunMerchantView>(FindObjectsInactive.Include),
            Object.FindAnyObjectByType<Code.UI.Run.RunRaidSelectView>(FindObjectsInactive.Include),
            Object.FindAnyObjectByType<Code.UI.Run.RunRouteMapView>(FindObjectsInactive.Include),
        });
    }

    /// <summary>시연 씬 공통 배선. 라우터 목록은 설정 창만 두고, DungeonRun은 <see cref="WireRunEscape"/>가 지도·상인을 더한다.</summary>
    private static SettingsScreenView WireDemo(GameObject instance)
    {
        var settings = instance.GetComponent<SettingsScreenView>();
        var so = new SerializedObject(settings);
        so.FindProperty("allowRunRestart").boolValue = false;
        so.ApplyModifiedPropertiesWithoutUndo();
        // 타이틀 화면 캔버스가 100이라 기본값 90으로는 그 밑에 깔려 보이지 않았다. 시연 씬은 맨 위로 올린다.
        var canvas = instance.GetComponent<Canvas>();
        canvas.sortingOrder = DemoSortingOrder;
        EditorUtility.SetDirty(canvas);
        var router = instance.GetComponent<UiEscapeRouter>();
        UiKit.Wire(router, "settings", settings);
        UiKit.WireArray(router, "windows", new Object[] { settings });
        return settings;
    }

    /// <summary>
    /// 이미 놓인 설정 화면의 ESC 라우터·톱니 배선을 다시 한다. 라우터 목록에 든 창을 다시 놓은 생성기가
    /// 자기 <c>UiSceneInstaller.Place</c> 배선 안에서 부른다(옛 인스턴스를 가리키던 칸이 비지 않게).
    /// </summary>
    public static void WireEscapeRouter()
    {
        var settings = Object.FindAnyObjectByType<SettingsScreenView>(FindObjectsInactive.Include);
        if (settings == null)
        {
            Debug.LogWarning($"{nameof(SettingsBuilder)}: 설정 화면이 씬에 없어 ESC 배선을 건너뜁니다. {nameof(BuildAll)}()을 돌리세요.");
            return;
        }

        Wire(settings.gameObject);
    }

    private static void Wire(GameObject instance)
    {
        var settings = instance.GetComponent<SettingsScreenView>();
        UiKit.Wire(settings, "speedController", Object.FindAnyObjectByType<GameSpeedController>(FindObjectsInactive.Include));

        var router = instance.GetComponent<UiEscapeRouter>();
        UiKit.Wire(router, "settings", settings);
        UiKit.Wire(router, "dialogue", Object.FindAnyObjectByType<DialogueView>(FindObjectsInactive.Include));
        UiKit.Wire(router, "policyChoice", Object.FindAnyObjectByType<PolicyChoiceScreenView>(FindObjectsInactive.Include));
        UiKit.Wire(router, "runEnd", Object.FindAnyObjectByType<RunEndScreenView>(FindObjectsInactive.Include));
        // 위에 그려지는 창부터. 한 번의 ESC는 이 순서로 처음 열린 창 하나만 닫는다.
        UiKit.WireArray(router, "windows", new Object[]
        {
            settings,
            Object.FindAnyObjectByType<SettlementScreenView>(FindObjectsInactive.Include),
            Object.FindAnyObjectByType<MerchantScreenView>(FindObjectsInactive.Include),
            Object.FindAnyObjectByType<RecruitScreenView>(FindObjectsInactive.Include),
            Object.FindAnyObjectByType<TreasuryView>(FindObjectsInactive.Include),
            Object.FindAnyObjectByType<UnitStatusView>(FindObjectsInactive.Include),
            Object.FindAnyObjectByType<EnemyStatusView>(FindObjectsInactive.Include),
            Object.FindAnyObjectByType<RoomManagementScreen>(FindObjectsInactive.Include)
        });

        UiKit.Wire(Object.FindAnyObjectByType<TopHudView>(FindObjectsInactive.Include), "settings", settings);
    }

    public static void BuildPrefab()
    {
        var canvas = UiKit.Canvas("SettingsScreen", 90);
        var view = canvas.AddComponent<SettingsScreenView>();
        canvas.AddComponent<UiEscapeRouter>();

        var root = UiKit.Fill(UiKit.Rect("Root", canvas.transform));
        UiKit.Image(root, null, HudPalette.Scrim, true);

        var card = UiKit.Place(UiKit.Rect("Card", root), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(CardWidth, CardHeight));
        UiKit.Panel(card, HudPalette.Surface, HudPalette.Border, true);

        Label(card, "Title", "설정", 24f, HudPalette.Accent, Pad, 18f, 200f, 36f, true);
        var closeRect = UiKit.TopRight(UiKit.Rect("Close", card), Pad, 20f, 72f, 32f);
        var close = UiKit.Button(closeRect, "닫기", UiKit.ButtonKind.Secondary, 14f);
        Divider(card, 64f);

        var (sfx, sfxValue) = VolumeRow(card, "Sfx", "효과음", 84f);
        var (music, musicValue) = VolumeRow(card, "Music", "음악", 130f);
        Divider(card, 182f);

        var fullscreenRect = UiKit.TopLeft(UiKit.Rect("Fullscreen", card), Pad, 198f, 220f, 44f);
        var fullscreen = UiKit.Button(fullscreenRect, "전체 화면: 켬", UiKit.ButtonKind.Secondary, 15f);
        // 전체 화면 옆 빈자리(2026-11-12 사용자 목업 승인). 던전 FTL 첫 판 안내 기록을 지운다.
        var coachRect = UiKit.TopLeft(UiKit.Rect("CoachReset", card), Pad + 236f, 198f, CardWidth - Pad * 2f - 236f, 44f);
        var coachReset = UiKit.Button(coachRect, "첫 판 안내 다시 보기", UiKit.ButtonKind.Secondary, 15f);
        Divider(card, 258f);

        var restartRect = UiKit.Place(UiKit.Rect("Restart", card), Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(Pad, 22f), new Vector2(180f, 48f));
        var restart = UiKit.Button(restartRect, "다시 시작", UiKit.ButtonKind.Danger, 16f);
        var titleRect = UiKit.Place(UiKit.Rect("Title", card), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-Pad, 22f), new Vector2(180f, 48f));
        var title = UiKit.Button(titleRect, "타이틀로", UiKit.ButtonKind.Secondary, 16f);

        UiKit.Wire(view, "root", root.gameObject);
        UiKit.Wire(view, "sfxSlider", sfx);
        UiKit.Wire(view, "sfxValue", sfxValue);
        UiKit.Wire(view, "musicSlider", music);
        UiKit.Wire(view, "musicValue", musicValue);
        UiKit.Wire(view, "fullscreenButton", fullscreen);
        UiKit.Wire(view, "restartButton", restart);
        UiKit.Wire(view, "titleButton", title);
        UiKit.Wire(view, "closeButton", close);
        UiKit.Wire(view, "coachResetButton", coachReset);
        root.gameObject.SetActive(false);

        UiKit.EnsureFolder("Assets/03.Prefabs/UI/Settings");
        UiKit.ApplyButtonFaces(canvas);
        PrefabUtility.SaveAsPrefabAsset(canvas, PrefabPath, out var saved);
        Object.DestroyImmediate(canvas);
        if (!saved)
            Debug.LogError($"{nameof(SettingsBuilder)}: {PrefabPath} 저장 실패");
    }

    /// <summary>이름 · 0~100 슬라이더 · 퍼센트 한 줄.</summary>
    private static (UnityEngine.UI.Slider slider, TextMeshProUGUI value) VolumeRow(RectTransform card, string name, string label, float y)
    {
        Label(card, name + "Label", label, 16f, HudPalette.TextBody, Pad, y, 110f, 32f);
        var sliderRect = UiKit.TopLeft(UiKit.Rect(name + "Slider", card), Pad + 120f, y, 280f, 32f);
        var slider = UiKit.Slider(sliderRect);
        slider.minValue = 0f;
        slider.maxValue = 100f;
        slider.value = 100f;
        var value = Label(card, name + "Value", "100%", 16f, HudPalette.Accent, Pad + 410f, y, 58f, 32f, true, true, TextAlignmentOptions.MidlineRight);
        return (slider, value);
    }

    private static TextMeshProUGUI Label(RectTransform parent, string name, string value, float size, Color color,
        float x, float y, float width, float height, bool bold = false, bool singleLine = true,
        TextAlignmentOptions alignment = TextAlignmentOptions.MidlineLeft)
    {
        var text = UiKit.Text(name, parent, value, size, color, alignment, bold, singleLine);
        UiKit.TopLeft(text.rectTransform, x, y, width, height);
        return text;
    }

    private static void Divider(RectTransform card, float y)
    {
        var line = UiKit.TopLeft(UiKit.Rect("Divider", card), Pad, y, CardWidth - Pad * 2f, 1f);
        UiKit.Image(line, null, HudPalette.BorderQuiet, false);
    }
}
