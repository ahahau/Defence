using Code.Buildings;
using Code.Manager;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Code.UI
{
    public class TreasuryPanelView : MonoBehaviour
    {
        private const int QuickAmount = 25;
        private static TreasuryPanelView instance;

        [SerializeField] private GameObject panelRoot;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text storageText;
        [SerializeField] private TMP_Text operatingFundsText;
        [SerializeField] private TMP_Text hintText;
        [SerializeField] private Button depositButton;
        [SerializeField] private Button depositAllButton;
        [SerializeField] private Button withdrawButton;
        [SerializeField] private Button withdrawAllButton;
        [SerializeField] private Button closeButton;

        private Treasury target;

        public static void ShowFor(Treasury treasury, Canvas canvas)
        {
            if (treasury == null || canvas == null)
                return;

            if (instance == null)
            {
                foreach (var sceneView in SceneUiRegistry.EnumerateLoaded<TreasuryPanelView>())
                {
                    if (sceneView == null)
                        continue;

                    instance = sceneView;
                    break;
                }

                if (instance == null)
                {
                    var prefab = Resources.Load<TreasuryPanelView>("UI/TreasuryPanel");
                    if (prefab == null)
                    {
                        Debug.LogError("TreasuryPanel prefab is missing from Resources/UI.");
                        return;
                    }

                    instance = Instantiate(prefab, canvas.transform, false);
                }
            }

            instance.target = treasury;
            instance.panelRoot?.SetActive(true);
            instance.transform.SetAsLastSibling();
            instance.Refresh();
        }

        public static void HideCurrent()
        {
            if (instance != null)
                instance.Hide();
        }

        private void Awake()
        {
            instance = this;
            panelRoot ??= gameObject;
            DungeonHudIcon.AttachToLabel(titleText, DungeonHudIcon.Skin != null ? DungeonHudIcon.Skin.TreasuryIcon : null);
            depositButton?.onClick.AddListener(DepositQuick);
            depositAllButton?.onClick.AddListener(DepositAll);
            withdrawButton?.onClick.AddListener(WithdrawQuick);
            withdrawAllButton?.onClick.AddListener(WithdrawAll);
            closeButton?.onClick.AddListener(Hide);
            Hide();
        }

        private void Update()
        {
            if (target == null || panelRoot == null || !panelRoot.activeSelf)
                return;

            Refresh();
        }

        private void OnDestroy()
        {
            if (instance == this)
                instance = null;
        }

        private void DepositQuick()
        {
            target?.DepositFromOperatingFunds(QuickAmount);
            Refresh();
        }

        private void DepositAll()
        {
            target?.DepositFromOperatingFunds(CostManager.Current != null ? CostManager.Current.CurrentGold : 0);
            Refresh();
        }

        private void WithdrawQuick()
        {
            target?.WithdrawToOperatingFunds(QuickAmount);
            Refresh();
        }

        private void WithdrawAll()
        {
            target?.WithdrawToOperatingFunds(target != null ? target.StoredGold : 0);
            Refresh();
        }

        private static int DaysUntilSettlement() =>
            Code.Manager.DayManager.Current != null
                ? Code.Manager.DayManager.Current.DaysUntilSettlement
                : Code.Manager.DayManager.WeekLength;

        private void Hide()
        {
            if (panelRoot != null)
                panelRoot.SetActive(false);
            target = null;
        }

        private void Refresh()
        {
            if (target == null)
            {
                Hide();
                return;
            }

            var operatingFunds = CostManager.Current != null ? CostManager.Current.CurrentGold : 0;
            if (titleText != null) titleText.text = "금고 관리";
            if (storageText != null)
            {
                // 이자와 약탈 위험을 같이 적어야 맡길지 말지가 판단이 된다.
                var interest = target.ProjectedInterest;
                var reachable = Code.Manager.IntrusionThreat.CanIntrudersReach(
                    target.GetComponentInParent<Code.MapCreateSystem.Node>());
                storageText.text = $"보관 금화  {target.StoredGold:N0} / {target.Capacity:N0}G\n"
                                   + (reachable
                                       ? $"<size=85%>정산 이자 {target.InterestPerSettlement:P0}"
                                         + (interest > 0 ? $"  ·  다음 정산 <color=#7ADB8A>+{interest}G</color>" : string.Empty)
                                         + "  ·  <color=#FF7A6B>침입자가 노리는 곳</color></size>"
                                       // 길이 막혀 안전한 금고는 이자도 없다. 왜 없는지 알려야 한다.
                                       : "<size=85%><color=#9A9182>길이 막혀 안전함 · 이자 없음</color></size>");
            }
            if (operatingFundsText != null) operatingFundsText.text = $"운영 자금  {operatingFunds:N0}G";

            // 맡기면 청산일까지 묶인다는 것을 넣기 전에 알아야 한다. 넣고 나서 알면
            // 그건 플레이어가 고른 결과가 아니라 화면이 숨긴 결과가 된다.
            var canWithdraw = Code.Buildings.Treasury.CanWithdrawToday;
            if (hintText != null)
                hintText.text = canWithdraw
                    ? "청산일입니다. 오늘은 금고를 헐 수 있습니다."
                    : $"맡긴 금화는 청산일에만 꺼낼 수 있습니다. (남은 {DaysUntilSettlement()}일)";

            if (depositButton != null) depositButton.interactable = operatingFunds > 0 && target.FreeSpace > 0;
            if (depositAllButton != null) depositAllButton.interactable = operatingFunds > 0 && target.FreeSpace > 0;
            if (withdrawButton != null) withdrawButton.interactable = canWithdraw && target.StoredGold > 0;
            if (withdrawAllButton != null) withdrawAllButton.interactable = canWithdraw && target.StoredGold > 0;
        }
    }
}
