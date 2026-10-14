using System.Linq;
using Code.UI.Toolkit;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace Tests.EditMode.Rules
{
    /// <summary>
    /// HUD UI Toolkit 조각의 동작.
    ///
    /// 최근 기록은 실측에서 "침입자가 늘었습니다. 현재 1명/2명/3명"이 세 줄로 쌓여 한 번뿐인 알림을 밀어냈다.
    /// 숫자만 다른 같은 종류 알림은 최신 문구 한 줄로 묶여야 한다.
    /// </summary>
    public class HudToolkitTests
    {
        [Test]
        public void ActivityLog_GroupsSameKindIntoLatestLine()
        {
            var log = new HudActivityLog(new VisualElement());
            log.Add("침입자가 늘었습니다. 현재 1명", true, "intruders");
            log.Add("침입자가 늘었습니다. 현재 2명", true, "intruders");
            log.Add("침입자가 늘었습니다. 현재 3명", true, "intruders");

            Assert.That(log.Lines.ToList(), Is.EqualTo(new[] { "침입자가 늘었습니다. 현재 3명 · 3회" }));
        }

        [Test]
        public void ActivityLog_GroupsIdenticalTextWithoutKeyAndSplitsOtherwise()
        {
            var log = new HudActivityLog(new VisualElement());
            log.Add("관리 모드가 활성화되었습니다.", false);
            log.Add("관리 모드가 활성화되었습니다.", false);
            log.Add("영업을 시작했습니다.", false);
            log.Add("침입자가 늘었습니다. 현재 1명", true, "intruders");
            log.Add("영업을 시작했습니다.", false);

            Assert.That(log.Lines.ToList(), Is.EqualTo(new[]
            {
                "관리 모드가 활성화되었습니다. · 2회",
                "영업을 시작했습니다.",
                "침입자가 늘었습니다. 현재 1명",
                "영업을 시작했습니다.",
            }), "이어질 때만 묶고, 키가 있는 알림과 없는 알림은 섞지 않습니다.");
        }

        [Test]
        public void ActivityLog_KeepsOnlyTheNewestEntries()
        {
            var log = new HudActivityLog(new VisualElement());
            for (var i = 0; i < HudActivityLog.MaxEntries + 3; i++)
                log.Add($"알림 {i}", false);

            var lines = log.Lines.ToList();
            Assert.That(lines.Count, Is.EqualTo(HudActivityLog.MaxEntries));
            Assert.That(lines.Last(), Is.EqualTo($"알림 {HudActivityLog.MaxEntries + 2}"));
        }
    }
}
