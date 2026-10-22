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

        /// <summary>
        /// 판 종료 결과 화면 값. 예전에는 판이 끝나도 그리는 화면이 없어 버틴 날·진행도·새로 열린 것을 볼 곳이 없었다.
        /// 정산 뒤에는 이번 판 기록(잡은 보스)과 지난 최고 기록이 덮이므로 결과에 남아야 하고, 이번 판으로 넘은 단계의 콘텐츠·보정만 "새로 열림"에 오른다.
        /// </summary>
        [Test]
        public void RunEndScreen_ShowsStatsProgressAndNewUnlocks()
        {
            MetaProfileStore.Profile.progress = 4;
            MetaProfileStore.Profile.bestDay = 9;
            MetaProfileStore.RecordBossDefeat();
            var result = MetaProfileStore.RecordRunEnd(14, 2);
            Assert.That(result.BossesDefeated, Is.EqualTo(1));
            Assert.That(result.PreviousBestDay, Is.EqualTo(9), "새 기록이어도 지난 최고가 남아야 한다.");
            Assert.That(result.ProgressBefore, Is.EqualTo(4));
            Assert.That(result.TotalProgress, Is.EqualTo(28));

            var catalog = UnityEditor.AssetDatabase.LoadAssetAtPath<DungeonUnlockCatalogSO>("Assets/07.SO/Progression/DungeonUnlockCatalog.asset");
            Assert.That(catalog, Is.Not.Null);
            var unlocks = new System.Collections.Generic.List<Code.UI.ViewModels.RunEndUnlockViewModel>();
            Code.UI.ViewModels.RunEndPresenter.AddCatalogUnlocks(catalog.Entries, 4, 28, unlocks);
            var catalogCount = unlocks.Count;
            foreach (var entry in catalog.Entries)
            {
                var opened = unlocks.Exists(u => u.Name == entry.DisplayName);
                Assert.That(opened, Is.EqualTo(entry.MetaProgressRequired is 10 or 25),
                    $"{entry.DisplayName}(진행도 {entry.MetaProgressRequired})는 4 → 28에서 {(opened ? "열리지 않아야" : "열려야")} 한다.");
            }

            Code.UI.ViewModels.RunEndPresenter.AddModifierUnlocks(4, 28, unlocks);
            Assert.That(unlocks.Count, Is.EqualTo(catalogCount + 1), "진행도 20의 민심 안정만 새로 열린다(비상금은 처음부터 열림).");
            Assert.That(unlocks[^1].Name, Is.EqualTo(MetaProgressRules.Label(MetaModifier.SteadyMorale)));

            var model = Code.UI.ViewModels.RunEndPresenter.Build("던전 함락", "파산했습니다", false, 14,
                new Code.UI.ViewModels.RunEndStats(13, 182, 170, 48210, 21940, 312, 3800), result, unlocks);
            Assert.That(model.Days, Is.EqualTo("14일"));
            Assert.That(model.NewBest, Is.True);
            Assert.That(model.BestNote, Is.EqualTo("지난 최고 9일"));
            Assert.That(model.StatLabels.Split('\n').Length, Is.EqualTo(model.StatValues.Split('\n').Length), "전과 이름과 값 줄 수가 같아야 한다.");
            StringAssert.Contains("48,210", model.StatValues);
            Assert.That(model.ProgressRange, Is.EqualTo("4 → 28"));
            StringAssert.StartsWith("+24", model.ProgressGain);
            Assert.That(model.BeforeRatio, Is.EqualTo(4f / 50f).Within(0.001f), "막대는 다음 단계(50) 기준이다.");
            Assert.That(model.AfterRatio, Is.EqualTo(28f / 50f).Within(0.001f));
            Assert.That(model.ProgressNote, Is.EqualTo("다음 단계 50까지 22"));
            Assert.That(model.Unlocks.Count, Is.LessThanOrEqualTo(Code.UI.ViewModels.RunEndPresenter.MaxUnlockRows));
            if (unlocks.Count > Code.UI.ViewModels.RunEndPresenter.MaxUnlockRows)
                Assert.That(model.Unlocks[^1].IsMore, Is.True, "줄이 넘치면 마지막 줄이 \"+N개 더\"여야 한다.");
            Assert.That(model.UnlockEmpty, Is.Empty);

            var quiet = Code.UI.ViewModels.RunEndPresenter.Build("던전 함락", "쓰러졌습니다", false, 3, default,
                new MetaRunResult(3, 31, 3, false, 2, 2, 0, 14), null);
            Assert.That(quiet.NewBest, Is.False);
            Assert.That(quiet.BestNote, Is.EqualTo("최고 기록 14일"));
            Assert.That(quiet.Unlocks, Is.Empty);
            Assert.That(quiet.UnlockEmpty, Is.Not.Empty, "새로 열린 것이 없으면 빈 자리에 안내를 쓴다.");
        }

        /// <summary>
        /// 결과 화면 부하 아이콘은 칠해진 몸만 칸에 맞춘다. 예전에는 무기 자리까지 비워 둔 프레임 전체를 칸에 맞춰
        /// 몸(예: 망령 암살귀 50×47 칸 중 28×33)이 칸의 절반 남짓으로 작게 보였다.
        /// </summary>
        [Test]
        public void RunEndIcon_FitsOpaqueBodyToBox()
        {
            var (size, offset) = Code.UI.UiSpriteFit.Fit(new Vector2(50f, 48f), new Rect(10f, 0f, 25f, 40f), new Vector2(36f, 36f));
            Assert.That(size.y, Is.EqualTo(48f * 36f / 40f).Within(0.001f), "긴 쪽(세로 40)이 칸 36에 맞아야 한다.");
            Assert.That(offset.x, Is.EqualTo((25f - 22.5f) * 0.9f).Within(0.001f), "몸 가운데가 칸 가운데로 와야 한다.");
            Assert.That(offset.y, Is.EqualTo((24f - 20f) * 0.9f).Within(0.001f));

            var catalog = UnityEditor.AssetDatabase.LoadAssetAtPath<DungeonUnlockCatalogSO>("Assets/07.SO/Progression/DungeonUnlockCatalog.asset");
            foreach (var entry in catalog.Entries)
            {
                var sprite = entry?.Unit != null ? entry.Unit.IconSprite : null;
                if (sprite == null)
                    continue;

                var opaque = Code.UI.UiSpriteFit.OpaqueRect(sprite);
                Assert.That(opaque.xMin, Is.GreaterThanOrEqualTo(-0.01f), $"{entry.DisplayName}: 칠해진 영역이 그림 칸 밖이다.");
                Assert.That(opaque.yMax, Is.LessThanOrEqualTo(sprite.rect.height + 0.01f), $"{entry.DisplayName}: 칠해진 영역이 그림 칸 밖이다.");
                var (fit, _) = Code.UI.UiSpriteFit.Fit(sprite.rect.size, opaque, new Vector2(36f, 36f));
                var bodyLong = Mathf.Max(opaque.width, opaque.height) * fit.x / sprite.rect.width;
                Assert.That(bodyLong, Is.EqualTo(36f).Within(0.01f), $"{entry.DisplayName}: 몸의 긴 쪽이 칸을 채워야 한다.");
            }
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
