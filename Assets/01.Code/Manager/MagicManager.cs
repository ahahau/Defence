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
            costEventChannel.AddListener<UnitDeployMagicRefundRequestedEvent>(HandleUnitDeployMagicRefundRequested);
        }

        private void Start()
        {
            RaiseMagicChanged();
        }

        private void OnDisable()
        {
            costEventChannel.RemoveListener<UnitDeployMagicRequestedEvent>(HandleUnitDeployMagicRequested);
            costEventChannel.RemoveListener<UnitDeployMagicRefundRequestedEvent>(HandleUnitDeployMagicRefundRequested);
        }

        private void HandleUnitDeployMagicRequested(UnitDeployMagicRequestedEvent evt)
        {
            if (evt.Node == null || evt.Unit == null || evt.MagicAmount < 0)
            {
                costEventChannel.RaiseEvent(new UnitDeployMagicRejectedEvent(
                    evt.Node,
                    evt.Unit,
                    Mathf.Max(0, evt.MagicAmount),
                    UsedMagic,
                    maxMagic));
                return;
            }

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

        private void HandleUnitDeployMagicRefundRequested(UnitDeployMagicRefundRequestedEvent evt)
        {
            if (evt.MagicAmount <= 0)
                return;

            var refunded = Mathf.Min(UsedMagic, evt.MagicAmount);
            UsedMagic = Mathf.Max(0, UsedMagic - refunded);
            RaiseMagicChanged();
            costEventChannel.RaiseEvent(new UnitDeployMagicRefundedEvent(
                evt.Unit,
                refunded,
                UsedMagic,
                maxMagic));
        }

        private void RaiseMagicChanged()
        {
            costEventChannel.RaiseEvent(new MagicChangedEvent(UsedMagic, maxMagic));
        }
    }
}
