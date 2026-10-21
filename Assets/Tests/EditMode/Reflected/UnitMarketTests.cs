using System.Collections;
using System.Reflection;
using Code.Manager;
using Code.Units;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode.Rules
{
    /// <summary>
    /// 주간 지원자 목록의 크기·시세·유지비.
    ///
    /// 예전에는 매주 같은 수의 지원자가 늘 정가로 와서 고를 거리도, 시기를 재 볼 이유도 없었다.
    /// 등급에 따라 목록이 길어지는지, 화면에 보인 시세와 실제 결제가 같은지, 유지비는 시세가 아닌
    /// 정가 기준인지를 지킨다.
    /// </summary>
    public class UnitMarketTests
    {
        private GameObject _host;
        private UnitDataSO _unit;

        [TearDown]
        public void TearDown()
        {
            if (_host != null)
                Object.DestroyImmediate(_host);
            if (_unit != null)
                Object.DestroyImmediate(_unit);
        }

        [Test]
        public void WeeklyCandidates_GrowEveryTwoGradesUpToSix()
        {
            Assert.That(UnitMarketRules.WeeklyCandidateCount(2, 0), Is.EqualTo(2));
            Assert.That(UnitMarketRules.WeeklyCandidateCount(2, 1), Is.EqualTo(2));
            Assert.That(UnitMarketRules.WeeklyCandidateCount(2, 2), Is.EqualTo(3));
            Assert.That(UnitMarketRules.WeeklyCandidateCount(2, 5), Is.EqualTo(4));
            Assert.That(UnitMarketRules.WeeklyCandidateCount(2, 20), Is.EqualTo(UnitMarketRules.MaxWeeklyCandidates));
        }

        [Test]
        public void Price_StaysWithinMarketBandAndKeepsFreeUnitsFree()
        {
            Assert.That(UnitMarketRules.PriceMultiplierFromRoll(0f), Is.EqualTo(0.75f).Within(0.0001f));
            Assert.That(UnitMarketRules.PriceMultiplierFromRoll(1f), Is.EqualTo(1.25f).Within(0.0001f));
            Assert.That(UnitMarketRules.Price(100, 0.8f), Is.EqualTo(80));
            Assert.That(UnitMarketRules.Price(100, 0f), Is.EqualTo(100), "옛 저장처럼 시세가 없으면 정가입니다.");
            Assert.That(UnitMarketRules.Price(0, 1.2f), Is.Zero, "공짜 유닛은 시세가 붙어도 공짜입니다.");
        }

        [Test]
        public void Tier_MarksCheapAndExpensiveBeyondTenPercent()
        {
            Assert.That(UnitMarketRules.Tier(0.85f), Is.EqualTo(UnitPriceTier.Cheap));
            Assert.That(UnitMarketRules.Tier(0.95f), Is.EqualTo(UnitPriceTier.Normal));
            Assert.That(UnitMarketRules.Tier(1.05f), Is.EqualTo(UnitPriceTier.Normal));
            Assert.That(UnitMarketRules.Tier(1.15f), Is.EqualTo(UnitPriceTier.Expensive));
        }

        [Test]
        public void Upkeep_FollowsListPriceNotMarketPrice()
        {
            Assert.That(UnitMarketRules.DailyUpkeep(50, 5), Is.EqualTo(10));
            Assert.That(UnitMarketRules.DailyUpkeep(51, 5), Is.EqualTo(11), "나눠 떨어지지 않으면 올림입니다.");
            Assert.That(UnitMarketRules.DailyUpkeep(0, 5), Is.EqualTo(1), "유지비는 최소 1입니다.");
        }

        /// <summary>
        /// 새 판 첫 주에도 청산일처럼 지원자 명단이 온다(시작 지원자는 그대로). 예전에는 첫 청산일까지 시작 지원자 한 명뿐이라
        /// 대기 중 고용을 열어도 고를 사람이 없었다.
        /// </summary>
        [Test]
        public void NewRun_FillsTheFirstWeekApplicantsOnTopOfTheStartingOne()
        {
            _unit = ScriptableObject.CreateInstance<UnitDataSO>();
            var second = ScriptableObject.CreateInstance<UnitDataSO>();
            try
            {
                _host = new GameObject("RosterHost");
                _host.SetActive(false);
                var roster = _host.AddComponent<HiredUnitRoster>();
                var flags = BindingFlags.Instance | BindingFlags.NonPublic;
                var unlocked = (IList)typeof(HiredUnitRoster).GetField("_unlockedUnits", flags).GetValue(roster);
                unlocked.Add(_unit);
                unlocked.Add(second);
                var owned = (IDictionary)typeof(HiredUnitRoster).GetField("_ownedUnits", flags).GetValue(roster);
                owned[_unit] = 1;

                var start = typeof(HiredUnitRoster).GetMethod("Start", flags);
                Assert.That(start, Is.Not.Null, "HiredUnitRoster.Start 를 찾지 못했습니다.");
                start.Invoke(roster, null);

                // 등급 0·민심 없음이면 주간 2명. 유닛당 최대 2명이라 어떻게 뽑혀도 시작 1명 + 2명 = 3명.
                Assert.That(roster.GetCandidateCount(_unit) + roster.GetCandidateCount(second),
                    Is.EqualTo(1 + UnitMarketRules.WeeklyCandidateCount(2, 0)));
                Assert.That(roster.GetCandidateCount(_unit), Is.GreaterThanOrEqualTo(1), "시작 지원자는 남습니다.");
            }
            finally
            {
                Object.DestroyImmediate(second);
            }
        }

        [Test]
        public void Roster_QuotesTheSamePriceItWillCharge()
        {
            _unit = ScriptableObject.CreateInstance<UnitDataSO>();
            typeof(UnitDataSO).GetField("<Cost>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(_unit, 100);

            _host = new GameObject("RosterHost");
            _host.SetActive(false);
            var roster = _host.AddComponent<HiredUnitRoster>();
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var owned = (IDictionary)typeof(HiredUnitRoster).GetField("_ownedUnits", flags).GetValue(roster);
            owned[_unit] = 1;

            var price = roster.GetApplicantPrice(_unit);
            Assert.That(price, Is.InRange(75, 125), "지원자 시세는 정가의 75~125%입니다.");
            Assert.That(roster.GetApplicantPrice(_unit), Is.EqualTo(price), "다시 물어도 같은 지원자, 같은 값이어야 합니다.");

            var tier = roster.GetApplicantPriceTier(_unit);
            var expected = price <= 90 ? UnitPriceTier.Cheap : price >= 110 ? UnitPriceTier.Expensive : UnitPriceTier.Normal;
            Assert.That(tier, Is.EqualTo(expected));
        }
    }
}
