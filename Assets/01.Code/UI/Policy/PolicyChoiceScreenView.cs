using System.Collections.Generic;
using Code.Audio;
using Code.Core;
using Code.Manager;
using Code.UI.ViewModels;
using TMPro;
using UnityEngine;

namespace Code.UI
{
    /// <summary>
    /// 운영 방침 창(uGUI, A안, 화면 가운데 카드 세 장). <see cref="MoralePolicyManager"/>가 방침을 내놓으면 열리고, 하나를 공포하면 닫힌다.
    /// 강제 선택이라 닫기 버튼이 없고 ESC로도 닫히지 않으며, 열린 동안 게임 시간을 멈춘다.
    /// 금화가 모자란 카드는 잠그고(<see cref="PolicyChoiceRules"/>), 셋 다 잠기면 "이번에는 공포하지 않음"을 연다.
    ///
    /// 예전에는 방침을 내놓아도 그리는 화면이 없어 아무도 고르지 못했고, 방침·민심 조합 효과가 한 번도 걸리지 않았다.
    /// </summary>
    public sealed class PolicyChoiceScreenView : MonoBehaviour
    {
        [SerializeField] private MoralePolicyManager policies;
        [SerializeField] private CostManager costManager;
        [SerializeField] private GameSpeedController speedController;
        [SerializeField, Tooltip("공포한 방침을 남길 기록판. 비면 남기지 않는다.")]
        private RecordBoardView recordBoard;
        [SerializeField] private GameObject root;
        [SerializeField] private TMP_Text title;
        [SerializeField] private TMP_Text subtitle;
        [SerializeField] private TMP_Text moraleLabel;
        [SerializeField] private UnityEngine.UI.Image moraleFill;
        [SerializeField] private TMP_Text activePolicies;
        [SerializeField] private PolicyCardSlot[] cards;
        [SerializeField] private TMP_Text footer;
        [SerializeField] private UiFrameButton declineButton;

        private readonly List<KeyValuePair<PolicyDataSO, int>> _active = new();
        private readonly List<(PolicyCombo combo, PolicyDataSO partner, bool partnerActive)> _combos = new();
        private UnityEngine.Events.UnityAction[] _cardHandlers;
        private bool _suspended;

        public bool IsOpen => root != null && root.activeSelf;

        /// <summary>튜토리얼이 비출 카드의 화면 사각형.</summary>
        public bool TryGetCardScreenRect(int index, out Rect rect)
        {
            rect = default;
            return IsOpen && cards != null && index >= 0 && index < cards.Length && cards[index] != null
                   && cards[index].gameObject.activeSelf
                   && UiScreenRect.TryGet((RectTransform)cards[index].transform, out rect);
        }

        private void Awake()
        {
            if (policies == null)
                policies = SceneReferenceFallback.Find<MoralePolicyManager>(this, nameof(policies));
            if (costManager == null)
                costManager = CostManager.Current;
            if (speedController == null)
                speedController = GameSpeedController.Current;

            _cardHandlers = new UnityEngine.Events.UnityAction[cards != null ? cards.Length : 0];
            for (var i = 0; i < _cardHandlers.Length; i++)
            {
                var index = i;
                _cardHandlers[i] = () => Choose(index);
            }

            if (root != null)
                root.SetActive(false);
        }

        private void OnEnable()
        {
            for (var i = 0; i < _cardHandlers.Length; i++)
                cards[i]?.Choose.Button.onClick.AddListener(_cardHandlers[i]);
            declineButton?.Button.onClick.AddListener(Decline);
            if (policies != null)
            {
                policies.ChoicesChanged += Render;
                policies.MoraleChanged += HandleMoraleChanged;
            }

            Render();
        }

        private void OnDisable()
        {
            for (var i = 0; i < _cardHandlers.Length; i++)
                cards[i]?.Choose.Button.onClick.RemoveListener(_cardHandlers[i]);
            declineButton?.Button.onClick.RemoveListener(Decline);
            if (policies != null)
            {
                policies.ChoicesChanged -= Render;
                policies.MoraleChanged -= HandleMoraleChanged;
            }

            // 열린 채로 꺼지면(씬 이동) 멈춤을 남기지 않는다.
            SetSuspended(false);
        }

        private void HandleMoraleChanged(int morale, int delta, string reason) => Render();

        private void Render()
        {
            var open = policies != null && policies.CurrentChoices.Count > 0;
            var wasOpen = IsOpen;
            if (root != null)
                root.SetActive(open);
            SetSuspended(open);
            if (!open)
                return;

            if (!wasOpen)
                GameSfxPlayer.Play(GameSfxCue.UiOpen);

            policies.CollectActivePolicies(_active);
            var gold = costManager != null ? costManager.CurrentGold : 0;
            var model = PolicyChoicePresenter.Build(policies.CurrentChoices, policies.OfferDay, gold,
                policies.CurrentMorale, _active, CombosFor);

            title.text = model.Title;
            subtitle.text = model.Subtitle;
            moraleLabel.text = model.MoraleLabel;
            moraleFill.fillAmount = model.MoraleRatio;
            moraleFill.color = model.MoraleTone switch
            {
                2 => HudPalette.Danger,
                1 => HudPalette.Warn,
                _ => HudPalette.Gain
            };
            activePolicies.text = model.ActivePolicies;

            for (var i = 0; i < cards.Length; i++)
            {
                if (i < model.Cards.Count)
                    cards[i].Bind(model.Cards[i]);
                else
                    cards[i].Hide();
            }

            footer.text = model.Footer;
            declineButton.SetVisible(model.ShowDecline);
        }

        private IReadOnlyList<(PolicyCombo combo, PolicyDataSO partner, bool partnerActive)> CombosFor(PolicyDataSO policy)
        {
            policies.CollectCombosFor(policy, _combos);
            return _combos;
        }

        private void Choose(int index)
        {
            if (policies == null || index >= policies.CurrentChoices.Count)
                return;

            var policy = policies.CurrentChoices[index];
            policies.SelectPolicy(policy);
            var chosen = policies.CurrentChoices.Count == 0;
            GameSfxPlayer.Play(chosen ? GameSfxCue.UiConfirm : GameSfxCue.UiFail);
            if (chosen)
                recordBoard?.AddMessage($"방침 공포 · {policy.DisplayName}", false);
        }

        private void Decline()
        {
            if (policies == null)
                return;

            policies.DeclinePolicies();
            GameSfxPlayer.Play(GameSfxCue.UiClose);
            recordBoard?.AddMessage("금화가 모자라 방침 없이 넘겼습니다", true);
        }

        private void SetSuspended(bool suspended)
        {
            if (_suspended == suspended)
                return;

            _suspended = suspended;
            if (speedController == null)
                return;
            if (suspended)
                speedController.Suspend(this);
            else
                speedController.Release(this);
        }
    }
}
