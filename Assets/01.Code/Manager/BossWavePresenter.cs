using System.Collections;
using System.Text;
using _01.Code.Core;
using _01.Code.Progression;
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
            ShowRunEndPanel("던전 사수 성공!", $"{day}일간의 침공을 모두 막아냈습니다", finalBannerColor, true);
        }

        /// <summary>
        /// 패배로 판이 끝났을 때. 여태 결과 화면이 없어 게임이 멈춘 채로 남아 있었다.
        /// </summary>
        public void ShowDefeatPanel(string reason)
        {
            var day = DayManager.Current != null ? DayManager.Current.CurrentDay : 0;
            var headline = string.IsNullOrWhiteSpace(reason)
                ? $"{day}일차에 던전이 무너졌습니다"
                : reason;
            ShowRunEndPanel("던전 함락", headline, defeatPanelColor, false);
        }

        /// <summary>
        /// 승리와 패배가 같은 패널을 쓴다. 둘이 동시에 뜰 일이 없고,
        /// 무엇이 남았는지 돌아보는 화면이라는 점에서 내용도 같다.
        /// </summary>
        private void ShowRunEndPanel(string title, string headline, Color accent, bool clearSaveOnRetry)
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
            view.Title.text = title;
            view.Title.color = accent;
            view.Subtitle.text = headline + BuildRunSummaryText();
            view.RetryButton.onClick.RemoveAllListeners();
            view.RetryButton.onClick.AddListener(() =>
            {
                if (clearSaveOnRetry)
                    _01.Code.Persistence.RunSaveSystem.DeleteSave();
                Time.timeScale = 1f;
                SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
            });

            var root = view.gameObject;
            var group = view.CanvasGroup;
            group.alpha = 0f;
            group.DOFade(1f, 0.5f).SetUpdate(true).SetLink(root);
        }

        /// <summary>
        /// 한 판을 돌아보는 숫자들. 정산은 하루치만 보여주고 사라지므로
        /// 판 전체가 어땠는지는 여기서만 볼 수 있다.
        /// </summary>
        private static string BuildRunSummaryText()
        {
            var lines = new StringBuilder();
            lines.Append("\n<size=80%>");

            var finalDay = WaveManager.Current != null ? WaveManager.Current.FinalDay : 0;
            var survived = DayManager.Current != null ? DayManager.Current.CurrentDay : 0;
            lines.Append(finalDay > 0
                ? $"\n버틴 날  {survived} / {finalDay}일"
                : $"\n버틴 날  {survived}일");

            var run = RunSummarySystem.Current;
            if (run != null && run.Invaders > 0)
            {
                lines.Append($"\n격퇴  {run.Kills} / {run.Invaders}명"
                             + $"  ·  방어전 {run.WavesFought}회");
                lines.Append($"\n가한 피해  {run.DamageDealt}"
                             + $"  ·  받은 피해 {run.DamageTaken}"
                             + $"  ·  치명타 {run.CriticalHits}");
            }

            var cost = CostManager.Current;
            if (cost != null)
            {
                var peakDebt = run != null ? run.PeakDebt : 0;
                lines.Append($"\n남은 금화  {cost.CurrentGold}G  ·  최대 부채 {peakDebt}G");
            }

            var roster = HiredUnitRoster.Current;
            if (roster != null)
            {
                lines.Append($"\n부하  {roster.TotalHiredCount}명"
                             + $"  ·  유닛 해금 {roster.UnlockedUnits.Count}/{roster.UnlockableUnitCount}"
                             + $"  ·  시설 해금 {roster.UnlockedBuildings.Count}/{roster.UnlockableBuildingCount}");
            }

            var conquest = VillageConquestSystem.Current;
            if (conquest != null && conquest.VillageCount > 0)
                lines.Append($"\n장악한 마을  {conquest.FullyConqueredCount} / {conquest.VillageCount}곳");

            lines.Append("</size>");
            return lines.ToString();
        }
    }
}
