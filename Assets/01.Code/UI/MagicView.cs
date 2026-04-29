using _01.Code.Core;
using _01.Code.Events;
using UnityEngine;
using UnityEngine.UI;

namespace _01.Code.UI
{
    public class MagicView : MonoBehaviour
    {
        [SerializeField] private GameEventChannelSO costEventChannel;
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private Text magicText;
        [SerializeField] private string format = "마력: {0}/{1}";

        private void OnEnable()
        {
            costEventChannel.AddListener<MagicChangedEvent>(HandleMagicChanged);
        }

        private void OnDisable()
        {
            costEventChannel.RemoveListener<MagicChangedEvent>(HandleMagicChanged);
        }

        private void HandleMagicChanged(MagicChangedEvent evt)
        {
            magicText.text = string.Format(format, evt.UsedMagic, evt.MaxMagic);
        }
    }
}
