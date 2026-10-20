using Code.UI;
using TMPro;
using UnityEditor;
using UnityEngine;

/// <summary>
/// A안("장부형") uGUI 조각 생성기. 모든 화면 생성기가 이것만 써서 모양이 한 벌로 맞는다.
/// 둥근 바탕·테두리 그림(<c>Assets/09.UI/Skin/Ledger/</c>)은 처음 부를 때 코드로 그려 9-slice 스프라이트로 가져온다.
/// 기준 해상도 1920×1080.
/// </summary>
public static class UiKit
{
    public const int UiLayer = 5;
    private const string SkinFolder = "Assets/09.UI/Skin/Ledger";
    private const int SpriteSize = 32;
    private const int Corner = 8;
    private const int LineWidth = 2;

    private static Sprite _fill;
    private static Sprite _line;

    public enum ButtonKind
    {
        Secondary,
        Primary,
        Danger,
        Quiet
    }

    public static Sprite FillSprite => _fill != null ? _fill : _fill = EnsureSprite("RoundFill", false);
    public static Sprite LineSprite => _line != null ? _line : _line = EnsureSprite("RoundLine", true);

    // ── 캔버스 ─────────────────────────────────────────────

    public static GameObject Canvas(string name, int sortingOrder)
    {
        var root = new GameObject(name, typeof(RectTransform)) { layer = UiLayer };
        var canvas = root.AddComponent<UnityEngine.Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;
        var scaler = root.AddComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = UnityEngine.UI.CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        root.AddComponent<UnityEngine.UI.GraphicRaycaster>();
        return root;
    }

    // ── 배치 ───────────────────────────────────────────────

    public static RectTransform Rect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform)) { layer = UiLayer };
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    /// <summary>앵커·피벗을 먼저 정하고 위치·크기를 넣는다(ui-ugui 순서).</summary>
    public static RectTransform Place(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 position, Vector2 size)
    {
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot;
        rt.sizeDelta = size;
        rt.anchoredPosition = position;
        return rt;
    }

    /// <summary>왼쪽 위 기준 고정 크기 배치.</summary>
    public static RectTransform TopLeft(RectTransform rt, float x, float y, float width, float height) =>
        Place(rt, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(x, -y), new Vector2(width, height));

    /// <summary>오른쪽 위 기준 고정 크기 배치. x는 오른쪽 끝에서 안쪽으로.</summary>
    public static RectTransform TopRight(RectTransform rt, float x, float y, float width, float height) =>
        Place(rt, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-x, -y), new Vector2(width, height));

    public static RectTransform BottomRight(RectTransform rt, float x, float y, float width, float height) =>
        Place(rt, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-x, y), new Vector2(width, height));

    public static RectTransform Fill(RectTransform rt, float inset = 0f) =>
        Place(rt, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-inset * 2f, -inset * 2f));

    // ── 면 ─────────────────────────────────────────────────

    /// <summary>둥근 바탕 + 테두리. 테두리 이미지를 돌려준다.</summary>
    public static UnityEngine.UI.Image Panel(RectTransform rt, Color fill, Color border, bool raycast = false)
    {
        var bg = Image(rt, FillSprite, fill, raycast);
        bg.type = UnityEngine.UI.Image.Type.Sliced;
        return Outline(rt, border);
    }

    public static UnityEngine.UI.Image Outline(RectTransform parent, Color border)
    {
        var line = Fill(Rect("Outline", parent));
        var image = Image(line, LineSprite, border, false);
        image.type = UnityEngine.UI.Image.Type.Sliced;
        return image;
    }

    public static UnityEngine.UI.Image Image(RectTransform rt, Sprite sprite, Color color, bool raycast)
    {
        var image = rt.gameObject.AddComponent<UnityEngine.UI.Image>();
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = raycast;
        if (sprite != null)
            image.type = UnityEngine.UI.Image.Type.Sliced;
        return image;
    }

    /// <summary>둥근 막대. 채움은 왼쪽에서 오른쪽으로(<c>fillAmount</c>).</summary>
    public static UnityEngine.UI.Image Bar(RectTransform rt, Color track, Color fill)
    {
        var bg = Image(rt, FillSprite, track, false);
        bg.pixelsPerUnitMultiplier = 2f;
        var inner = Fill(Rect("Fill", rt));
        var image = Image(inner, FillSprite, fill, false);
        image.type = UnityEngine.UI.Image.Type.Filled;
        image.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal;
        image.fillAmount = 1f;
        return image;
    }

    /// <summary>화면 전체를 덮는 네 조각 어둠(<see cref="UiSpotlightDim"/>). 조각은 클릭을 삼킨다. 처음에는 꺼 둔다.</summary>
    public static UiSpotlightDim SpotlightDim(Transform parent)
    {
        var holder = Fill(Rect("Spotlight", parent));
        var dim = holder.gameObject.AddComponent<UiSpotlightDim>();
        var area = Fill(Rect("Dims", holder));
        Wire(dim, "area", area);
        foreach (var side in new[] { "top", "bottom", "left", "right" })
        {
            var piece = Place(Rect($"Dim_{side}", area), Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            Image(piece, null, HudPalette.Scrim, true);
            Wire(dim, side, piece);
        }

        area.gameObject.SetActive(false);
        return dim;
    }

    /// <summary>
    /// 세로 스크롤 영역. 스크롤바 자리(<paramref name="gutter"/>)는 늘 비워 둬 내용 길이에 따라 폭이 흔들리지 않는다.
    /// 돌려주는 content는 위 기준으로 늘어나며(ContentSizeFitter), 자식 배치는 부르는 쪽이 정한다.
    /// </summary>
    public static UnityEngine.UI.ScrollRect ScrollArea(RectTransform rt, out RectTransform content, float gutter = 12f)
    {
        var scroll = rt.gameObject.AddComponent<UnityEngine.UI.ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = UnityEngine.UI.ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 30f;

        var viewport = Place(Rect("Viewport", rt), Vector2.zero, Vector2.one, new Vector2(0f, 1f), Vector2.zero, new Vector2(-gutter, 0f));
        viewport.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
        // 빈 자리에서도 휠·끌기가 먹도록 투명한 면을 깐다.
        Image(viewport, null, new Color(0f, 0f, 0f, 0f), true);

        content = Place(Rect("Content", viewport), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, Vector2.zero);
        var fitter = content.gameObject.AddComponent<UnityEngine.UI.ContentSizeFitter>();
        fitter.verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;

        var bar = Place(Rect("Scrollbar", rt), new Vector2(1f, 0f), Vector2.one, new Vector2(1f, 0.5f), Vector2.zero, new Vector2(6f, 0f));
        Image(bar, FillSprite, HudPalette.Track, true);
        var handleArea = Fill(Rect("Sliding Area", bar));
        var handle = Fill(Rect("Handle", handleArea));
        var handleImage = Image(handle, FillSprite, HudPalette.BorderQuiet, true);
        var scrollbar = bar.gameObject.AddComponent<UnityEngine.UI.Scrollbar>();
        scrollbar.handleRect = handle;
        scrollbar.targetGraphic = handleImage;
        scrollbar.direction = UnityEngine.UI.Scrollbar.Direction.BottomToTop;
        scrollbar.navigation = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.None };

        scroll.viewport = viewport;
        scroll.content = content;
        scroll.verticalScrollbar = scrollbar;
        scroll.verticalScrollbarVisibility = UnityEngine.UI.ScrollRect.ScrollbarVisibility.Permanent;
        return scroll;
    }

    /// <summary>스크롤 영역 안을 채우는 긴 글자 하나(내용만큼 늘어난다).</summary>
    public static TextMeshProUGUI ScrollText(RectTransform content, float size, Color color, TextAlignmentOptions alignment)
    {
        var text = Text("Text", content, string.Empty, size, color, alignment);
        Place(text.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, Vector2.zero);
        text.overflowMode = TextOverflowModes.Overflow;
        var layout = content.gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        return text;
    }

    /// <summary>가로 막대 슬라이더(정수). 손잡이는 금색.</summary>
    public static UnityEngine.UI.Slider Slider(RectTransform rt)
    {
        var track = Place(Rect("Track", rt), new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-16f, 8f));
        Image(track, FillSprite, HudPalette.Track, false);
        var fillArea = Place(Rect("Fill Area", rt), new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-16f, 8f));
        var fill = Place(Rect("Fill", fillArea), Vector2.zero, new Vector2(0f, 1f), new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
        Image(fill, FillSprite, HudPalette.Accent, false);
        var handleArea = Place(Rect("Handle Slide Area", rt), Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-16f, 0f));
        var handle = Place(Rect("Handle", handleArea), Vector2.zero, new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(16f, 0f));
        var handleImage = Image(handle, FillSprite, HudPalette.Text, true);

        // 끌기를 막대 전체에서 받는다.
        Image(rt, null, new Color(0f, 0f, 0f, 0f), true);
        var slider = rt.gameObject.AddComponent<UnityEngine.UI.Slider>();
        slider.fillRect = fill;
        slider.handleRect = handle;
        slider.targetGraphic = handleImage;
        slider.direction = UnityEngine.UI.Slider.Direction.LeftToRight;
        slider.wholeNumbers = true;
        slider.navigation = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.None };
        return slider;
    }

    // ── 글자 ───────────────────────────────────────────────

    public static TextMeshProUGUI Text(string name, Transform parent, string value, float size, Color color, TextAlignmentOptions alignment, bool bold = false, bool singleLine = false)
    {
        var rt = Rect(name, parent);
        var text = rt.gameObject.AddComponent<TextMeshProUGUI>();
        text.text = value;
        text.fontSize = size;
        text.color = color;
        text.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
        text.alignment = alignment;
        text.raycastTarget = false;
        text.richText = true;
        if (singleLine)
        {
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Ellipsis;
        }
        else
        {
            text.overflowMode = TextOverflowModes.Truncate;
        }

        return text;
    }

    // ── 버튼 ───────────────────────────────────────────────

    public static UiFrameButton Button(RectTransform rt, string label, ButtonKind kind, float fontSize = 16f)
    {
        var (fill, border, textColor) = kind switch
        {
            ButtonKind.Primary => (HudPalette.Primary, HudPalette.BorderAccent, HudPalette.Text),
            ButtonKind.Danger => (HudPalette.DangerSurface, HudPalette.BorderDanger, HudPalette.Loss),
            ButtonKind.Quiet => (new Color(0f, 0f, 0f, 0f), new Color(0f, 0f, 0f, 0f), HudPalette.TextDim),
            _ => (HudPalette.Control, HudPalette.BorderQuiet, HudPalette.TextBody)
        };

        var bg = Image(rt, FillSprite, fill, true);
        bg.type = UnityEngine.UI.Image.Type.Sliced;
        var outline = Outline(rt, border);
        var text = Text("Label", rt, label, fontSize, textColor, TextAlignmentOptions.Center, kind == ButtonKind.Primary, true);
        Fill(text.rectTransform, 6f);

        var button = rt.gameObject.AddComponent<UnityEngine.UI.Button>();
        button.targetGraphic = bg;
        var colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.25f, 1.2f, 1.1f, 1f);
        colors.pressedColor = new Color(0.75f, 0.7f, 0.65f, 1f);
        colors.selectedColor = Color.white;
        colors.disabledColor = new Color(0.6f, 0.6f, 0.6f, 0.5f);
        colors.colorMultiplier = 1.2f;
        colors.fadeDuration = 0.08f;
        button.colors = colors;
        // HUD 버튼이 키보드 탐색으로 선택돼 남지 않게 한다. 조작은 마우스·단축키다.
        button.navigation = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.None };

        var frame = rt.gameObject.AddComponent<UiFrameButton>();
        Wire(frame, "button", button);
        Wire(frame, "outline", outline);
        Wire(frame, "label", text);
        SetColor(frame, "normalBorder", border);
        SetColor(frame, "selectedBorder", HudPalette.BorderAccent);
        return frame;
    }

    // ── 직렬화 배선 ─────────────────────────────────────────

    public static void Wire(Object target, string field, Object value)
    {
        if (target == null)
            return;

        var so = new SerializedObject(target);
        var property = so.FindProperty(field);
        if (property == null)
        {
            Debug.LogError($"{nameof(UiKit)}: {target.GetType().Name}.{field} 필드가 없습니다.", target);
            return;
        }

        property.objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
        if (value == null)
            Debug.LogWarning($"{nameof(UiKit)}: {target.GetType().Name}.{field}에 넣을 대상을 찾지 못했습니다.", target);
    }

    public static void WireArray(Object target, string field, Object[] values)
    {
        var so = new SerializedObject(target);
        var property = so.FindProperty(field);
        if (property == null)
        {
            Debug.LogError($"{nameof(UiKit)}: {target.GetType().Name}.{field} 필드가 없습니다.", target);
            return;
        }

        property.arraySize = values.Length;
        for (var i = 0; i < values.Length; i++)
            property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetColor(Object target, string field, Color value)
    {
        var so = new SerializedObject(target);
        so.FindProperty(field).colorValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    public static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;

        var parent = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
    }

    // ── 그림 ───────────────────────────────────────────────

    /// <summary>둥근 사각(채움 또는 테두리) 32×32를 그려 9-slice 스프라이트로 가져온다. 이미 있으면 그대로 쓴다.</summary>
    private static Sprite EnsureSprite(string name, bool lineOnly)
    {
        var path = $"{SkinFolder}/{name}.png";
        if (!System.IO.File.Exists(path))
        {
            EnsureFolder(SkinFolder);
            var texture = new Texture2D(SpriteSize, SpriteSize, TextureFormat.RGBA32, false);
            for (var y = 0; y < SpriteSize; y++)
            for (var x = 0; x < SpriteSize; x++)
            {
                var inside = Coverage(x, y, 0f);
                var alpha = lineOnly ? Mathf.Clamp01(inside - Coverage(x, y, LineWidth)) : inside;
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }

            texture.Apply();
            System.IO.File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        }

        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        var border = new Vector4(Corner + 2, Corner + 2, Corner + 2, Corner + 2);
        if (importer.textureType != TextureImporterType.Sprite || importer.spriteBorder != border)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100f;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true;
            importer.spriteBorder = border;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    /// <summary>둥근 사각 안쪽 정도(0~1, 가장자리 부드럽게). inset만큼 안으로 들어간 사각을 잰다.</summary>
    private static float Coverage(int px, int py, float inset)
    {
        var cx = px + 0.5f;
        var cy = py + 0.5f;
        var min = inset;
        var max = SpriteSize - inset;
        var radius = Mathf.Max(0.5f, Corner - inset);
        var qx = Mathf.Clamp(cx, min + radius, max - radius);
        var qy = Mathf.Clamp(cy, min + radius, max - radius);
        var distance = Vector2.Distance(new Vector2(cx, cy), new Vector2(qx, qy));
        var edge = radius - distance;
        if (cx < min || cx > max || cy < min || cy > max)
            return 0f;
        return Mathf.Clamp01(edge + 0.5f);
    }
}
