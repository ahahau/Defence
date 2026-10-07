using System;
using Code.UI.Popup;
using NUnit.Framework;

namespace Tests.EditMode.Gameplay
{
    /// <summary>
    /// 팝업 요청의 역할별 규칙. 화면 없이 확인할 수 있는 부분만 본다.
    ///
    /// 비용 팝업이 금화 부족을 놓치면 확인을 눌렀을 때 결제가 조용히 실패하고,
    /// 버튼 수 제한이 풀리면 고정 폭 버튼 줄이 카드 밖으로 넘친다.
    /// </summary>
    public class PopupRequestTests
    {
        [Test]
        public void Cost_WhenGoldIsShort_LocksConfirmAndShowsShortfall()
        {
            var request = PopupRequest.Cost("건설", "함정을 설치합니다.", 1500, 470, () => { });

            Assert.That(request.Buttons[1].Interactable, Is.False);
            Assert.That(request.WarningText, Is.EqualTo("1,030 G 부족"));
            Assert.That(request.CostText, Is.EqualTo("비용 1,500 G"));
        }

        [Test]
        public void Cost_WhenAffordable_HasNoWarning()
        {
            var request = PopupRequest.Cost("건설", "함정을 설치합니다.", 300, 300, () => { });

            Assert.That(request.Buttons[1].Interactable, Is.True);
            Assert.That(request.WarningText, Is.Null);
        }

        [Test]
        public void Confirm_PutsCancelFirstAndCanBeDismissed()
        {
            var request = PopupRequest.Confirm("해고", "이 부하를 내보냅니다.", () => { }, destructive: true);

            Assert.That(request.Buttons[0].Style, Is.EqualTo(PopupButtonStyle.Secondary));
            Assert.That(request.Buttons[1].Style, Is.EqualTo(PopupButtonStyle.Danger));
            Assert.That(request.CanCancel, Is.True);
        }

        [Test]
        public void Choice_WithoutCancel_MustBeAnswered()
        {
            var options = new[]
            {
                new PopupButtonSpec("A", PopupButtonStyle.Primary, null),
                new PopupButtonSpec("B", PopupButtonStyle.Secondary, null)
            };

            Assert.That(PopupRequest.Choice("선택", "하나를 고르세요.", options).CanCancel, Is.False);
        }

        [Test]
        public void Choice_RejectsMoreButtonsThanTheActionRowHolds()
        {
            var options = new PopupButtonSpec[PopupRequest.MaxButtons + 1];

            Assert.Throws<ArgumentException>(() => PopupRequest.Choice("선택", "넘침", options));
        }

        [Test]
        public void Notice_DoesNotPauseUnlessAsked()
        {
            Assert.That(PopupRequest.Notice("알림", "본문").PausesGame, Is.False);
            Assert.That(PopupRequest.Notice("알림", "본문").PauseGame().PausesGame, Is.True);
        }
    }
}
