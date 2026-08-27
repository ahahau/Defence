using _01.Code.Manager;
using TMPro;
using UnityEngine;

namespace _01.Code.UI
{
    /// <summary>
    /// 핵심부 결속 현황을 오른쪽 HUD에 띄운다.
    /// 결속은 웨이브 보상을 최대 +15%까지 올리는데 여태 화면 어디에도 나오지 않아,
    /// 플레이어는 배치 방식으로 채점당하면서 그 규칙이 있다는 것조차 알 수 없었다.
    /// </summary>
    public sealed class CoreCohesionHudView : MonoBehaviour
    {
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private TMP_Text cohesionText;

        [SerializeField, Min(0f), Tooltip("갱신 간격(초). 매 프레임 전 노드의 격자를 훑을 일은 아니다.")]
        private float refreshInterval = 0.5f;

        private float _nextRefreshTime;
        private int _lastLinkCount = -1;

        private void Awake()
        {
            DungeonHudStyle.ApplyPanel(panelRoot != null ? panelRoot : gameObject);
            DungeonHudStyle.ApplyTopRightCard(panelRoot != null ? panelRoot : gameObject, cohesionText, 4,
                new Color(0.62f, 0.78f, 1f, 1f));
            Refresh(true);
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextRefreshTime)
                return;

            _nextRefreshTime = Time.unscaledTime + Mathf.Max(0.05f, refreshInterval);
            Refresh(false);
        }

        private void Refresh(bool force)
        {
            if (cohesionText == null)
                return;

            var links = CoreCohesionSystem.CurrentLinkCount;
            if (!force && links == _lastLinkCount)
                return;

            _lastLinkCount = links;
            cohesionText.text = $"결속 {links}/{CoreCohesionSystem.MaxContributingLinks}"
                                + $" · 보상 +{CoreCohesionSystem.CurrentRewardBonusPercent}%";
        }
    }
}
