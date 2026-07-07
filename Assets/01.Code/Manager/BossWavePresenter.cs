using System.Collections;
using _01.Code.Audio;
using _01.Code.Core;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace _01.Code.Manager
{
    /// <summary>보스 웨이브 연출 담당 — 시작 배너, 처치 시 카메라 줌인+슬로모 시네마틱, 승리 패널.
    /// UI는 런타임에 생성하므로 씬 배선이 필요 없다(TMP 기본 폰트가 한글 폰트로 설정되어 있음).
    /// WaveManager가 생성/호출한다.</summary>
    public class BossWavePresenter : MonoBehaviour
    {
        [Header("Banner")]
        [SerializeField] private Canvas uiCanvas;
        [SerializeField, Min(0.1f)] private float bannerHoldDuration = 1.6f;
        [SerializeField] private Color bossBannerColor = new(0.85f, 0.15f, 0.1f, 1f);
        [SerializeField] private Color finalBannerColor = new(1f, 0.78f, 0.2f, 1f);

        [Header("Death Cinematic")]
        [SerializeField, Range(0.05f, 0.5f), Tooltip("보스가 쓰러지는 동안의 슬로모 배율.")]
        private float cinematicTimeScale = 0.15f;
        [SerializeField, Min(0.1f)] private float cinematicZoomDuration = 0.45f;
        [SerializeField, Min(0.1f)] private float cinematicHoldDuration = 1.2f;
        [SerializeField, Min(1f), Tooltip("줌인 시 카메라 orthographicSize 목표(작을수록 확대).")]
        private float cinematicOrthoSize = 3.2f;

        private bool _cinematicRunning;

        /// <summary>처치 시네마틱 진행 중인지(승리 패널 타이밍 조율용).</summary>
        public bool IsCinematicRunning => _cinematicRunning;

        // ── Banner ──────────────────────────────────────────────

        public void ShowBossBanner(int day, bool isFinal)
        {
            if (uiCanvas == null)
                return;

            var accent = isFinal ? finalBannerColor : bossBannerColor;
            var title = isFinal ? "최후의 결전" : "보스 습격";
            var subtitle = isFinal
                ? $"Day {day} — 이 습격만 막아내면 던전을 지켜낸다"
                : $"Day {day} — 강력한 모험가가 쳐들어온다";

            var root = CreateOverlayRect(uiCanvas.transform, "BossWaveBanner");
            var rect = (RectTransform)root.transform;
            rect.anchorMin = new Vector2(0f, 0.62f);
            rect.anchorMax = new Vector2(1f, 0.62f);
            rect.sizeDelta = new Vector2(0f, 130f);

            var backdrop = root.AddComponent<Image>();
            backdrop.color = new Color(0.03f, 0.02f, 0.02f, 0.82f);
            backdrop.raycastTarget = false;

            CreateText(rect, title, 52f, accent, new Vector2(0f, 18f));
            CreateText(rect, subtitle, 22f, new Color(0.92f, 0.88f, 0.85f, 1f), new Vector2(0f, -32f));

            var group = root.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;

            DOTween.Sequence().SetUpdate(true).SetLink(root)
                .Append(group.DOFade(1f, 0.25f))
                .Join(rect.DOAnchorPosX(0f, 0.35f).From(new Vector2(-80f, rect.anchoredPosition.y)).SetEase(Ease.OutCubic))
                .AppendInterval(bannerHoldDuration)
                .Append(group.DOFade(0f, 0.4f))
                .OnComplete(() => Destroy(root));

            GameSfxPlayer.Play(GameSfxCue.WaveStart);
        }

        // ── Death Cinematic ─────────────────────────────────────

        /// <summary>보스 처치 순간 — 슬로모 + 카메라 줌인으로 쓰러지는 모습을 보여준 뒤 복귀.</summary>
        public void PlayBossDeathCinematic(Vector3 bossPosition)
        {
            if (_cinematicRunning || Time.timeScale <= 0f)
                return;

            var cam = Camera.main;
            if (cam == null || !cam.orthographic)
                return;

            StartCoroutine(DeathCinematicRoutine(cam, bossPosition));
        }

        private IEnumerator DeathCinematicRoutine(Camera cam, Vector3 bossPosition)
        {
            _cinematicRunning = true;

            var previousTimeScale = Time.timeScale;
            Time.timeScale = cinematicTimeScale;

            var camTransform = cam.transform;
            var originalPosition = camTransform.position;
            var originalSize = cam.orthographicSize;
            var focusPosition = new Vector3(bossPosition.x, bossPosition.y, originalPosition.z);
            var zoomSize = Mathf.Min(originalSize, Mathf.Max(1.5f, cinematicOrthoSize));

            camTransform.DOKill();
            cam.DOKill();
            camTransform.DOMove(focusPosition, cinematicZoomDuration).SetEase(Ease.InOutQuad).SetUpdate(true);
            cam.DOOrthoSize(zoomSize, cinematicZoomDuration).SetEase(Ease.InOutQuad).SetUpdate(true);
            // 시네마틱 동안 화면 가장자리를 조여 시선을 보스에 모은다.
            ScenePostProcessing.PulseVignette(0.18f, cinematicZoomDuration * 2f + cinematicHoldDuration);

            yield return new WaitForSecondsRealtime(cinematicZoomDuration + cinematicHoldDuration);

            camTransform.DOMove(originalPosition, cinematicZoomDuration).SetEase(Ease.InOutQuad).SetUpdate(true);
            cam.DOOrthoSize(originalSize, cinematicZoomDuration).SetEase(Ease.InOutQuad).SetUpdate(true);

            yield return new WaitForSecondsRealtime(cinematicZoomDuration);

            // 시네마틱 도중 외부(일시정지/게임오버)에서 timeScale을 건드렸다면 복원하지 않는다(HitStopRunner와 같은 규약).
            if (Mathf.Approximately(Time.timeScale, cinematicTimeScale))
                Time.timeScale = previousTimeScale;

            _cinematicRunning = false;
        }

        // ── Victory ─────────────────────────────────────────────

        /// <summary>최종 보스 웨이브 클리어 — 승리 패널을 띄우고 게임을 멈춘다.</summary>
        public void ShowVictoryPanel(int day)
        {
            if (uiCanvas == null)
                return;

            Time.timeScale = 0f;
            GameSfxPlayer.Play(GameSfxCue.UiReward);

            var root = CreateOverlayRect(uiCanvas.transform, "VictoryPanel");
            var rect = (RectTransform)root.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.sizeDelta = Vector2.zero;

            var backdrop = root.AddComponent<Image>();
            backdrop.color = new Color(0.02f, 0.025f, 0.03f, 0.92f);

            CreateText(rect, "던전 사수 성공!", 64f, finalBannerColor, new Vector2(0f, 90f));
            CreateText(rect, $"{day}일간의 침공을 모두 막아냈습니다", 26f, new Color(0.9f, 0.88f, 0.82f, 1f), new Vector2(0f, 20f));

            CreateButton(rect, "다시 도전하기", new Vector2(0f, -90f), () =>
            {
                Time.timeScale = 1f;
                SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
            });

            var group = root.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.DOFade(1f, 0.5f).SetUpdate(true).SetLink(root);
        }

        // ── UI Helpers ──────────────────────────────────────────

        private static GameObject CreateOverlayRect(Transform parent, string objectName)
        {
            var root = new GameObject(objectName, typeof(RectTransform));
            var rect = (RectTransform)root.transform;
            rect.SetParent(parent, false);
            rect.SetAsLastSibling();
            return root;
        }

        private static void CreateText(RectTransform parent, string text, float size, Color color, Vector2 offset)
        {
            var textObject = new GameObject("Text", typeof(RectTransform));
            var rect = (RectTransform)textObject.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(1f, 0.5f);
            rect.sizeDelta = new Vector2(0f, size + 16f);
            rect.anchoredPosition = offset;

            var tmp = textObject.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontStyle = FontStyles.Bold;
            tmp.raycastTarget = false;
        }

        private static void CreateButton(RectTransform parent, string label, Vector2 offset, UnityEngine.Events.UnityAction onClick)
        {
            var buttonObject = new GameObject("RetryButton", typeof(RectTransform));
            var rect = (RectTransform)buttonObject.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(280f, 64f);
            rect.anchoredPosition = offset;

            var image = buttonObject.AddComponent<Image>();
            image.color = new Color(0.25f, 0.45f, 0.27f, 0.95f);

            var button = buttonObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(onClick);

            var labelObject = new GameObject("Label", typeof(RectTransform));
            var labelRect = (RectTransform)labelObject.transform;
            labelRect.SetParent(rect, false);
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.sizeDelta = Vector2.zero;

            var tmp = labelObject.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = 26f;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontStyle = FontStyles.Bold;
            tmp.raycastTarget = false;
        }
    }
}
