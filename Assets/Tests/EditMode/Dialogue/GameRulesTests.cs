using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode.Rules
{
    /// <summary>
    /// 이번에 손댄 진행 규칙들이 어긋나지 않는지 지킨다.
    /// 테스트 어셈블리는 Assembly-CSharp를 참조할 수 없어 리플렉션으로 접근한다.
    /// </summary>
    public class GameRulesTests
    {
        private static Type Resolve(string fullName)
        {
            var type = Type.GetType($"{fullName}, Assembly-CSharp");
            Assert.That(type, Is.Not.Null, $"{fullName} 타입을 찾지 못했습니다.");
            return type;
        }

        private static ScriptableObject NewAsset(string fullName) =>
            ScriptableObject.CreateInstance(Resolve(fullName));

        private static void SetPrivate(object target, string field, object value)
        {
            var f = target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(f, Is.Not.Null, $"{target.GetType().Name}.{field} 필드를 찾지 못했습니다.");
            f.SetValue(target, value);
        }

        private static object Call(object target, string method, params object[] args)
        {
            var m = target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(m, Is.Not.Null, $"{target.GetType().Name}.{method} 메서드를 찾지 못했습니다.");
            return m.Invoke(target, args);
        }

        private static object Get(object target, string property)
        {
            var p = target.GetType().GetProperty(property, BindingFlags.Instance | BindingFlags.Public);
            Assert.That(p, Is.Not.Null, $"{target.GetType().Name}.{property} 속성을 찾지 못했습니다.");
            return p.GetValue(target);
        }

        // ── 떠돌이 상인 ──────────────────────────────────────────────

        [Test]
        public void Merchant_VisitsOnScheduleOnly()
        {
            var shop = NewAsset("_01.Code.Artifacts.ArtifactShopCatalogSO");
            SetPrivate(shop, "firstVisitDay", 2);
            SetPrivate(shop, "visitIntervalDays", 2);

            foreach (var day in new[] { 2, 4, 6, 20 })
                Assert.That(Call(shop, "IsVisitDay", day), Is.True, $"{day}일차에는 상인이 와야 합니다.");

            foreach (var day in new[] { 0, 1, 3, 5, 19 })
                Assert.That(Call(shop, "IsVisitDay", day), Is.False, $"{day}일차에는 상인이 없어야 합니다.");

            Assert.That(Call(shop, "GetNextVisitDay", 0), Is.EqualTo(2), "아직 안 왔으면 첫 방문일을 알려야 합니다.");
            Assert.That(Call(shop, "GetNextVisitDay", 3), Is.EqualTo(4));
            Assert.That(Call(shop, "GetNextVisitDay", 4), Is.EqualTo(4), "오늘 와 있으면 오늘을 그대로 돌려줍니다.");
        }

        [Test]
        public void Merchant_PriceRisesWithEveryPurchase()
        {
            var shop = NewAsset("_01.Code.Artifacts.ArtifactShopCatalogSO");
            SetPrivate(shop, "priceIncreasePerPurchase", 0.15f);
            SetPrivate(shop, "priceInflationPerDay", 0f);
            SetPrivate(shop, "randomArtifactPrice", 100);

            var atStart = (int)Call(shop, "GetRandomArtifactPrice", 0, 0);
            var afterOne = (int)Call(shop, "GetRandomArtifactPrice", 0, 1);
            var afterTwo = (int)Call(shop, "GetRandomArtifactPrice", 0, 2);

            Assert.That(atStart, Is.EqualTo(100));
            Assert.That(afterOne, Is.EqualTo(115), "한 번 사면 15%가 붙어야 합니다.");
            Assert.That(afterTwo, Is.EqualTo(130), "누적 인상은 곱이 아니라 합입니다.");
            Assert.That(afterTwo, Is.GreaterThan(afterOne));
        }

        [Test]
        public void Merchant_UnpricedArtifactIsNotSold()
        {
            var shop = NewAsset("_01.Code.Artifacts.ArtifactShopCatalogSO");
            var artifact = NewAsset("_01.Code.Artifacts.ArtifactDataSO");
            SetPrivate(artifact, "<Price>k__BackingField", 0);

            Assert.That(Call(shop, "GetPrice", artifact, 0, 0), Is.EqualTo(0),
                "가격이 0인 유물은 상인이 취급하지 않습니다.");
        }

        // ── 해금 ────────────────────────────────────────────────────

        [Test]
        public void Unlock_OpensOnItsDayAndStaysOpen()
        {
            var entryType = Resolve("_01.Code.Progression.DungeonUnlockEntry");
            var entry = Activator.CreateInstance(entryType);
            SetPrivate(entry, "startsUnlocked", false);
            SetPrivate(entry, "unlockDay", 7);

            Assert.That(Call(entry, "IsUnlockedOn", 6), Is.False, "해금일 전에는 잠겨 있어야 합니다.");
            Assert.That(Call(entry, "IsUnlockedOn", 7), Is.True, "해금일에 열려야 합니다.");
            Assert.That(Call(entry, "IsUnlockedOn", 20), Is.True, "한 번 열리면 계속 열려 있어야 합니다.");
        }

        [Test]
        public void Unlock_StartingEntryIsOpenFromDayZero()
        {
            var entry = Activator.CreateInstance(Resolve("_01.Code.Progression.DungeonUnlockEntry"));
            SetPrivate(entry, "startsUnlocked", true);
            SetPrivate(entry, "unlockDay", 0);

            Assert.That(Call(entry, "IsUnlockedOn", 0), Is.True);
        }

        // ── 웨이브 곡선 ──────────────────────────────────────────────

        [Test]
        public void BossDay_UsesThatDaysNumbersNotTheSharedBossWave()
        {
            var config = NewAsset("_01.Code.Manager.WaveConfigSO");
            var entryType = Resolve("_01.Code.Manager.WaveConfigSO+WaveEntry");

            var specific = Array.CreateInstance(entryType, 1);
            var day9 = Activator.CreateInstance(entryType);
            entryType.GetField("targetDay").SetValue(day9, 9);
            entryType.GetField("enemyCount").SetValue(day9, 26);
            specific.SetValue(day9, 0);

            var boss = Activator.CreateInstance(entryType);
            entryType.GetField("enemyCount").SetValue(boss, 8);

            SetPrivate(config, "specificWaves", specific);
            SetPrivate(config, "bossWave", boss);
            SetPrivate(config, "bossEveryNDays", 9);
            SetPrivate(config, "finalDay", 20);

            Assert.That(Call(config, "IsBossDay", 9), Is.True);

            var wave = Call(config, "GetWaveForDay", 9);
            Assert.That(entryType.GetField("enemyCount").GetValue(wave), Is.EqualTo(26),
                "보스날이 그날 수치를 무시하면 후반 보스전이 전날보다 한산해집니다.");
        }

        [Test]
        public void FinalDay_IsTheLastDayOfTheRun()
        {
            var config = NewAsset("_01.Code.Manager.WaveConfigSO");
            SetPrivate(config, "finalDay", 20);

            Assert.That(Call(config, "IsFinalDay", 20), Is.True);
            Assert.That(Call(config, "IsFinalDay", 19), Is.False);
            Assert.That(Get(config, "FinalDay"), Is.EqualTo(20));
        }

        // ── 정산과 부채 ──────────────────────────────────────────────

        [Test]
        public void Settlement_SurplusIsGained()
        {
            var host = new GameObject("CostManagerTestHost");
            try
            {
                var manager = host.AddComponent(Resolve("_01.Code.Manager.CostManager"));
                SetPrivate(manager, "debtLimit", 300);
                SetPrivate(manager, "autoRepayRatio", 0.5f);
                SetPrivate(manager, "dailyDebtInterest", 0.1f);

                var before = (int)Get(manager, "CurrentGold");
                Call(manager, "ApplySettlement", 120);

                Assert.That(Get(manager, "CurrentGold"), Is.EqualTo(before + 120));
                Assert.That(Get(manager, "CurrentDebt"), Is.EqualTo(0), "빚이 없으면 상환할 것도 없습니다.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void Settlement_ShortfallBecomesDebtAndSurplusRepaysIt()
        {
            var host = new GameObject("CostManagerTestHost");
            try
            {
                var manager = host.AddComponent(Resolve("_01.Code.Manager.CostManager"));
                SetPrivate(manager, "debtLimit", 1000);
                SetPrivate(manager, "autoRepayRatio", 0.5f);
                SetPrivate(manager, "dailyDebtInterest", 0f);

                var gold = (int)Get(manager, "CurrentGold");
                Call(manager, "ApplySettlement", -(gold + 80));

                Assert.That(Get(manager, "CurrentGold"), Is.EqualTo(0), "가진 금화를 먼저 씁니다.");
                Assert.That(Get(manager, "CurrentDebt"), Is.EqualTo(80), "못 낸 만큼만 빚이 됩니다.");

                Call(manager, "ApplySettlement", 40);
                Assert.That(Get(manager, "CurrentDebt"), Is.EqualTo(60), "흑자의 절반으로 빚을 먼저 갚습니다.");
                Assert.That(Get(manager, "CurrentGold"), Is.EqualTo(20), "나머지는 운영 자금으로 남습니다.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void Settlement_DebtGrowsWithInterest()
        {
            var host = new GameObject("CostManagerTestHost");
            try
            {
                var manager = host.AddComponent(Resolve("_01.Code.Manager.CostManager"));
                SetPrivate(manager, "debtLimit", 1000);
                SetPrivate(manager, "autoRepayRatio", 0f);
                SetPrivate(manager, "dailyDebtInterest", 0.1f);

                var gold = (int)Get(manager, "CurrentGold");
                Call(manager, "ApplySettlement", -(gold + 100));
                Assert.That(Get(manager, "CurrentDebt"), Is.EqualTo(100));

                // 다음 정산에서 남은 빚에 이자가 붙는다.
                Call(manager, "ApplySettlement", 0);
                Assert.That(Get(manager, "CurrentDebt"), Is.EqualTo(110), "10% 이자가 붙어야 합니다.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void Settlement_DebtPastTheLimitIsBankruptcy()
        {
            var host = new GameObject("CostManagerTestHost");
            try
            {
                var manager = host.AddComponent(Resolve("_01.Code.Manager.CostManager"));
                SetPrivate(manager, "debtLimit", 100);
                SetPrivate(manager, "autoRepayRatio", 0f);
                SetPrivate(manager, "dailyDebtInterest", 0f);

                var gold = (int)Get(manager, "CurrentGold");
                Call(manager, "ApplySettlement", -(gold + 150));

                Assert.That((int)Get(manager, "CurrentDebt"), Is.GreaterThan(100),
                    "한도를 넘긴 부채는 파산 조건입니다.");
                Assert.That(Get(manager, "RemainingCredit"), Is.EqualTo(0), "남은 한도는 음수가 되지 않습니다.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        // ── 정산 장부와 실제 금화 ────────────────────────────────────
        // 금화를 옮기는 쪽(CostManager)과 장부에 적는 쪽(ManagementSettlementManager)이
        // 같은 수입을 서로 다르게 판단하면 한 번 번 돈이 두 번 들어온다.

        private const int LedgerStartingGold = 100;

        private static object NewEvent(string fullName, params object[] args) =>
            Activator.CreateInstance(Resolve(fullName), args);

        private static void Raise(ScriptableObject channel, object gameEvent) =>
            Call(channel, "RaiseEvent", gameEvent);

        /// <summary>
        /// 금화 담당과 장부 담당을 한 오브젝트에 올려 같은 채널을 듣게 한다.
        /// 에디트 모드에서는 Awake·OnEnable이 저절로 돌지 않으므로 직접 불러 준다.
        /// <paramref name="costManagerFirst"/>가 곧 채널 수신 순서다.
        /// </summary>
        private static GameObject BuildLedgerHost(
            bool costManagerFirst,
            out object costManager,
            out ScriptableObject costChannel,
            out ScriptableObject waveChannel)
        {
            costChannel = NewAsset("_01.Code.Core.GameEventChannelSO");
            waveChannel = NewAsset("_01.Code.Core.GameEventChannelSO");

            var host = new GameObject("LedgerTestHost");
            var cost = host.AddComponent(Resolve("_01.Code.Manager.CostManager"));
            var settlement = host.AddComponent(Resolve("_01.Code.Manager.ManagementSettlementManager"));

            SetPrivate(cost, "costEventChannel", costChannel);
            SetPrivate(cost, "waveEventChannel", waveChannel);
            SetPrivate(cost, "initialGold", LedgerStartingGold);
            SetPrivate(cost, "dailyDebtInterest", 0f);
            SetPrivate(settlement, "costEventChannel", costChannel);
            SetPrivate(settlement, "waveEventChannel", waveChannel);

            Call(cost, "Awake");
            if (costManagerFirst)
            {
                Call(cost, "OnEnable");
                Call(settlement, "OnEnable");
            }
            else
            {
                Call(settlement, "OnEnable");
                Call(cost, "OnEnable");
            }

            costManager = cost;
            return host;
        }

        private static void DestroyLedgerHost(GameObject host, ScriptableObject costChannel, ScriptableObject waveChannel)
        {
            if (host != null)
            {
                foreach (var component in host.GetComponents<MonoBehaviour>())
                {
                    Call(component, "OnDisable");
                    var onDestroy = component.GetType()
                        .GetMethod("OnDestroy", BindingFlags.Instance | BindingFlags.NonPublic);
                    onDestroy?.Invoke(component, null);
                }

                UnityEngine.Object.DestroyImmediate(host);
            }

            if (costChannel != null)
                UnityEngine.Object.DestroyImmediate(costChannel);
            if (waveChannel != null)
                UnityEngine.Object.DestroyImmediate(waveChannel);
        }

        [Test]
        public void Ledger_StandbyIncomeIsPaidOnceNotAgainAtSettlement()
        {
            var host = BuildLedgerHost(true, out var cost, out var costChannel, out var waveChannel);
            try
            {
                // 대기 중 정책·이벤트 보상은 그 자리에서 들어온다.
                Raise(costChannel, NewEvent("_01.Code.Events.GoldEarnedEvent", 60));
                Assert.That(Get(cost, "CurrentGold"), Is.EqualTo(LedgerStartingGold + 60),
                    "대기 중 수입은 바로 지갑에 들어와야 합니다.");

                Raise(waveChannel, NewEvent("_01.Code.Events.WaveEndedEvent", 1, 0));
                Assert.That(Get(cost, "CurrentGold"), Is.EqualTo(LedgerStartingGold + 60),
                    "이미 받은 돈이 정산 순액으로 또 들어오면 안 됩니다.");
            }
            finally
            {
                DestroyLedgerHost(host, costChannel, waveChannel);
            }
        }

        [Test]
        public void Ledger_StandbyIncomeIsPaidOnceRegardlessOfListenerOrder()
        {
            // 장부가 먼저 이벤트를 받아도 판단이 갈리면 안 된다.
            var host = BuildLedgerHost(false, out var cost, out var costChannel, out var waveChannel);
            try
            {
                Raise(costChannel, NewEvent("_01.Code.Events.GoldEarnedEvent", 60));
                Raise(waveChannel, NewEvent("_01.Code.Events.WaveEndedEvent", 1, 0));

                Assert.That(Get(cost, "CurrentGold"), Is.EqualTo(LedgerStartingGold + 60),
                    "수신 순서가 바뀌어도 수입은 한 번만 반영돼야 합니다.");
            }
            finally
            {
                DestroyLedgerHost(host, costChannel, waveChannel);
            }
        }

        [Test]
        public void Ledger_WaveIncomeMovesOnlyAtSettlement()
        {
            var host = BuildLedgerHost(true, out var cost, out var costChannel, out var waveChannel);
            try
            {
                Raise(waveChannel, NewEvent("_01.Code.Events.WaveStartedEvent", 1, 5));
                Raise(costChannel, NewEvent("_01.Code.Events.GoldEarnedEvent", 60));

                Assert.That(Get(cost, "CurrentGold"), Is.EqualTo(LedgerStartingGold),
                    "웨이브 중 수입은 장부에만 쌓입니다.");

                Raise(waveChannel, NewEvent("_01.Code.Events.WaveEndedEvent", 1, 0));
                Assert.That(Get(cost, "CurrentGold"), Is.EqualTo(LedgerStartingGold + 60),
                    "정산에서 한 번에 들어와야 합니다.");
            }
            finally
            {
                DestroyLedgerHost(host, costChannel, waveChannel);
            }
        }

        [Test]
        public void Ledger_TreasuryRobberyDoesNotTouchOperatingGold()
        {
            var host = BuildLedgerHost(true, out var cost, out var costChannel, out var waveChannel);
            try
            {
                Raise(waveChannel, NewEvent("_01.Code.Events.WaveStartedEvent", 1, 5));
                Raise(costChannel, NewEvent("_01.Code.Events.TreasuryRobbedEvent", 40));
                Raise(waveChannel, NewEvent("_01.Code.Events.WaveEndedEvent", 1, 0));

                Assert.That(Get(cost, "CurrentGold"), Is.EqualTo(LedgerStartingGold),
                    "금고에서 털린 보관 금화를 운영 자금에서 또 빼면 안 됩니다.");
                Assert.That(Get(cost, "CurrentDebt"), Is.EqualTo(0), "약탈만으로 빚이 생기지 않습니다.");
            }
            finally
            {
                DestroyLedgerHost(host, costChannel, waveChannel);
            }
        }

        // ── 원정 ─────────────────────────────────────────────────────
        // 편성이 결정이 되려면 누구를 보내는지가 결과를 바꿔야 한다.

        private static object NewUnitData(int cost)
        {
            var unit = NewAsset("_01.Code.Units.UnitDataSO");
            SetPrivate(unit, "<Cost>k__BackingField", cost);
            return unit;
        }

        /// <summary>피로만 다르고 나머지는 기본값인 상태를 만든다.</summary>
        private static object NewCondition(float fatigue)
        {
            var injury = Enum.ToObject(Resolve("_01.Code.Units.InjurySeverity"), 0);
            var trait = Enum.ToObject(Resolve("_01.Code.Units.UnitTrait"), 0);
            var personality = Enum.ToObject(Resolve("_01.Code.Units.UnitPersonality"), 0);
            var command = Enum.ToObject(Resolve("_01.Code.Units.UnitCommand"), 0);
            return Activator.CreateInstance(
                Resolve("_01.Code.Units.UnitConditionState"),
                fatigue, injury, 1f, trait, personality, command);
        }

        private static object NewVillage(string name, int reward, int difficulty) =>
            Activator.CreateInstance(
                Resolve("_01.Code.Progression.ExpeditionVillageEntry"), name, string.Empty, reward, difficulty, 0);

        private static float SuccessChance(int power, int difficulty)
        {
            var method = Resolve("_01.Code.Progression.ExpeditionVillageCatalogSO")
                .GetMethod("GetSuccessChance", BindingFlags.Static | BindingFlags.Public);
            Assert.That(method, Is.Not.Null, "GetSuccessChance를 찾지 못했습니다.");
            return (float)method.Invoke(null, new object[] { power, difficulty });
        }

        [Test]
        public void Expedition_ValuableUnitsCarryMoreWeightThanHeadcount()
        {
            var catalog = NewAsset("_01.Code.Progression.ExpeditionVillageCatalogSO");
            SetPrivate(catalog, "costPerPower", 10);
            SetPrivate(catalog, "fatiguePowerPenalty", 0f);

            var cheap = NewUnitData(14);
            var expensive = NewUnitData(82);
            var fresh = NewCondition(0f);

            var cheapPower = (int)Call(catalog, "GetUnitPower", cheap, fresh);
            var expensivePower = (int)Call(catalog, "GetUnitPower", expensive, fresh);

            Assert.That(expensivePower, Is.GreaterThan(cheapPower),
                "값어치가 큰 부하가 더 큰 전력이어야 편성이 결정이 됩니다.");
            Assert.That(cheapPower, Is.GreaterThan(0), "가장 싼 부하도 전력 1은 냅니다.");
        }

        [Test]
        public void Expedition_TiredUnitsBringLessPower()
        {
            var catalog = NewAsset("_01.Code.Progression.ExpeditionVillageCatalogSO");
            SetPrivate(catalog, "costPerPower", 10);
            SetPrivate(catalog, "fatiguePowerPenalty", 0.6f);

            var unit = NewUnitData(80);
            var rested = (int)Call(catalog, "GetUnitPower", unit, NewCondition(0f));
            var worn = (int)Call(catalog, "GetUnitPower", unit, NewCondition(100f));

            Assert.That(worn, Is.LessThan(rested), "지친 부하는 전력이 깎여야 합니다.");
            Assert.That(worn, Is.EqualTo(Mathf.RoundToInt(rested * 0.4f)),
                "피로 100이면 감쇠율만큼만 남습니다.");
        }

        [Test]
        public void Expedition_OddsFallOffWhenThePartyIsTooWeak()
        {
            Assert.That(SuccessChance(8, 8), Is.EqualTo(1f), "난이도에 닿으면 확정 성공입니다.");
            Assert.That(SuccessChance(20, 8), Is.EqualTo(1f), "넘겨도 100%를 넘지 않습니다.");
            Assert.That(SuccessChance(4, 8), Is.EqualTo(0.5f).Within(0.001f), "절반 전력이면 절반 확률입니다.");
            Assert.That(SuccessChance(0, 8), Is.EqualTo(0f), "전력이 없으면 성공하지 않습니다.");
        }

        [Test]
        public void Expedition_RewardGrowsWithTheDayAndShrinksOnFailure()
        {
            var catalog = NewAsset("_01.Code.Progression.ExpeditionVillageCatalogSO");
            SetPrivate(catalog, "rewardGrowthPerDay", 0.1f);
            SetPrivate(catalog, "rewardBonusPerUnit", 0);
            SetPrivate(catalog, "failureRewardRatio", 0.33f);

            var village = NewVillage("시험 마을", 100, 4);

            var firstDay = (int)Call(catalog, "GetReward", village, 1, 1, true);
            var lastDay = (int)Call(catalog, "GetReward", village, 20, 1, true);
            var failed = (int)Call(catalog, "GetReward", village, 1, 1, false);

            Assert.That(firstDay, Is.EqualTo(100), "1일차는 기준 보상 그대로입니다.");
            Assert.That(lastDay, Is.EqualTo(290), "20일차에는 성장률만큼 올라야 후반에도 의미가 남습니다.");
            Assert.That(failed, Is.EqualTo(33), "실패하면 일부만 회수합니다.");
        }
    }
}
