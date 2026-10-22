using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode.Rules
{
    /// <summary>
    /// 이번에 손댄 진행 규칙들이 어긋나지 않는지 지킨다.
    /// 테스트 어셈블리는 DungeonKeeper.Runtime를 참조할 수 없어 리플렉션으로 접근한다.
    /// </summary>
    public class GameRulesTests
    {
        /// <summary>
        /// 픽셀 단위 카메라는 카메라 크기를 매 프레임 덮어써서 휠 확대가 먹지 않았다.
        /// 확대는 기준 해상도 단계로 한다: 단계를 넘으면 가장자리에서 멈추고, 지금 단계를 되읽을 수 있어야 한다.
        /// </summary>
        [Test]
        public void PixelPerfectZoom_StepsThroughReferenceResolutions()
        {
            var host = new GameObject("ZoomCamera", typeof(Camera));
            try
            {
                var pixelPerfect = host.AddComponent<Code.Core.TilePixelCamera>();
                var levels = Code.Core.PixelPerfectZoomRules.Levels;

                Assert.That(Code.Core.PixelPerfectZoomRules.Apply(pixelPerfect, levels.Length + 3), Is.EqualTo(levels.Length - 1));
                Assert.That(pixelPerfect.RefResolutionY, Is.EqualTo(levels[levels.Length - 1].y));

                Assert.That(Code.Core.PixelPerfectZoomRules.Apply(pixelPerfect, -2), Is.EqualTo(0), "가장 가까운 단계에서 멈춥니다.");
                Assert.That(Code.Core.PixelPerfectZoomRules.LevelOf(pixelPerfect), Is.EqualTo(0));

                Code.Core.PixelPerfectZoomRules.Apply(pixelPerfect, Code.Core.PixelPerfectZoomRules.FocusLevel);
                Assert.That(Code.Core.PixelPerfectZoomRules.LevelOf(pixelPerfect), Is.EqualTo(Code.Core.PixelPerfectZoomRules.FocusLevel));
                Assert.That(pixelPerfect.RefResolutionX, Is.EqualTo(levels[Code.Core.PixelPerfectZoomRules.FocusLevel].x));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        /// <summary>
        /// 던전 타일은 0.625배로 줄여 1유닛 = 25.6텍셀인데, 카메라(PixelPerfectCamera)는 캐릭터에 맞춰 1유닛 = 28픽셀로 그렸다.
        /// 텍셀 하나가 1.09픽셀이 되어 칸 경계마다 테두리 픽셀이 두 줄로 겹쳐 바닥에 굵은 칸 줄무늬가 생겼다.
        /// 카메라 크기는 타일 텍셀 하나가 정수 픽셀이 되도록 정한다.
        /// </summary>
        [TestCase(1920, 1080, 1280, 720, 1)]
        [TestCase(1920, 1080, 960, 540, 2)]
        [TestCase(1920, 1080, 480, 270, 4)]
        [TestCase(1280, 720, 1280, 720, 1)]
        [TestCase(2560, 1440, 1280, 720, 2)]
        [TestCase(1366, 768, 640, 360, 2)]
        [TestCase(800, 600, 1280, 720, 1)]
        public void TilePixelCamera_DrawsEachTileTexelAsWholePixels(int screenWidth, int screenHeight, int refWidth, int refHeight,
            int expectedPixelsPerTexel)
        {
            var size = Code.Core.TilePixelCamera.OrthographicSize(screenWidth, screenHeight, refWidth, refHeight);
            var pixelsPerTexel = screenHeight / (2f * size) / Code.Core.TilePixelCamera.TexelsPerUnit;
            Assert.That(pixelsPerTexel, Is.EqualTo(expectedPixelsPerTexel).Within(0.0001f));
        }

        private static Type Resolve(string fullName)
        {
            var type = Type.GetType($"{fullName}, DungeonKeeper.Runtime");
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

        /// <summary>
        /// 테스트용 호스트를 치운다. OnDestroy를 손으로 불러 Current 싱글턴을 놓아주지 않으면
        /// 다음 테스트가 죽은 인스턴스를 붙잡는다.
        /// </summary>
        private static void DestroyHost(GameObject host)
        {
            if (host == null)
                return;

            foreach (var component in host.GetComponents<MonoBehaviour>())
                component.GetType().GetMethod("OnDestroy", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.Invoke(component, null);

            UnityEngine.Object.DestroyImmediate(host);
        }

        // ── 떠돌이 상인 ──────────────────────────────────────────────

        [Test]
        public void Merchant_VisitsOnScheduleOnly()
        {
            var shop = NewAsset("Code.Artifacts.ArtifactShopCatalogSO");
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
            var shop = NewAsset("Code.Artifacts.ArtifactShopCatalogSO");
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
            var shop = NewAsset("Code.Artifacts.ArtifactShopCatalogSO");
            var artifact = NewAsset("Code.Artifacts.ArtifactDataSO");
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
            var shop = NewAsset("Code.Artifacts.ArtifactShopCatalogSO");
            var potion = NewAsset("Code.Artifacts.ArtifactDataSO");
            SetPrivate(potion, "<Price>k__BackingField", 30);
            SetPrivate(potion, "<IsConsumable>k__BackingField", true);

            var stock = (System.Collections.IList)Activator.CreateInstance(
                typeof(System.Collections.Generic.List<>)
                    .MakeGenericType(Resolve("Code.Artifacts.ArtifactDataSO")));
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

        // ── 던전 등급 ────────────────────────────────────────────────
        // 압력이 날짜가 아니라 플레이어가 지은 것에서 나온다. 이 계산이 곧 난이도 곡선이다.

        [Test]
        public void Grade_RaisesHeadcountAndStrengthTogether()
        {
            var rules = Resolve("Code.Manager.DungeonGradeRules");

            var quiet = (int)CallStatic(rules, "ResolveEnemyCount", 0);
            var busy = (int)CallStatic(rules, "ResolveEnemyCount", 10);
            Assert.That(busy, Is.GreaterThan(quiet), "등급이 오르면 더 많이 옵니다.");
            Assert.That(quiet, Is.GreaterThan(0), "아무것도 없어도 길 잃은 모험가는 옵니다.");

            Assert.That(CallStatic(rules, "ResolveEnemyLevel", 0), Is.EqualTo(1));
            Assert.That((int)CallStatic(rules, "ResolveEnemyLevel", 10),
                Is.GreaterThan((int)CallStatic(rules, "ResolveEnemyLevel", 0)),
                "등급이 오르면 더 센 모험가가 옵니다.");
        }

        [Test]
        public void Grade_PaysBackWhatItPutsAtRisk()
        {
            var rules = Resolve("Code.Manager.DungeonGradeRules");

            // 위험만 오르고 벌이가 그대로면 웅크리는 것이 언제나 최선이 되어,
            // 플레이어가 아무것도 짓지 않는 것이 정답인 게임이 된다.
            // 등급이 오르면 인원도 수준도 오르니 다 막아냈을 때의 전리품도 커져야 한다.
            var quiet = DayTakeAt(rules, 0);
            var busy = DayTakeAt(rules, 10);
            Assert.That(busy, Is.GreaterThan(quiet));
        }

        /// <summary>이 등급에서 온 인원을 전부 막았을 때 하루 전리품.</summary>
        private static int DayTakeAt(Type rules, int grade)
        {
            var visitors = (int)CallStatic(rules, "ResolveVisitorCount", grade);
            var level = (int)CallStatic(rules, "ResolveVisitorLevel", grade);
            return visitors * (int)CallStatic(rules, "ResolveBounty", level);
        }

        [TestCase(0, 1)]
        [TestCase(1, 2)]
        [TestCase(2, 3)]
        [TestCase(5, 6)]
        public void Grade_MovesWithoutSteps(int lower, int higher)
        {
            var rules = Resolve("Code.Manager.DungeonGradeRules");

            // 시설 하나를 더 지었을 때 딱 그만큼만 늘어나야, 무엇 때문에 힘들어졌는지 짚을 수 있다.
            var atLower = (int)CallStatic(rules, "ResolveEnemyCount", lower);
            var atHigher = (int)CallStatic(rules, "ResolveEnemyCount", higher);
            Assert.That(atHigher, Is.GreaterThan(atLower),
                $"등급 {lower}→{higher}에서 인원이 그대로면 단을 밟는 곡선입니다.");
        }

        [Test]
        public void Grade_IsNeverNegativeEvenIfTheDungeonIsEmptied()
        {
            var rules = Resolve("Code.Manager.DungeonGradeRules");

            Assert.That((int)CallStatic(rules, "ResolveEnemyCount", -5), Is.GreaterThan(0));
            Assert.That(CallStatic(rules, "ResolveEnemyLevel", -5), Is.EqualTo(1));
            Assert.That(DayTakeAt(rules, -5), Is.GreaterThanOrEqualTo(0));
        }

        // ── 시설 휴업 ────────────────────────────────────────────────
        // 등급은 지은 것과 막아낸 수가 만든다. 닫는 것이 건물 쪽에서 물러설 유일한 길이다.

        private static Component NewBuilding(out GameObject host)
        {
            host = new GameObject("Facility");
            host.SetActive(false);
            return host.AddComponent(Resolve("Code.Buildings.Building"));
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
        public void Closure_WaitsTheSameNumberOfDaysBeforeTheFirstDayStarts()
        {
            var building = NewBuilding(out var host);
            try
            {
                SetPrivate(building, "reopenDays", 2);
                Call(building, "Close");

                // 준비 단계는 0일차다. 여기서 닫은 시설만 하루 더 기다리면 안 된다.
                Call(building, "BeginReopen", 0);
                Assert.That(Call(building, "ReopenDaysRemaining", 0), Is.EqualTo(2));
                Assert.That(Call(building, "TryCompleteReopen", 2), Is.True);
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

        // 시설 마모는 내구도로 합쳐졌다 → FacilityDurabilityTests

        // ── 해금 ────────────────────────────────────────────────────

        [Test]
        public void Unlock_OpensOnItsDayAndStaysOpen()
        {
            var entryType = Resolve("Code.Progression.DungeonUnlockEntry");
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
            var entry = Activator.CreateInstance(Resolve("Code.Progression.DungeonUnlockEntry"));
            SetPrivate(entry, "startsUnlocked", true);
            SetPrivate(entry, "unlockDay", 0);

            Assert.That(Call(entry, "IsUnlockedOn", 0), Is.True);
        }

        // ── 웨이브 곡선 ──────────────────────────────────────────────

        [Test]
        public void BossDay_UsesThatDaysNumbersNotTheSharedBossWave()
        {
            var config = NewAsset("Code.Manager.WaveConfigSO");
            var entryType = Resolve("Code.Manager.WaveConfigSO+WaveEntry");

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
            var config = NewAsset("Code.Manager.WaveConfigSO");
            SetPrivate(config, "finalDay", 20);

            Assert.That(Call(config, "IsFinalDay", 20), Is.True);
            Assert.That(Call(config, "IsFinalDay", 19), Is.False);
            Assert.That(Get(config, "FinalDay"), Is.EqualTo(20));
        }

        // ── 정산과 부채 ──────────────────────────────────────────────

        /// <summary>이자율만 정해 둔 빈 CostManager. 금화와 빚은 각 테스트가 정산으로 만든다.</summary>
        private static object NewCostManager(GameObject host, float weeklyInterest = 0.1f)
        {
            var manager = host.AddComponent(Resolve("Code.Manager.CostManager"));
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
                Assert.That(Get(manager, "WeeklyDue"), Is.EqualTo(30),
                    "최소 상환액은 이자 10G와 원금 20G를 포함해야 합니다.");
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
                Assert.That(Get(manager, "CurrentDebt"), Is.EqualTo(80), "최소 상환 뒤 원금 일부는 다음 주로 넘어갑니다.");
                Assert.That(Get(manager, "CurrentGold"), Is.EqualTo(170), "200G에서 이자 10G와 원금 20G가 빠집니다.");
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
                Call(manager, "ApplySettlement", 29); // 최소 상환 30G에 1G 모자란다.

                Assert.That(Call(manager, "SettleWeek"), Is.False);
                Assert.That(Get(manager, "CurrentDebt"), Is.EqualTo(100),
                    "부분 상환은 없습니다. 빚은 그대로 남습니다.");
                Assert.That(Get(manager, "CurrentGold"), Is.EqualTo(29), "갚지 못했으니 금화도 그대로입니다.");
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
            var dayManager = Resolve("Code.Manager.DayManager");
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
            var dayManager = Resolve("Code.Manager.DayManager");
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
            costChannel = NewAsset("Code.Core.GameEventChannelSO");
            waveChannel = NewAsset("Code.Core.GameEventChannelSO");

            var host = new GameObject("LedgerTestHost");
            var cost = host.AddComponent(Resolve("Code.Manager.CostManager"));
            var settlement = host.AddComponent(Resolve("Code.Manager.ManagementSettlementManager"));

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
                Raise(costChannel, NewEvent("Code.Events.GoldEarnedEvent", 60));
                Assert.That(Get(cost, "CurrentGold"), Is.EqualTo(LedgerStartingGold + 60),
                    "대기 중 수입은 바로 지갑에 들어와야 합니다.");

                Raise(waveChannel, NewEvent("Code.Events.WaveEndedEvent", 1, 0, 0, 0));
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
                Raise(costChannel, NewEvent("Code.Events.GoldEarnedEvent", 60));
                Raise(waveChannel, NewEvent("Code.Events.WaveEndedEvent", 1, 0, 0, 0));

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
                Raise(waveChannel, NewEvent("Code.Events.WaveStartedEvent", 1, 5));
                Raise(costChannel, NewEvent("Code.Events.GoldEarnedEvent", 60));

                Assert.That(Get(cost, "CurrentGold"), Is.EqualTo(LedgerStartingGold),
                    "웨이브 중 수입은 장부에만 쌓입니다.");

                Raise(waveChannel, NewEvent("Code.Events.WaveEndedEvent", 1, 0, 0, 0));
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
                Raise(waveChannel, NewEvent("Code.Events.WaveStartedEvent", 1, 5));
                Raise(costChannel, NewEvent("Code.Events.TreasuryRobbedEvent", 40));
                Raise(waveChannel, NewEvent("Code.Events.WaveEndedEvent", 1, 0, 0, 0));

                Assert.That(Get(cost, "CurrentGold"), Is.EqualTo(LedgerStartingGold),
                    "금고에서 털린 보관 금화를 운영 자금에서 또 빼면 안 됩니다.");
                Assert.That(Get(cost, "CurrentDebt"), Is.EqualTo(0), "약탈만으로 빚이 생기지 않습니다.");
            }
            finally
            {
                DestroyLedgerHost(host, costChannel, waveChannel);
            }
        }

        // ── 민심 ───────────────────────────────────────────────────────
        // 민심은 오래도록 표시만 되는 숫자였다. 이제 유지비와 지원자에 걸리므로 곡선이 어긋나면 안 된다.

        private static GameObject BuildMoraleHost(out object morale)
        {
            var host = new GameObject("MoraleTestHost");
            var component = host.AddComponent(Resolve("Code.Manager.MoralePolicyManager"));
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
                var treasury = host.AddComponent(Resolve("Code.Buildings.Treasury"));
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
                var treasury = host.AddComponent(Resolve("Code.Buildings.Treasury"));
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
                var roster = host.AddComponent(Resolve("Code.Manager.HiredUnitRoster"));
                var unit = NewAsset("Code.Units.UnitDataSO");

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
            var method = Resolve("Code.Manager.IntrusionThreat")
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
            var noThreat = (int)Resolve("Code.Manager.IntrusionThreat")
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
            var component = host.AddComponent(Resolve("Code.Progression.RunSummarySystem"));
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
            var config = NewAsset("Code.Manager.WaveConfigSO");
            var ninth = NewAsset("Code.Manager.AdventurerPartySO");
            var final = NewAsset("Code.Manager.AdventurerPartySO");

            var entryType = Resolve("Code.Manager.WaveConfigSO+BossEntry");
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
            var config = NewAsset("Code.Manager.WaveConfigSO");
            var shared = NewAsset("Code.Manager.AdventurerPartySO");
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
            var rules = Resolve("Code.UI.CoreLoopFeatureUnlocks");

            Assert.That(CallStatic(rules, "IsArtifactUnlocked", 1), Is.False);
            Assert.That(CallStatic(rules, "IsArtifactUnlocked", 2), Is.True,
                "첫 방어를 마친 뒤 유물 계층이 열려야 합니다.");
        }

        /// <summary>
        /// 금화가 모자란 방침은 잠그고(예전에는 그대로 차감했다), 셋 다 잠기면 "이번에는 공포하지 않음"을 연다 —
        /// 강제 선택 창이 고를 것 없이 시간을 멈춰 두면 판이 굳는다(사용자 결정 2026-10-22).
        /// </summary>
        [Test]
        public void PolicyChoice_LocksUnaffordableAndOffersDeclineOnlyWhenAllLocked()
        {
            Assert.That(Code.Manager.PolicyChoiceRules.IsAffordable(-70, 69), Is.False);
            Assert.That(Code.Manager.PolicyChoiceRules.IsAffordable(-70, 70), Is.True);
            Assert.That(Code.Manager.PolicyChoiceRules.IsAffordable(120, 0), Is.True, "금화를 받는 방침은 늘 고를 수 있다.");
            Assert.That(Code.Manager.PolicyChoiceRules.IsAffordable(0, 0), Is.True);

            Assert.That(Code.Manager.PolicyChoiceRules.ShouldOfferDecline(new[] { false, false, false }), Is.True);
            Assert.That(Code.Manager.PolicyChoiceRules.ShouldOfferDecline(new[] { false, true, false }), Is.False);
            Assert.That(Code.Manager.PolicyChoiceRules.ShouldOfferDecline(Array.Empty<bool>()), Is.False, "제시가 없으면 창도 없다.");
        }

        /// <summary>
        /// 방침 카드 값: 효과는 0인 항목을 빼고 금화 → 민심 → 매일 민심 → 부하 공격 → 부하 방어 순으로, 좋은 쪽은 초록·나쁜 쪽은 빨강.
        /// 짝이 시행 중이면 조합 칸이 "조합 성립"(금색), 아니면 묶이는 짝 목록. 모두 잠기면 기본 선택이 열린다.
        /// </summary>
        [Test]
        public void PolicyChoicePresenter_ListsEffectsCombosAndLocks()
        {
            var draft = MakePolicy("비상 징집", gold: 30, morale: -8, daily: -2, damage: 1.1f, defense: 3, days: 3);
            var martial = MakePolicy("계엄령", gold: 0, morale: -20, daily: -3, damage: 1.35f, defense: 5, days: 5);
            var rite = MakePolicy("주술 의식", gold: -70, morale: -6, daily: 0, damage: 1f, defense: 0, days: 4);
            var combo = new Code.Manager.PolicyCombo();
            var comboData = typeof(Code.Manager.PolicyCombo);
            comboData.GetField("displayName", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(combo, "총동원 체제");
            comboData.GetField("first", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(combo, draft);
            comboData.GetField("second", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(combo, martial);
            comboData.GetField("unitDamageMultiplier", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(combo, 1.15f);
            try
            {
                var card = Code.UI.ViewModels.PolicyChoicePresenter.BuildCard(draft, true,
                    new[] { (combo, martial, true) });
                Assert.That(card.EffectLabels, Is.EqualTo("금화\n민심\n매일 민심\n부하 공격\n부하 방어"));
                StringAssert.Contains("+30", card.EffectValues);
                StringAssert.Contains("+10%", card.EffectValues);
                Assert.That(card.Duration, Is.EqualTo("3일간"));
                Assert.That(card.ComboComplete, Is.True);
                Assert.That(card.ComboTitle, Is.EqualTo("조합 성립 · 총동원 체제"));
                StringAssert.StartsWith("계엄령과 함께", card.ComboBody);

                var waiting = Code.UI.ViewModels.PolicyChoicePresenter.BuildCard(draft, true, new[] { (combo, martial, false) });
                Assert.That(waiting.ComboComplete, Is.False);
                Assert.That(waiting.ComboTitle, Is.EqualTo("함께 쓰면"));
                Assert.That(waiting.ComboBody, Is.EqualTo("계엄령 → 총동원 체제"));

                var poor = Code.UI.ViewModels.PolicyChoicePresenter.Build(new[] { rite }, 3, 10, 50, null, _ => null);
                Assert.That(poor.Cards[0].Locked, Is.True);
                Assert.That(poor.Cards[0].ButtonLabel, Does.EndWith(" · 금화 부족"));
                Assert.That(poor.ShowDecline, Is.True, "하나뿐인 방침이 잠기면 기본 선택이 열려야 한다.");

                var mixed = Code.UI.ViewModels.PolicyChoicePresenter.Build(new[] { rite, draft }, 3, 10, 50, null, _ => null);
                Assert.That(mixed.ShowDecline, Is.False);
                Assert.That(mixed.Title, Is.EqualTo("운영 방침 · 3일차"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(draft);
                UnityEngine.Object.DestroyImmediate(martial);
                UnityEngine.Object.DestroyImmediate(rite);
            }
        }

        /// <summary>
        /// 보스 배너 값: 첫 줄은 종류(습격·증원·격노)와 습격 일차, 둘째 줄은 보스 이름(없으면 종류 이름), 격노만 흔든다.
        /// 예전에는 배너를 그리는 화면이 없어 보스 등장·증원·격노가 알림 없이 지나갔고, 증원·격노는 제목 글자("· 증원")로만 갈렸다.
        /// </summary>
        [Test]
        public void BossBanner_ShowsKindDayAndFallbacks()
        {
            var arrival = Code.UI.ViewModels.BossBannerPresenter.Build(
                new Code.Manager.BossBannerRequest(Code.Manager.BossBannerKind.Arrival, 7, "토벌대장", "방패 뒤의 적", 1.6f));
            Assert.That(arrival.Caption, Is.EqualTo("보스 습격 · 7일차"));
            Assert.That(arrival.Title, Is.EqualTo("토벌대장"));
            Assert.That(arrival.Shake, Is.False);
            Assert.That(arrival.HoldSeconds, Is.EqualTo(1.6f));

            var reinforcement = Code.UI.ViewModels.BossBannerPresenter.Build(
                new Code.Manager.BossBannerRequest(Code.Manager.BossBannerKind.Reinforcement, 14, "사냥꾼 두목", "호각", 0f));
            Assert.That(reinforcement.Caption, Is.EqualTo("증원 도착"), "증원·격노는 이미 보스가 나온 뒤라 일차를 붙이지 않는다.");
            Assert.That(reinforcement.HoldSeconds, Is.EqualTo(Code.UI.ViewModels.BossBannerPresenter.MinHoldSeconds));

            var rage = Code.UI.ViewModels.BossBannerPresenter.Build(
                new Code.Manager.BossBannerRequest(Code.Manager.BossBannerKind.Rage, 7, null, null, 1.6f));
            Assert.That(rage.Title, Is.EqualTo("보스 격노"), "보스 이름이 비면 종류 이름을 쓴다.");
            Assert.That(rage.Subtitle, Is.Empty);
            Assert.That(rage.Shake, Is.True);

            Assert.That(Code.UI.ViewModels.BossBannerPresenter.BorderColor(Code.Manager.BossBannerKind.Reinforcement),
                Is.Not.EqualTo(Code.UI.ViewModels.BossBannerPresenter.BorderColor(Code.Manager.BossBannerKind.Arrival)), "증원은 습격과 색이 달라야 한다.");
            Assert.That(Code.UI.ViewModels.BossBannerPresenter.FillColor(Code.Manager.BossBannerKind.Rage),
                Is.Not.EqualTo(Code.UI.ViewModels.BossBannerPresenter.FillColor(Code.Manager.BossBannerKind.Arrival)), "격노만 붉은 바탕이다.");
        }

        private static Code.Manager.PolicyDataSO MakePolicy(string name, int gold, int morale, int daily, float damage, int defense, int days)
        {
            var policy = ScriptableObject.CreateInstance<Code.Manager.PolicyDataSO>();
            var data = new UnityEditor.SerializedObject(policy);
            data.FindProperty("displayName").stringValue = name;
            data.FindProperty("goldDeltaOnSelect").intValue = gold;
            data.FindProperty("moraleDeltaOnSelect").intValue = morale;
            data.FindProperty("dailyMoraleDelta").intValue = daily;
            data.FindProperty("unitDamageMultiplier").floatValue = damage;
            data.FindProperty("unitDefenseBonus").intValue = defense;
            data.FindProperty("durationDays").intValue = days;
            data.ApplyModifiedPropertiesWithoutUndo();
            return policy;
        }
    }
}
