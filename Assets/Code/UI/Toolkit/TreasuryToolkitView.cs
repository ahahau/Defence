using Code.Buildings;
using Code.Manager;
using Code.MapCreateSystem;
using UnityEngine;
using UnityEngine.UIElements;

namespace Code.UI.Toolkit
{
    /// <summary>
    /// 금고 관리 패널(UI Toolkit). 금고 칸을 누르면 뜨고, 맡기기·꺼내기와 이자·약탈 위험을 보여 준다.
    /// 옛 uGUI 패널(Resources/UI/TreasuryPanel)을 대신한다. 문서는 처음 열 때 지금 씬에 세운다.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class TreasuryToolkitView : MonoBehaviour
    {
        private const string ScreenResourcePath = "UI/TreasuryScreen";
        private const string RootName = "TreasuryToolkitUi";
        private const int QuickAmount = 25;
        private const float RefreshInterval = 0.25f;

        private const string RootElement = "treasury-root";
        private const string CardElement = "treasury-card";
        private const string IconElement = "treasury-icon";
        private const string StorageLabel = "treasury-storage";
        private const string InterestLabel = "treasury-interest";
        private const string RiskLabel = "treasury-risk";
        private const string FundsLabel = "treasury-funds";
        private const string HintLabel = "treasury-hint";
        private const string CloseButton = "treasury-close";
        private const string DepositButton = "treasury-deposit";
        private const string DepositAllButton = "treasury-deposit-all";
        private const string WithdrawButton = "treasury-withdraw";
        private const string WithdrawAllButton = "treasury-withdraw-all";
        private const string GainClass = "text-gain";
        private const string LossClass = "text-loss";

        private static TreasuryToolkitView _instance;

        private VisualElement _root;
        private VisualElement _card;
        private Label _storage;
        private Label _interest;
        private Label _risk;
        private Label _funds;
        private Label _hint;
        private Button _close;
        private Button _deposit;
        private Button _depositAll;
        private Button _withdraw;
        private Button _withdrawAll;

        private Treasury _target;
        private float _refreshTimer;

        /// <summary>그 금고의 관리 패널을 연다. 문서가 없으면 지금 씬에 세운다.</summary>
        public static void ShowFor(Treasury treasury)
        {
            if (treasury == null)
                return;

            if (_instance == null)
                _instance = ToolkitScreenSO.Spawn<TreasuryToolkitView>(ScreenResourcePath, RootName);
            if (_instance == null)
                return;

            _instance._target = treasury;
            _instance.SetVisible(true);
            _instance.Refresh();
        }

        public static void HideCurrent()
        {
            if (_instance != null)
                _instance.Hide();
        }

        /// <summary>지금 열려 있는가.</summary>
        public static bool IsOpen => _instance != null && _instance._target != null;

        private void OnEnable()
        {
            var document = GetComponent<UIDocument>();
            var tree = document.rootVisualElement;
            _root = tree.Q<VisualElement>(RootElement) ?? tree;
            _card = tree.Q<VisualElement>(CardElement);
            _storage = tree.Q<Label>(StorageLabel);
            _interest = tree.Q<Label>(InterestLabel);
            _risk = tree.Q<Label>(RiskLabel);
            _funds = tree.Q<Label>(FundsLabel);
            _hint = tree.Q<Label>(HintLabel);
            _close = tree.Q<Button>(CloseButton);
            _deposit = tree.Q<Button>(DepositButton);
            _depositAll = tree.Q<Button>(DepositAllButton);
            _withdraw = tree.Q<Button>(WithdrawButton);
            _withdrawAll = tree.Q<Button>(WithdrawAllButton);

            var icon = tree.Q<VisualElement>(IconElement);
            var sprite = DungeonHudIcon.Skin != null ? DungeonHudIcon.Skin.TreasuryIcon : null;
            if (icon != null)
            {
                if (sprite != null)
                    icon.style.backgroundImage = new StyleBackground(sprite);
                else
                    icon.style.display = DisplayStyle.None;
            }

            if (_close != null) _close.clicked += Hide;
            if (_deposit != null) _deposit.clicked += DepositQuick;
            if (_depositAll != null) _depositAll.clicked += DepositAll;
            if (_withdraw != null) _withdraw.clicked += WithdrawQuick;
            if (_withdrawAll != null) _withdrawAll.clicked += WithdrawAll;

            ToolkitPointerBlockers.Register(_card);
            SetVisible(false);
        }

        private void OnDisable()
        {
            if (_close != null) _close.clicked -= Hide;
            if (_deposit != null) _deposit.clicked -= DepositQuick;
            if (_depositAll != null) _depositAll.clicked -= DepositAll;
            if (_withdraw != null) _withdraw.clicked -= WithdrawQuick;
            if (_withdrawAll != null) _withdrawAll.clicked -= WithdrawAll;
            ToolkitPointerBlockers.Unregister(_card);
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }

        // 이자·운영 자금은 영업 중에도 바뀐다. 매 프레임 글자를 새로 만들지 않게 간격을 둔다.
        private void Update()
        {
            if (_target == null)
                return;

            _refreshTimer -= Time.unscaledDeltaTime;
            if (_refreshTimer > 0f)
                return;

            _refreshTimer = RefreshInterval;
            Refresh();
        }

        private void DepositQuick()
        {
            _target?.DepositFromOperatingFunds(QuickAmount);
            Refresh();
        }

        private void DepositAll()
        {
            _target?.DepositFromOperatingFunds(CostManager.Current != null ? CostManager.Current.CurrentGold : 0);
            Refresh();
        }

        private void WithdrawQuick()
        {
            _target?.WithdrawToOperatingFunds(QuickAmount);
            Refresh();
        }

        private void WithdrawAll()
        {
            _target?.WithdrawToOperatingFunds(_target != null ? _target.StoredGold : 0);
            Refresh();
        }

        private void Hide()
        {
            _target = null;
            SetVisible(false);
        }

        private void SetVisible(bool visible)
        {
            if (_root != null)
                _root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void Refresh()
        {
            if (_target == null || _target.IsDestroyed)
            {
                Hide();
                return;
            }

            var operatingFunds = CostManager.Current != null ? CostManager.Current.CurrentGold : 0;
            SetText(_storage, $"보관 금화  {_target.StoredGold:N0} / {_target.Capacity:N0}G");
            SetText(_funds, $"운영 자금  {operatingFunds:N0}G");

            // 이자와 약탈 위험을 같이 적어야 맡길지 말지가 판단이 된다. 길이 막힌 금고는 안전하지만 이자도 없다.
            var reachable = IntrusionThreat.CanIntrudersReach(_target.GetComponentInParent<Node>());
            var interest = _target.ProjectedInterest;
            if (reachable)
            {
                SetText(_interest, interest > 0
                    ? $"정산 이자 {_target.InterestPerSettlement:P0}  ·  다음 정산 +{interest}G"
                    : $"정산 이자 {_target.InterestPerSettlement:P0}");
                SetTone(_interest, interest > 0 ? GainClass : null);
                SetText(_risk, "침입자가 노리는 곳");
                SetTone(_risk, LossClass);
            }
            else
            {
                SetText(_interest, "이자 없음");
                SetTone(_interest, null);
                SetText(_risk, "길이 막혀 안전함");
                SetTone(_risk, null);
            }

            // 맡기면 청산일까지 묶인다는 것을 넣기 전에 알아야 한다.
            var canWithdraw = Treasury.CanWithdrawToday;
            SetText(_hint, canWithdraw
                ? "청산일입니다. 오늘은 금고를 헐 수 있습니다."
                : $"맡긴 금화는 청산일에만 꺼낼 수 있습니다. (남은 {DaysUntilSettlement()}일)");

            var canDeposit = operatingFunds > 0 && _target.FreeSpace > 0;
            var canTake = canWithdraw && _target.StoredGold > 0;
            _deposit?.SetEnabled(canDeposit);
            _depositAll?.SetEnabled(canDeposit);
            _withdraw?.SetEnabled(canTake);
            _withdrawAll?.SetEnabled(canTake);
        }

        private static int DaysUntilSettlement() =>
            DayManager.Current != null ? DayManager.Current.DaysUntilSettlement : DayManager.WeekLength;

        private static void SetText(Label label, string text)
        {
            if (label != null && label.text != text)
                label.text = text;
        }

        private static void SetTone(Label label, string toneClass)
        {
            if (label == null)
                return;

            label.EnableInClassList(GainClass, toneClass == GainClass);
            label.EnableInClassList(LossClass, toneClass == LossClass);
        }
    }
}
