using System.Collections.Generic;
using Code.Stages;
using Code.UI.Popup;

namespace Code.UI.Stages
{
    /// <summary>
    /// 1스테이지 준비 단계의 첫 보스 소개와 사건 "남은 보급품" 팝업(2026-11-02 사용자 승인, StagePreparationContentDesign.md).
    /// 범용 팝업으로 띄우기만 하고 효과는 <see cref="StageSession.TryChooseSupply"/>가 다시 검사한 뒤 적용한다.
    /// </summary>
    public sealed class StagePreparationPopups
    {
        private const string SupplyTag = "stage-supply";
        private const string BossTag = "stage-boss-intro";
        private readonly StageSession _session;
        private int _supplyShownAttempt = -1;

        public StagePreparationPopups(StageSession session) => _session = session;

        /// <summary>설정·유물 창·저장 오류처럼 가리는 것이 있으면 <paramref name="blocked"/>로 미룬다.</summary>
        public void Tick(UiFrameButton supplyButton, bool blocked)
        {
            StagePreparationEvents events = _session.PreparationEvents;
            bool offered = events.IsSupplyOffered(_session);
            if (supplyButton != null)
                supplyButton.SetVisible(offered);
            if (blocked || PopupController.IsOpen)
                return;
            if (events.NeedsBossIntro(_session))
            {
                events.MarkBossIntroShown();
                PopupController.Show(BossIntro());
            }
            // ESC로 닫으면 미선택으로 남는다. 같은 시도에서는 다시 저절로 열지 않고 보급품 버튼으로 연다.
            else if (offered && _supplyShownAttempt != events.Attempt)
                ShowSupply();
        }

        public void ShowSupply()
        {
            StagePreparationEvents events = _session.PreparationEvents;
            if (!events.IsSupplyOffered(_session) || PopupController.IsShowing(SupplyTag))
                return;
            _supplyShownAttempt = events.Attempt;
            int attempt = events.Attempt;
            int current = _session.CoreHealth;
            int maximum = _session.MaxCoreHealth;
            int repair = System.Math.Min(StagePreparationEvents.SupplyRepair, maximum - current);
            // 팝업 본문은 508×104(17pt, 넘치면 잘림)라 한 줄 29자 안쪽 4줄로 쓴다
            string repairText = repair > 0
                ? $"금고 보수: 코어 {current} → {current + repair}/{maximum}"
                : "금고 보수: 코어가 가득 차 있습니다";
            string message = "임프 참모: \"남은 보급품을 어디에 쓸까요?\"\n"
                + $"금화 교환: 이번 스테이지 금화 +{StagePreparationEvents.SupplyGold}G\n{repairText}\n"
                + "닫으면 보급품 버튼으로 다시 엽니다";
            List<PopupButtonSpec> options = new()
            {
                new($"금화 +{StagePreparationEvents.SupplyGold}G", PopupButtonStyle.Primary,
                    () => Choose(StageSupplyChoice.Gold, attempt)),
                new($"금고 보수 +{System.Math.Max(0, repair)}", PopupButtonStyle.Primary,
                    () => Choose(StageSupplyChoice.Repair, attempt), repair > 0),
                new("사용하지 않음", PopupButtonStyle.Secondary, () => Choose(StageSupplyChoice.Skip, attempt))
            };
            // 닫기(ESC)는 아무것도 고르지 않는다
            PopupController.Show(PopupRequest.Choice("남은 보급품", message, options, () => { }).WithTag(SupplyTag));
        }

        private void Choose(StageSupplyChoice choice, int attempt)
        {
            // 씬을 떠났거나 재도전·다른 준비로 넘어간 뒤의 콜백은 세션이 다시 걸러 낸다
            if (_session != null)
                _session.TryChooseSupply(choice, attempt);
        }

        private PopupRequest BossIntro()
        {
            StageWave[] waves = _session.Definition.Waves;
            StageWave wave = waves[waves.Length - 1];
            int escorts = wave.BossEscorts != null ? wave.BossEscorts.Length : 0;
            string message = $"임프 참모: \"{wave.Boss.Name} 일행이 금고를 노립니다.\"\n"
                + $"호위 {escorts}명 · 코어에 닿으면 코어 피해 {wave.Boss.CoreArrivalDamage}\n"
                + "호위까지 함께 막을 방어선을 세우세요\n"
                + "보스가 살아 있는 동안 일시정지할 수 없습니다";
            return PopupRequest.Notice("보스 예고", message).WithTag(BossTag);
        }
    }
}
