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
        // 아래 상성표 띠와 여는 버튼(2026-11-12 사용자 선택 "띠와 버튼 모두 빼기"). 상성은 카드의 색 점으로만 보인다.
        "AffinityToggle",
        "BattleLegend",
    };

    // 마우스 설명 카드·정보 창 폭(2026-11-12 사용자 선택 "두 줄짜리 작은 카드"·"같이 줄이기"). 높이는 RunHudView가 글에 맞춘다.
    private const float TooltipWidth = 280f;
    private const float TooltipFont = 16f;
    private const float InfoWidth = 300f;
    // 전투 중 한 줄 알림 칸(2026-11-12 사용자 요청 "알림도 줄이기"). 높이는 RunCrisisView가 글에 맞춘다.
    private const float NoticeWidth = 300f;
    private const float NoticeFont = 17f;

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
        result += DropFromPhaseLists();
        result += ShrinkPartyCards(hud);
        result += ShrinkPanels(hud);
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

    /// <summary>준비·전투 전환 때 켜고 끄는 목록에서 끈 오브젝트를 뺀다. 그대로 두면 전투가 시작될 때 다시 켜진다.</summary>
    private static string DropFromPhaseLists()
    {
        var view = Object.FindAnyObjectByType<Code.UI.Run.RunPreparationView>(FindObjectsInactive.Include);
        if (view == null) return " missing:RunPreparationView";
        var so = new SerializedObject(view);
        int removed = 0;
        foreach (string list in new[] { "preparationParts", "battleParts" })
        {
            SerializedProperty parts = so.FindProperty(list);
            for (int i = parts.arraySize - 1; i >= 0; i--)
            {
                var part = parts.GetArrayElementAtIndex(i).objectReferenceValue as GameObject;
                if (part == null || part.activeSelf) continue;
                if (System.Array.IndexOf(HiddenPaths, part.name) < 0 && !System.Array.Exists(HiddenPaths, p => p.EndsWith("/" + part.name))) continue;
                parts.DeleteArrayElementAtIndex(i);
                removed++;
            }
        }
        so.ApplyModifiedPropertiesWithoutUndo();
        return $" phase lists -{removed}";
    }

    private static string ShrinkPanels(Transform hud)
    {
        string result = string.Empty;
        if (hud.Find("Tooltip") is RectTransform tooltip)
        {
            tooltip.sizeDelta = new Vector2(TooltipWidth, tooltip.sizeDelta.y);
            foreach (TMPro.TMP_Text text in tooltip.GetComponentsInChildren<TMPro.TMP_Text>(true)) text.fontSize = TooltipFont;
            result += $" tooltip {TooltipWidth}";
        }
        else result += " missing:Tooltip";
        if (hud.Find("InfoPanel") is RectTransform info)
        {
            info.sizeDelta = new Vector2(InfoWidth, info.sizeDelta.y);
            result += $" info {InfoWidth}";
        }
        else result += " missing:InfoPanel";
        if (hud.Find("BattleNotice") is RectTransform notice)
        {
            float inset = notice.sizeDelta.x;
            notice.sizeDelta = new Vector2(NoticeWidth, notice.sizeDelta.y);
            if (notice.Find("Body") is RectTransform body)
            {
                // 글 칸은 왼쪽 위 고정이라 좌우 여백(상자 폭 − 글 폭)을 그대로 지킨다.
                inset -= body.sizeDelta.x;
                body.sizeDelta = new Vector2(NoticeWidth - inset, body.sizeDelta.y);
                foreach (TMPro.TMP_Text text in body.GetComponentsInChildren<TMPro.TMP_Text>(true)) text.fontSize = NoticeFont;
            }
            result += $" notice {NoticeWidth}";
        }
        else result += " missing:BattleNotice";
        return result;
    }
}
