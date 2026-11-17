using System.Collections.Generic;
using System.Reflection;
using Code.Buildings;
using Code.Enemies;
using Code.Manager;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode.Rules
{
    /// <summary>
    /// 시설 대기열(FIFO·동시 이용 수)과 대기 만족도 수입 감액.
    ///
    /// 대기열이 순서를 어기면 늦게 온 손님이 먼저 돈을 쓰고, 감액이 50% 바닥을 뚫으면
    /// 붐비는 시설이 오히려 손해가 된다. 둘 다 플레이로는 숫자가 작아 눈에 띄지 않는다.
    /// </summary>
    public class FacilityQueueTests
    {
        private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        private readonly List<Object> _created = new();

        [TearDown]
        public void TearDown()
        {
            foreach (Object created in _created)
                if (created != null)
                    Object.DestroyImmediate(created);
            _created.Clear();
        }

        [TestCase(0f, 100)]
        [TestCase(4f, 80)]
        [TestCase(10f, 50)]
        [TestCase(30f, 50)]
        public void QueueSatisfaction_LosesFivePercentPerSecondDownToHalf(float waitSeconds, int expected)
        {
            Assert.That(FacilityDwellRules.ApplyQueueSatisfaction(100, waitSeconds), Is.EqualTo(expected));
        }

        [Test]
        public void QueueSatisfaction_NeverDropsAPaidVisitToZero()
        {
            Assert.That(FacilityDwellRules.ApplyQueueSatisfaction(1, 60f), Is.EqualTo(1));
            Assert.That(FacilityDwellRules.ApplyQueueSatisfaction(0, 0f), Is.EqualTo(0));
        }

        [Test]
        public void Queue_ServesVisitorsInArrivalOrder()
        {
            Building shop = CreateFacility(1);
            Enemy first = CreateVisitor("first");
            Enemy second = CreateVisitor("second");
            Enemy third = CreateVisitor("third");

            Assert.That(shop.TryStartVisit(first), Is.True);
            Assert.That(shop.TryStartVisit(second), Is.False);
            Assert.That(shop.TryStartVisit(third), Is.False);

            shop.CancelVisit(first);

            // 세 번째 손님이 먼저 물어봐도 두 번째 손님을 건너뛰지 못한다.
            Assert.That(shop.TryStartVisit(third), Is.False);
            Assert.That(shop.TryStartVisit(second), Is.True);
        }

        [Test]
        public void Queue_RespectsConcurrentVisitorLimit()
        {
            Building inn = CreateFacility(2);
            Enemy a = CreateVisitor("a");
            Enemy b = CreateVisitor("b");
            Enemy c = CreateVisitor("c");

            Assert.That(inn.TryStartVisit(a), Is.True);
            Assert.That(inn.TryStartVisit(b), Is.True);
            Assert.That(inn.TryStartVisit(c), Is.False);
            Assert.That(inn.CurrentVisitorCount, Is.EqualTo(2));
            Assert.That(inn.WaitingVisitorCount, Is.EqualTo(1));
        }

        [Test]
        public void Queue_CancelledWaiterLetsTheNextOneIn()
        {
            Building shop = CreateFacility(1);
            Enemy first = CreateVisitor("first");
            Enemy leaver = CreateVisitor("leaver");
            Enemy next = CreateVisitor("next");

            shop.TryStartVisit(first);
            shop.TryStartVisit(leaver);
            shop.TryStartVisit(next);

            // 줄을 포기한 손님은 빠지고, 자리가 나면 그 뒤 손님이 바로 들어간다.
            shop.CancelVisit(leaver);
            shop.CancelVisit(first);

            Assert.That(shop.WaitingVisitorCount, Is.EqualTo(1));
            Assert.That(shop.TryStartVisit(next), Is.True);
        }

        /// <summary>
        /// 만족도 임계점(사용자 결정): 줄에서 참을성이 바닥나고 갈 곳도 없으면 던전을 떠난다. 성격마다 참을성이
        /// 다르고, 모두 대체 시설을 찾기 시작하는 10초보다 길어야 먼저 다른 시설을 찾아본다.
        /// </summary>
        [Test]
        public void EscapeWait_DependsOnTraitAndComesAfterTheAlternativeSearch()
        {
            var alternativeSearch = FacilityDwellRules.MaxQueueIncomePenalty / FacilityDwellRules.QueueIncomePenaltyPerSecond;
            foreach (AdventurerTrait trait in System.Enum.GetValues(typeof(AdventurerTrait)))
                Assert.That(AdventurerTraitRules.GetEscapeWaitSeconds(trait), Is.GreaterThan(alternativeSearch), trait.ToString());

            Assert.That(AdventurerTraitRules.GetEscapeWaitSeconds(AdventurerTrait.Coward),
                Is.LessThan(AdventurerTraitRules.GetEscapeWaitSeconds(AdventurerTrait.None)));
            Assert.That(AdventurerTraitRules.GetEscapeWaitSeconds(AdventurerTrait.Shopaholic),
                Is.GreaterThan(AdventurerTraitRules.GetEscapeWaitSeconds(AdventurerTrait.None)));
        }

        private Building CreateFacility(int maxConcurrentVisitors)
        {
            var data = ScriptableObject.CreateInstance<BuildingDataSO>();
            _created.Add(data);
            typeof(BuildingDataSO).GetField("<DwellSeconds>k__BackingField", Instance).SetValue(data, 3f);

            var host = new GameObject("Facility");
            _created.Add(host);
            var building = host.AddComponent<Building>();
            // EditMode에서는 Awake가 돌지 않으므로 Initialize로 내구도까지 채운다.
            building.Initialize(data);
            typeof(Building).GetField("maxConcurrentVisitors", Instance).SetValue(building, maxConcurrentVisitors);

            Assert.That(building.AcceptsDwell, Is.True, "시험용 시설이 손님을 받지 않습니다.");
            return building;
        }

        private Enemy CreateVisitor(string name)
        {
            var host = new GameObject(name);
            _created.Add(host);
            return host.AddComponent<Enemy>();
        }

        /// <summary>
        /// 만족도 2단계: 감소 속도 = 시설 기준값(주점 3·대장간 4·상점 5%p) × 성격(겁쟁이 1.5·성직자 0.8·쇼핑광 0.6).
        /// 대체 시설 탐색은 만족도가 바닥(50%)에 닿는 때다. 성격이 같은 속도로 기다리면 성격 선택이 의미가 없다.
        /// </summary>
        [Test]
        public void QueuePenalty_DependsOnFacilityAndTrait()
        {
            var inn = NewFacilityOf<Inn>();
            var smith = NewFacilityOf<Blacksmith>();
            var store = NewFacilityOf<Store>();

            Assert.That(FacilityDwellRules.QueuePenaltyPerSecond(inn, AdventurerTrait.None), Is.EqualTo(0.03f).Within(0.0001f));
            Assert.That(FacilityDwellRules.QueuePenaltyPerSecond(smith, AdventurerTrait.None), Is.EqualTo(0.04f).Within(0.0001f));
            Assert.That(FacilityDwellRules.QueuePenaltyPerSecond(store, AdventurerTrait.None), Is.EqualTo(0.05f).Within(0.0001f));
            Assert.That(FacilityDwellRules.QueuePenaltyPerSecond(store, AdventurerTrait.Coward), Is.EqualTo(0.075f).Within(0.0001f));
            Assert.That(FacilityDwellRules.QueuePenaltyPerSecond(store, AdventurerTrait.Shopaholic), Is.EqualTo(0.03f).Within(0.0001f));

            Assert.That(FacilityDwellRules.AlternativeSearchSeconds(0.05f), Is.EqualTo(10f).Within(0.001f), "상점 일반 손님은 1단계와 같은 10초.");
            Assert.That(FacilityDwellRules.AlternativeSearchSeconds(0.075f), Is.LessThan(10f), "겁쟁이는 더 빨리 다른 곳을 찾습니다.");
            Assert.That(FacilityDwellRules.QueueSatisfaction(100f, 0.05f), Is.EqualTo(0.5f).Within(0.0001f), "만족도는 절반 아래로 떨어지지 않습니다.");
            Assert.That(FacilityDwellRules.ApplyQueueSatisfaction(100, 4f, FacilityDwellRules.QueueIncomePenaltyPerSecond),
                Is.EqualTo(FacilityDwellRules.ApplyQueueSatisfaction(100, 4f)), "기존 두 인자 판은 상점 기준값과 같습니다.");
        }

        /// <summary>불만 탈출은 성격 참을성을 따르되, 대체 시설 탐색 시작 + 2초보다 이르지 않다.</summary>
        [Test]
        public void EscapeWait_NeverBeforeAlternativeSearch()
        {
            foreach (AdventurerTrait trait in System.Enum.GetValues(typeof(AdventurerTrait)))
            foreach (var rate in new[] { 0.018f, 0.03f, 0.05f, 0.075f })
            {
                var escape = FacilityDwellRules.EscapeWaitSeconds(trait, rate);
                Assert.That(escape, Is.GreaterThanOrEqualTo(FacilityDwellRules.AlternativeSearchSeconds(rate) + FacilityDwellRules.EscapeMarginSeconds),
                    $"{trait} · {rate}");
                Assert.That(escape, Is.GreaterThanOrEqualTo(AdventurerTraitRules.GetEscapeWaitSeconds(trait)));
            }
        }

        private T NewFacilityOf<T>() where T : Building
        {
            var host = new GameObject(typeof(T).Name);
            host.SetActive(false);
            _created.Add(host);
            return host.AddComponent<T>();
        }
    }
}
