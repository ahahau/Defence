using Code.Run;
using Code.UI;
using Code.UI.Run;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// DungeonRun 씬 HUD 맨 위에 첫 판 안내(깜빡이는 테두리 + 노란 한 줄)를 놓고 전투 디렉터에 잇는다
/// (2026-11-12 사용자 목업 승인). 다시 돌리면 있던 안내를 지우고 새로 놓는다.
/// Unity CLI: <c>RunCoachBuilder.Apply()</c>.
/// </summary>
public static class RunCoachBuilder
{
    private const string ScenePath = "Assets/00.Scenes/DungeonRun.unity";
    private const string RootName = "Coach";
    // 1920×1080 기준. 처음 34/18은 1280 화면에서 글이 읽히지 않았다.
    private const float TipHeight = 48f;
    private const float TipFont = 24f;

    [MenuItem("Tools/Dungeon Keeper/Build Run Coach")]
    public static string Apply()
    {
        if (EditorSceneManager.GetActiveScene().path != ScenePath) EditorSceneManager.OpenScene(ScenePath);
        var scene = EditorSceneManager.GetActiveScene();
        Transform hud = null;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name == "RunHud") hud = root.transform;
            else if (hud == null) hud = root.transform.Find("RunHud");
        }
        if (hud == null) return "RunHud not found";
        var director = Object.FindAnyObjectByType<RunBattleDirector>(FindObjectsInactive.Include);
        if (director == null) return "RunBattleDirector not found";
        var trap = Object.FindAnyObjectByType<RunTrapControlView>(FindObjectsInactive.Include);
        if (trap == null) return "RunTrapControlView not found";

        Transform old = hud.Find(RootName);
        if (old != null) Object.DestroyImmediate(old.gameObject);

        RectTransform coachRoot = UiKit.Fill(UiKit.Rect(RootName, hud));
        coachRoot.SetAsLastSibling();
        var view = coachRoot.gameObject.AddComponent<RunCoachView>();

        RectTransform frame = UiKit.Rect("Frame", coachRoot);
        UiKit.Place(frame, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(100f, 40f));
        UiKit.Outline(frame, HudPalette.BorderAccent);
        var group = frame.gameObject.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.interactable = false;

        RectTransform tip = UiKit.Rect("Tip", coachRoot);
        UiKit.Place(tip, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(200f, TipHeight));
        UiKit.Panel(tip, HudPalette.Accent, HudPalette.Primary);
        TextMeshProUGUI text = UiKit.Text("Text", tip, string.Empty, TipFont, HudPalette.Deep, TextAlignmentOptions.Center, true, true);
        UiKit.Fill(text.rectTransform);

        // 안내가 누를 곳을 덮으므로 클릭을 하나도 받지 않게 한다.
        foreach (UnityEngine.UI.Graphic graphic in coachRoot.GetComponentsInChildren<UnityEngine.UI.Graphic>(true))
            graphic.raycastTarget = false;

        UiKit.Wire(view, "frame", frame);
        UiKit.Wire(view, "frameGroup", group);
        UiKit.Wire(view, "tip", tip);
        UiKit.Wire(view, "tipText", text);
        UiKit.Wire(director, "coach", view);
        UiKit.Wire(director, "trapControl", trap);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return "coach built under " + hud.name;
    }
}
