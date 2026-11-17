namespace Code.Stages
{
    /// <summary>
    /// 준비 단계 사건과 1스테이지 첫 보스 소개의 진행 상태(2026-11-02 사용자 승인, StagePreparationContentDesign.md).
    /// 사건 내용은 스테이지 데이터(<see cref="StageDefinitionSO.PreparationEvent"/>·<see cref="StageDefinitionSO.FollowUpEvents"/>)가 정하고,
    /// 효과 적용은 <see cref="StageSession.TryChooseEvent"/>가, 팝업 표시는 UI가 맡는다.
    /// </summary>
    public sealed class StagePreparationEvents
    {
        private const int BossIntroStage = 0;
        private bool _bossIntroShown;

        /// <summary>스테이지 시작·재도전·이어하기마다 바뀐다. 이전 시도의 팝업 콜백을 거르는 데 쓴다.</summary>
        public int Attempt { get; private set; }
        /// <summary>첫 사건에서 고른 선택지 번호(1부터). 0이면 아직 고르지 않았다. 준비 저장의 정수와 같다.</summary>
        public int Choice { get; private set; }
        /// <summary>첫 사건 보수로 실제 회복한 코어 체력. 준비 저장 검사에서 코어 손실을 맞출 때 쓴다.</summary>
        public int CoreRepaired { get; private set; }
        /// <summary>후속 사건에서 고른 선택지 번호(1부터). 0이면 고르지 않았다.</summary>
        public int FollowUpChoice { get; private set; }
        /// <summary>후속 사건 보수로 실제 회복한 코어 체력.</summary>
        public int FollowUpRepaired { get; private set; }

        /// <summary>지금 스테이지의 첫 사건. 없으면 null.</summary>
        public static StagePreparationEvent EventOf(StageDefinitionSO definition) =>
            definition != null && definition.PreparationEvent != null && definition.PreparationEvent.IsDefined
                ? definition.PreparationEvent : null;

        /// <summary>첫 사건 선택에 따른 후속 사건. 선택하지 않았거나 후속이 없으면 null.</summary>
        public static StagePreparationEvent FollowUpOf(StageDefinitionSO definition, int firstChoice)
        {
            if (EventOf(definition) == null || firstChoice < 1 || definition.FollowUpEvents == null
                || firstChoice > definition.FollowUpEvents.Length)
                return null;
            StagePreparationEvent followUp = definition.FollowUpEvents[firstChoice - 1];
            return followUp != null && followUp.IsDefined ? followUp : null;
        }

        /// <summary>지금 준비 단계에 고를 수 있는 사건(첫 사건 또는 후속). 없으면 null.</summary>
        public StagePreparationEvent Offered(StageSession session, out bool followUp)
        {
            followUp = false;
            if (session.Phase != StagePhase.Preparing)
                return null;
            StagePreparationEvent first = EventOf(session.Definition);
            if (first != null && Choice == 0 && session.WaveIndex == first.Wave)
                return first;
            StagePreparationEvent next = FollowUpOf(session.Definition, Choice);
            if (next == null || FollowUpChoice != 0 || session.WaveIndex != next.Wave)
                return null;
            followUp = true;
            return next;
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

        /// <summary>저장된 첫 사건 선택이 이 스테이지의 사건·진행과 맞는가.</summary>
        public static bool IsValidSaved(StageDefinitionSO definition, int nextWave, int choice, int repaired) =>
            IsValidChoice(EventOf(definition), nextWave, choice, repaired);

        /// <summary>저장된 후속 선택이 첫 사건 선택·진행과 맞는가.</summary>
        public static bool IsValidSavedFollowUp(StageDefinitionSO definition, int nextWave, int firstChoice, int choice, int repaired) =>
            IsValidChoice(FollowUpOf(definition, firstChoice), nextWave, choice, repaired);

        internal void Reset()
        {
            Attempt++;
            Choice = 0;
            CoreRepaired = 0;
            FollowUpChoice = 0;
            FollowUpRepaired = 0;
            _bossIntroShown = false;
        }

        /// <summary>첫 사건에서 고른 결과나 준비 저장에서 읽은 결과를 기록한다.</summary>
        internal void Set(int choice, int repaired)
        {
            Choice = choice;
            CoreRepaired = repaired;
        }

        /// <summary>후속 사건에서 고른 결과나 준비 저장에서 읽은 결과를 기록한다.</summary>
        internal void SetFollowUp(int choice, int repaired)
        {
            FollowUpChoice = choice;
            FollowUpRepaired = repaired;
        }

        private static bool IsValidChoice(StagePreparationEvent preparationEvent, int nextWave, int choice, int repaired)
        {
            if (choice == 0)
                return repaired == 0;
            if (preparationEvent == null || choice < 0 || choice > preparationEvent.Options.Length || nextWave < preparationEvent.Wave)
                return false;
            int maximum = preparationEvent.Options[choice - 1].CoreRepair;
            return maximum > 0 ? repaired > 0 && repaired <= maximum : repaired == 0;
        }
    }
}
