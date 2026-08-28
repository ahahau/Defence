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

        protected override void InitializeModules()
        {
            base.InitializeModules();
            Sensor = GetComponentInChildren<EntitySensor>(true);
            Stats = GetModule<IStatModule>();
        }
    }
}
