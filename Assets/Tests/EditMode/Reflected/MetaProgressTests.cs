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
    }
}
