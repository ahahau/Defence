using Code.Enemies;
using Code.StatusEffects;
using UnityEngine;
using UnityEngine.UIElements;

namespace Code.UI.Toolkit
{
    /// <summary>
    /// 모험가 상태 패널(UI Toolkit). 모험가를 누르면 그 자리 옆에 뜬다. 체력·공격·목적·예산·파티 자금·경계·탐욕·
    /// 철수 확률과 상태이상을 보여 준다. 이벤트 구독은 씬의 <see cref="EnemyStatusPanelView"/>가 맡고 여기로 넘긴다.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class EnemyStatusToolkitView : MonoBehaviour
    {
        private const string ScreenResourcePath = "UI/EnemyStatusScreen";
        private const string RootName = "EnemyStatusToolkitUi";
        private const float RefreshInterval = 0.12f;
        private const int MaxEffectLines = 4;
        private static readonly Vector2 ScreenOffset = new(24f, 24f);

        private const string RootElement = "enemy-status-root";
        private const string CardElement = "enemy-status-card";
        private const string TitleLabel = "enemy-status-title";
        private const string StateLabel = "enemy-status-state";
        private const string HpLabel = "enemy-status-hp";
        private const string AttackLabel = "enemy-status-attack";
        private const string PurposeLabel = "enemy-status-purpose";
        private const string MoodLabel = "enemy-status-mood";
        private const string EffectsTitleLabel = "enemy-status-effects-title";
        private const string EffectLabelPrefix = "enemy-status-effect-";
        private const string CloseButton = "enemy-status-close";

        private static EnemyStatusToolkitView _instance;

        private VisualElement _root;
        private VisualElement _card;
        private Label _title;
        private Label _state;
        private Label _hp;
        private Label _attack;
        private Label _purpose;
        private Label _mood;
        private Label _effectsTitle;
        private readonly Label[] _effects = new Label[MaxEffectLines];
        private Button _close;

        private Enemy _target;
        private Vector2 _screenPosition;
        private bool _needsPlacement;
        private float _refreshTimer;

        /// <summary>그 모험가의 상태를 누른 화면 좌표(왼쪽 아래 원점) 옆에 띄운다.</summary>
        public static void Show(Enemy enemy, Vector2 screenPosition)
        {
            if (enemy == null)
                return;

            if (_instance == null)
                _instance = ToolkitScreenSO.Spawn<EnemyStatusToolkitView>(ScreenResourcePath, RootName);
            if (_instance == null)
                return;

            _instance._target = enemy;
            _instance._screenPosition = screenPosition;
            _instance._needsPlacement = true;
            _instance._refreshTimer = 0f;
            _instance.SetVisible(true);
            _instance.Refresh();
        }

        public static void HideCurrent()
        {
            if (_instance != null)
                _instance.Hide();
        }

        public static bool IsOpen => _instance != null && _instance._target != null;

        private void OnEnable()
        {
            var tree = GetComponent<UIDocument>().rootVisualElement;
            _root = tree.Q<VisualElement>(RootElement) ?? tree;
            _card = tree.Q<VisualElement>(CardElement);
            _title = tree.Q<Label>(TitleLabel);
            _state = tree.Q<Label>(StateLabel);
            _hp = tree.Q<Label>(HpLabel);
            _attack = tree.Q<Label>(AttackLabel);
            _purpose = tree.Q<Label>(PurposeLabel);
            _mood = tree.Q<Label>(MoodLabel);
            _effectsTitle = tree.Q<Label>(EffectsTitleLabel);
            for (var i = 0; i < MaxEffectLines; i++)
                _effects[i] = tree.Q<Label>(EffectLabelPrefix + i);
            _close = tree.Q<Button>(CloseButton);

            if (_close != null)
                _close.clicked += Hide;
            // 카드 크기가 정해진 뒤에야 화면 안으로 밀어 넣을 수 있다.
            _card?.RegisterCallback<GeometryChangedEvent>(HandleGeometryChanged);
            ToolkitPointerBlockers.Register(_card);
            SetVisible(false);
        }

        private void OnDisable()
        {
            if (_close != null)
                _close.clicked -= Hide;
            _card?.UnregisterCallback<GeometryChangedEvent>(HandleGeometryChanged);
            ToolkitPointerBlockers.Unregister(_card);
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }

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

        private void HandleGeometryChanged(GeometryChangedEvent evt)
        {
            if (_needsPlacement)
                Place();
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

        /// <summary>누른 자리의 오른쪽 위에 카드를 놓고, 화면 밖으로 나가면 안으로 밀어 넣는다.</summary>
        private void Place()
        {
            if (ToolkitPlacement.PlaceNear(_card, _screenPosition, ScreenOffset))
                _needsPlacement = false;
        }

        private void Refresh()
        {
            if (_target == null)
            {
                Hide();
                return;
            }

            if (_needsPlacement)
                Place();

            SetText(_title, $"{_target.DisplayName} Lv {_target.Level}");
            SetText(_state, ResolveStateText());

            var health = _target.Health;
            SetText(_hp, health != null ? $"HP {health.CurrentHealth}/{health.MaxHealth}" : "HP -");

            var combatant = _target.Combatant;
            SetText(_attack, combatant != null
                ? $"ATK {combatant.AttackDamage}  SPD {combatant.AttackInterval:0.##}s"
                : "ATK -  SPD -");

            // 예산은 이 모험가가 쓸 마음이 있는 금액, 파티 자금은 실제 지갑이다. 지갑은 단계로만 보여 준다.
            SetText(_purpose, $"목적 {_target.VisitPurposeLabel}  ·  예산 {_target.RemainingBudget}/{_target.InitialBudget}G"
                              + $"  ·  파티 자금 {PartyFundLedger.TierLabel(_target.PartyFundTier)}");
            SetText(_mood, $"경계 {_target.Fear}  ·  탐욕 {_target.Greed}  ·  철수 {Mathf.RoundToInt(_target.RetreatChance * 100f)}%");

            RefreshStatusEffects();
        }

        private string ResolveStateText()
        {
            if (_target.Health == null || !_target.Health.IsAlive)
                return "처치됨";

            var trait = string.IsNullOrWhiteSpace(_target.TraitLabel) ? string.Empty : $"{_target.TraitLabel} · ";
            return $"{trait}{_target.VisitPurposeLabel} · {_target.InstinctState}";
        }

        private void RefreshStatusEffects()
        {
            var controller = _target.StatusController != null
                ? _target.StatusController
                : _target.GetComponent<EnemyStatusController>();
            var active = controller != null ? controller.GetActiveEffects() : null;
            var count = active != null ? active.Count : 0;

            if (_effectsTitle != null)
                _effectsTitle.style.visibility = count > 0 ? Visibility.Visible : Visibility.Hidden;

            for (var i = 0; i < MaxEffectLines; i++)
            {
                var label = _effects[i];
                if (label == null)
                    continue;

                var effect = i < count ? active[i].Effect : null;
                if (effect == null)
                {
                    label.style.visibility = Visibility.Hidden;
                    continue;
                }

                // 넘치는 상태이상은 마지막 줄에 개수로 줄인다.
                var text = i == MaxEffectLines - 1 && count > MaxEffectLines
                    ? $"외 {count - (MaxEffectLines - 1)}개"
                    : $"{effect.DisplayName}  {active[i].RemainingNodeVisits}칸";
                SetText(label, text);
                label.style.visibility = Visibility.Visible;
            }
        }

        private static void SetText(Label label, string text)
        {
            if (label != null && label.text != text)
                label.text = text;
        }
    }
}
