using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// DungeonRun 준비 화면의 고정 안내 글을 끈다(2026-11-12 사용자 목업 승인 "준비 화면 양쪽 글" 줄이기).
/// 위쪽 조작 안내 문장, 왼쪽 상성 줄, 아래 팁 띠다. 오브젝트는 지우지 않고 꺼 두어 되살리기 쉽게 한다.
/// Unity CLI: <c>RunHudTrimBuilder.Apply()</c>.
/// </summary>
public static class RunHudTrimBuilder
{
    private const string ScenePath = "Assets/00.Scenes/DungeonRun.unity";

    private static readonly string[] HiddenPaths =
    {
        "PreparationTop/Instruction",
        "PreparationParty/AffinityHelp",
        "PreparationTip/Tip",
    };

    [MenuItem("Tools/Dungeon Keeper/Trim Run HUD Text")]
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
        string result = string.Empty;
        foreach (string path in HiddenPaths)
        {
            Transform target = hud.Find(path);
            if (target == null) { result += " missing:" + path; continue; }
            target.gameObject.SetActive(false);
            result += " off:" + path;
        }
        result += ShrinkPartyCards(hud);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return result.Trim();
    }

    // 적 카드가 "이름 ● → 노리는 곳" 한 줄이 되어 높이를 한 줄에 맞춘다(2026-11-12 사용자 요청 "적 카드 높이 줄이기").
    private const int PartyCardCount = 5;
    private const float PartyTop = 66f;
    private const float PartyCardHeight = 44f;
    private const float PartyCardGap = 10f;
    private const float PartyTextInset = 8f;

    private static string ShrinkPartyCards(Transform hud)
    {
        string result = string.Empty;
        for (int i = 0; i < PartyCardCount; i++)
        {
            var card = hud.Find($"PreparationParty/Party_{i}") as RectTransform;
            if (card == null) { result += $" missing:Party_{i}"; continue; }
            card.anchoredPosition = new Vector2(card.anchoredPosition.x, -(PartyTop + i * (PartyCardHeight + PartyCardGap)));
            card.sizeDelta = new Vector2(card.sizeDelta.x, PartyCardHeight);
            if (card.Find("Description") is RectTransform text)
            {
                text.anchoredPosition = new Vector2(text.anchoredPosition.x, -PartyTextInset);
                text.sizeDelta = new Vector2(text.sizeDelta.x, PartyCardHeight - PartyTextInset * 2f);
            }
        }
        return result + $" party cards {PartyCardHeight}px";
    }
}
