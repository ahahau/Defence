using Code.Skills;

namespace Code.Run
{
    /// <summary>
    /// 인물마다 준비해 재사용하는 예고 상태. 활성 목록에서 제거돼도 종료 이유는 다음 예고까지 남는다.
    /// 화면은 Caster.Id와 Sequence를 함께 사용해 같은 상태 객체의 새 예고를 구별한다.
    /// </summary>
    public class RunTelegraph
    {
        private readonly RunPosition[] _pathStarts;
        private readonly RunPosition[] _pathEnds;
        private readonly bool[] _doorLockedAtStart;

        public RunTelegraph(RunCombatant caster, int pathCapacity)
        {
            Caster = caster;
            Skill = caster.Definition.Skill;
            if (Skill != null && Skill.TurnEffects != null)
            {
                foreach (RunTurnSkillEffectSO effect in Skill.TurnEffects)
                {
                    if (effect is RunTelegraphSkillEffectSO telegraph)
                    {
                        Effect = telegraph;
                        break;
                    }
                }
            }
            _pathStarts = new RunPosition[pathCapacity];
            _pathEnds = new RunPosition[pathCapacity];
            _doorLockedAtStart = new bool[pathCapacity];
        }

        public RunCombatant Caster { get; }
        public SkillDataSO Skill { get; }
        public RunTelegraphSkillEffectSO Effect { get; }
        public RunTelegraphScope Scope => Effect.Scope;
        public RunTelegraphKind Kind => Effect.Kind;
        public int Sequence { get; private set; }
        public bool IsActive { get; private set; }
        public float RemainingSeconds { get; private set; }
        public float DurationSeconds { get; private set; }
        public int TargetRoom { get; private set; }
        public RunCombatant Target { get; private set; }
        public RunPosition Origin { get; private set; }
        public RunPosition End { get; private set; }
        public RunPosition ImpactEnd { get; private set; }
        public int PathCount { get; private set; }
        public RunTelegraphEndReason EndReason { get; private set; }
        public bool IsInterrupted => EndReason is RunTelegraphEndReason.Damage or RunTelegraphEndReason.LockedDoor
            or RunTelegraphEndReason.CasterLeft or RunTelegraphEndReason.BattleEnded;

        public RunPosition PathStart(int index) => _pathStarts[index];
        public RunPosition PathEnd(int index) => _pathEnds[index];

        /// <summary>예고를 시작할 때 이 경로 구간의 문이 잠겨 있었는가. 돌진은 그 문에서 멈춘다.</summary>
        public bool WasDoorLockedAtStart(int index) => _doorLockedAtStart[index];

        internal void MarkDoorLockedAtStart(int index) => _doorLockedAtStart[index] = true;

        internal void Begin(int room, RunCombatant target, RunPosition end)
        {
            Sequence++;
            TargetRoom = room;
            Target = target;
            Origin = Caster.Position;
            End = end;
            ImpactEnd = end;
            RemainingSeconds = DurationSeconds = Effect.WarningSeconds;
            EndReason = RunTelegraphEndReason.None;
            PathCount = 0;
            IsActive = true;
        }

        internal bool AppendPath(RunPosition start, RunPosition end)
        {
            if (PathCount >= _pathStarts.Length) return false;
            _pathStarts[PathCount] = start;
            _doorLockedAtStart[PathCount] = false;
            _pathEnds[PathCount++] = end;
            return true;
        }

        internal void Tick(float dt) => RemainingSeconds = System.Math.Max(0f, RemainingSeconds - dt);
        internal void SetImpactEnd(RunPosition position) => ImpactEnd = position;

        internal void Finish(RunTelegraphEndReason reason)
        {
            IsActive = false;
            EndReason = reason;
        }
    }
}
