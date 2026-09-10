using _01.Code.Manager;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.InputSystem;

namespace _01.Code.UI
{
    /// <summary>
    /// 한동안 아무것도 건드리지 않으면 판을 저장하고 타이틀로 돌려보낸다.
    ///
    /// 발표 자리에 켜 둔 채 자리를 비우면 게임이 아무 데서나 멈춰 있게 된다. 다음 사람이
    /// 와서 처음부터 하려면 남의 판을 손으로 정리해야 한다. 시간이 지나면 스스로 첫 화면으로
    /// 돌아가는 편이 낫다.
    ///
    /// 습격이 도는 동안에는 세지 않는다. 습격 한 판은 3분을 넘기기도 하는데, 그동안 보고만
    /// 있는 것도 게임을 하는 것이다. 그때 쫓아내면 기능이 아니라 버그다.
    ///
    /// 끌려 나가기 전에 남은 시간을 알려 준다. 아무 예고 없이 화면이 바뀌면 무엇이 잘못된 줄 안다.
    /// </summary>
    public sealed class IdleReturnToTitle : MonoBehaviour
    {
        private const string NoticePrefabResourcePath = "UI/IdleReturnNotice";

        /// <summary>이만큼 아무것도 안 하면 나간다.</summary>
        private const float IdleSeconds = 60f;

        /// <summary>나가기 이만큼 전부터 알린다.</summary>
        private const float WarnSeconds = 10f;

        /// <summary>
        /// 이만큼 넘게 움직인 마우스만 "만졌다"로 친다.
        ///
        /// 책상에 놓인 마우스는 가만히 두어도 1픽셀씩 떨린다. 그걸 다 활동으로 세면
        /// 시계가 영영 돌지 않는다.
        /// </summary>
        private const float MouseMoveThreshold = 4f;

        private static readonly Color NoticeColor = new(0.055f, 0.034f, 0.025f, 0.95f);
        private static readonly Color TextColor = new(0.96f, 0.88f, 0.72f, 1f);

        private IdleReturnNoticeRefs notice;
        private float _idleSeconds;
        private int _shownRemaining = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            var host = new GameObject("Idle Return To Title");
            host.AddComponent<IdleReturnToTitle>();
            DontDestroyOnLoad(host);
        }

        private void Start()
        {
            notice = ResolveNotice(BuildCanvas().transform);
            SetNoticeVisible(false);
        }

        private void Update()
        {
            if (!ShouldCount())
            {
                ResetTimer();
                return;
            }

            _idleSeconds += Time.unscaledDeltaTime;

            var remaining = IdleSeconds - _idleSeconds;
            if (remaining <= 0f)
            {
                LeaveToTitle();
                return;
            }

            if (remaining > WarnSeconds)
            {
                SetNoticeVisible(false);
                return;
            }

            ShowCountdown(Mathf.CeilToInt(remaining));
        }

        /// <summary>
        /// 지금 시간을 세도 되는가.
        ///
        /// 타이틀에서는 셀 이유가 없고 — 이미 돌아갈 곳에 있다 — 습격 중에도 세지 않는다.
        /// 나머지 경우에 손이 닿았으면 시계를 되돌린다.
        /// </summary>
        private bool ShouldCount()
        {
            if (SceneManager.GetActiveScene().name == TitleMenuActions.TitleSceneName)
                return false;

            var day = DayManager.Current;
            if (day != null && !day.IsStandby)
                return false;

            return !TouchedThisFrame();
        }

        private void ResetTimer()
        {
            _idleSeconds = 0f;
            SetNoticeVisible(false);
        }

        /// <summary>
        /// 이번 프레임에 사람이 무엇이든 건드렸는가.
        ///
        /// 게임의 입력 배선을 거치지 않고 장치를 직접 본다. 키 하나하나를 어디에 묶었는지와
        /// 상관없이 "사람이 있다"만 알면 되기 때문이다.
        /// </summary>
        private static bool TouchedThisFrame()
        {
            // 눌렀는가가 아니라 누르고 있는가를 본다. WASD 로 화면을 미는 동안에는 새로 눌리는
            // 키가 없어서, 누른 순간만 세면 화면을 밀고 있는 사람도 자리를 비운 것으로 친다.
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.anyKey.isPressed)
                return true;

            var mouse = Mouse.current;
            if (mouse != null)
            {
                if (mouse.leftButton.isPressed
                    || mouse.rightButton.isPressed
                    || mouse.middleButton.isPressed)
                    return true;

                if (Mathf.Abs(mouse.scroll.ReadValue().y) > Mathf.Epsilon)
                    return true;

                if (mouse.delta.ReadValue().sqrMagnitude > MouseMoveThreshold * MouseMoveThreshold)
                    return true;
            }

            var touch = Touchscreen.current;
            return touch != null && touch.primaryTouch.press.isPressed;
        }

        /// <summary>
        /// 판을 저장하고 첫 화면으로 돌아간다.
        ///
        /// 저장을 먼저 하는 게 핵심이다. 판은 습격이 끝날 때마다 저장되므로, 대기 중에 지은 것을
        /// 남기지 않으면 자리를 비운 벌로 그날 지은 방이 사라진다.
        ///
        /// 시간 배속을 되돌리는 것도 <see cref="SettingsPanelView"/>와 같은 이유다 — 정산 창이
        /// timeScale 을 0 으로 잡아 둔 채 나가면 다음 판이 멈춘 채로 시작한다.
        /// </summary>
        private void LeaveToTitle()
        {
            ResetTimer();
            _01.Code.Persistence.RunSaveSystem.SaveCurrentRun();
            Time.timeScale = 1f;
            SceneManager.LoadScene(TitleMenuActions.TitleSceneName);
        }

        private void ShowCountdown(int seconds)
        {
            SetNoticeVisible(true);

            if (notice == null || notice.countdownText == null || _shownRemaining == seconds)
                return;

            _shownRemaining = seconds;
            notice.countdownText.text = $"{seconds}초 뒤 타이틀로 돌아갑니다\n아무 키나 누르면 계속합니다";
        }

        private void SetNoticeVisible(bool visible)
        {
            if (notice == null || notice.window == null)
                return;

            if (!visible)
                _shownRemaining = -1;

            if (notice.window.activeSelf != visible)
                notice.window.SetActive(visible);
        }

        /// <summary>
        /// 알림을 구해 온다. 프리팹이 있으면 그것을, 없으면 코드로 세운다.
        ///
        /// <see cref="SettingsPanelView"/>와 같은 이유로 코드 경로를 남겨 둔다. 프리팹이 빠진 채
        /// 배포되면 예고 없이 화면이 바뀌는데, 모양이 조금 달라도 뜨는 편이 훨씬 낫다.
        /// </summary>
        private IdleReturnNoticeRefs ResolveNotice(Transform parent)
        {
            var prefab = Resources.Load<GameObject>(NoticePrefabResourcePath);
            if (prefab == null)
                return BuildNotice(parent);

            var instance = Instantiate(prefab, parent, false);
            var refs = instance.GetComponent<IdleReturnNoticeRefs>();
            if (refs != null && refs.IsComplete)
                return refs;

            Debug.LogWarning($"{NoticePrefabResourcePath} 의 참조 표가 비어 있어 알림을 코드로 세웁니다.", instance);
            Destroy(instance);
            return BuildNotice(parent);
        }

        private Canvas BuildCanvas()
        {
            var go = new GameObject("Idle Notice Canvas",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.transform.SetParent(transform, false);

            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // 설정 창보다도 위에 뜬다. 무엇을 열어 두었든 나간다는 말은 보여야 한다.
            canvas.sortingOrder = 6000;

            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            return canvas;
        }

        private static IdleReturnNoticeRefs BuildNotice(Transform parent)
        {
            var window = new GameObject("Idle Notice", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            window.transform.SetParent(parent, false);

            var rect = (RectTransform)window.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(560f, 150f);
            rect.anchoredPosition = Vector2.zero;

            var background = window.GetComponent<Image>();
            background.color = NoticeColor;
            // 알림은 읽으라고 띄우는 것이지 누르라고 띄우는 게 아니다. 클릭을 삼키면
            // 계속하려고 누른 손가락이 게임에 닿지 않는다.
            background.raycastTarget = false;

            var label = new GameObject("Countdown", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            label.transform.SetParent(window.transform, false);

            var labelRect = (RectTransform)label.transform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(24f, 20f);
            labelRect.offsetMax = new Vector2(-24f, -20f);

            var text = label.GetComponent<TextMeshProUGUI>();
            text.color = TextColor;
            text.fontSize = 30f;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;

            var refs = window.AddComponent<IdleReturnNoticeRefs>();
            refs.window = window;
            refs.countdownText = text;
            return refs;
        }
    }
}
