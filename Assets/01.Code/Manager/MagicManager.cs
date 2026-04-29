using _01.Code.Core;
using _01.Code.Events;
using UnityEngine;

namespace _01.Code.Manager
{
    public class MagicManager : MonoBehaviour
    {
        [SerializeField] private GameEventChannelSO costEventChannel;
        [SerializeField, Min(0)] private int maxMagic = 5;

        public int UsedMagic { get; private set; }
        public int MaxMagic => maxMagic;

        private void Awake()
        {
            UsedMagic = 0;
        }

        private void OnEnable()
        {
            costEventChannel.AddListener<UnitDeployMagicRequestedEvent>(HandleUnitDeployMagicRequested);
        }

        private void Start()
        {
            RaiseMagicChanged();
        }

        private void OnDisable()
        {
            costEventChannel.RemoveListener<UnitDeployMagicRequestedEvent>(HandleUnitDeployMagicRequested);
        }

        private void HandleUnitDeployMagicRequested(UnitDeployMagicRequestedEvent evt)
        {
            if (UsedMagic + evt.MagicAmount > maxMagic)
            {
                costEventChannel.RaiseEvent(new UnitDeployMagicRejectedEvent(
                    evt.Node,
                    evt.Unit,
                    evt.MagicAmount,
                    UsedMagic,
                    maxMagic));
                return;
            }

            UsedMagic += evt.MagicAmount;
            RaiseMagicChanged();
            costEventChannel.RaiseEvent(new UnitDeployMagicPaidEvent(
                evt.Node,
                evt.Unit,
                evt.MagicAmount,
                UsedMagic,
                maxMagic));
        }

        private void RaiseMagicChanged()
        {
            costEventChannel.RaiseEvent(new MagicChangedEvent(UsedMagic, maxMagic));
        }
    }
}
