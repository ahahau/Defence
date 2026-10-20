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
    private static Sprite _frameFill;
    private static Sprite _frameLine;

    /// <summary>픽셀 UI 팩(SuperGUIBundle, Black 테마). 패널 테두리 조각·아이콘 버튼·금화를 여기서 가져온다.</summary>
    private const string PackFolder = "Assets/04.Sprites/UI/SuperGUIBundle";
    private const string PackPanelFolder = PackFolder + "/1 UI/Panels/Black";
    private const string PackButtonFolder = PackFolder + "/1 UI/Buttons & Scroll Bars/Black";
    public const string CoinSpritePath = PackFolder + "/2 HUD/Coins/Coins 1/Sprites 32x32/Gold/0.png";

    /// <summary>팩 테두리 그림의 9-slice 두께(px, 나선 모서리 9칸).</summary>
    private const int FrameBorder = 9;

    /// <summary>픽셀 그림을 화면에 몇 배로 그리는가(1920×1080 기준). 정수여야 픽셀이 고르다.</summary>
    public const float PixelScale = 2f;

    /// <summary>팩 아이콘 버튼 시트에서 테두리 없는 기호만 있는 칸.</summary>
    public const int GlyphPause = 213;
    public const int GlyphPlay = 214;
    public const int GlyphFast = 227;

    public enum ButtonKind
    {
        Secondary,
        Primary,
        Danger,
        Quiet
    }

    public static Sprite FillSprite => _fill != null ? _fill : _fill = EnsureSprite("RoundFill", false);
    public static Sprite LineSprite => _line != null ? _line : _line = EnsureSprite("RoundLine", true);

    /// <summary>패널·버튼 바탕: 팩 테두리 모양을 흰색으로 채운 그림(색은 칠하는 쪽이 정한다).</summary>
    public static Sprite FrameFillSprite => _frameFill != null ? _frameFill : _frameFill = EnsureFrameSprite("FrameFill", false);

    /// <summary>패널·버튼 테두리: 팩의 사슬 테두리와 나선 모서리를 밝기만 남긴 흰 그림(테두리 색으로 칠한다).</summary>
    public static Sprite FrameLineSprite => _frameLine != null ? _frameLine : _frameLine = EnsureFrameSprite("FrameLine", true);

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

    /// <summary>픽셀 테두리 바탕 + 테두리. 테두리 이미지를 돌려준다.</summary>
    public static UnityEngine.UI.Image Panel(RectTransform rt, Color fill, Color border, bool raycast = false)
    {
        var bg = Image(rt, FrameFillSprite, fill, raycast);
        ApplyPixelFrame(bg);
        return Outline(rt, border);
    }

    public static UnityEngine.UI.Image Outline(RectTransform parent, Color border)
    {
        var line = Fill(Rect("Outline", parent));
        var image = Image(line, FrameLineSprite, border, false);
        ApplyPixelFrame(image);
        return image;
    }

    /// <summary>
    /// 픽셀 테두리 그림을 <see cref="PixelScale"/>배로 그린다. 사슬 무늬가 늘어나지 않게 가장자리를 늘이지 않고 되풀이한다.
    /// 이미 만든 프리팹을 고칠 때(<c>UiSkinRefresher</c>)도 이 함수를 쓴다.
    /// </summary>
    public static void ApplyPixelFrame(UnityEngine.UI.Image image)
    {
        image.type = UnityEngine.UI.Image.Type.Tiled;
        image.pixelsPerUnitMultiplier = 1f / PixelScale;
    }

    /// <summary>팩 아이콘의 기호를 흰색으로 바꾼 그림. 버튼 글자 색으로 칠해 쓴다.</summary>
    public static Sprite GlyphSprite(int index) => EnsureGlyph(index);

    /// <summary>버튼 가운데에 기호를 놓고 글자는 비운다. 기호는 픽셀 그대로 <see cref="PixelScale"/>배 크기다.</summary>
    public static UnityEngine.UI.Image ButtonGlyph(UiFrameButton button, int glyph, Color color)
    {
        var rt = (RectTransform)button.transform;
        var sprite = GlyphSprite(glyph);
        var size = sprite != null ? sprite.rect.size * PixelScale : Vector2.one * 24f;
        var icon = Place(Rect("Glyph", rt), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size);
        var image = Image(icon, sprite, color, false);
        image.type = UnityEngine.UI.Image.Type.Simple;
        if (button.Label != null)
            button.Label.text = string.Empty;
        return image;
    }

    /// <summary>
    /// 금화 그림(팩 32px 금화 첫 장)을 놓는다. 그림이 30px로 촘촘해 2배면 금화 숫자보다 커서 원래 크기(1배)로 둔다.
    /// 움직임은 넣지 않는다(HUD는 조용해야 한다).
    /// </summary>
    public static UnityEngine.UI.Image Coin(RectTransform rt)
    {
        var sprite = EnsureCrop(CoinSpritePath, "Coin", false);
        var image = Image(rt, sprite, Color.white, false);
        image.type = UnityEngine.UI.Image.Type.Simple;
        if (sprite != null)
            rt.sizeDelta = sprite.rect.size;
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

        var bg = Image(rt, FrameFillSprite, fill, true);
        ApplyPixelFrame(bg);
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

    /// <summary>
    /// 팩 패널 조각 네 모서리(0 왼쪽 위·2 오른쪽 위·6 왼쪽 아래·8 오른쪽 아래)의 16×16 귀퉁이를 이어 32×32 테두리 그림을 만든다.
    /// 바탕(<paramref name="lineOnly"/> 거짓)은 모양만 흰색으로, 테두리는 안쪽 바탕색을 비우고 밝기만 남긴다 — 둘 다 흰 그림이라
    /// 지금처럼 <see cref="HudPalette"/> 색으로 칠해 쓴다(금빛 강조·붉은 위험 테두리가 그대로 산다).
    /// </summary>
    private static Sprite EnsureFrameSprite(string name, bool lineOnly)
    {
        var path = $"{SkinFolder}/{name}.png";
        if (!System.IO.File.Exists(path))
        {
            EnsureFolder(SkinFolder);
            var pieces = new[] { (0, 0, 16), (2, 16, 16), (6, 0, 0), (8, 16, 0) };
            var texture = new Texture2D(SpriteSize, SpriteSize, TextureFormat.RGBA32, false);
            Color32? fillColor = null;
            float brightest = 0f;
            var source = new Color32[SpriteSize * SpriteSize];
            foreach (var (index, ox, oy) in pieces)
            {
                var piece = LoadPackTexture($"{PackPanelFolder}/{index}.png");
                for (var y = 0; y < 16; y++)
                for (var x = 0; x < 16; x++)
                    source[(oy + y) * SpriteSize + ox + x] = piece.GetPixel(ox + x, oy + y);
                // 조각 한가운데는 바탕색이다.
                fillColor ??= piece.GetPixel(16, 16);
                Object.DestroyImmediate(piece);
            }

            foreach (var c in source)
                brightest = Mathf.Max(brightest, Luminance(c));

            for (var i = 0; i < source.Length; i++)
            {
                var c = source[i];
                float alpha = c.a / 255f;
                var value = 1f;
                if (lineOnly)
                {
                    var isFill = fillColor.HasValue && c.r == fillColor.Value.r && c.g == fillColor.Value.g && c.b == fillColor.Value.b;
                    alpha = isFill ? 0f : alpha;
                    value = brightest > 0f ? Mathf.Clamp01(Luminance(c) / brightest) : 1f;
                }

                texture.SetPixel(i % SpriteSize, i / SpriteSize, new Color(value, value, value, alpha));
            }

            texture.Apply();
            System.IO.File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        }

        ImportPixelSprite(path, new Vector4(FrameBorder, FrameBorder, FrameBorder, FrameBorder));
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    /// <summary>팩 버튼 면(빈 버튼 칸 20, 32×32, 테두리 9)을 테마 색 그대로 가져온다.</summary>
    public static Sprite ButtonFaceSprite(string theme)
    {
        var path = $"{SkinFolder}/Button_{theme}.png";
        if (!System.IO.File.Exists(path))
        {
            EnsureFolder(SkinFolder);
            System.IO.File.Copy($"{PackFolder}/1 UI/Buttons & Scroll Bars/{theme}/20.png", path);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        }

        ImportPixelSprite(path, new Vector4(FrameBorder, FrameBorder, FrameBorder, FrameBorder));
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    /// <summary>밝은 팩 버튼 면 위 글자 색.</summary>
    public static readonly Color FaceText = new Color32(40, 34, 52, 255);

    /// <summary>
    /// 글자 하나뿐인 버튼을 팩 버튼 면으로 바꾼다(보통 Black·주요 Gold·위험 Red, 고르면 Gold·주요는 Green).
    /// 글자 말고 다른 내용(아이콘·둘째 줄 글자)이 든 버튼은 카드·목록 줄이라 패널 테두리를 그대로 둔다. 투명한 조용한 버튼도 그대로.
    /// 생성기와 <c>UiSkinRefresher</c>가 함께 쓴다. 바꾼 버튼 수를 돌려준다.
    /// </summary>
    public static int ApplyButtonFaces(GameObject root)
    {
        var normal = ButtonFaceSprite("Black");
        var primary = ButtonFaceSprite("Gold");
        var danger = ButtonFaceSprite("Red");
        var highlight = ButtonFaceSprite("Green");
        var count = 0;
        foreach (var frame in root.GetComponentsInChildren<UiFrameButton>(true))
        {
            var so = new SerializedObject(frame);
            if ((UiFrameButton.FaceRole)so.FindProperty("faceRole").enumValueIndex != UiFrameButton.FaceRole.None)
                continue;
            if (frame.Button == null || frame.Button.targetGraphic is not UnityEngine.UI.Image face || face.color.a <= 0f)
                continue;
            if (!HoldsOnlyLabel(frame))
                continue;

            var role = SameColor(face.color, HudPalette.Primary) ? UiFrameButton.FaceRole.Primary
                : SameColor(face.color, HudPalette.DangerSurface) ? UiFrameButton.FaceRole.Danger
                : UiFrameButton.FaceRole.Normal;
            face.sprite = role switch
            {
                UiFrameButton.FaceRole.Primary => primary,
                UiFrameButton.FaceRole.Danger => danger,
                _ => normal
            };
            face.color = Color.white;
            ApplyPixelFrame(face);
            // 2배 테두리(위아래 18씩)는 낮은 버튼(닫기·배치·명령 줄, 28~36)에서 모서리가 글자를 덮는다. 낮은 버튼은 1배로 그린다.
            if (ButtonHeight(frame) < SmallButtonHeight)
                face.pixelsPerUnitMultiplier = 1f;
            // 잠긴 버튼이 반투명이면 밝은 면이 뒤 어두운 패널에 묻혀 글자까지 사라진다. 불투명한 회색으로 어둡게만 한다.
            var colors = frame.Button.colors;
            colors.disabledColor = new Color(0.62f, 0.6f, 0.6f, 1f);
            frame.Button.colors = colors;

            var outline = so.FindProperty("outline").objectReferenceValue as UnityEngine.UI.Image;
            if (outline != null)
                Object.DestroyImmediate(outline.gameObject, true);
            so.FindProperty("outline").objectReferenceValue = null;
            so.FindProperty("faceRole").enumValueIndex = (int)role;
            so.FindProperty("normalFace").objectReferenceValue = normal;
            so.FindProperty("primaryFace").objectReferenceValue = primary;
            so.FindProperty("dangerFace").objectReferenceValue = danger;
            so.FindProperty("highlightFace").objectReferenceValue = highlight;
            so.FindProperty("faceText").colorValue = FaceText;
            so.ApplyModifiedPropertiesWithoutUndo();

            if (frame.Label != null)
                frame.SetLabelColor(frame.Label.color);
            var glyph = frame.transform.Find("Glyph");
            if (glyph != null && glyph.TryGetComponent<UnityEngine.UI.Image>(out var glyphImage))
                glyphImage.color = FaceText;
            count++;
        }

        return count;
    }

    /// <summary>이보다 낮은 팩 버튼은 테두리를 1배로 그린다.</summary>
    private const float SmallButtonHeight = 44f;

    /// <summary>버튼 높이. 레이아웃이 크기를 정하는 줄(목록)은 프리팹에서 높이가 0이라 LayoutElement 값을 본다.</summary>
    private static float ButtonHeight(UiFrameButton frame)
    {
        if (frame.TryGetComponent<UnityEngine.UI.LayoutElement>(out var element) && element.preferredHeight > 0f)
            return element.preferredHeight;
        return ((RectTransform)frame.transform).rect.height;
    }

    private static bool HoldsOnlyLabel(UiFrameButton frame)
    {
        foreach (var text in frame.GetComponentsInChildren<TMP_Text>(true))
        {
            if (text != frame.Label)
                return false;
        }

        foreach (var image in frame.GetComponentsInChildren<UnityEngine.UI.Image>(true))
        {
            if (image.gameObject == frame.gameObject || image.name == "Outline" || image.name == "Glyph")
                continue;
            return false;
        }

        return true;
    }

    private static bool SameColor(Color a, Color b) =>
        Mathf.Abs(a.r - b.r) < 0.01f && Mathf.Abs(a.g - b.g) < 0.01f && Mathf.Abs(a.b - b.b) < 0.01f && Mathf.Abs(a.a - b.a) < 0.01f;

    /// <summary>팩 아이콘 버튼 시트의 기호 칸을 흰색으로 바꿔 가져온다.</summary>
    private static Sprite EnsureGlyph(int index) => EnsureCrop($"{PackButtonFolder}/{index}.png", $"Glyph_{index}", true);

    /// <summary>
    /// 팩 그림 한 장을 보이는 부분만 잘라 스킨 폴더에 픽셀 그림으로 가져온다. 팩 원본의 가져오기 설정(흐림 필터·여러 장)은 건드리지 않는다.
    /// <paramref name="whiten"/>이면 색을 지우고 흰색만 남긴다(칠해 쓰는 기호).
    /// </summary>
    private static Sprite EnsureCrop(string sourcePath, string name, bool whiten)
    {
        var path = $"{SkinFolder}/{name}.png";
        if (!System.IO.File.Exists(path))
        {
            EnsureFolder(SkinFolder);
            var piece = LoadPackTexture(sourcePath);
            // 기호가 칸 안 어디에 있든 화면에서 가운데에 오도록 보이는 부분만 잘라 낸다.
            int minX = piece.width, minY = piece.height, maxX = -1, maxY = -1;
            for (var y = 0; y < piece.height; y++)
            for (var x = 0; x < piece.width; x++)
            {
                if (piece.GetPixel(x, y).a <= 0f)
                    continue;
                minX = Mathf.Min(minX, x);
                minY = Mathf.Min(minY, y);
                maxX = Mathf.Max(maxX, x);
                maxY = Mathf.Max(maxY, y);
            }

            if (maxX < 0)
            {
                minX = minY = 0;
                maxX = piece.width - 1;
                maxY = piece.height - 1;
            }

            var texture = new Texture2D(maxX - minX + 1, maxY - minY + 1, TextureFormat.RGBA32, false);
            for (var y = minY; y <= maxY; y++)
            for (var x = minX; x <= maxX; x++)
            {
                var c = piece.GetPixel(x, y);
                texture.SetPixel(x - minX, y - minY, whiten ? new Color(1f, 1f, 1f, c.a) : c);
            }
            texture.Apply();
            System.IO.File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(piece);
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        }

        ImportPixelSprite(path, Vector4.zero);
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    /// <summary>팩 PNG를 가져오기 설정과 상관없이 파일에서 바로 읽는다(읽기 허용을 켜지 않으려고).</summary>
    private static Texture2D LoadPackTexture(string path)
    {
        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        texture.LoadImage(System.IO.File.ReadAllBytes(path));
        return texture;
    }

    private static float Luminance(Color32 c) => (0.299f * c.r + 0.587f * c.g + 0.114f * c.b) / 255f;

    /// <summary>픽셀 그림 가져오기: 점 필터(흐려지지 않게), 압축 없음, 9-slice 두께.</summary>
    private static void ImportPixelSprite(string path, Vector4 border)
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        if (importer.textureType == TextureImporterType.Sprite && importer.spriteBorder == border && importer.filterMode == FilterMode.Point)
            return;

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 100f;
        importer.mipmapEnabled = false;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.alphaIsTransparency = true;
        importer.spriteBorder = border;
        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        importer.SetTextureSettings(settings);
        importer.SaveAndReimport();
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
