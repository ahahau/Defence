using System.IO;
using Code.Progression;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode.Rules
{
    /// <summary>
    /// 판이 끝나도 남는 기록과 시작 보정.
    ///
    /// 끝없는 판이라 패배가 곧 엔딩인데, 예전에는 패배가 아무것도 남기지 않았다. 버틴 날·잡은 보스가
    /// 진행도로 쌓이는지, 진행도에 따라 보정이 열리고 칸이 늘어나는지, 잠긴 보정이나 칸을 넘는 보정이
    /// 새 판에 걸리지 않는지, 기록이 파일을 거쳐 남는지를 지킨다. 실제 기록 파일은 건드리지 않는다.
    /// </summary>
    public class MetaProgressTests
    {
        private string _path;

        [SetUp]
        public void SetUp()
        {
            _path = Path.Combine(Application.temporaryCachePath, $"meta-test-{System.Guid.NewGuid():N}.json");
            MetaProfileStore.PathOverride = _path;
            MetaProfileStore.Reload();
        }

        [TearDown]
        public void TearDown()
        {
            MetaProfileStore.PathOverride = null;
            MetaProfileStore.Reload();
            if (File.Exists(_path))
                File.Delete(_path);
        }

        [Test]
        public void RunPoints_CountDaysAndBosses()
        {
            Assert.That(MetaProgressRules.RunPoints(12, 1), Is.EqualTo(22));
            Assert.That(MetaProgressRules.RunPoints(0, 0), Is.Zero);
        }

        [Test]
        public void Slots_GrowWithProgress()
        {
            Assert.That(MetaProgressRules.SlotCount(0), Is.EqualTo(1));
            Assert.That(MetaProgressRules.SlotCount(30), Is.EqualTo(2));
            Assert.That(MetaProgressRules.SlotCount(100), Is.EqualTo(3));
        }

        [Test]
        public void RunEnd_AddsProgressAndKeepsBestRecords()
        {
            MetaProfileStore.RecordWaveEnd(5, 2);
            MetaProfileStore.RecordWaveEnd(7, 3);
            MetaProfileStore.RecordBossDefeat();

            var first = MetaProfileStore.RecordRunEnd(8, 1);
            Assert.That(first.PointsEarned, Is.EqualTo(18), "8일 + 보스 1명 = 18점");
            Assert.That(first.NewBestDay, Is.True);

            var profile = MetaProfileStore.Profile;
            Assert.That(profile.bestDay, Is.EqualTo(8));
            Assert.That(profile.bestGrade, Is.EqualTo(3), "판 중 최고 등급이 남습니다.");
            Assert.That(profile.totalKills, Is.EqualTo(12));
            Assert.That(profile.currentRun.active, Is.False, "끝난 판의 기록은 비웁니다.");

            var second = MetaProfileStore.RecordRunEnd(3, 0);
            Assert.That(second.NewBestDay, Is.False);
            Assert.That(profile.bestDay, Is.EqualTo(8), "짧은 판이 최고 기록을 덮지 않습니다.");
            Assert.That(profile.progress, Is.EqualTo(21));
        }

        [Test]
        public void Equip_RespectsUnlocksAndSlots()
        {
            Assert.That(MetaProfileStore.ToggleEquip(MetaModifier.ManaReserve), Is.False, "진행도 50 전에는 잠겨 있습니다.");
            Assert.That(MetaProfileStore.ToggleEquip(MetaModifier.EmergencyFund), Is.True);

            MetaProfileStore.Profile.progress = 60;
            Assert.That(MetaProfileStore.ToggleEquip(MetaModifier.SteadyMorale), Is.True, "진행도 30부터 2칸입니다.");
            Assert.That(MetaProfileStore.ToggleEquip(MetaModifier.ManaReserve), Is.False, "칸이 꽉 차면 더 낄 수 없습니다.");
            Assert.That(MetaProfileStore.ToggleEquip(MetaModifier.EmergencyFund), Is.True, "끼운 보정은 언제든 뺄 수 있습니다.");
            Assert.That(MetaProfileStore.IsEquipped(MetaModifier.EmergencyFund), Is.False);
        }

        [Test]
        public void NewRun_UsesOnlyUnlockedModifiersThatFit()
        {
            var profile = MetaProfileStore.Profile;
            // 기록 파일을 손으로 고쳤거나 진행도가 깎인 경우를 흉내 낸다.
            profile.equipped.Add((int)MetaModifier.ManaReserve);
            profile.equipped.Add((int)MetaModifier.EmergencyFund);
            profile.equipped.Add((int)MetaModifier.SteadyMorale);

            var applied = MetaProfileStore.BeginNewRun();
            Assert.That(applied, Is.EquivalentTo(new[] { (int)MetaModifier.EmergencyFund }),
                "진행도 0이면 열린 보정 하나, 칸 하나만 걸립니다.");
            Assert.That(profile.currentRun.active, Is.True);
            Assert.That(MetaProgressRules.IsPersistent(MetaModifier.ManaReserve), Is.True);
            Assert.That(MetaProgressRules.IsPersistent(MetaModifier.EmergencyFund), Is.False);
        }

        [Test]
        public void Profile_SurvivesAReload()
        {
            MetaProfileStore.RecordBossDefeat();
            MetaProfileStore.RecordRunEnd(10, 2);
            MetaProfileStore.ToggleEquip(MetaModifier.EmergencyFund);

            MetaProfileStore.Reload();
            var profile = MetaProfileStore.Profile;
            Assert.That(profile.progress, Is.EqualTo(20));
            Assert.That(profile.bestDay, Is.EqualTo(10));
            Assert.That(profile.runsPlayed, Is.EqualTo(1));
            Assert.That(MetaProfileStore.IsEquipped(MetaModifier.EmergencyFund), Is.True);
        }

        /// <summary>
        /// 진행도로 콘텐츠가 새 판 후보에 들어온다(사용자 결정: 유닛·함정·시설 + 사건). 진행도가 모자라면
        /// 해금일이 와도 열리지 않고, 안내에 필요한 진행도가 붙는다.
        /// </summary>
        [Test]
        public void MetaGatedContent_OpensOnlyWithEnoughProgress()
        {
            var entry = new DungeonUnlockEntry((Code.Units.UnitDataSO)null, false, "16일차 · 시험");
            typeof(DungeonUnlockEntry)
                .GetField("unlockDay", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .SetValue(entry, 3);
            typeof(DungeonUnlockEntry)
                .GetField("metaProgressRequired", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .SetValue(entry, 25);

            Assert.That(entry.IsUnlockedOn(20), Is.False, "진행도 0이면 해금일이 지나도 이번 판에는 나오지 않습니다.");
            Assert.That(entry.UnlockHint, Does.StartWith("진행도 25 필요"));

            MetaProfileStore.Profile.progress = 25;
            Assert.That(entry.IsUnlockedOn(2), Is.False, "진행도를 채워도 해금일 규칙은 그대로입니다.");
            Assert.That(entry.IsUnlockedOn(3), Is.True);
            Assert.That(entry.UnlockHint, Is.EqualTo("16일차 · 시험"));

            Assert.That(MetaUnlockRules.BecameUnlocked(25, 20, 30), Is.True);
            Assert.That(MetaUnlockRules.BecameUnlocked(25, 25, 30), Is.False, "이미 열린 것은 새로 열린 것이 아닙니다.");
            Assert.That(MetaUnlockRules.BecameUnlocked(0, 0, 30), Is.False);
            Assert.That(MetaUnlockRules.NextTier(0), Is.EqualTo(10));
            Assert.That(MetaUnlockRules.NextTier(25), Is.EqualTo(50));
            Assert.That(MetaUnlockRules.NextTier(50), Is.Zero);
        }

#if UNITY_EDITOR
        /// <summary>잠근 콘텐츠의 필요 진행도는 정해 둔 단계 값이어야 하고, 단계마다 무언가 열린다.</summary>
        [Test]
        public void MetaGatedAssets_UseTheDefinedTiers()
        {
            var catalog = UnityEditor.AssetDatabase.LoadAssetAtPath<DungeonUnlockCatalogSO>(
                "Assets/07.SO/Progression/DungeonUnlockCatalog.asset");
            Assert.That(catalog, Is.Not.Null);

            var opened = new System.Collections.Generic.HashSet<int>();
            foreach (var item in catalog.Entries)
            {
                if (item == null || item.MetaProgressRequired <= 0)
                    continue;

                Assert.That(MetaUnlockRules.Tiers, Does.Contain(item.MetaProgressRequired), item.DisplayName);
                Assert.That(item.StartsUnlocked, Is.False, $"{item.DisplayName}: 처음부터 열린 기본 콘텐츠는 잠그지 않습니다.");
                opened.Add(item.MetaProgressRequired);
            }

            Assert.That(opened, Is.EquivalentTo(MetaUnlockRules.Tiers), "단계마다 새로 열리는 콘텐츠가 있어야 합니다.");

            foreach (var (name, tier) in new[] { ("CursedRelic", 10), ("BlackMarket", 25), ("RivalWarlord", 50) })
            {
                var sequence = UnityEditor.AssetDatabase.LoadAssetAtPath<Code.Dialogue.DialogueSequenceSO>(
                    $"Assets/07.SO/Dialogue/Sequences/DayEvent_{name}.asset");
                Assert.That(sequence, Is.Not.Null, name);
                Assert.That(sequence.MetaProgressRequired, Is.EqualTo(tier), name);
            }
        }
#endif
    }
}
