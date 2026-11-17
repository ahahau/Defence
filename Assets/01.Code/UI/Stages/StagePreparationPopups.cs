using System.Collections.Generic;
using Code.Stages;
using Code.UI.Popup;

namespace Code.UI.Stages
{
    /// <summary>
    /// 스테이지 준비 사건과 1스테이지 첫 보스 소개 팝업(2026-11-02 사용자 승인, StagePreparationContentDesign.md).
    /// 범용 팝업으로 띄우기만 하고 효과는 <see cref="StageSession.TryChooseEvent"/>가 다시 검사한 뒤 적용한다.
    /// </summary>
    public sealed class StagePreparationPopups
    {
        private const string EventTag = "stage-preparation-event";
        private const string BossTag = "stage-boss-intro";
        private readonly StageSession _session;
        // 저절로 띄운 사건의 시도·준비 단계. 첫 사건과 후속은 준비 단계가 달라 따로 한 번씩 뜬다.
        private int _eventShownAttempt = -1;
        private int _eventShownWave = -1;

        public StagePreparationPopups(StageSession session) => _session = session;

        /// <summary>설정·유물 창·저장 오류처럼 가리는 것이 있으면 <paramref name="blocked"/>로 미룬다.</summary>
        public void Tick(UiFrameButton eventButton, bool blocked)
        {
            StagePreparationEvents events = _session.PreparationEvents;
            StagePreparationEvent offeredEvent = events.Offered(_session, out _);
            bool offered = offeredEvent != null;
            if (eventButton != null)
            {
                eventButton.SetVisible(offered);
                if (offered)
                    eventButton.SetLabel(offeredEvent.ButtonLabel);
            }
            if (blocked || PopupController.IsOpen)
                return;
            if (events.NeedsBossIntro(_session))
            {
                events.MarkBossIntroShown();
                PopupController.Show(BossIntro());
            }
            // ESC로 닫으면 미선택으로 남는다. 같은 시도에서는 다시 저절로 열지 않고 사건 버튼으로 연다.
            else if (offered && (_eventShownAttempt != events.Attempt || _eventShownWave != _session.WaveIndex))
                ShowEvent();
        }

        public void ShowEvent()
        {
            StagePreparationEvents events = _session.PreparationEvents;
            StagePreparationEvent preparationEvent = events.Offered(_session, out _);
            if (preparationEvent == null || PopupController.IsShowing(EventTag))
                return;
            _eventShownAttempt = events.Attempt;
            _eventShownWave = _session.WaveIndex;
            int attempt = events.Attempt;
            int wave = _session.WaveIndex;
            // 팝업 본문은 508×104(17pt, 넘치면 잘림)라 대사 한 줄에, 이름만으로 알 수 없는 선택지(보수·비용)만 짧게 붙인다
            System.Text.StringBuilder message = new($"임프 참모: \"{preparationEvent.Line}\"");
            List<PopupButtonSpec> options = new();
            for (int i = 0; i < preparationEvent.Options.Length; i++)
            {
                StagePreparationOption option = preparationEvent.Options[i];
                int choice = i + 1;
                if (option.CoreRepair > 0 || option.GoldChange < 0)
                    message.Append('\n').Append(option.Label).Append(": ").Append(EffectText(option));
                bool usable = !option.HasEffect || Available(option);
                options.Add(new PopupButtonSpec(option.Label, option.HasEffect ? PopupButtonStyle.Primary : PopupButtonStyle.Secondary,
                    () => Choose(choice, attempt, wave), usable));
            }
            // 닫기(ESC)는 아무것도 고르지 않는다
            PopupController.Show(PopupRequest.Choice(preparationEvent.Title, message.ToString(), options, () => { }).WithTag(EventTag));
        }

        /// <summary>예: "-20G, 코어 7→10/10". 한 줄 29자 안쪽으로 짧게 쓴다.</summary>
        private string EffectText(StagePreparationOption option)
        {
            string text = option.GoldChange != 0 ? $"{option.GoldChange:+0;-0}G" : string.Empty;
            if (option.CoreRepair > 0)
                text += (text.Length > 0 ? ", " : string.Empty)
                    + (StageCoreRepairPreview.TryText(_session, option.CoreRepair, out string repair) ? repair : "코어가 가득 참");
            if (option.GoldChange < 0 && _session.Gold < -option.GoldChange)
                text += " (금화 부족)";
            return text;
        }

        private bool Available(StagePreparationOption option) =>
            (option.GoldChange >= 0 || _session.Gold >= -option.GoldChange)
            && (option.CoreRepair == 0 || _session.CoreHealth < _session.MaxCoreHealth);

        private void Choose(int choice, int attempt, int wave)
        {
            // 씬을 떠났거나 재도전·다른 준비로 넘어간 뒤의 콜백은 세션이 다시 걸러 낸다
            if (_session != null)
                _session.TryChooseEvent(choice, attempt, wave);
        }

        private PopupRequest BossIntro()
        {
            StageWave[] waves = _session.Definition.Waves;
            StageWave wave = waves[waves.Length - 1];
            int escorts = wave.BossEscorts != null ? wave.BossEscorts.Length : 0;
            string message = $"임프 참모: \"{wave.Boss.Name} 일행이 금고를 노립니다.\"\n"
                + $"호위 {escorts}명 · 코어에 닿으면 코어 피해 {wave.BossArrivalDamage}\n"
                + "호위까지 함께 막을 방어선을 세우세요\n"
                + "보스가 살아 있는 동안 일시정지할 수 없습니다";
            return PopupRequest.Notice("보스 예고", message).WithTag(BossTag);
        }
    }
}
