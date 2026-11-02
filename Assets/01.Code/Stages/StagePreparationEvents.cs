namespace Code.Stages
{
    /// <summary>
    /// 1스테이지 준비 단계의 첫 보스 소개와 사건 "남은 보급품" 상태(2026-11-02 사용자 승인, StagePreparationContentDesign.md).
    /// 효과 적용은 <see cref="StageSession.TryChooseSupply"/>가, 팝업 표시는 UI가 맡는다. 수치는 임시 값이다.
    /// </summary>
    public sealed class StagePreparationEvents
    {
        public const int SupplyGold = 20;
        public const int SupplyRepair = 2;
        /// <summary>사건을 여는 준비 단계(0부터). 첫 웨이브를 막은 뒤 둘째 웨이브 준비다.</summary>
        public const int SupplyWave = 1;
        private const int EventStage = 0;
        private bool _bossIntroShown;

        /// <summary>스테이지 시작·재도전·이어하기마다 바뀐다. 이전 시도의 팝업 콜백을 거르는 데 쓴다.</summary>
        public int Attempt { get; private set; }
        public StageSupplyChoice Supply { get; private set; }
        /// <summary>금고 보수로 실제 회복한 코어 체력. 준비 저장 검사에서 코어 손실을 맞출 때 쓴다.</summary>
        public int SupplyRepaired { get; private set; }

        public bool IsSupplyOffered(StageSession session) => session.StageIndex == EventStage
            && session.Phase == StagePhase.Preparing && session.WaveIndex == SupplyWave && Supply == StageSupplyChoice.None;

        /// <summary>1스테이지 보스 웨이브의 준비 단계에 처음 들어왔는가. 같은 시도에서는 한 번만 참이다.</summary>
        public bool NeedsBossIntro(StageSession session)
        {
            if (_bossIntroShown || session.StageIndex != EventStage || session.Phase != StagePhase.Preparing
                || session.Definition == null)
                return false;
            StageWave[] waves = session.Definition.Waves;
            return session.WaveIndex == waves.Length - 1 && waves[waves.Length - 1].HasBoss;
        }

        public void MarkBossIntroShown() => _bossIntroShown = true;

        /// <summary>저장된 선택이 이 스테이지·진행과 맞는가.</summary>
        public static bool IsValidSaved(int stageIndex, int nextWave, int choice, int repaired)
        {
            if (!System.Enum.IsDefined(typeof(StageSupplyChoice), choice))
                return false;
            if (choice == (int)StageSupplyChoice.None)
                return repaired == 0;
            if (stageIndex != EventStage || nextWave < SupplyWave)
                return false;
            return choice == (int)StageSupplyChoice.Repair ? repaired > 0 && repaired <= SupplyRepair : repaired == 0;
        }

        internal void Reset()
        {
            Attempt++;
            Supply = StageSupplyChoice.None;
            SupplyRepaired = 0;
            _bossIntroShown = false;
        }

        /// <summary>고른 결과나 준비 저장에서 읽은 결과를 기록한다.</summary>
        internal void Set(StageSupplyChoice choice, int repaired)
        {
            Supply = choice;
            SupplyRepaired = repaired;
        }
    }
}
