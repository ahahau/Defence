using Code.Manager;
using NUnit.Framework;
using UnityEditor;

namespace Tests.EditMode.Rules
{
    /// <summary>
    /// 보스별 패배 조건과 예고 문구.
    ///
    /// 보스는 탈출하지 않아 버티면 결국 잡혔고, 보스날은 오래 걸리는 날일 뿐이었다. 제한 시간·금고 방 침입·
    /// 부하 손실이 각각 제때 패배로 이어지는지, 예고·HUD 문구가 조건을 빠짐없이 적는지, 청산일 보스마다
    /// 조건이 하나 이상 들어 있는지(데이터가 조용히 지워지지 않았는지)를 지킨다.
    /// </summary>
    public class BossDefeatTests
    {
        [Test]
        public void TimeLimit_FailsOnlyAfterTheLimit()
        {
            Assert.That(BossDefeatRules.Check("토벌대장", 149.9f, 150f, false, false, 0, 0), Is.Null);
            Assert.That(BossDefeatRules.Check("토벌대장", 150f, 150f, false, false, 0, 0), Does.Contain("150초"));
            Assert.That(BossDefeatRules.Check("토벌대장", 999f, 0f, false, false, 0, 0), Is.Null, "제한 시간이 0이면 시간으로는 지지 않습니다.");
        }

        [Test]
        public void TreasuryBreach_FailsOnlyWhenTheBossCares()
        {
            Assert.That(BossDefeatRules.Check("탐욕의 기사", 0f, 0f, true, true, 0, 0), Does.Contain("금고"));
            Assert.That(BossDefeatRules.Check("사냥꾼 두목", 0f, 0f, true, false, 0, 0), Is.Null);
        }

        [Test]
        public void UnitLosses_FailAtTheThreshold()
        {
            Assert.That(BossDefeatRules.Check("사냥꾼 두목", 0f, 0f, false, false, 2, 3), Is.Null);
            Assert.That(BossDefeatRules.Check("사냥꾼 두목", 0f, 0f, false, false, 3, 3), Does.Contain("부하 3명"));
        }

        [Test]
        public void Describe_ListsEveryCondition()
        {
            Assert.That(BossDefeatRules.Describe(180f, true, 0), Is.EqualTo("180초 안에 처치 · 금고 방 침입 시 패배"));
            Assert.That(BossDefeatRules.Describe(0f, false, 3), Is.EqualTo("부하 3명 쓰러지면 패배"));
            Assert.That(BossDefeatRules.Describe(0f, false, 0), Is.Empty);
            Assert.That(BossDefeatRules.DescribeProgress(30.2f, 150f, false, 0, 0), Is.EqualTo("남은 시간 120초"));
            Assert.That(BossDefeatRules.DescribeProgress(0f, 0f, true, 1, 3), Is.EqualTo("금고 방 침입 금지 · 부하 손실 1/3"));
        }

        [Test]
        public void EverySettlementDayBoss_HasADefeatCondition()
        {
            var config = AssetDatabase.LoadAssetAtPath<WaveConfigSO>("Assets/07.SO/WaveConfig.asset");
            Assert.That(config, Is.Not.Null, "WaveConfig.asset 을 찾지 못했습니다.");

            foreach (var day in new[] { 7, 14, 21, 28 })
            {
                var boss = config.GetBossForDay(day);
                Assert.That(boss, Is.Not.Null, $"{day}일 보스가 없습니다.");
                var hasCondition = boss.timeLimitSeconds > 0f || boss.loseOnTreasuryBreach || boss.loseAfterUnitsDowned > 0;
                Assert.That(hasCondition, Is.True, $"{day}일 보스({boss.title})에 패배 조건이 없습니다.");
            }
        }

        [Test]
        public void Preview_EndsWithTheChurchRule()
        {
            Assert.That(BossDefeatRules.DescribePreview(150f, false, 0, false), Is.EqualTo("150초 안에 처치 · 교회 부활 불가"));
            Assert.That(BossDefeatRules.DescribePreview(0f, true, 0, true), Is.EqualTo("금고 방 침입 시 패배 · 교회 부활 가능"));
        }

        /// <summary>
        /// 교회 부활 여부는 보스 성격대로 정했다(사용자 결정): 시간에 쫓기는 토벌대장·사냥꾼 두목은 불가,
        /// 돈으로 되살리는 탐욕의 기사와 교단이 뒤를 봐주는 기사단장은 가능. 값이 조용히 바뀌지 않게 지킨다.
        /// </summary>
        [Test]
        public void ChurchRevival_FollowsEachBossCharacter()
        {
            var config = AssetDatabase.LoadAssetAtPath<WaveConfigSO>("Assets/07.SO/WaveConfig.asset");
            Assert.That(config, Is.Not.Null, "WaveConfig.asset 을 찾지 못했습니다.");

            Assert.That(config.GetBossForDay(7).allowChurchRevival, Is.False, "7일 토벌대장");
            Assert.That(config.GetBossForDay(14).allowChurchRevival, Is.True, "14일 탐욕의 기사");
            Assert.That(config.GetBossForDay(21).allowChurchRevival, Is.False, "21일 사냥꾼 두목");
            Assert.That(config.GetBossForDay(28).allowChurchRevival, Is.True, "28일 왕국 기사단장");
        }
    }
}
