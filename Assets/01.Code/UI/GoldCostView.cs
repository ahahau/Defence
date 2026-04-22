using _01.Code.Core;
using _01.Code.Events;
using UnityEngine;
using UnityEngine.UI;

namespace _01.Code.UI
{
    public class GoldCostView : MonoBehaviour
    {
        [field: SerializeField]
        public GameEventChannelSO EventChannel { get; private set; }

        [field: SerializeField]
        public Text GoldText { get; private set; }

        [field: SerializeField]
        public string Format { get; private set; } = "Gold: {0}";

        private void OnEnable()
        {
            EventChannel.AddListener<GoldChangedEvent>(HandleGoldChanged);
        }

        private void OnDisable()
        {
            EventChannel.RemoveListener<GoldChangedEvent>(HandleGoldChanged);
        }

        private void HandleGoldChanged(GoldChangedEvent evt)
        {
            GoldText.text = string.Format(Format, evt.CurrentGold);
        }
    }
}
