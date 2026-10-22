using Code.Manager;
using Code.UI;
using TMPro;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 보스 배너(uGUI, A안) 프리팹을 만들고 SampleScene에 놓는다. 화면 위 가운데 띠 880×118(2026-10-22 사용자 승인 목업).
/// 캔버스 36 — HUD(상태 창 30) 위, 상인·고용 창(40)과 모달 아래. 클릭을 받지 않는다(바탕도 raycast 없음, 묶음 blocksRaycasts 끔).
/// 세 줄: 종류·일차(테두리 색) / 보스 이름 / 경고 문구. 처음에는 꺼 둔다.
/// Unity CLI: <c>BossBannerBuilder.BuildAll()</c>.
/// </summary>
public static class BossBannerBuilder
{
    public const string PrefabPath = "Assets/03.Prefabs/UI/Hud/BossBanner.prefab";
    public const int SortingOrder = 36;
    private const float Width = 880f;
    private const float Height = 118f;
    private const float Top = 28f;

    [MenuItem("Tools/Dungeon Keeper/Build Boss Banner")]
    public static void BuildAll()
    {
        BuildPrefab();
        UiSceneInstaller.Place(PrefabPath, typeof(BossBannerView), instance =>
        {
            var view = instance.GetComponent<BossBannerView>();
            UiKit.Wire(view, "bossWave", Object.FindAnyObjectByType<BossWavePresenter>(FindObjectsInactive.Include));
        });
    }

    public static void BuildPrefab()
    {
        var rootObject = UiKit.Canvas("BossBanner", SortingOrder);
        var view = rootObject.AddComponent<BossBannerView>();

        var banner = UiKit.Place(UiKit.Rect("Banner", rootObject.transform), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -Top), new Vector2(Width, Height));
        var outline = UiKit.Panel(banner, HudPalette.Surface, HudPalette.Danger);
        var fill = banner.GetComponent<UnityEngine.UI.Image>();
        var group = banner.gameObject.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.interactable = false;

        var caption = Line(banner, "Caption", "보스 습격 · 7일차", 15f, HudPalette.Danger, 12f, 22f, true);
        var title = Line(banner, "Title", "토벌대장", 32f, HudPalette.Text, 34f, 42f, true);
        var subtitle = Line(banner, "Subtitle", "방패 뒤의 적은 어설픈 공격으로는 흠집도 나지 않는다", 16f, HudPalette.TextDim, 80f, 26f, false);

        UiKit.Wire(view, "banner", banner);
        UiKit.Wire(view, "group", group);
        UiKit.Wire(view, "fill", fill);
        UiKit.Wire(view, "outline", outline);
        UiKit.Wire(view, "caption", caption);
        UiKit.Wire(view, "title", title);
        UiKit.Wire(view, "subtitle", subtitle);
        banner.gameObject.SetActive(false);

        UiKit.EnsureFolder("Assets/03.Prefabs/UI/Hud");
        PrefabUtility.SaveAsPrefabAsset(rootObject, PrefabPath, out var saved);
        Object.DestroyImmediate(rootObject);
        if (!saved)
            Debug.LogError($"{nameof(BossBannerBuilder)}: {PrefabPath} 저장 실패");
    }

    /// <summary>가운데 맞춤 한 줄(배너 폭에서 좌우 24씩 뺀 폭).</summary>
    private static TextMeshProUGUI Line(RectTransform parent, string name, string value, float size, Color color, float y, float height, bool bold)
    {
        var text = UiKit.Text(name, parent, value, size, color, TextAlignmentOptions.Center, bold, true);
        UiKit.TopLeft(text.rectTransform, 24f, y, Width - 48f, height);
        return text;
    }
}
