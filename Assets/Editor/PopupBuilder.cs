using Code.UI;
using Code.UI.Popup;
using TMPro;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 범용 팝업 화면(uGUI, A안) 프리팹을 만들고 SampleScene에 놓는다. HUD(10)보다 위 캔버스(100)다.
/// Unity CLI: <c>PopupBuilder.BuildAll()</c>.
///
/// 덮개(화면 전체) 위 가운데 카드 560×300: 제목, 본문(가변), 비용 줄, 경고 줄(비어도 자리 유지), 오른쪽 아래 버튼 최대 3개(160×48).
/// </summary>
public static class PopupBuilder
{
    public const string PrefabPath = "Assets/03.Prefabs/UI/Popup/Popup.prefab";
    private const float CardWidth = 560f;
    private const float CardHeight = 300f;

    [MenuItem("Tools/Dungeon Keeper/Build Popup")]
    public static void BuildAll()
    {
        BuildPrefab();
        UiSceneInstaller.Place(PrefabPath, typeof(PopupView), null);
    }

    public static void BuildPrefab()
    {
        var root = UiKit.Canvas("Popup", 100);
        var view = root.AddComponent<PopupView>();

        var panel = UiKit.Fill(UiKit.Rect("Root", root.transform));
        UiKit.Image(panel, null, HudPalette.Scrim, true);

        var card = UiKit.Place(UiKit.Rect("Card", panel), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(CardWidth, CardHeight));
        UiKit.Panel(card, HudPalette.Surface, HudPalette.Border, true);

        var title = UiKit.Text("Title", card, "제목", 24f, HudPalette.Accent, TextAlignmentOptions.Left, true, true);
        UiKit.TopLeft(title.rectTransform, 26f, 22f, CardWidth - 52f, 32f);
        var divider = UiKit.TopLeft(UiKit.Rect("Divider", card), 26f, 60f, CardWidth - 52f, 2f);
        UiKit.Image(divider, null, HudPalette.BorderQuiet, false);

        var message = UiKit.Text("Message", card, string.Empty, 17f, HudPalette.TextBody, TextAlignmentOptions.TopLeft);
        UiKit.Place(message.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 1f), new Vector2(0f, -74f), new Vector2(-52f, -74f - 122f));
        message.overflowMode = TextOverflowModes.Truncate;

        var cost = UiKit.Text("Cost", card, string.Empty, 16f, HudPalette.Accent, TextAlignmentOptions.Left, true, true);
        UiKit.Place(cost.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(26f, 88f), new Vector2(260f, 22f));
        var warning = UiKit.Text("Warning", card, string.Empty, 15f, HudPalette.Loss, TextAlignmentOptions.Right, false, true);
        UiKit.Place(warning.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-26f, 88f), new Vector2(250f, 22f));

        var buttons = new Object[PopupRequest.MaxButtons];
        for (var i = 0; i < buttons.Length; i++)
        {
            // 오른쪽 아래 기준. 쓰는 버튼 수에 맞춰 PopupView가 오른쪽으로 붙여 다시 놓는다.
            var x = -26f - (buttons.Length - 1 - i) * 168f;
            var rt = UiKit.Place(UiKit.Rect($"Button{i}", card), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(x, 22f), new Vector2(160f, 48f));
            buttons[i] = UiKit.Button(rt, "확인", UiKit.ButtonKind.Secondary, 17f);
        }

        UiKit.Wire(view, "root", panel.gameObject);
        UiKit.Wire(view, "title", title);
        UiKit.Wire(view, "message", message);
        UiKit.Wire(view, "cost", cost);
        UiKit.Wire(view, "warning", warning);
        UiKit.WireArray(view, "buttons", buttons);
        panel.gameObject.SetActive(false);

        UiKit.EnsureFolder("Assets/03.Prefabs/UI/Popup");
        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out var saved);
        Object.DestroyImmediate(root);
        if (!saved)
            Debug.LogError($"{nameof(PopupBuilder)}: {PrefabPath} 저장 실패");
    }
}
