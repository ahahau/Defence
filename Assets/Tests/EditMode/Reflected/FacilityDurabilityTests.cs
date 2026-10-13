using System.Reflection;
using Code.Buildings;
using Code.Manager;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode.Rules
{
    /// <summary>
    /// 수익 시설의 내구도·수리·철거.
    ///
    /// 예전에는 마모가 청산일에 자동으로 고쳐지고 값만 빠져나갔다. 시설이 닳아도 벌이와 화면이
    /// 그대로라 수리는 세금일 뿐이었다. 내구도가 벌이를 깎고 0이면 영업을 멈추는지, 수리 중에는
    /// 손님을 받지 않는지를 지킨다.
    /// </summary>
    public class FacilityDurabilityTests
    {
        private GameObject _host;
        private BuildingDataSO _data;

        [TearDown]
        public void TearDown()
        {
            if (_host != null)
                Object.DestroyImmediate(_host);
            if (_data != null)
                Object.DestroyImmediate(_data);
        }

        private Building NewServiceFacility(int maxDurability = 20, int wearPerVisit = 1, int tier = 1)
        {
            _data = ScriptableObject.CreateInstance<BuildingDataSO>();
            SetField(_data, "<DwellSeconds>k__BackingField", 3f);
            SetField(_data, "<Cost>k__BackingField", 100);
            SetField(_data, "<FacilityTier>k__BackingField", tier);

            _host = new GameObject("Facility");
            _host.SetActive(false);
            var building = _host.AddComponent<Building>();
            SetField(building, "maxDurability", maxDurability);
            SetField(building, "wearPerVisit", wearPerVisit);
            SetField(building, "repairCostPerWear", 4);
            building.Initialize(_data);
            return building;
        }

        private static void SetField(object target, string name, object value)
        {
            var type = target.GetType();
            FieldInfo field = null;
            while (type != null && field == null)
            {
                field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                type = type.BaseType;
            }

            Assert.That(field, Is.Not.Null, $"{target.GetType().Name}.{name} 를 찾지 못했습니다.");
            field.SetValue(target, value);
        }

        [Test]
        public void IncomeMultiplier_FallsInProportionToDurability()
        {
            Assert.That(FacilityDurabilityRules.IncomeMultiplier(20, 20), Is.EqualTo(1f).Within(0.001f));
            Assert.That(FacilityDurabilityRules.IncomeMultiplier(10, 20), Is.EqualTo(0.75f).Within(0.001f));
            Assert.That(FacilityDurabilityRules.IncomeMultiplier(1, 20), Is.EqualTo(0.525f).Within(0.001f));
            Assert.That(FacilityDurabilityRules.IncomeMultiplier(0, 20), Is.Zero, "내구도 0이면 영업하지 못합니다.");
        }

        [Test]
        public void RepairCost_GrowsWithEveryRepair()
        {
            Assert.That(FacilityDurabilityRules.RepairCost(10, 4, 0), Is.EqualTo(40));
            Assert.That(FacilityDurabilityRules.RepairCost(10, 4, 2), Is.EqualTo(60), "두 번 고친 시설은 50% 더 비쌉니다.");
            Assert.That(FacilityDurabilityRules.RepairSeconds(10), Is.EqualTo(5f).Within(0.001f));
        }

        [Test]
        public void DemolishRefund_ShrinksWithWear()
        {
            Assert.That(FacilityDurabilityRules.DemolishRefund(100, 20, 20), Is.EqualTo(50));
            Assert.That(FacilityDurabilityRules.DemolishRefund(100, 10, 20), Is.EqualTo(25));
            Assert.That(FacilityDurabilityRules.DemolishRefund(100, 0, 20), Is.Zero);
        }

        [Test]
        public void Visits_WearDurabilityAndStopServiceAtZero()
        {
            var building = NewServiceFacility(maxDurability: 20, wearPerVisit: 10);
            Assert.That(building.UsesServiceDurability, Is.True);
            Assert.That(building.AcceptsDwell, Is.True);

            building.RecordVisitWear();
            Assert.That(building.CurrentDurability, Is.EqualTo(10));
            Assert.That(building.DurabilityIncomeMultiplier, Is.EqualTo(0.75f).Within(0.001f));

            building.RecordVisitWear();
            Assert.That(building.CurrentDurability, Is.Zero);
            Assert.That(building.IsWornOut, Is.True);
            Assert.That(building.AcceptsDwell, Is.False, "다 닳은 시설은 손님을 받지 않아야 대기열이 흩어집니다.");
            Assert.That(building.IsDestroyed, Is.False, "수익 시설은 닳아도 사라지지 않고 수리를 기다립니다.");
        }

        [Test]
        public void Repair_BlocksVisitsUntilDoneThenRefills()
        {
            var building = NewServiceFacility(maxDurability: 20, wearPerVisit: 10);
            building.RecordVisitWear();
            Assert.That(building.RepairCost, Is.EqualTo(40));
            Assert.That(building.RepairSeconds, Is.EqualTo(5f).Within(0.001f));

            Assert.That(building.BeginRepair(), Is.True);
            Assert.That(building.IsRepairing, Is.True);
            Assert.That(building.AcceptsDwell, Is.False, "수리 중 시설은 이용할 수 없습니다.");
            Assert.That(building.CanRepair, Is.False, "수리 중에 또 수리비를 받으면 안 됩니다.");

            building.TickRepair(4.9f);
            Assert.That(building.IsRepairing, Is.True);
            Assert.That(building.CurrentDurability, Is.EqualTo(10));

            building.TickRepair(0.2f);
            Assert.That(building.IsRepairing, Is.False);
            Assert.That(building.CurrentDurability, Is.EqualTo(20));
            Assert.That(building.AcceptsDwell, Is.True);
            Assert.That(building.RepairCount, Is.EqualTo(1));

            building.RecordVisitWear();
            Assert.That(building.RepairCost, Is.EqualTo(50), "한 번 고친 시설은 다음 수리비가 25% 오릅니다.");
        }

        [Test]
        public void DestructibleBuilding_IgnoresVisitorWear()
        {
            var building = NewServiceFacility();
            SetField(building, "destructible", true);

            building.RecordVisitWear();
            Assert.That(building.CurrentDurability, Is.EqualTo(20), "함정·회복실은 맞아서 깎이지 손님으로 닳지 않습니다.");
            Assert.That(building.RepairCost, Is.Zero);
        }

        [Test]
        public void Restore_KeepsZeroDurabilityRepairAndConvertsLegacyWear()
        {
            var building = NewServiceFacility();

            building.RestoreDurability(0);
            Assert.That(building.CurrentDurability, Is.Zero, "영업 정지 상태가 이어하기에서 1로 살아나면 안 됩니다.");

            building.RestoreDurability(20);
            building.RestoreWear(6);
            Assert.That(building.CurrentDurability, Is.EqualTo(14), "옛 저장의 마모는 내구도에서 깎여야 합니다.");

            building.RestoreRepair(2.5f, 3);
            Assert.That(building.IsRepairing, Is.True);
            Assert.That(building.RepairRemaining, Is.EqualTo(2.5f).Within(0.001f));
            Assert.That(building.RepairCount, Is.EqualTo(3));
        }

        /// <summary>
        /// 누가 쓰느냐에 따라 닳는 양이 다르다(사용자 결정): 레벨 1 위로 레벨당 +25%, 쇼핑광 2배, 엘리트·보스 3배, 곱으로 겹침.
        /// 예전에는 손님 누구나 똑같이 1씩 닳아 등급이 올라도 시설 관리가 그대로였다.
        /// </summary>
        [Test]
        public void Wear_GrowsWithLevelShopaholicAndElite()
        {
            Assert.That(FacilityDurabilityRules.WearMultiplier(1, false, false), Is.EqualTo(1f).Within(0.001f));
            Assert.That(FacilityDurabilityRules.WearMultiplier(3, false, false), Is.EqualTo(1.5f).Within(0.001f));
            Assert.That(FacilityDurabilityRules.WearMultiplier(1, true, false), Is.EqualTo(2f).Within(0.001f));
            Assert.That(FacilityDurabilityRules.WearMultiplier(3, true, true), Is.EqualTo(9f).Within(0.001f));
        }

        /// <summary>1.25배처럼 소수 마모도 쌓였다가 1이 되면 깎인다. 버리면 레벨 2 손님이 레벨 1과 같아진다.</summary>
        [Test]
        public void FractionalWear_AccumulatesAcrossVisits()
        {
            var building = NewServiceFacility(maxDurability: 20, wearPerVisit: 1);
            for (var i = 0; i < 4; i++)
                building.RecordVisitWear(1.25f);

            Assert.That(building.CurrentDurability, Is.EqualTo(15), "1.25 × 4 = 5만큼 닳아야 합니다.");
        }

        /// <summary>낮은 내구도는 대기열을 밀리게 한다: 절반까지는 그대로, 바닥에 가까울수록 최대 1.5배.</summary>
        [Test]
        public void LowDurability_SlowsTheVisit()
        {
            Assert.That(FacilityDurabilityRules.DwellTimeMultiplier(20, 20), Is.EqualTo(1f).Within(0.001f));
            Assert.That(FacilityDurabilityRules.DwellTimeMultiplier(10, 20), Is.EqualTo(1f).Within(0.001f));
            Assert.That(FacilityDurabilityRules.DwellTimeMultiplier(5, 20), Is.EqualTo(1.25f).Within(0.001f));
            Assert.That(FacilityDurabilityRules.DwellTimeMultiplier(0, 20), Is.EqualTo(1.5f).Within(0.001f));

            var building = NewServiceFacility(maxDurability: 20, wearPerVisit: 15);
            building.RecordVisitWear();
            Assert.That(building.EffectiveDwellSeconds, Is.EqualTo(3f * 1.25f).Within(0.001f), "내구도 5/20이면 3초가 3.75초가 됩니다.");
        }

        /// <summary>시설이 손님에게 주는 효과도 수입과 같은 비율로 약해진다. 1점짜리는 남고 2점부터 준다.</summary>
        [Test]
        public void LowDurability_WeakensTheServiceEffect()
        {
            Assert.That(FacilityDurabilityRules.ScaleEffect(2, 20, 20), Is.EqualTo(2));
            Assert.That(FacilityDurabilityRules.ScaleEffect(2, 4, 20), Is.EqualTo(1), "내구도 20%면 2점 효과가 1점이 됩니다.");
            Assert.That(FacilityDurabilityRules.ScaleEffect(1, 1, 20), Is.EqualTo(1), "1점짜리 효과는 사라지지 않습니다.");
            Assert.That(FacilityDurabilityRules.ScaleEffect(4, 10, 20), Is.EqualTo(3));
        }

        /// <summary>
        /// 시설 등급이 내구도에 반영된다(사용자 결정: 승급 시설은 튼튼하게). 예전에는 큰 주점도 주점과
        /// 똑같이 20이라 비싸게 승급해도 관리 부담이 그대로였다.
        /// </summary>
        [Test]
        public void UpgradedFacility_HasMoreDurability()
        {
            Assert.That(FacilityDurabilityRules.MaxDurability(20, 1), Is.EqualTo(20));
            Assert.That(FacilityDurabilityRules.MaxDurability(20, 2), Is.EqualTo(30));

            var building = NewServiceFacility(maxDurability: 20, tier: 2);
            Assert.That(building.MaxDurability, Is.EqualTo(30));
            Assert.That(building.CurrentDurability, Is.EqualTo(30), "지을 때 늘어난 최대치로 가득 차야 합니다.");

            building.RestoreDurability(30);
            Assert.That(building.CurrentDurability, Is.EqualTo(30), "복원이 기본 최대치 20으로 잘리면 안 됩니다.");
        }

        /// <summary>
        /// 시설 방 전투·사건은 손님과 상관없이 수익 시설만 깎는다. 함정·회복실은 맞아서 깎이는 규칙을 따른다.
        /// </summary>
        [Test]
        public void CombatAndEventWear_CutsOnlyServiceFacilities()
        {
            var building = NewServiceFacility(maxDurability: 20);
            Assert.That(building.ApplyWear(FacilityDurabilityRules.CombatDeathWear), Is.EqualTo(2));
            Assert.That(building.CurrentDurability, Is.EqualTo(18));

            Assert.That(building.ApplyWear(50), Is.EqualTo(18), "남은 만큼만 깎였다고 알려야 합니다.");
            Assert.That(building.IsWornOut, Is.True);
            Assert.That(building.IsDestroyed, Is.False);

            Object.DestroyImmediate(_host);
            building = NewServiceFacility(maxDurability: 20);
            SetField(building, "destructible", true);
            Assert.That(building.ApplyWear(5), Is.Zero);
            Assert.That(building.CurrentDurability, Is.EqualTo(20));
        }

#if UNITY_EDITOR
        /// <summary>승급 시설 데이터는 2등급이고, 화재·붕괴 사건의 한 선택지는 시설 내구도를 깎는다(요약과 값 일치).</summary>
        [Test]
        public void Assets_UpgradeTiersAndEventWearAreWired()
        {
            foreach (var (name, tier) in new[]
                     {
                         ("Store", 1), ("Inn", 1), ("Mine", 1),
                         ("ArmoryStore", 2), ("GrandInn", 2), ("DeepMine", 2),
                     })
            {
                var data = UnityEditor.AssetDatabase.LoadAssetAtPath<BuildingDataSO>(
                    $"Assets/GameModules/Data/Buildings/{name}BuildingData.asset");
                Assert.That(data, Is.Not.Null, name);
                Assert.That(data.FacilityTier, Is.EqualTo(tier), name);
            }

            foreach (var name in new[] { "MarketFire", "MineCollapse", "TunnelCollapse" })
            {
                var sequence = UnityEditor.AssetDatabase.LoadAssetAtPath<Code.Dialogue.DialogueSequenceSO>(
                    $"Assets/GameModules/Data/Dialogue/Sequences/DayEvent_{name}.asset");
                Assert.That(sequence, Is.Not.Null, name);

                var wired = false;
                for (var i = 0; sequence.TryGetLine(i, out var line); i++)
                foreach (var choice in line.Choices)
                {
                    foreach (var action in choice.Actions)
                    {
                        if (action is not Code.Dialogue.FacilityWearDialogueActionSO wear)
                            continue;

                        wired = true;
                        Assert.That(choice.EffectSummary, Does.Contain($"시설 내구도 -{wear.Amount}"), name);
                    }
                }

                Assert.That(wired, Is.True, $"{name} 사건에 시설 마모 행동이 없습니다.");
            }
        }
#endif
    }
}
