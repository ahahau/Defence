using _01.Code.Core;
using _01.Code.Events;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace _01.Code.UI
{
    public class DefaultCostBarUI : MonoBehaviour
    {
        [SerializeField] private GameEventChannelSO uiEventChannel;
        [SerializeField] private Image[] icons;
        [SerializeField] private TextMeshProUGUI[] labels;
        [SerializeField] private TextMeshProUGUI[] values;

        private void OnEnable()
        {
            uiEventChannel.AddListener<UiDefaultCostBarStateChangedEvent>(HandleChanged);
        }

        private void OnDisable()
        {
            uiEventChannel.RemoveListener<UiDefaultCostBarStateChangedEvent>(HandleChanged);
        }

        private void HandleChanged(UiDefaultCostBarStateChangedEvent evt)
        {
            for (int i = 0; i < labels.Length; i++)
            {
                bool hasData = i < evt.Costs.Count && evt.Costs[i] != null;
                labels[i].transform.parent.gameObject.SetActive(hasData);
                if (!hasData)
                {
                    continue;
                }

                icons[i].sprite = evt.Costs[i].Definition.Icon;
                icons[i].enabled = evt.Costs[i].Definition.Icon != null;
                labels[i].text = evt.Costs[i].Definition.DisplayName;
                values[i].text = evt.Costs[i].Current.ToString();
            }
        }
    }
}
