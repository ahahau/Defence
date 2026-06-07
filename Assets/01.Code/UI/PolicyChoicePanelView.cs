using System.Collections.Generic;
using _01.Code.Core;
using _01.Code.Events;
using _01.Code.Manager;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace _01.Code.UI
{
    public class PolicyChoicePanelView : MonoBehaviour
    {
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

        private void OnEnable()
        {
            managementEventChannel?.AddListener<PolicyChoicesOfferedEvent>(HandlePolicyChoicesOffered);
            managementEventChannel?.AddListener<MoraleChangedEvent>(HandleMoraleChanged);
            closeButton?.onClick.AddListener(Hide);
            WirePolicyButtons();
            Hide();
        }

        private void OnDisable()
        {
            managementEventChannel?.RemoveListener<PolicyChoicesOfferedEvent>(HandlePolicyChoicesOffered);
            managementEventChannel?.RemoveListener<MoraleChangedEvent>(HandleMoraleChanged);
            closeButton?.onClick.RemoveListener(Hide);
            UnwirePolicyButtons();
        }

        private void HandlePolicyChoicesOffered(PolicyChoicesOfferedEvent evt)
        {
            currentChoices.Clear();
            if (evt.Choices != null)
                currentChoices.AddRange(evt.Choices);

            if (currentChoices.Count == 0 || !HasPanelReferences())
            {
                Hide();
                return;
            }

            if (titleText != null)
                titleText.text = string.Format(titleFormat, evt.Day);

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

                SetText(policyNameTexts, i, currentChoices[i].DisplayName);
                SetText(policyDescriptionTexts, i, currentChoices[i].Description);
            }
        }

        private void SelectPolicy(int index)
        {
            if (index < 0 || index >= currentChoices.Count)
                return;

            moralePolicyManager?.SelectPolicy(currentChoices[index]);
            Hide();
        }

        private void Show()
        {
            panelRoot.SetActive(true);
            panelRoot.transform.SetAsLastSibling();
        }

        private void Hide()
        {
            if (panelRoot != null)
                panelRoot.SetActive(false);
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
    }
}
