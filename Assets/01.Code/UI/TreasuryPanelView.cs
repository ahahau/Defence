using _01.Code.Buildings;
using _01.Code.Manager;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace _01.Code.UI
{
    public sealed class TreasuryPanelView : MonoBehaviour
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
                var reachable = _01.Code.Manager.IntrusionThreat.CanIntrudersReach(
                    target.GetComponentInParent<_01.Code.MapCreateSystem.Node>());
                storageText.text = $"보관 금화  {target.StoredGold:N0} / {target.Capacity:N0}G\n"
                                   + (reachable
                                       ? $"<size=85%>정산 이자 {target.InterestPerSettlement:P0}"
                                         + (interest > 0 ? $"  ·  다음 정산 <color=#7ADB8A>+{interest}G</color>" : string.Empty)
                                         + "  ·  <color=#FF7A6B>침입자가 노리는 곳</color></size>"
                                       // 길이 막혀 안전한 금고는 이자도 없다. 왜 없는지 알려야 한다.
                                       : "<size=85%><color=#9A9182>길이 막혀 안전함 · 이자 없음</color></size>");
            }
            if (operatingFundsText != null) operatingFundsText.text = $"운영 자금  {operatingFunds:N0}G";
            if (hintText != null) hintText.text = "금고의 금화는 습격 목표가 될 수 있습니다.";

            if (depositButton != null) depositButton.interactable = operatingFunds > 0 && target.FreeSpace > 0;
            if (depositAllButton != null) depositAllButton.interactable = operatingFunds > 0 && target.FreeSpace > 0;
            if (withdrawButton != null) withdrawButton.interactable = target.StoredGold > 0;
            if (withdrawAllButton != null) withdrawAllButton.interactable = target.StoredGold > 0;
        }
    }
}
