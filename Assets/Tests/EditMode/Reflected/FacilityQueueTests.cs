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

        private Building CreateFacility(int maxConcurrentVisitors)
        {
            var data = ScriptableObject.CreateInstance<BuildingDataSO>();
            _created.Add(data);
            typeof(BuildingDataSO).GetField("<DwellSeconds>k__BackingField", Instance).SetValue(data, 3f);

            var host = new GameObject("Facility");
            _created.Add(host);
            var building = host.AddComponent<Building>();
            typeof(Building).GetProperty(nameof(Building.Data), Instance).SetValue(building, data);
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
    }
}
