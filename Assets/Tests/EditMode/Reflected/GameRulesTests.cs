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

        private static object CallStatic(Type type, string method, params object[] args)
        {
            var m = type.GetMethod(method, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(m, Is.Not.Null, $"{type.Name}.{method} 정적 메서드를 찾지 못했습니다.");
            return m.Invoke(null, args);
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

        [Test]
        public void Merchant_MysteryBoxNeverDispensesAConsumable()
        {
            // 소모품은 사고 나도 소지품에 남지 않아 "이미 가졌다"로 걸러지지 않는다.
            // 그래서 상자 후보에 계속 남아, 90G짜리 정체불명의 유물이 30G짜리 물약을
            // 뱉을 수 있었다. 값도 손해고 손에 남는 것도 없다.
            var shop = NewAsset("_01.Code.Artifacts.ArtifactShopCatalogSO");
            var potion = NewAsset("_01.Code.Artifacts.ArtifactDataSO");
            SetPrivate(potion, "<Price>k__BackingField", 30);
            SetPrivate(potion, "<IsConsumable>k__BackingField", true);

            var stock = (System.Collections.IList)Activator.CreateInstance(
                typeof(System.Collections.Generic.List<>)
                    .MakeGenericType(Resolve("_01.Code.Artifacts.ArtifactDataSO")));
            stock.Add(potion);
            Call(shop, "ReplaceStock", stock);

            Assert.That(Call(shop, "PickRandomUnowned", new object[] { null }), Is.Null,
                "상자에 내줄 영구 유물이 없으면 아무것도 나오지 않아야 합니다.");
            Assert.That(Call(shop, "HasAvailableArtifact", new object[] { null }), Is.False,
                "물약만 남았으면 상자 칸은 닫혀야 합니다.");

            // 두 번째 인자는 직전 진열. 리플렉션 호출은 선택 매개변수도 반드시 채워야 한다.
            var display = (System.Collections.IList)Call(shop, "RollDisplay", new object[] { null, null });
            Assert.That(display, Has.Count.EqualTo(1),
                "물약은 지정 진열에는 계속 올라야 합니다 — 살 자리가 여기뿐입니다.");
        }

        // ── 던전 명성 ────────────────────────────────────────────────
        // 압력이 날짜가 아니라 플레이어가 지은 것에서 나온다. 이 계산이 곧 난이도 곡선이다.

        [Test]
        public void Fame_RaisesHeadcountAndStrengthTogether()
        {
            var rules = Resolve("_01.Code.Manager.DungeonFameRules");

            var quiet = (int)CallStatic(rules, "ResolveEnemyCount", 0);
            var busy = (int)CallStatic(rules, "ResolveEnemyCount", 10);
            Assert.That(busy, Is.GreaterThan(quiet), "명성이 오르면 더 많이 옵니다.");
            Assert.That(quiet, Is.GreaterThan(0), "아무것도 없어도 길 잃은 모험가는 옵니다.");

            Assert.That(CallStatic(rules, "ResolveEnemyLevel", 0), Is.EqualTo(1));
            Assert.That((int)CallStatic(rules, "ResolveEnemyLevel", 10),
                Is.GreaterThan((int)CallStatic(rules, "ResolveEnemyLevel", 0)),
                "명성이 오르면 더 센 모험가가 옵니다.");
        }

        [Test]
        public void Fame_PaysBackWhatItPutsAtRisk()
        {
            var rules = Resolve("_01.Code.Manager.DungeonFameRules");

            // 위험만 오르고 벌이가 그대로면 웅크리는 것이 언제나 최선이 되어,
            // 플레이어가 아무것도 짓지 않는 것이 정답인 게임이 된다.
            var quiet = (int)CallStatic(rules, "ResolveClearGold", 0);
            var busy = (int)CallStatic(rules, "ResolveClearGold", 10);
            Assert.That(busy, Is.GreaterThan(quiet));
        }

        [TestCase(0, 1)]
        [TestCase(1, 2)]
        [TestCase(2, 3)]
        [TestCase(5, 6)]
        public void Fame_MovesWithoutSteps(int lower, int higher)
        {
            var rules = Resolve("_01.Code.Manager.DungeonFameRules");

            // 시설 하나를 더 지었을 때 딱 그만큼만 늘어나야, 무엇 때문에 힘들어졌는지 짚을 수 있다.
            var atLower = (int)CallStatic(rules, "ResolveEnemyCount", lower);
            var atHigher = (int)CallStatic(rules, "ResolveEnemyCount", higher);
            Assert.That(atHigher, Is.GreaterThan(atLower),
                $"명성 {lower}→{higher}에서 인원이 그대로면 단을 밟는 곡선입니다.");
        }

        [Test]
        public void Fame_IsNeverNegativeEvenIfTheDungeonIsEmptied()
        {
            var rules = Resolve("_01.Code.Manager.DungeonFameRules");

            Assert.That((int)CallStatic(rules, "ResolveEnemyCount", -5), Is.GreaterThan(0));
            Assert.That(CallStatic(rules, "ResolveEnemyLevel", -5), Is.EqualTo(1));
            Assert.That((int)CallStatic(rules, "ResolveClearGold", -5), Is.GreaterThanOrEqualTo(0));
        }

        // ── 시설 휴업 ────────────────────────────────────────────────
        // 명성은 지은 것이 만들고 오르기만 한다. 닫는 것이 유일하게 물러설 곳이다.

        private static Component NewBuilding(out GameObject host)
        {
            host = new GameObject("Facility");
            host.SetActive(false);
            return host.AddComponent(Resolve("_01.Code.Buildings.Building"));
        }

        [Test]
        public void Closure_ClosedFacilityStopsOperating()
        {
            var building = NewBuilding(out var host);
            try
            {
                Assert.That(Get(building, "IsOperating"), Is.True);

                Call(building, "Close");
                Assert.That(Get(building, "IsClosed"), Is.True);
                Assert.That(Get(building, "IsOperating"), Is.False,
                    "닫힌 시설은 벌지도 운영비를 먹지도 소문을 내지도 않습니다.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void Closure_ReopeningWaitsForTheDayItWasBookedFor()
        {
            var building = NewBuilding(out var host);
            try
            {
                SetPrivate(building, "reopenDays", 2);
                Call(building, "Close");
                Call(building, "BeginReopen", 5);

                Assert.That(Get(building, "IsReopening"), Is.True);
                Assert.That(Call(building, "ReopenDaysRemaining", 5), Is.EqualTo(2));

                // 청산 직전에 급히 열어 막을 수는 없어야 한다.
                Assert.That(Call(building, "TryCompleteReopen", 6), Is.False);
                Assert.That(Get(building, "IsClosed"), Is.True);

                Assert.That(Call(building, "TryCompleteReopen", 7), Is.True);
                Assert.That(Get(building, "IsClosed"), Is.False);
                Assert.That(Get(building, "IsOperating"), Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void Closure_BookingTwiceDoesNotShortenTheWait()
        {
            var building = NewBuilding(out var host);
            try
            {
                SetPrivate(building, "reopenDays", 3);
                Call(building, "Close");
                Call(building, "BeginReopen", 1);
                Call(building, "BeginReopen", 4);

                // 두 번 눌러 날짜를 다시 잡을 수 있으면 기다림이 의미를 잃는다.
                Assert.That(Call(building, "ReopenDaysRemaining", 1), Is.EqualTo(3));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void Closure_SurvivesASaveAndLoad()
        {
            var building = NewBuilding(out var host);
            try
            {
                Call(building, "RestoreClosure", true, 9);

                Assert.That(Get(building, "IsClosed"), Is.True);
                Assert.That(Get(building, "IsReopening"), Is.True);
                Assert.That(Get(building, "ReopenOnDay"), Is.EqualTo(9),
                    "이어하기가 재개 예약을 잊으면 낸 금화가 사라집니다.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
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

        /// <summary>이자율만 정해 둔 빈 CostManager. 금화와 빚은 각 테스트가 정산으로 만든다.</summary>
        private static object NewCostManager(GameObject host, float weeklyInterest = 0.1f)
        {
            var manager = host.AddComponent(Resolve("_01.Code.Manager.CostManager"));
            SetPrivate(manager, "weeklyDebtInterest", weeklyInterest);
            return manager;
        }

        [Test]
        public void Settlement_SurplusIsGained()
        {
            var host = new GameObject("CostManagerTestHost");
            try
            {
                var manager = NewCostManager(host);

                var before = (int)Get(manager, "CurrentGold");
                Call(manager, "ApplySettlement", 120);

                Assert.That(Get(manager, "CurrentGold"), Is.EqualTo(before + 120));
                Assert.That(Get(manager, "CurrentDebt"), Is.EqualTo(0), "빚이 없으면 생길 것도 없습니다.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void Settlement_ShortfallBecomesDebtThatTheWeekCannotRepay()
        {
            var host = new GameObject("CostManagerTestHost");
            try
            {
                var manager = NewCostManager(host);

                var gold = (int)Get(manager, "CurrentGold");
                Call(manager, "ApplySettlement", -(gold + 80));

                Assert.That(Get(manager, "CurrentGold"), Is.EqualTo(0), "가진 금화를 먼저 씁니다.");
                Assert.That(Get(manager, "CurrentDebt"), Is.EqualTo(80), "못 낸 만큼만 빚이 됩니다.");

                // 흑자가 나도 주중에는 빚이 줄지 않는다. 갚는 자리는 청산일 하나뿐이다.
                Call(manager, "ApplySettlement", 40);
                Assert.That(Get(manager, "CurrentDebt"), Is.EqualTo(80), "주중에는 빚을 갚을 수 없습니다.");
                Assert.That(Get(manager, "CurrentGold"), Is.EqualTo(40), "번 돈은 그대로 운영 자금이 됩니다.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void Settlement_DebtDoesNotGrowUntilTheSettlementDay()
        {
            var host = new GameObject("CostManagerTestHost");
            try
            {
                var manager = NewCostManager(host);

                var gold = (int)Get(manager, "CurrentGold");
                Call(manager, "ApplySettlement", -(gold + 100));
                Assert.That(Get(manager, "CurrentDebt"), Is.EqualTo(100));

                Call(manager, "ApplySettlement", 0);
                Assert.That(Get(manager, "CurrentDebt"), Is.EqualTo(100),
                    "이자는 하루마다가 아니라 청산일에 한 번 붙습니다.");
                Assert.That(Get(manager, "WeeklyDue"), Is.EqualTo(110),
                    "내야 할 금액에는 10% 이자가 미리 보여야 합니다.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void SettleWeek_PaysThePrincipalAndInterestAtOnce()
        {
            var host = new GameObject("CostManagerTestHost");
            try
            {
                var manager = NewCostManager(host);

                var gold = (int)Get(manager, "CurrentGold");
                Call(manager, "ApplySettlement", -(gold + 100));
                Call(manager, "ApplySettlement", 200);

                Assert.That(Call(manager, "SettleWeek"), Is.True);
                Assert.That(Get(manager, "CurrentDebt"), Is.Zero, "청산하면 빚이 남지 않습니다.");
                Assert.That(Get(manager, "CurrentGold"), Is.EqualTo(90), "200G에서 이자 포함 110G가 빠집니다.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void SettleWeek_ShortOfTheFullAmountIsBankruptcy()
        {
            var host = new GameObject("CostManagerTestHost");
            try
            {
                var manager = NewCostManager(host);

                var gold = (int)Get(manager, "CurrentGold");
                Call(manager, "ApplySettlement", -(gold + 100));
                Call(manager, "ApplySettlement", 109); // 이자 포함 110G에 1G 모자란다.

                Assert.That(Call(manager, "SettleWeek"), Is.False);
                Assert.That(Get(manager, "CurrentDebt"), Is.EqualTo(100),
                    "부분 상환은 없습니다. 빚은 그대로 남습니다.");
                Assert.That(Get(manager, "CurrentGold"), Is.EqualTo(109), "갚지 못했으니 금화도 그대로입니다.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void SettleWeek_WithoutDebtPassesAndCostsNothing()
        {
            var host = new GameObject("CostManagerTestHost");
            try
            {
                var manager = NewCostManager(host);
                var gold = (int)Get(manager, "CurrentGold");

                Assert.That(Call(manager, "SettleWeek"), Is.True);
                Assert.That(Get(manager, "CurrentGold"), Is.EqualTo(gold), "빚이 없으면 낼 것도 없습니다.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [TestCase(1, false)]
        [TestCase(6, false)]
        [TestCase(7, true)]
        [TestCase(8, false)]
        [TestCase(14, true)]
        [TestCase(28, true)]
        public void SettlementDay_FallsOnEverySeventhDay(int day, bool expected)
        {
            var dayManager = Resolve("_01.Code.Manager.DayManager");
            var isSettlementDay = dayManager.GetMethod("IsSettlementDay");

            Assert.That(isSettlementDay.Invoke(null, new object[] { day }), Is.EqualTo(expected));
        }

        [TestCase(0, 7)]
        [TestCase(1, 6)]
        [TestCase(6, 1)]
        [TestCase(7, 0)]
        [TestCase(8, 6)]
        [TestCase(13, 1)]
        [TestCase(14, 0)]
        public void SettlementCountdown_ReachesZeroOnTheDeadlineAndResetsAfter(int day, int expected)
        {
            var dayManager = Resolve("_01.Code.Manager.DayManager");
            var daysUntil = dayManager.GetMethod("DaysUntilSettlementFrom");

            Assert.That(daysUntil.Invoke(null, new object[] { day }), Is.EqualTo(expected),
                "빚을 보여주는 화면이 여럿이라 남은 날은 한 곳에서만 세야 합니다.");
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
            SetPrivate(cost, "weeklyDebtInterest", 0f);
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

                Raise(waveChannel, NewEvent("_01.Code.Events.WaveEndedEvent", 1, 0, 0, 0));
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
                Raise(waveChannel, NewEvent("_01.Code.Events.WaveEndedEvent", 1, 0, 0, 0));

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

                Raise(waveChannel, NewEvent("_01.Code.Events.WaveEndedEvent", 1, 0, 0, 0));
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
                Raise(waveChannel, NewEvent("_01.Code.Events.WaveEndedEvent", 1, 0, 0, 0));

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
            // 생성자는 레벨·경험치까지 받는다. Activator는 기본 인자를 채워 주지 않으므로
            // 인자 수가 맞지 않으면 조용히 MissingMethodException으로 떨어진다.
            return Activator.CreateInstance(
                Resolve("_01.Code.Units.UnitConditionState"),
                fatigue, injury, 1f, trait, personality, command, 1, 0);
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

        // ── 민심 ───────────────────────────────────────────────────────
        // 민심은 오래도록 표시만 되는 숫자였다. 이제 유지비와 지원자에 걸리므로 곡선이 어긋나면 안 된다.

        private static GameObject BuildMoraleHost(out object morale)
        {
            var host = new GameObject("MoraleTestHost");
            var component = host.AddComponent(Resolve("_01.Code.Manager.MoralePolicyManager"));
            SetPrivate(component, "upkeepAtZeroMorale", 1.5f);
            SetPrivate(component, "upkeepAtFullMorale", 0.8f);
            SetPrivate(component, "applicantsAtZeroMorale", 0.25f);
            Call(component, "Awake");
            morale = component;
            return host;
        }

        private static void SetMorale(object morale, int value) =>
            morale.GetType().GetProperty("CurrentMorale", BindingFlags.Instance | BindingFlags.Public)
                .SetValue(morale, value);

        [Test]
        public void Morale_LowMoraleMakesKeepingMinionsMoreExpensive()
        {
            var host = BuildMoraleHost(out var morale);
            try
            {
                SetMorale(morale, 0);
                Assert.That((float)Get(morale, "UpkeepMultiplier"), Is.EqualTo(1.5f).Within(0.001f),
                    "민심이 바닥이면 위험수당이 붙습니다.");

                SetMorale(morale, 50);
                Assert.That((float)Get(morale, "UpkeepMultiplier"), Is.EqualTo(1.15f).Within(0.001f));

                SetMorale(morale, 100);
                Assert.That((float)Get(morale, "UpkeepMultiplier"), Is.EqualTo(0.8f).Within(0.001f),
                    "민심이 좋으면 같은 부하를 더 싸게 붙잡아 둡니다.");
            }
            finally
            {
                DestroyHost(host);
            }
        }

        [Test]
        public void Morale_LowMoraleThinsOutApplicants()
        {
            var host = BuildMoraleHost(out var morale);
            try
            {
                SetMorale(morale, 100);
                Assert.That(Call(morale, "AdjustRecruitCount", 4), Is.EqualTo(4), "민심이 좋으면 다 찾아옵니다.");

                SetMorale(morale, 0);
                Assert.That(Call(morale, "AdjustRecruitCount", 4), Is.EqualTo(1), "민심이 바닥이면 거의 오지 않습니다.");

                Assert.That(Call(morale, "AdjustRecruitCount", 0), Is.EqualTo(0), "원래 0이면 0입니다.");
            }
            finally
            {
                DestroyHost(host);
            }
        }

        // ── 금고 ───────────────────────────────────────────────────────
        // 보관 금화는 약탈 대상이고 침입자를 끌어당기기까지 한다. 이자가 없으면 맡길 이유가 없다.

        [Test]
        public void Treasury_StoredGoldEarnsInterestButStaysAtRisk()
        {
            var host = new GameObject("TreasuryTestHost");
            try
            {
                var treasury = host.AddComponent(Resolve("_01.Code.Buildings.Treasury"));
                SetPrivate(treasury, "capacity", 1000);
                SetPrivate(treasury, "storedGold", 200);
                SetPrivate(treasury, "interestPerSettlement", 0.1f);

                Assert.That(Get(treasury, "ProjectedInterest"), Is.EqualTo(20),
                    "맡기기 전에 얼마가 붙는지 보여야 판단이 됩니다.");

                Assert.That(Call(treasury, "AccrueInterest"), Is.EqualTo(20));
                Assert.That(Get(treasury, "StoredGold"), Is.EqualTo(220), "이자도 금고에 쌓입니다.");

                // 불어난 금화는 그대로 약탈 대상이다 — 그게 이 결정의 값이다.
                Assert.That(Call(treasury, "StealGold", 50), Is.EqualTo(50));
                Assert.That(Get(treasury, "StoredGold"), Is.EqualTo(170));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void Treasury_InterestNeverOverflowsCapacity()
        {
            var host = new GameObject("TreasuryTestHost");
            try
            {
                var treasury = host.AddComponent(Resolve("_01.Code.Buildings.Treasury"));
                SetPrivate(treasury, "capacity", 100);
                SetPrivate(treasury, "storedGold", 98);
                SetPrivate(treasury, "interestPerSettlement", 0.5f);

                Assert.That(Call(treasury, "AccrueInterest"), Is.EqualTo(2), "한도까지만 채웁니다.");
                Assert.That(Get(treasury, "StoredGold"), Is.EqualTo(100));
                Assert.That(Call(treasury, "AccrueInterest"), Is.EqualTo(0), "가득 차면 더 붙지 않습니다.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        // ── 지원자 ─────────────────────────────────────────────────────
        // 특성과 성격이 스탯을 바꾸므로, 보고 고른 사람과 실제로 오는 사람이 같아야 한다.

        [Test]
        public void Applicant_StaysTheSamePersonUntilHired()
        {
            var host = new GameObject("RosterTestHost");
            try
            {
                var roster = host.AddComponent(Resolve("_01.Code.Manager.HiredUnitRoster"));
                var unit = NewAsset("_01.Code.Units.UnitDataSO");

                // 후보 한 명을 세워 둔다. 명단은 읽을 때 이 수에 맞춰 채워진다.
                var owned = roster.GetType()
                    .GetField("_ownedUnits", BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(roster);
                owned.GetType().GetMethod("set_Item").Invoke(owned, new object[] { unit, 1 });

                var first = Call(roster, "PeekApplicant", unit);
                var second = Call(roster, "PeekApplicant", unit);

                Assert.That(second, Is.EqualTo(first),
                    "화면을 다시 그릴 때마다 지원자가 바뀌면 보고 고를 수가 없습니다.");

                var traitProperty = first.GetType().GetProperty("Trait", BindingFlags.Instance | BindingFlags.Public);
                Assert.That(traitProperty, Is.Not.Null);
                Assert.That(traitProperty.GetValue(first), Is.Not.Null, "지원자에게는 특성이 정해져 있어야 합니다.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        // ── 침입 경고 ──────────────────────────────────────────────────

        private static string IntrusionWarning(int steps)
        {
            // 목표 종류를 받는 오버로드가 생겨서 이름만으로는 모호하다. 인자 형태로 집어 준다.
            var method = Resolve("_01.Code.Manager.IntrusionThreat")
                .GetMethod(
                    "BuildWarning",
                    BindingFlags.Static | BindingFlags.Public,
                    null,
                    new[] { typeof(int) },
                    null);
            Assert.That(method, Is.Not.Null, "BuildWarning(int)을 찾지 못했습니다.");
            return (string)method.Invoke(null, new object[] { steps });
        }

        [Test]
        public void Intrusion_WarningSharpensAsTheTreasuryGetsCloser()
        {
            var noThreat = (int)Resolve("_01.Code.Manager.IntrusionThreat")
                .GetField("NoThreat", BindingFlags.Static | BindingFlags.Public).GetValue(null);

            Assert.That(IntrusionWarning(noThreat), Is.Empty, "닿을 수 있는 침입자가 없으면 경고도 없습니다.");
            Assert.That(IntrusionWarning(0), Does.Contain("금고 침입"), "금고에 선 순간은 따로 알려야 합니다.");
            Assert.That(IntrusionWarning(1), Does.Contain("1구역"));
            Assert.That(IntrusionWarning(4), Does.Contain("4구역"));

            // 가까울수록 붉어져야 눈에 먼저 들어온다.
            Assert.That(IntrusionWarning(1), Does.Contain("FF5A4A"));
            Assert.That(IntrusionWarning(2), Does.Contain("FFB03A"));
            Assert.That(IntrusionWarning(5), Does.Contain("C9BFA8"));
        }

        // ── 런 결과 ───────────────────────────────────────────────────
        // 웨이브 집계는 매일 초기화되므로 판 전체 전과는 따로 누적해야 남는다.

        private static GameObject BuildRunSummaryHost(out object summary)
        {
            var host = new GameObject("RunSummaryTestHost");
            var component = host.AddComponent(Resolve("_01.Code.Progression.RunSummarySystem"));
            Call(component, "Awake");
            summary = component;
            return host;
        }

        [Test]
        public void RunSummary_AccumulatesAcrossEveryWave()
        {
            var host = BuildRunSummaryHost(out var summary);
            try
            {
                Call(summary, "RecordWave", 10, 8, 200, 40, 3);
                Call(summary, "RecordWave", 14, 14, 350, 25, 5);

                Assert.That(Get(summary, "WavesFought"), Is.EqualTo(2));
                Assert.That(Get(summary, "Invaders"), Is.EqualTo(24), "침입자는 판 전체로 쌓여야 합니다.");
                Assert.That(Get(summary, "Kills"), Is.EqualTo(22));
                Assert.That(Get(summary, "DamageDealt"), Is.EqualTo(550));
                Assert.That(Get(summary, "DamageTaken"), Is.EqualTo(65));
                Assert.That(Get(summary, "CriticalHits"), Is.EqualTo(8));
            }
            finally
            {
                DestroyHost(host);
            }
        }

        [Test]
        public void RunSummary_ADayWithoutAWaveIsNotCountedAsAFight()
        {
            var host = BuildRunSummaryHost(out var summary);
            try
            {
                Call(summary, "RecordWave", 0, 0, 0, 0, 0);
                Assert.That(Get(summary, "WavesFought"), Is.EqualTo(0),
                    "침입자가 없는 날은 방어전으로 세지 않습니다.");
            }
            finally
            {
                DestroyHost(host);
            }
        }

        [Test]
        public void RunSummary_DebtRemembersItsWorstMomentNotItsLast()
        {
            var host = BuildRunSummaryHost(out var summary);
            try
            {
                Call(summary, "RecordDebt", 40);
                Call(summary, "RecordDebt", 260);
                Call(summary, "RecordDebt", 0);

                Assert.That(Get(summary, "PeakDebt"), Is.EqualTo(260),
                    "빚을 갚았어도 가장 위험했던 순간이 남아야 합니다.");
            }
            finally
            {
                DestroyHost(host);
            }
        }

        // ── 보스 ─────────────────────────────────────────────────────
        // 보스날이 셋인데 정의가 하나면 9·18·20일이 같은 덩치가 된다.

        [Test]
        public void Boss_EachBossDayCanHaveItsOwnFight()
        {
            var config = NewAsset("_01.Code.Manager.WaveConfigSO");
            var ninth = NewAsset("_01.Code.Manager.AdventurerPartySO");
            var final = NewAsset("_01.Code.Manager.AdventurerPartySO");

            var entryType = Resolve("_01.Code.Manager.WaveConfigSO+BossEntry");
            var entries = Array.CreateInstance(entryType, 2);
            entries.SetValue(NewBossEntry(entryType, 9, ninth, 5f), 0);
            entries.SetValue(NewBossEntry(entryType, 20, final, 6f), 1);
            SetPrivate(config, "bossEntries", entries);

            var ninthBoss = Call(config, "GetBossForDay", 9);
            var finalBoss = Call(config, "GetBossForDay", 20);

            Assert.That(ninthBoss, Is.Not.Null, "9일 보스 정의를 찾아야 합니다.");
            Assert.That(finalBoss, Is.Not.Null, "20일 보스 정의를 찾아야 합니다.");
            Assert.That(entryType.GetField("healthMultiplier").GetValue(ninthBoss), Is.EqualTo(5f));
            Assert.That(entryType.GetField("healthMultiplier").GetValue(finalBoss), Is.EqualTo(6f),
                "보스마다 다른 배율을 가져야 같은 덩치가 되지 않습니다.");
            Assert.That(Call(config, "GetBossPartyForDay", 9), Is.SameAs(ninth),
                "그 날 보스는 자기 파티를 이끌어야 합니다.");
        }

        [Test]
        public void Boss_ADayWithoutItsOwnEntryFallsBackToTheSharedParty()
        {
            var config = NewAsset("_01.Code.Manager.WaveConfigSO");
            var shared = NewAsset("_01.Code.Manager.AdventurerPartySO");
            SetPrivate(config, "bossParty", shared);

            Assert.That(Call(config, "GetBossForDay", 13), Is.Null, "정의하지 않은 날은 전용 보스가 없습니다.");
            Assert.That(Call(config, "GetBossPartyForDay", 13), Is.SameAs(shared),
                "전용 정의가 없으면 공용 보스 파티로 떨어져야 합니다.");
        }

        private static object NewBossEntry(Type entryType, int day, object party, float healthMultiplier)
        {
            var entry = Activator.CreateInstance(entryType);
            entryType.GetField("targetDay").SetValue(entry, day);
            entryType.GetField("party").SetValue(entry, party);
            entryType.GetField("healthMultiplier").SetValue(entry, healthMultiplier);
            return entry;
        }

        // ── 핵심 루프 기능 해금 ───────────────────────────────────────

        [Test]
        public void CoreLoopFeatures_UnlockOneLayerAtATime()
        {
            var rules = Resolve("_01.Code.UI.CoreLoopFeatureUnlocks");

            Assert.That(CallStatic(rules, "IsArtifactUnlocked", 1), Is.False);
            Assert.That(CallStatic(rules, "IsArtifactUnlocked", 2), Is.True,
                "첫 방어를 마친 뒤 유물 계층이 열려야 합니다.");
            Assert.That(CallStatic(rules, "IsExpeditionUnlocked", 3), Is.False);
            Assert.That(CallStatic(rules, "IsExpeditionUnlocked", 4), Is.True,
                "원정은 핵심 전투 기능을 익힌 뒤 마지막으로 열려야 합니다.");
        }

        // ── 장악도 ────────────────────────────────────────────────────
        // 장악은 금화가 아니라 방어로 돌아와야 원정이 돈벌이 버튼이 되지 않는다.

        /// <summary>장악도는 이제 컴포넌트라 호스트를 세워야 Current가 잡힌다.</summary>
        private static GameObject BuildConquestHost(out object conquest)
        {
            var host = new GameObject("VillageConquestTestHost");
            var component = host.AddComponent(Resolve("_01.Code.Progression.VillageConquestSystem"));
            Call(component, "Awake");
            conquest = component;
            return host;
        }

        private static void DestroyHost(GameObject host)
        {
            if (host == null)
                return;

            foreach (var component in host.GetComponents<MonoBehaviour>())
                component.GetType().GetMethod("OnDestroy", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.Invoke(component, null);

            UnityEngine.Object.DestroyImmediate(host);
        }

        [Test]
        public void Conquest_AConqueredVillageStopsSendingItsRaiders()
        {
            var host = BuildConquestHost(out var conquest);
            try
            {
                var patrol = NewAsset("_01.Code.Manager.AdventurerPartySO");
                var hunters = NewAsset("_01.Code.Manager.AdventurerPartySO");
                Call(conquest, "Register", patrol, 0);

                Assert.That(Call(conquest, "GetSuppression", patrol), Is.EqualTo(0f), "장악 전에는 그대로 쳐들어옵니다.");

                Call(conquest, "SetConquest", patrol, 100);
                Assert.That(Call(conquest, "GetSuppression", patrol), Is.EqualTo(1f), "완전히 장악하면 더 이상 오지 않습니다.");
                Assert.That(Call(conquest, "GetSuppression", hunters), Is.EqualTo(0f),
                    "등록되지 않은 파티는 장악과 무관하게 계속 옵니다.");
            }
            finally
            {
                DestroyHost(host);
            }
        }

        [Test]
        public void Conquest_AveragesAcrossEveryVillageNotJustTheConqueredOne()
        {
            var host = BuildConquestHost(out var conquest);
            try
            {
                var first = NewAsset("_01.Code.Manager.AdventurerPartySO");
                Call(conquest, "Register", first, 0);
                Call(conquest, "Register", NewAsset("_01.Code.Manager.AdventurerPartySO"), 0);

                Call(conquest, "SetConquest", first, 100);

                Assert.That((float)Get(conquest, "AverageConquestRatio"), Is.EqualTo(0.5f).Within(0.001f),
                    "마을 하나를 다 장악해도 전체로는 절반입니다.");
            }
            finally
            {
                DestroyHost(host);
            }
        }

        [Test]
        public void Conquest_ShrinksTheWaveButNeverToNothing()
        {
            var conquestHost = BuildConquestHost(out var conquest);
            var host = new GameObject("WaveManagerTestHost");
            try
            {
                var wave = host.AddComponent(Resolve("_01.Code.Manager.WaveManager"));
                SetPrivate(wave, "maxWaveReductionFromConquest", 0.4f);

                var party = NewAsset("_01.Code.Manager.AdventurerPartySO");
                Call(conquest, "Register", party, 0);

                Assert.That(Call(wave, "GetConquestAdjustedEnemyCount", 20), Is.EqualTo(20),
                    "장악하지 않았으면 웨이브가 그대로입니다.");

                Call(conquest, "SetConquest", party, 100);
                Assert.That(Call(wave, "GetConquestAdjustedEnemyCount", 20), Is.EqualTo(12),
                    "전부 장악하면 최대 감소율만큼 줄어듭니다.");
                Assert.That(Call(wave, "GetConquestAdjustedEnemyCount", 1), Is.EqualTo(1),
                    "아무리 장악해도 습격이 0명이 되지는 않습니다.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
                DestroyHost(conquestHost);
            }
        }


        [Test]
        public void Morale_LowMoraleRaisesWaveRewardSoABadRunCanCatchUp()
        {
            // 민심은 오래도록 일방통행이었다 — 실패하면 유지비만 오르고 만회할 길이 없어서,
            // 3일차 광산 하나 차이가 8일차에 유닛 한 명 차이로 굳었다(2026-09-01 실측).
            // 보상 배율이 그 대칭축이다.
            var host = BuildMoraleHost(out var morale);
            try
            {
                SetPrivate(morale, "rewardAtZeroMorale", 1.4f);

                SetMorale(morale, 100);
                var atFull = (float)Get(morale, "WaveRewardMultiplier");
                SetMorale(morale, 0);
                var atZero = (float)Get(morale, "WaveRewardMultiplier");
                SetMorale(morale, 50);
                var atHalf = (float)Get(morale, "WaveRewardMultiplier");

                Assert.That(atFull, Is.EqualTo(1f).Within(0.001f),
                    "잘 굴러가는 판의 보상은 건드리지 않아야 합니다.");
                Assert.That(atZero, Is.EqualTo(1.4f).Within(0.001f),
                    "민심이 바닥이면 보상이 올라야 만회가 가능합니다.");
                Assert.That(atHalf, Is.GreaterThan(atFull).And.LessThan(atZero),
                    "중간 민심은 중간 보상이어야 합니다.");

                // 유지비와 방향이 반대여야 대칭축이 성립한다.
                SetMorale(morale, 0);
                var upkeepAtZero = (float)Get(morale, "UpkeepMultiplier");
                SetMorale(morale, 100);
                var upkeepAtFull = (float)Get(morale, "UpkeepMultiplier");
                Assert.That(upkeepAtZero, Is.GreaterThan(upkeepAtFull),
                    "유지비는 민심이 낮을수록 비싸야 합니다(대칭 확인).");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }
        [Test]
        public void Morale_FallsWhenTheWaveBreaksThrough()
        {
            // 예전에는 웨이브가 끝나기만 하면 격퇴율과 무관하게 "방어 성공" 보너스가 붙었다.
            // 0/14로 전멸한 날에도 민심이 올랐고, 그래서 민심이 늘 100에 붙어 살았다.
            var host = BuildMoraleHost(out var morale);
            try
            {
                SetPrivate(morale, "waveClearMoraleDelta", 2);
                SetPrivate(morale, "waveBreachMoraleDelta", -6);

                var eventType = Resolve("_01.Code.Events.WaveEndedEvent");
                var handler = morale.GetType().GetMethod(
                    "HandleWaveEnded", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(handler, Is.Not.Null, "HandleWaveEnded를 찾지 못했습니다.");

                int MoraleAfter(int start, int enemies, int kills)
                {
                    SetMorale(morale, start);
                    handler.Invoke(morale, new[]
                    {
                        Activator.CreateInstance(eventType, 5, 0, enemies, kills)
                    });
                    return (int)Get(morale, "CurrentMorale");
                }

                Assert.That(MoraleAfter(50, 10, 10), Is.EqualTo(52),
                    "완전히 막아내면 예전과 같은 보너스여야 합니다.");
                Assert.That(MoraleAfter(50, 10, 0), Is.EqualTo(44),
                    "한 명도 못 막으면 민심이 떨어져야 합니다.");
                Assert.That(MoraleAfter(50, 10, 5), Is.LessThan(50),
                    "절반만 막아도 손해여야 합니다.");
                Assert.That(MoraleAfter(50, 0, 0), Is.EqualTo(52),
                    "적이 없던 날을 실패로 세면 안 됩니다.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }
    }
}
