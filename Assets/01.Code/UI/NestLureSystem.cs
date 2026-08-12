using _01.Code.Manager;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace _01.Code.UI
{
    public enum NestLureIntensity
    {
        Cautious,
        Standard,
        Greedy
    }

    /// <summary>
    /// 이전 선택형 유인 UI의 프리팹 호환용 컴포넌트.
    /// 실제 심리 수치는 Enemy의 경계/탐욕과 건물 조우 로직에서 개체별로 처리한다.
    /// </summary>
    public sealed class NestLureSystem : MonoBehaviour
    {
        private static readonly Color IdleColor = new(0.11f, 0.075f, 0.055f, 0.98f);
        private static readonly Color SelectedColor = new(0.42f, 0.075f, 0.045f, 0.98f);
        private static NestLureSystem _instance;

        [SerializeField] private RectTransform panel;
        [SerializeField] private TMP_Text summaryText;
        [SerializeField] private TMP_Text cohesionText;
        [SerializeField] private Button[] buttons = new Button[3];
        private float _nextCohesionRefreshTime;
        private int _lastCohesionCount = -1;

        public static NestLureIntensity SelectedIntensity { get; private set; } = NestLureIntensity.Standard;

        public static int ScaleEnemyCount(int baseCount)
        {
            return Mathf.Max(0, baseCount);
        }

        public static int ScaleGoldReward(int baseReward)
        {
            return Mathf.Max(0, baseReward);
        }

        public static string CurrentReport => "모험가별 경계 · 탐욕 추적";

        private void Awake()
        {
            _instance = this;
            panel ??= transform as RectTransform;
            for (var i = 0; i < buttons.Length; i++)
            {
                if (buttons[i] == null)
                    continue;
                var captured = (NestLureIntensity)i;
                buttons[i].onClick.AddListener(() => Select(captured));
            }
            RefreshSelection();
            RefreshCohesion(true);
        }

        private void Update()
        {
            if (panel == null)
                return;

            var dayManager = DayManager.Current;
            var shouldShow = dayManager == null || dayManager.IsStandby;
            if (panel.gameObject.activeSelf != shouldShow)
                panel.gameObject.SetActive(shouldShow);

            if (shouldShow && Time.unscaledTime >= _nextCohesionRefreshTime)
            {
                _nextCohesionRefreshTime = Time.unscaledTime + 0.25f;
                RefreshCohesion();
            }
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }

        private void Select(NestLureIntensity intensity)
        {
            if (DayManager.Current != null && !DayManager.Current.IsStandby)
                return;

            SelectedIntensity = intensity;
            RefreshSelection();
        }

        private void RefreshSelection()
        {
            if (summaryText != null)
            {
                summaryText.text = SelectedIntensity switch
                {
                    NestLureIntensity.Cautious => "경계 성향 관찰",
                    NestLureIntensity.Greedy => "탐욕 성향 관찰",
                    _ => "개체별 성향 추적"
                };
            }

            for (var i = 0; i < buttons.Length; i++)
            {
                var button = buttons[i];
                if (button == null)
                    continue;

                var selected = i == (int)SelectedIntensity;
                if (button.targetGraphic is Image image)
                    image.color = selected ? SelectedColor : IdleColor;

                var outline = button.GetComponent<Outline>();
                if (outline != null)
                {
                    outline.enabled = selected;
                    outline.effectColor = i == (int)NestLureIntensity.Greedy
                        ? new Color(1f, 0.25f, 0.12f, 1f)
                        : new Color(1f, 0.65f, 0.2f, 1f);
                    outline.effectDistance = new Vector2(2f, -2f);
                }
            }
        }

        private void RefreshCohesion(bool force = false)
        {
            if (cohesionText == null)
                return;

            var linkCount = NestCohesionSystem.CurrentLinkCount;
            if (!force && linkCount == _lastCohesionCount)
                return;

            _lastCohesionCount = linkCount;
            cohesionText.text = NestCohesionSystem.CurrentReport;
            cohesionText.color = NestCohesionSystem.CurrentTier > 0
                ? new Color(1f, 0.66f, 0.22f, 1f)
                : new Color(0.68f, 0.62f, 0.54f, 1f);
        }

    }
}
