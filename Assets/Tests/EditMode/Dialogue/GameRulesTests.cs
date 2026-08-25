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
    }
}
