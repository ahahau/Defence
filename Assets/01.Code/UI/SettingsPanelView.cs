using _01.Code.Audio;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace _01.Code.UI
{
    /// <summary>
    /// 효과음 음량을 조절하는 설정 창.
    ///
    /// 씬에 미리 배선하지 않고 실행 시점에 스스로 올라온다. 씬은 다른 작업과 함께 건드리는 파일이라
    /// 여기에 패널을 하나 더 얹으면 충돌하기 쉽고, 이 창은 게임 상태에 전혀 의존하지 않아서
    /// 코드만으로 세워도 잃는 게 없다.
    ///
    /// 그림은 <see cref="UiSkinSO"/>를 통해 UI 팩에서 가져온다. 팩은 Resources 밖에 있어서
    /// 실행 중에 직접 못 집기 때문에, Resources에 둔 그 에셋이 다리 역할을 한다.
    /// 표가 없거나 비어 있으면 게임 톤에 맞춘 색으로 직접 칠해서 창이 비지 않게 한다.
    /// </summary>
    public sealed class SettingsPanelView : MonoBehaviour
    {
        private const string SkinResourcePath = "UI/UiSkin";

        private static readonly Color PanelColor = new(0.055f, 0.034f, 0.025f, 0.98f);
        private static readonly Color EdgeColor = new(0.62f, 0.44f, 0.20f, 1f);
        /// <summary>창 뒤를 덮는 막. 완전히 가리지 않고 판이 비치게 두어 어디로 돌아가는지 보이게 한다.</summary>
        private static readonly Color BackdropColor = new(0f, 0f, 0f, 0.66f);
        private static readonly Color TrackColor = new(0.16f, 0.11f, 0.07f, 1f);
        private static readonly Color FillColor = new(0.78f, 0.56f, 0.24f, 1f);
        private static readonly Color TextColor = new(0.94f, 0.90f, 0.82f, 1f);

        private static SettingsPanelView current;

        private GameObject window;
        private GameObject backdrop;
        private GameObject confirmWindow;
        private GameObject restartButton;
        private GameObject titleButton;
        private GameObject closeButton;
        private Slider slider;
        private TMP_Text valueLabel;
        private Slider musicSlider;
        private UiSkinSO skin;
        private TMP_Text musicValueLabel;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            var host = new GameObject("Settings Panel");
            host.AddComponent<SettingsPanelView>();
            DontDestroyOnLoad(host);
        }

        private void Start()
        {
            current = this;
            skin = Resources.Load<UiSkinSO>(SkinResourcePath);
            var canvas = BuildCanvas();
            BuildWindow(canvas.transform);
            window.SetActive(false);
            FitToScene();
        }

        private void OnEnable()
        {
            // 이 창은 씬을 넘어가도 살아남는다. 씬이 바뀌면 구성을 다시 맞춰야
            // 판을 떠나는 두 버튼이 제자리를 찾는다 — 타이틀에서 만들어진 창이
            // 그대로 판 위로 따라오기 때문이다.
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private void OnDisable()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= HandleSceneLoaded;
        }

        private void HandleSceneLoaded(UnityEngine.SceneManagement.Scene scene,
            UnityEngine.SceneManagement.LoadSceneMode mode)
        {
            if (window == null)
                return;

            Toggle(false);
            SetConfirmVisible(false);
            FitToScene();
        }

        /// <summary>타이틀 메뉴처럼 바깥에서 설정을 열 때 쓴다.</summary>
        public static void Open()
        {
            if (current != null && current.window != null)
                current.Toggle(true);
        }

        private void Update()
        {
            if (!EscapePressedThisFrame())
                return;

            // 확인 창이 떠 있으면 그것부터 닫는다. 한 번에 둘을 닫으면 취소한 줄 모르고 지나간다.
            if (confirmWindow != null && confirmWindow.activeSelf)
            {
                SetConfirmVisible(false);
                return;
            }

            Toggle(!window.activeSelf);
        }

        /// <summary>
        /// 이번 프레임에 ESC 를 눌렀는가.
        ///
        /// 이 프로젝트는 입력 처리가 Input System 패키지로 넘어가 있어서(activeInputHandler 1)
        /// 옛 UnityEngine.Input 을 부르면 그 자리에서 예외가 난다. 두 방식 다 켜 둔 프로젝트도
        /// 있으므로 컴파일 기호로 갈라 둔다.
        /// </summary>
        private static bool EscapePressedThisFrame()
        {
#if ENABLE_INPUT_SYSTEM
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            return keyboard != null && keyboard.escapeKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.Escape);
#endif
        }

        private Canvas BuildCanvas()
        {
            var go = new GameObject("Settings Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.transform.SetParent(transform, false);

            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // 다른 패널 위에 확실히 뜨게 한다. 설정은 언제든 닫을 수 있어야 한다.
            canvas.sortingOrder = 5000;

            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            return canvas;
        }

        /// <summary>
        /// 창 뒤를 화면 끝까지 덮어 클릭을 받아 삼킨다.
        ///
        /// 설정 창은 440x386짜리 판 하나뿐이라 그 바깥을 누르면 그대로 지도로 내려갔다.
        /// 설정을 열어 둔 채 방을 짓거나 유닛을 옮길 수 있었다는 뜻이다.
        ///
        /// 창보다 먼저 만들어야 형제 순서상 뒤에 깔린다.
        /// </summary>
        private void BuildBackdrop(Transform parent)
        {
            var image = CreateImage(parent, "Backdrop", BackdropColor);
            var rect = image.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            image.raycastTarget = true;

            backdrop = image.gameObject;
            backdrop.SetActive(false);
        }

        private void BuildWindow(Transform parent)
        {
            BuildBackdrop(parent);

            // 이 창은 DontDestroyOnLoad 라 씬을 넘어가도 다시 만들어지지 않는다. 그래서
            // 만들 때의 씬으로 구성을 정하면 안 된다 — 타이틀에서 만들어진 창이 판 위로
            // 따라와 "타이틀로 나가기"가 없는 채로 열렸다. 항상 다 만들어 두고 열 때 가린다.
            window = CreatePanel(parent, "Settings Window", new Vector2(440f, 386f), Vector2.zero);

            CreateLabel(window.transform, "설정", 26, TextAlignmentOptions.Center,
                new Vector2(0.5f, 1f), new Vector2(400f, 40f), new Vector2(0f, -34f));

            slider = BuildRow(window.transform, "효과음", -92f, GameSfxPlayer.Volume,
                out valueLabel, OnSfxVolumeChanged);

            musicSlider = BuildRow(window.transform, "배경음악", -168f, GameMusicPlayer.Volume,
                out musicValueLabel, OnMusicVolumeChanged);

            var restart = CreateButton(window.transform, "다시하기", new Vector2(0.5f, 1f),
                new Vector2(180f, 40f), new Vector2(-98f, -252f));
            restart.onClick.AddListener(() => SetConfirmVisible(true));
            restartButton = restart.gameObject;

            var toTitle = CreateButton(window.transform, "타이틀로 나가기", new Vector2(0.5f, 1f),
                new Vector2(180f, 40f), new Vector2(98f, -252f));
            toTitle.onClick.AddListener(GoToTitle);
            titleButton = toTitle.gameObject;

            var close = CreateButton(window.transform, "닫기", new Vector2(0.5f, 0f), new Vector2(120f, 38f), new Vector2(0f, 34f));
            close.onClick.AddListener(() => Toggle(false));
            closeButton = close.gameObject;

            BuildConfirm(parent);
        }

        /// <summary>
        /// 지금 씬에 맞게 창을 고쳐 놓는다. 타이틀에서는 판을 떠나는 두 버튼이 뜻이 없으므로
        /// 감추고 창도 그만큼 줄인다. 열 때마다 보므로 씬을 오간 뒤에도 어긋나지 않는다.
        /// </summary>
        private void FitToScene()
        {
            var inGame = IsInGame;

            if (restartButton != null)
                restartButton.SetActive(inGame);
            if (titleButton != null)
                titleButton.SetActive(inGame);

            if (window != null && window.transform is RectTransform rect)
                rect.sizeDelta = new Vector2(440f, inGame ? 386f : 274f);

        }

        /// <summary>
        /// 다시하기 확인 창. 되돌릴 수 없는 일이라 한 번 더 묻는다 — 눌리는 순간
        /// 저장이 지워지고 판이 처음으로 돌아간다.
        /// </summary>
        private void BuildConfirm(Transform parent)
        {
            confirmWindow = CreatePanel(parent, "Restart Confirm", new Vector2(460f, 220f), Vector2.zero);

            CreateLabel(confirmWindow.transform, "지금까지의 진행이 모두 사라집니다", 21, TextAlignmentOptions.Center,
                new Vector2(0.5f, 1f), new Vector2(420f, 34f), new Vector2(0f, -52f));
            CreateLabel(confirmWindow.transform, "부하도 건물도 금화도 처음으로 돌아갑니다", 17, TextAlignmentOptions.Center,
                new Vector2(0.5f, 1f), new Vector2(420f, 30f), new Vector2(0f, -88f));

            var cancel = CreateButton(confirmWindow.transform, "취소", new Vector2(0.5f, 0f),
                new Vector2(150f, 40f), new Vector2(-82f, 40f));
            cancel.onClick.AddListener(() => SetConfirmVisible(false));

            var confirm = CreateButton(confirmWindow.transform, "초기화", new Vector2(0.5f, 0f),
                new Vector2(150f, 40f), new Vector2(82f, 40f));
            confirm.onClick.AddListener(RestartRun);

            confirmWindow.SetActive(false);
        }

        /// <summary>판 위인가. 타이틀 씬에서는 나가기·다시하기가 뜻이 없다.</summary>
        private static bool IsInGame =>
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != TitleMenuActions.TitleSceneName;

        private void SetConfirmVisible(bool visible)
        {
            if (confirmWindow == null)
                return;

            confirmWindow.SetActive(visible);
            GameSfxPlayer.Play(visible ? GameSfxCue.UiOpen : GameSfxCue.UiClose);
        }

        /// <summary>저장을 지우고 판을 처음부터 다시 올린다.</summary>
        private void RestartRun()
        {
            // 되돌릴 수 없는 동작이라 둘러보는 클릭과 다른 소리를 낸다.
            GameSfxPlayer.Play(GameSfxCue.UiConfirm);
            _01.Code.Persistence.RunSaveSystem.DeleteSave();
            LeaveTo(TitleMenuActions.GameSceneName);
        }

        private void GoToTitle()
        {
            LeaveTo(TitleMenuActions.TitleSceneName);
        }

        /// <summary>
        /// 창을 정리하고 씬을 바꾼다. 시간 배속을 되돌리는 게 핵심이다 —
        /// 정산이나 모달이 timeScale 을 0 으로 잡아 둔 채 나가면 다음 판이 멈춘 채로 시작한다.
        /// </summary>
        private void LeaveTo(string sceneName)
        {
            SetConfirmVisible(false);
            Toggle(false);
            Time.timeScale = 1f;
            UnityEngine.SceneManagement.SceneManager.LoadScene(sceneName);
        }

        /// <summary>이름표 · 슬라이더 · 퍼센트 한 줄을 만든다.</summary>
        private Slider BuildRow(Transform parent, string label, float top, float initial,
            out TMP_Text percentLabel, UnityEngine.Events.UnityAction<float> onChanged)
        {
            CreateLabel(parent, label, 19, TextAlignmentOptions.Left,
                new Vector2(0.5f, 1f), new Vector2(140f, 30f), new Vector2(-130f, top));

            percentLabel = CreateLabel(parent, "0%", 19, TextAlignmentOptions.Right,
                new Vector2(0.5f, 1f), new Vector2(80f, 30f), new Vector2(158f, top));

            var root = new GameObject(label + " Slider", typeof(RectTransform), typeof(Slider));
            root.transform.SetParent(parent, false);
            var rect = (RectTransform)root.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(340f, 18f);
            rect.anchoredPosition = new Vector2(0f, top - 44f);

            var track = CreateImage(root.transform, "Track", TrackColor,
                skin != null ? skin.SliderTrack : null, rect.sizeDelta);
            Stretch(track.rectTransform);

            var fillArea = new GameObject("Fill Area", typeof(RectTransform));
            fillArea.transform.SetParent(root.transform, false);
            Stretch((RectTransform)fillArea.transform);

            var fill = CreateImage(fillArea.transform, "Fill", FillColor,
                skin != null ? skin.SliderFill : null, rect.sizeDelta);
            Stretch(fill.rectTransform);

            var built = root.GetComponent<Slider>();
            built.fillRect = fill.rectTransform;
            built.targetGraphic = track;
            built.minValue = 0f;
            built.maxValue = 1f;
            built.wholeNumbers = false;
            built.SetValueWithoutNotify(initial);
            built.onValueChanged.AddListener(onChanged);

            percentLabel.text = Percent(initial);
            return built;
        }

        private void OnSfxVolumeChanged(float value)
        {
            // 정한 크기를 바로 들려주려 했는데, 슬라이더는 끄는 동안 값이 수십 번 바뀐다.
            // 한 번 만질 때마다 딸깍 소리가 연달아 터져서 오히려 크기를 가늠할 수 없었다.
            GameSfxPlayer.Volume = value;
            valueLabel.text = Percent(value);
        }

        private void OnMusicVolumeChanged(float value)
        {
            // 음악은 계속 흐르고 있으므로 따로 들려줄 필요가 없다.
            GameMusicPlayer.Volume = value;
            musicValueLabel.text = Percent(value);
        }

        private static string Percent(float value) => Mathf.RoundToInt(value * 100f) + "%";

        private void Toggle(bool open)
        {
            if (open)
                FitToScene();

            if (backdrop != null)
                backdrop.SetActive(open);
            window.SetActive(open);
            if (open)
            {
                slider.SetValueWithoutNotify(GameSfxPlayer.Volume);
                valueLabel.text = Percent(slider.value);
                musicSlider.SetValueWithoutNotify(GameMusicPlayer.Volume);
                musicValueLabel.text = Percent(musicSlider.value);
            }

            GameSfxPlayer.Play(open ? GameSfxCue.UiOpen : GameSfxCue.UiClose);
        }

        // ---- 조각 만들기 ----

        private GameObject CreatePanel(Transform parent, string name, Vector2 size, Vector2 position)
        {
            var frame = skin != null ? skin.WindowFrame : null;

            // 팩 프레임이 있으면 그것 한 장으로 끝난다. 없을 때만 테두리색 + 안쪽색 두 겹으로 흉내 낸다.
            var root = CreateImage(parent, name, frame != null ? PanelColor : EdgeColor, frame, size);
            var rect = root.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;

            if (frame != null)
                return root.gameObject;

            var inner = CreateImage(root.transform, "Inner", PanelColor);
            var innerRect = inner.rectTransform;
            innerRect.anchorMin = Vector2.zero;
            innerRect.anchorMax = Vector2.one;
            innerRect.offsetMin = new Vector2(2f, 2f);
            innerRect.offsetMax = new Vector2(-2f, -2f);

            return root.gameObject;
        }

        private static Image CreateImage(Transform parent, string name, Color color,
            Sprite sprite = null, Vector2 size = default)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.color = color;

            if (sprite == null)
                return image;

            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            // 9-슬라이스 경계가 대상보다 크면 모서리가 서로 겹쳐 뭉갠다. 요소 크기에 맞춰 줄인다.
            image.pixelsPerUnitMultiplier = ResolveBorderScale(sprite, size);
            return image;
        }

        /// <summary>경계 합이 대상 크기를 넘지 않도록 하는 배수. 여유를 조금 둬서 가운데가 눌리지 않게 한다.</summary>
        private static float ResolveBorderScale(Sprite sprite, Vector2 size)
        {
            if (size.x <= 0f || size.y <= 0f)
                return 1f;

            var border = sprite.border;
            var needX = (border.x + border.z) / size.x;
            var needY = (border.y + border.w) / size.y;
            return Mathf.Max(1f, Mathf.Max(needX, needY) * 1.35f);
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static TMP_Text CreateLabel(Transform parent, string text, float size,
            TextAlignmentOptions alignment, Vector2 anchor, Vector2 sizeDelta, Vector2 position)
        {
            var go = new GameObject("Label", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var label = go.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = size;
            label.alignment = alignment;
            label.color = TextColor;
            label.raycastTarget = false;

            var font = FindSceneFont();
            if (font != null)
                label.font = font;

            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = sizeDelta;
            rect.anchoredPosition = position;
            return label;
        }

        private Button CreateButton(Transform parent, string text, Vector2 anchor, Vector2 size, Vector2 position)
        {
            var image = CreateImage(parent, "Button " + text, PanelColor,
                skin != null ? skin.ButtonFrame : null, size);
            var rect = image.rectTransform;
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;

            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.35f, 1.25f, 1.1f, 1f);
            colors.pressedColor = new Color(0.75f, 0.7f, 0.62f, 1f);
            button.colors = colors;

            var label = CreateLabel(image.transform, text, 18f, TextAlignmentOptions.Center,
                new Vector2(0.5f, 0.5f), size, Vector2.zero);
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = Vector2.zero;
            label.rectTransform.offsetMax = Vector2.zero;

            return button;
        }

        /// <summary>
        /// 씬이 쓰는 한글 폰트를 그대로 빌린다. TMP 기본 폰트에는 한글이 없어서
        /// 그냥 두면 글자가 전부 네모로 나온다.
        /// </summary>
        private static TMP_FontAsset cachedFont;

        private static TMP_FontAsset FindSceneFont()
        {
            if (cachedFont != null)
                return cachedFont;

            foreach (var text in FindObjectsByType<TMP_Text>(FindObjectsInactive.Include))
                if (text.font != null)
                {
                    cachedFont = text.font;
                    break;
                }

            return cachedFont;
        }
    }
}
