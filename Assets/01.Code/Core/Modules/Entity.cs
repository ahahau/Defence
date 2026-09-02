using _01.Code.Core.Sensing;
using _01.Code.Core.Stats;
using UnityEngine;
using UnityEngine.Events;

namespace _01.Code.Core.Modules
{
    /// <summary>
    /// 싸우고 죽을 수 있는 것들의 공통 뿌리 — 침입자·부하·주인공이 여기서 갈라진다.
    ///
    /// 참조 프로젝트의 Agent와 같은 자리다. Defence에서는 Enemy와 Unit이 각자
    /// 체력 구독·사망 처리·상태 플래그를 따로 들고 있어서, 같은 고침을 두 번씩 해야 했다
    /// (이번 세션의 레벨 복원·행동불능 처리가 정확히 그랬다). 그 공통분모를 여기로 올린다.
    /// </summary>
    public abstract class Entity : ModuleOwner
    {
        [SerializeField, Tooltip("피해를 무시하고 경직되지 않는 상태(보스 연출 등).")]
        private bool isSuperArmor;

        /// <summary>죽었는가. 연출이 끝나 오브젝트가 사라지기 전까지도 참이다.</summary>
        public bool IsDead { get; protected set; }

        public bool IsSuperArmor
        {
            get => isSuperArmor;
            set => isSuperArmor = value;
        }

        public EntitySensor Sensor { get; private set; }
        public IStatModule Stats { get; private set; }

        /// <summary>맞았을 때. 피격 연출·카메라 흔들림이 여기에 붙는다.</summary>
        public UnityEvent Hit;

        /// <summary>죽는 순간. 정산 집계·보상·시네마틱이 여기에 붙는다.</summary>
        public UnityEvent Died;

        private HealthModule _health;
        private bool _deathBroadcast;

        protected override void InitializeModules()
        {
            base.InitializeModules();
            Sensor = GetComponentInChildren<EntitySensor>(true);
            Stats = GetModule<IStatModule>();
            SubscribeHealth(GetModule<HealthModule>());
        }

        /// <summary>
        /// 체력 모듈을 물어 Hit/Died를 대신 울려 준다.
        ///
        /// 파생 클래스(Enemy·Unit)의 사망 처리는 건드리지 않는다 — 적은 시체를 남기고 사라지고
        /// 부하는 행동불능이 될 뿐이라, 공통으로 올릴 만한 몸통이 애초에 없다.
        /// 여기가 맡는 것은 "누가 맞았고 누가 죽었다"를 바깥에 알리는 일 하나뿐이다.
        /// </summary>
        private void SubscribeHealth(HealthModule health)
        {
            if (health == null || _health == health)
                return;

            UnsubscribeHealth();
            _health = health;
            _health.Damaged += HandleDamaged;
            _health.Changed += HandleHealthRatioChanged;
        }

        private void UnsubscribeHealth()
        {
            if (_health == null)
                return;

            _health.Damaged -= HandleDamaged;
            _health.Changed -= HandleHealthRatioChanged;
            _health = null;
        }

        private void HandleDamaged(int amount)
        {
            if (amount > 0 && !IsSuperArmor)
                Hit?.Invoke();
        }

        private void HandleHealthRatioChanged(float ratio)
        {
            // 체력은 최대치가 바뀔 때도 이 콜백을 태우므로 살아났다 죽었다를 오갈 수 있다.
            // 죽음은 한 번만 알린다.
            if (_health == null || _health.IsAlive || _deathBroadcast)
                return;

            _deathBroadcast = true;
            Died?.Invoke();
        }

        // OnDestroy에서 구독을 걷지 않는다. 체력 모듈은 같은 GameObject에 있어 함께 파괴되므로
        // 구독이 살아남을 수가 없고, 여기에 OnDestroy를 두면 이미 각자 OnDestroy를 가진
        // Enemy·Unit이 그것을 가려(CS0114) 어차피 호출되지도 않는다.
    }
}
