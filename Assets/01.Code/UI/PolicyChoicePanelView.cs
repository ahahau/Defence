using System.Collections.Generic;
using _01.Code.Core;
using _01.Code.Events;
using _01.Code.Manager;
using _01.Code.Tutorial;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace _01.Code.UI
{
    public class PolicyChoicePanelView : MonoBehaviour
    {
        public static PolicyChoicePanelView Current { get; private set; }

        [SerializeField] private GameEventChannelSO managementEventChannel;
        [SerializeField] private MoralePolicyManager moralePolicyManager;

        [Header("Panel References")]
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text moraleText;
        [SerializeField] private TMP_Text[] policyNameTexts;
        [SerializeField] private TMP_Text[] policyDescriptionTexts;
        [SerializeField] private Button[] policyButtons;
        [SerializeField] private Button closeButton;

        [SerializeField] private string titleFormat = "{0}일차 정책 선택";
        [SerializeField] private string moraleFormat = "민심 {0}";

        private readonly List<PolicyDataSO> currentChoices = new();
        private readonly List<UnityAction> policyButtonActions = new();
        private bool _isPausingGame;

        public bool IsPanelOpen => panelRoot != null && panelRoot.activeInHierarchy;
        public RectTransform FirstPolicyButtonRect => policyButtons != null
                                                      && policyButtons.Length > 0
                                                      && policyButtons[0] != null
            ? policyButtons[0].transform as RectTransform
            : null;

        public void BringToFront()
        {
            if (panelRoot != null)
                panelRoot.transform.SetAsLastSibling();
        }

        public void CloseForRunEnd()
        {
            if (panelRoot != null)
                panelRoot.SetActive(false);

            _isPausingGame = false;
        }

        private void OnEnable()
        {
            Current = this;

            managementEventChannel?.AddListener<PolicyChoicesOfferedEvent>(HandlePolicyChoicesOffered);
            managementEventChannel?.AddListener<MoraleChangedEvent>(HandleMoraleChanged);
            closeButton?.onClick.AddListener(HandleCloseClicked);
            WirePolicyButtons();
            ForceHide();
        }

        private void OnDisable()
        {
            if (Current == this)
                Current = null;

            managementEventChannel?.RemoveListener<PolicyChoicesOfferedEvent>(HandlePolicyChoicesOffered);
            managementEventChannel?.RemoveListener<MoraleChangedEvent>(HandleMoraleChanged);
            closeButton?.onClick.RemoveListener(HandleCloseClicked);
            UnwirePolicyButtons();
            ResumeGameIfPaused();
        }

        private void HandlePolicyChoicesOffered(PolicyChoicesOfferedEvent evt)
        {
            currentChoices.Clear();
            if (evt.Choices != null)
                currentChoices.AddRange(evt.Choices);

            if (currentChoices.Count == 0 || !HasPanelReferences())
            {
                ForceHide();
                return;
            }

            if (titleText != null)
                titleText.text = $"{string.Format(titleFormat, evt.Day)} · 전투 일시정지";

            RefreshPolicyButtons();
            Show();
        }

        private void HandleMoraleChanged(MoraleChangedEvent evt)
        {
            if (moraleText != null)
                moraleText.text = string.Format(moraleFormat, evt.CurrentMorale);
        }

        private void WirePolicyButtons()
        {
            if (policyButtons == null)
                return;

            for (var i = 0; i < policyButtons.Length; i++)
            {
                var index = i;
                UnityAction action = () => SelectPolicy(index);
                policyButtonActions.Add(action);

                if (policyButtons[i] != null)
                    policyButtons[i].onClick.AddListener(action);
            }
        }

        private void UnwirePolicyButtons()
        {
            if (policyButtons == null)
                return;

            for (var i = 0; i < policyButtons.Length && i < policyButtonActions.Count; i++)
            {
                if (policyButtons[i] != null)
                    policyButtons[i].onClick.RemoveListener(policyButtonActions[i]);
            }

            policyButtonActions.Clear();
        }

        private void RefreshPolicyButtons()
        {
            var buttonCount = policyButtons?.Length ?? 0;
            for (var i = 0; i < buttonCount; i++)
            {
                var button = policyButtons[i];
                if (button == null)
                    continue;

                var hasChoice = i < currentChoices.Count && currentChoices[i] != null;
                button.gameObject.SetActive(hasChoice);
                button.interactable = hasChoice;

                if (!hasChoice)
                    continue;

                var policy = currentChoices[i];
                SetText(policyNameTexts, i, policy.DisplayName);
                // 조합은 고르는 순간에 보여야 선택이 된다. 짝이 이미 돌고 있으면 그 사실도 함께.
                var comboHint = MoralePolicyManager.Current != null
                    ? MoralePolicyManager.Current.DescribeCombosFor(policy)
                    : string.Empty;
                SetText(policyDescriptionTexts, i, $"{policy.Description}\n\n{BuildEffectSummary(policy)}"
                    + (string.IsNullOrEmpty(comboHint) ? string.Empty : $"\n{comboHint}"));
                if (policyNameTexts != null && i < policyNameTexts.Length)
                {
                    InstallCardPresenter.PlaceCardGraphic(policyNameTexts[i], 26f, 220f, 40f);
                    InstallCardPresenter.StyleCardText(policyNameTexts[i], 21);
                }
                if (policyDescriptionTexts != null && i < policyDescriptionTexts.Length && policyDescriptionTexts[i] != null)
                {
                    var description = policyDescriptionTexts[i];
                    InstallCardPresenter.PlaceCardGraphic(description, 76f, 220f, 164f);
                    description.enableAutoSizing = true;
                    description.fontSizeMin = 13f;
                    description.fontSizeMax = 17f;
                    description.textWrappingMode = TextWrappingModes.Normal;
                    description.alignment = TextAlignmentOptions.TopLeft;
                }
            }
        }

        private void SelectPolicy(int index)
        {
            if (index < 0 || index >= currentChoices.Count)
                return;

            if (!TutorialInputGate.AllowsPolicyChoice(index))
                return;

            moralePolicyManager?.SelectPolicy(currentChoices[index]);
            ForceHide();
        }

        private void Show()
        {
            panelRoot.SetActive(true);
            panelRoot.transform.SetAsLastSibling();
            PauseGameForSelection();
        }

        private void HandleCloseClicked()
        {
            if (!TutorialInputGate.AllowsPolicyPanelClose())
                return;

            ForceHide();
        }

        private void ForceHide()
        {
            if (panelRoot != null)
                panelRoot.SetActive(false);

            ResumeGameIfPaused();
        }

        private void PauseGameForSelection()
        {
            if (_isPausingGame)
                return;

            // 배속을 기억했다가 되돌리지 않는다. 창이 떠 있는 동안 플레이어가 배속을 바꿀 수도 있고,
            // 그때는 기억해 둔 옛 값이 아니라 새로 고른 값으로 돌아가야 한다.
            if (GameSpeedController.Current != null)
                GameSpeedController.Current.Suspend(this);
            else
                Time.timeScale = 0f;
            _isPausingGame = true;
        }

        private void ResumeGameIfPaused()
        {
            if (!_isPausingGame)
                return;

            if (GameSpeedController.Current != null)
                GameSpeedController.Current.Release(this);
            else
                Time.timeScale = GameSpeedController.NormalSpeed;
            _isPausingGame = false;
        }

        private bool HasPanelReferences()
        {
            return panelRoot != null && policyButtons != null && policyButtons.Length > 0;
        }

        private void SetText(TMP_Text[] texts, int index, string value)
        {
            if (texts == null || index >= texts.Length || texts[index] == null)
                return;

            texts[index].text = value;
        }

        private string BuildEffectSummary(PolicyDataSO policy)
        {
            var parts = new List<string>();

            if (policy.MoraleDeltaOnSelect != 0)
                parts.Add($"민심 {FormatSigned(policy.MoraleDeltaOnSelect)}");

            if (policy.GoldDeltaOnSelect != 0)
                parts.Add($"골드 {FormatSigned(policy.GoldDeltaOnSelect)}");

            if (policy.DailyMoraleDelta != 0 && policy.DurationDays > 0)
                parts.Add($"{policy.DurationDays}일간 민심 {FormatSigned(policy.DailyMoraleDelta)}/일");

            if (!Mathf.Approximately(policy.UnitDamageMultiplier, 1f))
                parts.Add($"{policy.DurationDays}일간 아군 공격 {FormatPercent(policy.UnitDamageMultiplier)}");

            if (policy.UnitDefenseBonus != 0)
                parts.Add($"{policy.DurationDays}일간 아군 방어 {FormatSigned(policy.UnitDefenseBonus)}");


            return parts.Count > 0 ? string.Join(" / ", parts) : "효과 없음";
        }

        private string FormatSigned(int value)
        {
            return value > 0 ? $"+{value}" : value.ToString();
        }

        private static string FormatPercent(float multiplier)
        {
            var percent = Mathf.RoundToInt((multiplier - 1f) * 100f);
            return percent > 0 ? $"+{percent}%" : $"{percent}%";
        }
    }
}
