using _01.Code.Core.Stats;
using UnityEngine;

namespace _01.Code.Core.Modules
{
    /// <summary>
    /// 체력을 스탯에 매달아 관리하는 모듈.
    ///
    /// 기존 Health는 SetMaxHealth로 최대치를 통째로 덮어썼다. 그래서 일차 배율을 얹은 뒤
    /// 데이터를 한 번 더 바르면 배율이 지워졌고, 그게 보스가 약했던 원인이었다.
    /// 여기서는 최대 체력이 스탯의 값을 따라가므로 덮어쓸 일이 없다 —
    /// 일차 배율은 가감치로 붙고, 떼면 원래대로 돌아온다.
    /// </summary>
    public sealed class HealthModule : MonoBehaviour, IModule, IAfterInitModule
    {
        [SerializeField, Tooltip("최대 체력을 가져올 스탯. 비우면 아래 기본값만 쓴다.")]
        private StatSO maxHealthStat;

        [SerializeField, Min(1f), Tooltip("스탯이 없을 때 쓸 최대 체력.")]
        private float fallbackMaxHealth = 30f;

        public delegate void HealthChanged(float previous, float current, float max);

        public event HealthChanged Changed;

        private IStatModule _stats;
        private float _current;

        public float MaxHealth { get; private set; }

        public float CurrentHealth
        {
            get => _current;
            private set
            {
                var previous = _current;
                _current = Mathf.Clamp(value, 0f, MaxHealth);
                if (!Mathf.Approximately(previous, _current))
                    Changed?.Invoke(previous, _current, MaxHealth);
            }
        }

        public bool IsAlive => _current > 0f;
        public float Ratio => MaxHealth > 0f ? _current / MaxHealth : 0f;

        public void Initialize(ModuleOwner owner)
        {
            _stats = owner.GetModule<IStatModule>();
            MaxHealth = fallbackMaxHealth;
        }

        public void AfterInitialize()
        {
            // 스탯 모듈이 자기 표를 다 만든 뒤여야 값을 읽을 수 있다.
            if (_stats != null && maxHealthStat != null)
                MaxHealth = _stats.SubscribeStat(maxHealthStat.AssetIndex, HandleMaxHealthChanged, fallbackMaxHealth);

            CurrentHealth = MaxHealth;
        }

        private void OnDestroy()
        {
            if (_stats != null && maxHealthStat != null)
                _stats.UnsubscribeStat(maxHealthStat.AssetIndex, HandleMaxHealthChanged);
        }

        public void TakeDamage(float amount)
        {
            if (amount <= 0f)
                return;

            CurrentHealth -= amount;
        }

        public void Heal(float amount)
        {
            if (amount <= 0f)
                return;

            CurrentHealth += amount;
        }

        /// <summary>비율로 되돌린다. 회수했다 다시 내보낸 부하의 상태 복원에 쓴다.</summary>
        public void SetRatio(float ratio) => CurrentHealth = MaxHealth * Mathf.Clamp01(ratio);

        /// <summary>
        /// 최대 체력이 자라면 그만큼 현재 체력도 같이 올린다. 안 그러면 성장한 순간
        /// 체력 비율이 깎여 보이고, 반대로 줄었을 땐 상한에 맞춰 잘린다.
        /// 죽어 있을 땐 올리지 않는다 — 최대치 상승으로 되살아나면 안 된다.
        /// </summary>
        private void HandleMaxHealthChanged(StatSO stat, float current, float previous)
        {
            var gained = current - previous;
            MaxHealth = current;

            if (gained > 0f && IsAlive)
                CurrentHealth += gained;
            else
                CurrentHealth = Mathf.Min(_current, MaxHealth);
        }
    }
}
