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

    private static void Wire(GameObject instance)
    {
        var settings = instance.GetComponent<SettingsScreenView>();
        UiKit.Wire(settings, "speedController", Object.FindAnyObjectByType<GameSpeedController>(FindObjectsInactive.Include));

        var router = instance.GetComponent<UiEscapeRouter>();
        UiKit.Wire(router, "settings", settings);
        UiKit.Wire(router, "dialogue", Object.FindAnyObjectByType<DialogueView>(FindObjectsInactive.Include));
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
