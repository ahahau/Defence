using System.Collections.Generic;
using Code.Buildings;
using Code.Enemies;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Tests.EditMode.Rules
{
    /// <summary>
    /// 관 상태와 교회 부활.
    ///
    /// 쓰러진 파티원이 관으로 남아 파티를 따라가고, 교회에서 공동 자금을 내고 되살아나는지를 지킨다.
    /// 부활비가 레벨에 비례하는지, 모자라면 낮은 체력(최소 25%)으로 살아나는지, 파티가 사라지면
    /// 관도 함께 치워지는지, 교회 데이터가 시설 규칙(중앙·유닛/함정 금지)을 지키는지 본다.
    /// </summary>
    public class ChurchRevivalTests
    {
        private EnemyDataSO _data;

        [TearDown]
        public void TearDown()
        {
            PartyCoffin.ClearAll();
            if (_data != null)
                Object.DestroyImmediate(_data);
        }

        [Test]
        public void Cost_GrowsWithLevel()
        {
            Assert.That(ChurchRevivalRules.Cost(1), Is.EqualTo(15));
            Assert.That(ChurchRevivalRules.Cost(5), Is.EqualTo(35));
            Assert.That(ChurchRevivalRules.Cost(0), Is.EqualTo(15), "레벨은 최소 1로 봅니다.");
        }

        [Test]
        public void HealthRatio_FollowsPaymentWithAFloor()
        {
            Assert.That(ChurchRevivalRules.HealthRatio(35, 35), Is.EqualTo(1f).Within(0.001f));
            Assert.That(ChurchRevivalRules.HealthRatio(20, 40), Is.EqualTo(0.5f).Within(0.001f));
            Assert.That(ChurchRevivalRules.HealthRatio(0, 40), Is.EqualTo(0.25f).Within(0.001f),
                "돈이 없어도 낮은 체력으로는 살아납니다.");
            Assert.That(ChurchRevivalRules.HealthRatio(99, 40), Is.EqualTo(1f).Within(0.001f));
        }

        [Test]
        public void Coffins_AreTrackedPerParty_AndClearedWithTheirParty()
        {
            _data = ScriptableObject.CreateInstance<EnemyDataSO>();
            PartyCoffin.Spawn(11, _data, 3, AdventurerVisitPurpose.Rest, null, Vector3.zero);
            PartyCoffin.Spawn(11, _data, 2, AdventurerVisitPurpose.Shopping, null, Vector3.zero);
            PartyCoffin.Spawn(12, _data, 1, AdventurerVisitPurpose.Rest, null, Vector3.zero);

            Assert.That(PartyCoffin.CountFor(11), Is.EqualTo(2));
            Assert.That(PartyCoffin.CountFor(12), Is.EqualTo(1));

            var buffer = new List<PartyCoffin>();
            PartyCoffin.CollectFor(11, buffer);
            Assert.That(buffer.Count, Is.EqualTo(2));
            Assert.That(buffer[0].Level, Is.EqualTo(3), "관은 쓰러질 때의 레벨을 기억합니다.");

            PartyCoffin.ClearParty(11);
            Assert.That(PartyCoffin.CountFor(11), Is.Zero, "파티가 전멸하거나 나가면 관도 사라집니다.");
            Assert.That(PartyCoffin.CountFor(12), Is.EqualTo(1), "다른 파티의 관은 남습니다.");
        }

        [Test]
        public void Coffin_RequiresAPartyAndData()
        {
            _data = ScriptableObject.CreateInstance<EnemyDataSO>();
            Assert.That(PartyCoffin.Spawn(0, _data, 1, AdventurerVisitPurpose.Rest, null, Vector3.zero), Is.Null);
            Assert.That(PartyCoffin.Spawn(5, null, 1, AdventurerVisitPurpose.Rest, null, Vector3.zero), Is.Null);
        }

        [Test]
        public void ChurchData_IsACentralFacilityThatBlocksUnitsAndTraps()
        {
            var data = AssetDatabase.LoadAssetAtPath<BuildingDataSO>("Assets/07.SO/Buildings/ChurchBuildingData.asset");
            Assert.That(data, Is.Not.Null, "ChurchBuildingData.asset 을 찾지 못했습니다.");
            Assert.That(data.Prefab, Is.InstanceOf<Church>());
            Assert.That(data.CentralOnly, Is.True);
            Assert.That(data.InstallOnEdge, Is.False);
            Assert.That(data.DwellSeconds, Is.GreaterThan(0f), "교회 방은 벌이 방이라 유닛·함정을 둘 수 없어야 합니다.");
            Assert.That(data.BoardSprite, Is.Not.Null);
        }
    }
}
