namespace Code.Stages
{
    /// <summary>
    /// 준비 단계 사건과 1스테이지 첫 보스 소개의 진행 상태(2026-11-02 사용자 승인, StagePreparationContentDesign.md).
    /// 사건 내용은 스테이지 데이터(<see cref="StageDefinitionSO.PreparationEvent"/>)가 정하고,
    /// 효과 적용은 <see cref="StageSession.TryChooseEvent"/>가, 팝업 표시는 UI가 맡는다.
    /// </summary>
    public sealed class StagePreparationEvents
    {
        private const int BossIntroStage = 0;
        private bool _bossIntroShown;

        /// <summary>스테이지 시작·재도전·이어하기마다 바뀐다. 이전 시도의 팝업 콜백을 거르는 데 쓴다.</summary>
        public int Attempt { get; private set; }
        /// <summary>고른 선택지 번호(1부터). 0이면 아직 고르지 않았다. 준비 저장의 정수와 같다.</summary>
        public int Choice { get; private set; }
        /// <summary>보수로 실제 회복한 코어 체력. 준비 저장 검사에서 코어 손실을 맞출 때 쓴다.</summary>
        public int CoreRepaired { get; private set; }

        /// <summary>지금 스테이지의 사건. 없으면 null.</summary>
        public static StagePreparationEvent EventOf(StageDefinitionSO definition) =>
            definition != null && definition.PreparationEvent != null && definition.PreparationEvent.IsDefined
                ? definition.PreparationEvent : null;

        public bool IsOffered(StageSession session)
        {
            StagePreparationEvent preparationEvent = EventOf(session.Definition);
            return preparationEvent != null && session.Phase == StagePhase.Preparing
                && session.WaveIndex == preparationEvent.Wave && Choice == 0;
        }

        /// <summary>1스테이지 보스 웨이브의 준비 단계에 처음 들어왔는가. 같은 시도에서는 한 번만 참이다.</summary>
        public bool NeedsBossIntro(StageSession session)
        {
            if (_bossIntroShown || session.StageIndex != BossIntroStage || session.Phase != StagePhase.Preparing
                || session.Definition == null)
                return false;
            StageWave[] waves = session.Definition.Waves;
            return session.WaveIndex == waves.Length - 1 && waves[waves.Length - 1].HasBoss;
        }

        public void MarkBossIntroShown() => _bossIntroShown = true;

        /// <summary>저장된 선택이 이 스테이지의 사건·진행과 맞는가.</summary>
        public static bool IsValidSaved(StageDefinitionSO definition, int nextWave, int choice, int repaired)
        {
            if (choice == 0)
                return repaired == 0;
            StagePreparationEvent preparationEvent = EventOf(definition);
            if (preparationEvent == null || choice < 0 || choice > preparationEvent.Options.Length || nextWave < preparationEvent.Wave)
                return false;
            int maximum = preparationEvent.Options[choice - 1].CoreRepair;
            return maximum > 0 ? repaired > 0 && repaired <= maximum : repaired == 0;
        }

        internal void Reset()
        {
            Attempt++;
            Choice = 0;
            CoreRepaired = 0;
            _bossIntroShown = false;
        }

        /// <summary>고른 결과나 준비 저장에서 읽은 결과를 기록한다.</summary>
        internal void Set(int choice, int repaired)
        {
            Choice = choice;
            CoreRepaired = repaired;
        }
    }
}
