namespace Code.Run
{
    /// <summary>배치 → 전투 → 결과. 배치 중에는 시간을 넘겨도 시뮬레이션하지 않는다.</summary>
    public class RunBattleFlow
    {
        public const int EncounterLimit = 3;

        private readonly RunEncounterSO _encounter;
        private readonly RunDeploymentMemory _memory;
        private RunBattleCarryover _carryover;
        private RunBattleSettings _demoSettings;
        private RunIntruderEntry[] _demoParty;

        public RunBattleFlow(RunEncounterSO encounter, RunDeploymentMemory memory)
            : this(encounter, memory, null, null)
        {
        }

        internal RunBattleFlow(RunEncounterSO encounter, RunDeploymentMemory memory, RunBattleSettings settings, RunIntruderEntry[] party)
        {
            _encounter = encounter;
            _memory = memory;
            _demoSettings = settings;
            _demoParty = party;
            Deployment = new RunDeployment(encounter);
            if (settings != null) Deployment.Capacity = settings.StartingManaCapacity;
            _memory.Restore(Deployment);
            Battle = CreateBattle();
        }

        public RunDeployment Deployment { get; }
        public RunBattle Battle { get; private set; }
        public RunBattlePhase Phase { get; private set; }
        public int EncounterNumber { get; private set; } = 1;
        public bool CanPrepareNext => Phase == RunBattlePhase.Result
            && Battle.Outcome == RunBattleOutcome.Victory && EncounterNumber < EncounterLimit;

        public bool PrepareNext()
        {
            if (!CanPrepareNext) return false;
            _carryover = new RunBattleCarryover(Battle);
            _memory.Restore(Deployment);
            Battle = CreateBattle();
            EncounterNumber++;
            Phase = RunBattlePhase.Preparation;
            return true;
        }

        internal void PrepareDemoDefense(int number, RunBattleSettings settings, RunIntruderEntry[] party, RunBattleCarryover carryover)
        {
            _carryover = carryover;
            _demoSettings = settings;
            _demoParty = party;
            Deployment.Capacity = settings.StartingManaCapacity;
            _memory.Restore(Deployment);
            Battle = CreateBattle();
            EncounterNumber = number;
            Phase = RunBattlePhase.Preparation;
        }

        internal void RestoreDemoResult(RunBattleOutcome outcome)
        {
            Battle.RestoreDemoOutcome(outcome);
            Phase = RunBattlePhase.Result;
        }

        internal RunIntruderEntry[] CurrentParty => _demoParty ?? _encounter.Party;

        public bool RestorePrevious() => Phase == RunBattlePhase.Preparation && _memory.Restore(Deployment);

        public bool StartBattle()
        {
            if (Phase != RunBattlePhase.Preparation) return false;
            Battle = CreateBattle();
            _memory.Remember(Deployment);
            Phase = RunBattlePhase.Battle;
            return true;
        }

        public void Advance(float seconds)
        {
            if (Phase != RunBattlePhase.Battle) return;
            Battle.Tick(seconds);
            if (Battle.Outcome != RunBattleOutcome.Ongoing) Phase = RunBattlePhase.Result;
        }

        private RunBattle CreateBattle()
        {
            RunBattle battle = _demoSettings == null ? _encounter.CreateBattle(Deployment)
                : _encounter.CreateBattle(Deployment, _demoSettings, _demoParty);
            _carryover?.ApplyTo(battle);
            return battle;
        }
    }
}
