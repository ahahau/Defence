using System.Collections;
using _01.Code.Core;
using _01.Code.UI;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace _01.Code.Manager
{
    /// <summary>보스 웨이브 연출 담당 — 시작 배너, 처치 시 카메라 줌인+슬로모 시네마틱, 승리 패널.
    /// UI는 프리팹으로 구성하며 Resources/UI에 기본 프리팹을 둔다.
    /// WaveManager가 생성/호출한다.</summary>
    public class BossWavePresenter : MonoBehaviour
    {
        [Header("Banner")]
        [SerializeField] private Canvas uiCanvas;
        [SerializeField] private BossWaveUiView bannerPrefab;
        [SerializeField] private BossWaveUiView victoryPrefab;
        [SerializeField] private BossWaveUiView bannerView;
        [SerializeField] private BossWaveUiView victoryView;
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

        /// <param name="bossTitle">그 날 보스의 이름. 비어 있으면 기본 문구로 떨어진다.</param>
        /// <param name="bossSubtitle">보스마다 다른 경고 문구. 무엇을 조심해야 하는지 알린다.</param>
        public void ShowBossBanner(int day, bool isFinal, string bossTitle = null, string bossSubtitle = null)
        {
            if (uiCanvas == null)
                return;

            var accent = isFinal ? finalBannerColor : bossBannerColor;
            var title = !string.IsNullOrWhiteSpace(bossTitle)
                ? bossTitle
                : isFinal ? "최후의 결전" : "보스 습격";
            var subtitle = !string.IsNullOrWhiteSpace(bossSubtitle)
                ? $"Day {day} — {bossSubtitle}"
                : isFinal
                    ? $"Day {day} — 이 습격만 막아내면 던전을 지켜낸다"
                    : $"Day {day} — 강력한 모험가가 쳐들어온다";

            var view = bannerView;
            if (view == null)
            {
                Debug.LogError("BossWavePresenter requires a scene-assigned boss banner view.", this);
                return;
            }

            view.gameObject.SetActive(true);
            view.transform.SetAsLastSibling();
            view.Title.text = title;
            view.Title.color = accent;
            view.Subtitle.text = subtitle;
            var root = view.gameObject;
            var rect = (RectTransform)view.transform;
            var group = view.CanvasGroup;
            group.alpha = 0f;
            group.blocksRaycasts = false;

            DOTween.Sequence().SetUpdate(true).SetLink(root)
                .Append(group.DOFade(1f, 0.25f))
                .Join(rect.DOAnchorPosX(0f, 0.35f).From(new Vector2(-80f, rect.anchoredPosition.y)).SetEase(Ease.OutCubic))
                .AppendInterval(bannerHoldDuration)
                .Append(group.DOFade(0f, 0.4f))
                .OnComplete(() => root.SetActive(false));

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

            var view = victoryView;
            if (view == null)
            {
                Debug.LogError("BossWavePresenter requires a scene-assigned victory panel view.", this);
                Time.timeScale = 1f;
                return;
            }

            view.gameObject.SetActive(true);
            view.transform.SetAsLastSibling();
            view.Title.text = "던전 사수 성공!";
            view.Title.color = finalBannerColor;
            view.Subtitle.text = $"{day}일간의 침공을 모두 막아냈습니다";
            view.RetryButton.onClick.RemoveAllListeners();
            view.RetryButton.onClick.AddListener(() =>
            {
                Time.timeScale = 1f;
                SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
            });

            var root = view.gameObject;
            var group = view.CanvasGroup;
            group.alpha = 0f;
            group.DOFade(1f, 0.5f).SetUpdate(true).SetLink(root);
        }
    }
}
