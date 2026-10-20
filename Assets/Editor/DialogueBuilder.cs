using Code.Dialogue;
using Code.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 사건·튜토리얼 대화 상자(uGUI, A안) 프리팹을 만들고 SampleScene에 놓는다. 캔버스 80(HUD 10 위, 팝업 100 아래).
/// Unity CLI: <c>DialogueBuilder.BuildAll()</c>.
///
/// 아래 가운데(바닥에서 176, 방 관리 줄 위) 상자 1120×338: 말하는 사람 이름표 240×38, 카드(제목·진행·닫기, 본문 왼쪽,
/// 선택지 오른쪽 430 폭 최대 5칸, 선택지가 없을 때 오른쪽 아래 "다음").
/// </summary>
public static class DialogueBuilder
{
    public const string PrefabPath = "Assets/03.Prefabs/UI/Dialogue/Dialogue.prefab";
    public const int MaxChoices = 5;
    private const float BoxWidth = 1120f;
    private const float BoxHeight = 338f;
    private const float TagHeight = 38f;
    private const float Pad = 28f;
    private const float ChoiceWidth = 430f;

    [MenuItem("Tools/Dungeon Keeper/Build Dialogue")]
    public static void BuildAll()
    {
        BuildPrefab();
        UiSceneInstaller.Place(PrefabPath, typeof(DialogueUiView), instance =>
        {
            var view = instance.GetComponent<DialogueUiView>();
            var source = Object.FindAnyObjectByType<DialogueView>(FindObjectsInactive.Include);
            UiKit.Wire(view, "dialogueView", source);
        });
    }

    public static void BuildPrefab()
    {
        var canvas = UiKit.Canvas("Dialogue", 80);
        var view = canvas.AddComponent<DialogueUiView>();

        var root = UiKit.Fill(UiKit.Rect("Root", canvas.transform));
        var scrim = UiKit.Fill(UiKit.Rect("Scrim", root));
        UiKit.Image(scrim, null, HudPalette.Scrim, true);
        var spotlight = UiKit.SpotlightDim(root);

        var box = UiKit.Place(UiKit.Rect("Box", root), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 176f), new Vector2(BoxWidth, BoxHeight));

        var tag = UiKit.TopLeft(UiKit.Rect("SpeakerTag", box), Pad, 0f, 240f, TagHeight);
        UiKit.Panel(tag, HudPalette.Chip, HudPalette.Border, true);
        var speaker = UiKit.Text("Speaker", tag, "임프 참모", 17f, HudPalette.Accent, TextAlignmentOptions.MidlineLeft, true, true);
        UiKit.Fill(speaker.rectTransform, 0f);
        speaker.rectTransform.offsetMin = new Vector2(14f, 0f);
        speaker.rectTransform.offsetMax = new Vector2(-14f, 0f);

        var card = UiKit.Place(UiKit.Rect("Card", box), Vector2.zero, Vector2.one, new Vector2(0.5f, 0f), Vector2.zero, new Vector2(0f, -TagHeight));
        UiKit.Panel(card, HudPalette.Surface, HudPalette.Border, true);
        var cardHeight = BoxHeight - TagHeight;
        var inner = BoxWidth - Pad * 2f;

        var title = UiKit.Text("Title", card, "제목", 20f, HudPalette.Text, TextAlignmentOptions.MidlineLeft, true, true);
        UiKit.TopLeft(title.rectTransform, Pad, 18f, inner - 80f - 10f - 72f - 10f, 30f);
        var progress = UiKit.Text("Progress", card, "1/3", 14f, HudPalette.TextDim, TextAlignmentOptions.MidlineRight, false, true);
        UiKit.TopRight(progress.rectTransform, Pad + 72f + 10f, 18f, 80f, 30f);

        var closeRect = UiKit.TopRight(UiKit.Rect("Close", card), Pad, 19f, 72f, 28f);
        var close = UiKit.Button(closeRect, "닫기", UiKit.ButtonKind.Secondary, 14f);
        var closeGroup = closeRect.gameObject.AddComponent<CanvasGroup>();

        var divider = UiKit.TopLeft(UiKit.Rect("Divider", card), Pad, 56f, inner, 2f);
        UiKit.Image(divider, null, HudPalette.BorderQuiet, false);

        var contentTop = 66f;
        var contentHeight = cardHeight - contentTop - 18f;
        var body = UiKit.Text("Body", card, string.Empty, 18f, HudPalette.TextBody, TextAlignmentOptions.TopLeft);
        UiKit.TopLeft(body.rectTransform, Pad, contentTop, inner - ChoiceWidth - 18f, contentHeight);
        body.lineSpacing = 8f;

        var column = UiKit.TopRight(UiKit.Rect("Choices", card), Pad, contentTop, ChoiceWidth, contentHeight);
        var layout = column.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.childAlignment = TextAnchor.LowerCenter;
        layout.spacing = 8f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        var choices = new Object[MaxChoices];
        for (var i = 0; i < MaxChoices; i++)
            choices[i] = BuildChoice(column, i);

        var nextRect = UiKit.Place(UiKit.Rect("Next", card), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-Pad, 18f), new Vector2(140f, 40f));
        var next = UiKit.Button(nextRect, "다음", UiKit.ButtonKind.Primary, 17f);

        UiKit.Wire(view, "root", root.gameObject);
        UiKit.Wire(view, "scrim", scrim.gameObject);
        UiKit.Wire(view, "spotlight", spotlight);
        UiKit.Wire(view, "speaker", speaker);
        UiKit.Wire(view, "title", title);
        UiKit.Wire(view, "progress", progress);
        UiKit.Wire(view, "body", body);
        UiKit.WireArray(view, "choices", choices);
        UiKit.Wire(view, "nextButton", next);
        UiKit.Wire(view, "closeButton", close);
        UiKit.Wire(view, "closeGroup", closeGroup);
        root.gameObject.SetActive(false);

        UiKit.EnsureFolder("Assets/03.Prefabs/UI/Dialogue");
        UiKit.ApplyButtonFaces(canvas);
        PrefabUtility.SaveAsPrefabAsset(canvas, PrefabPath, out var saved);
        Object.DestroyImmediate(canvas);
        if (!saved)
            Debug.LogError($"{nameof(DialogueBuilder)}: {PrefabPath} 저장 실패");
    }

    /// <summary>선택지 한 칸: 바깥(세로 배치가 크기를 정함) 안에 미끄러지는 버튼 묶음.</summary>
    private static DialogueChoiceButton BuildChoice(RectTransform column, int index)
    {
        var slot = UiKit.Rect($"Choice{index}", column);
        var element = slot.gameObject.AddComponent<LayoutElement>();
        element.preferredHeight = 58f;
        element.minHeight = 38f;
        var group = slot.gameObject.AddComponent<CanvasGroup>();

        var content = UiKit.Fill(UiKit.Rect("Content", slot));
        var frame = UiKit.Button(content, "선택지", UiKit.ButtonKind.Secondary, 16f);
        var text = frame.Label;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.color = HudPalette.Text;
        var textRect = text.rectTransform;
        textRect.anchorMin = new Vector2(0f, 0.42f);
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(14f, 0f);
        textRect.offsetMax = new Vector2(-14f, -3f);

        var summary = UiKit.Text("Summary", content, "결과 요약", 13f, HudPalette.TextDim, TextAlignmentOptions.MidlineLeft, false, true);
        var summaryRect = summary.rectTransform;
        summaryRect.anchorMin = Vector2.zero;
        summaryRect.anchorMax = new Vector2(1f, 0.42f);
        summaryRect.offsetMin = new Vector2(14f, 4f);
        summaryRect.offsetMax = new Vector2(-14f, 0f);

        var choice = slot.gameObject.AddComponent<DialogueChoiceButton>();
        UiKit.Wire(choice, "frame", frame);
        UiKit.Wire(choice, "text", text);
        UiKit.Wire(choice, "summary", summary);
        UiKit.Wire(choice, "layout", element);
        UiKit.Wire(choice, "content", content);
        UiKit.Wire(choice, "group", group);
        slot.gameObject.SetActive(false);
        return choice;
    }
}
