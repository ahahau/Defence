namespace Code.Run
{
    /// <summary>
    /// 실시간 전투 한 판 동안의 부하·적 상태. 값은 `RunBattle`만 바꾼다.
    /// 자리는 방·통로 번호와 그 안의 거리다.
    /// </summary>
    public class RunCombatant
    {
        public RunCombatant(int id, RunCombatantSO definition, RunPosition position, int homeRoom, float entryTime,
            RunAffinity? affinity = null)
        {
            Id = id;
            Definition = definition;
            Affinity = affinity ?? definition.Affinity;
            Position = position;
            HomeRoom = homeRoom;
            EntryTime = entryTime;
            HasEntered = entryTime <= 0f;
            Health = definition.MaxHealth;
            Facing = Side == RunSide.Minion ? -1 : 1;
        }

        public int Id { get; }
        public RunCombatantSO Definition { get; }
        public RunSide Side => Definition.Side;
        public RunAffinity Affinity { get; }
        public RunTelegraph Telegraph { get; internal set; }
        public int MaxHealth => Definition.MaxHealth;
        public int Health { get; private set; }
        internal void SetInitialHealth(int health) => Health = System.Math.Min(MaxHealth, health);
        public bool IsDown => Health <= 0;

        public RunPosition Position { get; private set; }

        /// <summary>마지막으로 자신을 친 상대. 근접 부하의 방 밖 대응에 쓴다.</summary>
        public RunCombatant LastAttacker { get; private set; }

        /// <summary>방 또는 통로 내부 거리의 양의 방향 1, 음의 방향 -1.</summary>
        public int Facing { get; private set; }

        /// <summary>도주하거나 심장을 치고 퇴장해 전장에서 빠졌다.</summary>
        public bool HasLeft { get; private set; }

        /// <summary>쓰러지지도 빠지지도 않았다. 아직 입구 밖에서 기다리는 적도 포함한다.</summary>
        public bool IsActive => !IsDown && !HasLeft;

        /// <summary>던전 안에 들어와 있다. 싸움·방 효과는 이 인물만 받는다.</summary>
        public bool IsOnField => IsActive && HasEntered;

        public bool HasEntered { get; private set; }
        public float EntryTime { get; }

        /// <summary>부하가 지키는 방. 이동 명령이 끝나면 목적지가 된다.</summary>
        public int HomeRoom { get; private set; }

        /// <summary>부하의 이동 명령 목적지. 없으면 -1.</summary>
        public int MoveTarget { get; private set; } = -1;

        public bool HasMoveOrder => MoveTarget >= 0;

        /// <summary>체력이 낮아 치유 샘으로 물러나는 중이거나, 샘에서 회복하고 있다.</summary>
        public bool IsRetreating { get; private set; }

        /// <summary>물러나기 전에 지키던 방. 다 나으면 돌아간다.</summary>
        public int ReturnRoom { get; private set; } = -1;

        /// <summary>체력이 낮을 때 이동 명령을 받아 자동 물러나기를 멈췄다. 체력이 물러나기 기준 이상이 되면 풀린다.</summary>
        public bool IsRetreatSuppressed { get; private set; }

        /// <summary>예고 시전자에게 가려고 길의 적을 지나친 부하. 다음 이동 명령에서 적을 다시 지나쳐 돌아갈 수 있다.</summary>
        public bool HasSlippedPast { get; private set; }

        /// <summary>지금 이동 명령이 적을 지나쳐 돌아가는 길인가. 도착하면 끝난다.</summary>
        public bool IsSlippingBack { get; private set; }

        public RunActionKind Activity { get; private set; }

        /// <summary>지금 치고 있는 상대. 없으면 null.</summary>
        public RunCombatant Target { get; private set; }

        /// <summary>다음 공격까지 남은 초.</summary>
        public float AttackTimer { get; private set; }

        public float SkillCooldownLeft { get; private set; }

        /// <summary>마지막으로 쓴 스킬의 전체 쿨(초). 화면 게이지에 쓴다. 쓰기 전에는 0.</summary>
        public float SkillCooldownTotal { get; private set; }

        public bool IsSkillReady => SkillCooldownLeft <= 0f;

        /// <summary>공격·스킬·문·방을 친 횟수. 화면이 바뀐 것을 보고 공격 동작을 튼다.</summary>
        public int StrikeCount { get; private set; }

        public int Guard { get; private set; }
        public int CarriedGold { get; private set; }
        public bool IsEscaping { get; private set; }

        internal float StealProgress { get; set; }

        public void Heal(int amount)
        {
            if (!IsActive || amount <= 0) return;
            Health = System.Math.Min(MaxHealth, Health + amount);
        }

        public void AddGuard(int amount)
        {
            if (amount > 0)
                Guard += amount;
        }

        /// <summary>방어(다음 피해 감소)를 한 번 소모하고 실제로 깎인 체력을 돌려준다.</summary>
        internal int TakeDamage(int amount)
        {
            if (!IsActive) return 0;
            int reduced = System.Math.Max(0, amount - Guard);
            Guard = 0;
            int before = Health;
            Health = System.Math.Max(0, Health - reduced);
            return before - Health;
        }

        internal void Enter() => HasEntered = true;
        internal void Leave() => HasLeft = true;

        internal void MoveTo(RunPosition position)
        {
            if (position.IsInRoom && Position.IsInRoom && position.Room == Position.Room)
            {
                if (position.Offset > Position.Offset) Facing = 1;
                else if (position.Offset < Position.Offset) Facing = -1;
            }
            Position = position;
        }

        internal void NoteAttacker(RunCombatant attacker) => LastAttacker = attacker;

        internal void Face(int direction)
        {
            if (direction != 0) Facing = direction > 0 ? 1 : -1;
        }

        internal void SetMoveTarget(int room) => MoveTarget = room;

        internal void Arrive()
        {
            HomeRoom = MoveTarget;
            MoveTarget = -1;
            if (IsSlippingBack) HasSlippedPast = false;
            IsSlippingBack = false;
        }

        internal void MarkSlippedPast() => HasSlippedPast = true;

        internal void BeginSlipBack()
        {
            if (HasSlippedPast) IsSlippingBack = true;
        }

        internal void BeginRetreat(int spring)
        {
            IsRetreating = true;
            ReturnRoom = HasMoveOrder ? MoveTarget : HomeRoom;
            MoveTarget = spring;
        }

        internal void SetRetreatSuppressed(bool suppressed) => IsRetreatSuppressed = suppressed;

        internal void EndRetreat(bool goBack)
        {
            IsRetreating = false;
            if (goBack && ReturnRoom >= 0) MoveTarget = ReturnRoom;
            ReturnRoom = -1;
        }

        internal void SetActivity(RunActionKind activity, RunCombatant target = null)
        {
            Activity = activity;
            Target = target;
        }

        internal void Strike(float attackInterval)
        {
            AttackTimer = attackInterval;
            StrikeCount++;
        }

        internal void OnSkillUsed(float cooldownSeconds)
        {
            SkillCooldownLeft = cooldownSeconds;
            SkillCooldownTotal = cooldownSeconds;
        }

        internal void TickTimers(float deltaTime)
        {
            if (AttackTimer > 0f) AttackTimer = System.Math.Max(0f, AttackTimer - deltaTime);
            if (SkillCooldownLeft > 0f) SkillCooldownLeft = System.Math.Max(0f, SkillCooldownLeft - deltaTime);
        }

        internal void Carry(int gold) => CarriedGold += gold;

        internal int DropGold()
        {
            int gold = CarriedGold;
            CarriedGold = 0;
            return gold;
        }

        internal void StartEscape() => IsEscaping = true;
    }
}
