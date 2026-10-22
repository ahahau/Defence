using System.Collections;
using System.Text;
using Code.Core;
using Code.Progression;
using Code.UI;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Code.Manager
{
    /// <summary>보스 웨이브 연출 담당 — 시작 배너, 처치 시 카메라 줌인+슬로모 시네마틱, 승리 패널.
    /// UI는 프리팹으로 구성하며 Resources/UI에 기본 프리팹을 둔다.
    /// WaveManager가 생성/호출한다.</summary>
    public class BossWavePresenter : MonoBehaviour
    {
        [Header("Runtime Dependencies")]
        [SerializeField] private DayManager dayManager;
        [SerializeField] private WaveManager waveManager;
        [SerializeField] private RunSummarySystem runSummarySystem;
        [SerializeField] private CostManager costManager;
        [SerializeField] private HiredUnitRoster hiredUnitRoster;
        [SerializeField] private GameSpeedController gameSpeedController;

        [Header("Banner")]
        [SerializeField, Min(0.1f)] private float bannerHoldDuration = 1.6f;
        [SerializeField] private Color finalBannerColor = new(1f, 0.78f, 0.2f, 1f);
        [SerializeField, Tooltip("패배 결과 화면의 강조색.")]
        private Color defeatPanelColor = new(0.9f, 0.3f, 0.25f, 1f);

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

        /// <summary>보스 배너를 띄운다. 그리는 화면은 <c>BossBannerView</c>.</summary>
        public event System.Action<BossBannerRequest> BannerRequested;

        /// <summary>판이 끝나 결과 화면을 띄운다(제목, 한 줄 요약, 이겼는가).</summary>
        public event System.Action<string, string, bool> RunEndRequested;

        private void Awake()
        {
            ResolveDependencies();
        }

        // ── Banner ──────────────────────────────────────────────

        /// <param name="bossTitle">그 날 보스의 이름. 비어 있으면 기본 문구로 떨어진다.</param>
        /// <param name="bossSubtitle">보스마다 다른 경고 문구. 무엇을 조심해야 하는지 알린다.</param>
        public void ShowBossBanner(int day, bool isFinal, string bossTitle = null, string bossSubtitle = null)
        {
            var subtitle = !string.IsNullOrWhiteSpace(bossSubtitle)
                ? bossSubtitle
                : isFinal
                    ? "이 습격만 막아내면 던전을 지켜낸다"
                    : "강력한 모험가가 쳐들어온다";

            RequestBanner(isFinal ? BossBannerKind.Final : BossBannerKind.Arrival, day, bossTitle, subtitle);
        }

        /// <summary>보스가 부른 증원이 합류했을 때.</summary>
        public void ShowReinforcementBanner(int day, string bossTitle, string subtitle) =>
            RequestBanner(BossBannerKind.Reinforcement, day, bossTitle, subtitle);

        /// <summary>호위가 무너져 보스가 마지막 단계에 들어갔을 때.</summary>
        public void ShowRageBanner(int day, string bossTitle, string subtitle) =>
            RequestBanner(BossBannerKind.Rage, day, bossTitle, subtitle);

        private void RequestBanner(BossBannerKind kind, int day, string bossTitle, string subtitle) =>
            BannerRequested?.Invoke(new BossBannerRequest(kind, day, bossTitle, subtitle, bannerHoldDuration));

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

            yield return new WaitForSecondsRealtime(cinematicZoomDuration + cinematicHoldDuration);

            camTransform.DOMove(originalPosition, cinematicZoomDuration).SetEase(Ease.InOutQuad).SetUpdate(true);
            cam.DOOrthoSize(originalSize, cinematicZoomDuration).SetEase(Ease.InOutQuad).SetUpdate(true);

            yield return new WaitForSecondsRealtime(cinematicZoomDuration);

            // 연출 도중 플레이어가 배속을 바꿨거나 모달이 열렸으면 그쪽 뜻을 따른다(HitStopRunner와 같은 규약).
            if (Mathf.Approximately(Time.timeScale, cinematicTimeScale))
                Time.timeScale = gameSpeedController != null
                    ? gameSpeedController.RestoreTimeScale
                    : GameSpeedController.NormalSpeed;

            _cinematicRunning = false;
        }

        // ── Victory ─────────────────────────────────────────────

        /// <summary>최종 보스 웨이브 클리어 — 승리 패널을 띄우고 게임을 멈춘다.</summary>
        public void ShowVictoryPanel(int day)
        {
            ShowRunEndPanel("던전 사수 성공!", $"{day}일간의 침공을 모두 막아냈습니다", true);
        }

        /// <summary>
        /// 패배로 판이 끝났을 때. 여태 결과 화면이 없어 게임이 멈춘 채로 남아 있었다.
        /// </summary>
        public void ShowDefeatPanel(string reason)
        {
            var day = dayManager != null ? dayManager.CurrentDay : 0;
            var headline = string.IsNullOrWhiteSpace(reason)
                ? $"{day}일차에 던전이 무너졌습니다"
                : reason;
            ShowRunEndPanel("던전 함락", headline, false);
        }

        /// <summary>
        /// 승리와 패배가 같은 패널을 쓴다. 둘이 동시에 뜰 일이 없고,
        /// 무엇이 남았는지 돌아보는 화면이라는 점에서 내용도 같다.
        /// </summary>
        private void ShowRunEndPanel(string title, string headline, bool victory)
        {
            // 런 종료 뒤의 이어하기는 끝난 판을 되살릴 수 없다. 재시작 버튼을 누를 때까지
            // 저장을 남기면 타이틀에서 끝난 런을 이어하는 모순이 생긴다.
            Code.Persistence.RunSaveSystem.DeleteSave();
            // 판이 끝난 화면이라 푸는 쪽이 없다. 재시작할 때 씬이 새로 뜨며 정상으로 돌아간다.
            if (gameSpeedController != null)
                gameSpeedController.Suspend(this);
            else
                Time.timeScale = 0f;

            RunEndRequested?.Invoke(title, headline, victory);
        }

        private void ResolveDependencies()
        {
            waveManager ??= GetComponent<WaveManager>();
        }
    }
}
