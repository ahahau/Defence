using Code.UI;
using TMPro;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 이미 만든 UI 프리팹(<c>Assets/03.Prefabs/UI</c>)의 패널 그림을 픽셀 테두리(<see cref="UiKit.FrameFillSprite"/>·
/// <see cref="UiKit.FrameLineSprite"/>)로, 글자 하나뿐인 버튼을 팩 버튼 면(<see cref="UiKit.ApplyButtonFaces"/>)으로 바꾸고,
/// HUD 배속 버튼에 기호·금화 앞에 금화 그림을 넣는다. 여러 번 돌려도 같은 결과다.
///
/// 생성기로 프리팹을 다시 만들면 씬의 인스턴스를 지우고 새로 놓아야 해서(<see cref="UiSceneInstaller"/>) 씬이 바뀐다.
/// 여기서는 프리팹 안의 그림만 그 자리에서 바꾸므로 씬과 씬 배선은 그대로다. 생성기도 같은 그림을 쓰도록 맞춰 두었다.
/// 메뉴: Tools/Dungeon Keeper/Refresh UI Skin. Unity CLI: <c>UiSkinRefresher.RefreshAll()</c>.
/// </summary>
public static class UiSkinRefresher
{
    private const string UiPrefabFolder = "Assets/03.Prefabs/UI";

    [MenuItem("Tools/Dungeon Keeper/Refresh UI Skin")]
    public static string RefreshAll()
    {
        var oldFill = UiKit.FillSprite;
        var oldLine = UiKit.LineSprite;
        var frameFill = UiKit.FrameFillSprite;
        var frameLine = UiKit.FrameLineSprite;
        var report = new System.Text.StringBuilder();

        foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { UiPrefabFolder }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var frames = 0;
                foreach (var image in root.GetComponentsInChildren<UnityEngine.UI.Image>(true))
                {
                    if (image.sprite == oldLine && image.name == "Outline")
                    {
                        image.sprite = frameLine;
                        UiKit.ApplyPixelFrame(image);
                        frames++;
                    }
                    else if (image.sprite == oldFill && HasOutline(image.transform, oldLine, frameLine))
                    {
                        // 테두리가 붙은 바탕만 바꾼다. 막대·칩·스크롤바는 둥근 그림 그대로다.
                        image.sprite = frameFill;
                        UiKit.ApplyPixelFrame(image);
                        frames++;
                    }
                }

                var hudParts = RefreshHud(root);
                // HUD 기호를 넣은 뒤에 버튼 면을 입혀야 기호도 어두운 색이 된다.
                var faces = UiKit.ApplyButtonFaces(root);
                if (frames == 0 && hudParts == 0 && faces == 0)
                    continue;

                PrefabUtility.SaveAsPrefabAsset(root, path);
                report.AppendLine($"{path}: 테두리 {frames}, HUD {hudParts}, 버튼 면 {faces}");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"{nameof(UiSkinRefresher)}:\n{report}");
        return report.ToString();
    }

    private static bool HasOutline(Transform host, Sprite oldLine, Sprite frameLine)
    {
        var outline = host.Find("Outline");
        if (outline == null || !outline.TryGetComponent<UnityEngine.UI.Image>(out var image))
            return false;
        return image.sprite == oldLine || image.sprite == frameLine;
    }

    /// <summary>HUD 프리팹이면 배속 기호와 금화 그림을 넣는다(이미 있으면 건너뜀). 넣은 개수를 돌려준다.</summary>
    private static int RefreshHud(GameObject root)
    {
        var top = root.GetComponentInChildren<TopHudView>(true);
        if (top == null)
            return 0;

        var added = 0;
        var so = new SerializedObject(top);
        foreach (var field in new[] { "pauseButton", "normalButton", "fastButton" })
        {
            var button = so.FindProperty(field)?.objectReferenceValue as UiFrameButton;
            if (button == null || button.transform.Find("Glyph") != null)
                continue;
            GameplayHudBuilder.AddSpeedGlyph(button);
            added++;
        }

        var gold = so.FindProperty("goldValue")?.objectReferenceValue as TMP_Text;
        if (gold != null && gold.transform.parent is RectTransform ledger && ledger.Find("Coin") == null)
        {
            GameplayHudBuilder.AddGoldCoin(ledger, gold);
            added++;
        }

        return added;
    }
}
