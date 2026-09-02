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

        /// <summary>
        /// 주둔 마력 상한을 영구히 올린다. 마을을 완전히 장악한 보상으로 쓰인다.
        /// 이미 배치한 부하는 건드리지 않으므로 늘어난 칸은 즉시 쓸 수 있다.
        /// </summary>
        public void IncreaseMaxMagic(int amount)
        {
            if (amount <= 0)
                return;

            maxMagic += amount;
            RaiseMagicChanged();
        }

        /// <summary>
        /// 저장에서 되돌린다. 상한과 사용량을 함께 받는다 —
        /// 복원은 유닛을 직접 인스턴스화하므로 배치가 마력을 다시 청구하지 않는다.
        /// 사용량을 되돌리지 않으면 불러올 때마다 마력이 공짜로 비워진다.
        /// </summary>
        public void RestoreState(int max, int used)
        {
            maxMagic = Mathf.Max(0, max);
            UsedMagic = Mathf.Clamp(used, 0, maxMagic);
            RaiseMagicChanged();
        }

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
