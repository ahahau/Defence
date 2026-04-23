using _01.Code.Events;
using _01.Code.Core;
using UnityEngine;
using UnityEngine.UI;

namespace _01.Code.UI
{
    public class GoldCostView : MonoBehaviour
    {
        [SerializeField]
        private GameEventChannelSO costEventChannel;

        [SerializeField]
        private Text goldText;

        [SerializeField]
        private string format = "Gold: {0}";

        private void OnEnable()
        {
            costEventChannel.AddListener<GoldChangedEvent>(HandleGoldChanged);
        }

        private void OnDisable()
        {
            costEventChannel.RemoveListener<GoldChangedEvent>(HandleGoldChanged);
        }

        private void HandleGoldChanged(GoldChangedEvent evt)
        {
            goldText.text = string.Format(format, evt.CurrentGold);
        }
    }
}
