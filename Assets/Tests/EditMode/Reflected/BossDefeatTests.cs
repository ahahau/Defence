using Code.Manager;
using NUnit.Framework;
using UnityEditor;

namespace Tests.EditMode.Rules
{
    /// <summary>
    /// 보스별 패배 조건과 예고 문구.
    ///
    /// 보스는 탈출하지 않아 버티면 결국 잡혔고, 보스날은 오래 걸리는 날일 뿐이었다. 제한 시간·금고 방 침입·
    /// 부하 손실이 각각 제때 패배로 이어지는지, 예고·HUD 문구가 조건을 빠짐없이 적는지, 청산일 보스마다
    /// 조건이 하나 이상 들어 있는지(데이터가 조용히 지워지지 않았는지)를 지킨다.
    /// </summary>
    public class BossDefeatTests
    {
        private static void SetNarrativeField(object target, string name, object value)
        {
            System.Reflection.FieldInfo field = target.GetType().GetField(name,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, target.GetType().Name + "." + name);
            field.SetValue(target, value);
        }

        [Test]
        public void TimeLimit_FailsOnlyAfterTheLimit()
        {
            Assert.That(BossDefeatRules.Check("토벌대장", 149.9f, 150f, false, false, 0, 0), Is.Null);
            Assert.That(BossDefeatRules.Check("토벌대장", 150f, 150f, false, false, 0, 0), Does.Contain("150초"));
            Assert.That(BossDefeatRules.Check("토벌대장", 999f, 0f, false, false, 0, 0), Is.Null, "제한 시간이 0이면 시간으로는 지지 않습니다.");
        }

        [Test]
        public void TreasuryBreach_FailsOnlyWhenTheBossCares()
        {
            Assert.That(BossDefeatRules.Check("탐욕의 기사", 0f, 0f, true, true, 0, 0), Does.Contain("금고"));
            Assert.That(BossDefeatRules.Check("사냥꾼 두목", 0f, 0f, true, false, 0, 0), Is.Null);
        }

        [Test]
        public void UnitLosses_FailAtTheThreshold()
        {
            Assert.That(BossDefeatRules.Check("사냥꾼 두목", 0f, 0f, false, false, 2, 3), Is.Null);
            Assert.That(BossDefeatRules.Check("사냥꾼 두목", 0f, 0f, false, false, 3, 3), Does.Contain("부하 3명"));
        }

        [Test]
        public void Describe_ListsEveryCondition()
        {
            Assert.That(BossDefeatRules.Describe(180f, true, 0), Is.EqualTo("180초 안에 처치 · 금고 방 침입 시 패배"));
            Assert.That(BossDefeatRules.Describe(0f, false, 3), Is.EqualTo("부하 3명 쓰러지면 패배"));
            Assert.That(BossDefeatRules.Describe(0f, false, 0), Is.Empty);
            Assert.That(BossDefeatRules.DescribeProgress(30.2f, 150f, false, 0, 0), Is.EqualTo("남은 시간 120초"));
            Assert.That(BossDefeatRules.DescribeProgress(0f, 0f, true, 1, 3), Is.EqualTo("금고 방 침입 금지 · 부하 손실 1/3"));
        }

        [Test]
        public void EverySettlementDayBoss_HasADefeatCondition()
        {
            var config = AssetDatabase.LoadAssetAtPath<WaveConfigSO>("Assets/07.SO/WaveConfig.asset");
            Assert.That(config, Is.Not.Null, "WaveConfig.asset 을 찾지 못했습니다.");

            foreach (var day in new[] { 7, 14, 21, 28 })
            {
                var boss = config.GetBossForDay(day);
                Assert.That(boss, Is.Not.Null, $"{day}일 보스가 없습니다.");
                var hasCondition = boss.timeLimitSeconds > 0f || boss.loseOnTreasuryBreach || boss.loseAfterUnitsDowned > 0;
                Assert.That(hasCondition, Is.True, $"{day}일 보스({boss.title})에 패배 조건이 없습니다.");
            }
        }

        [Test]
        public void Preview_EndsWithTheChurchRule()
        {
            Assert.That(BossDefeatRules.DescribePreview(150f, false, 0, false), Is.EqualTo("150초 안에 처치 · 교회 부활 불가"));
            Assert.That(BossDefeatRules.DescribePreview(0f, true, 0, true), Is.EqualTo("금고 방 침입 시 패배 · 교회 부활 가능"));
        }

        /// <summary>
        /// 교회 부활 여부는 보스 성격대로 정했다(사용자 결정): 시간에 쫓기는 토벌대장·사냥꾼 두목은 불가,
        /// 돈으로 되살리는 탐욕의 기사와 교단이 뒤를 봐주는 기사단장은 가능. 값이 조용히 바뀌지 않게 지킨다.
        /// </summary>
        [Test]
        public void ChurchRevival_FollowsEachBossCharacter()
        {
            var config = AssetDatabase.LoadAssetAtPath<WaveConfigSO>("Assets/07.SO/WaveConfig.asset");
            Assert.That(config, Is.Not.Null, "WaveConfig.asset 을 찾지 못했습니다.");

            Assert.That(config.GetBossForDay(7).allowChurchRevival, Is.False, "7일 토벌대장");
            Assert.That(config.GetBossForDay(14).allowChurchRevival, Is.True, "14일 탐욕의 기사");
            Assert.That(config.GetBossForDay(21).allowChurchRevival, Is.False, "21일 사냥꾼 두목");
            Assert.That(config.GetBossForDay(28).allowChurchRevival, Is.True, "28일 왕국 기사단장");
        }

        /// <summary>
        /// 29일 이후 보스 날은 정의가 없어 가장 강한 일반 적을 키워 보냈고, 패배 조건·단계가 없었다
        /// (Docs/Design/RotatingBossDesign.md). 35일부터 순환 보스를 차례로 돌린다. 28일까지와 보스 날이 아닌 날은 손대지 않는다.
        /// </summary>
        [TestCase(28, false, 0, 0)]
        [TestCase(30, false, 0, 0)]
        [TestCase(36, false, 0, 0)]
        [TestCase(35, true, 0, 0)]
        [TestCase(42, true, 1, 0)]
        [TestCase(49, true, 2, 0)]
        [TestCase(56, true, 0, 1)]
        [TestCase(77, true, 0, 2)]
        public void RotatingBoss_PicksTheNextBossFromDay35(int day, bool expected, int index, int lap)
        {
            Assert.That(RotatingBossRules.TryGetSlot(day, 3, out var actualIndex, out var actualLap), Is.EqualTo(expected), $"{day}일");
            if (!expected)
                return;

            Assert.That(actualIndex, Is.EqualTo(index), $"{day}일 순서");
            Assert.That(actualLap, Is.EqualTo(lap), $"{day}일 바퀴");
        }

        [Test]
        public void RotatingBoss_EmptyRosterLeavesTheDayAlone()
        {
            Assert.That(RotatingBossRules.TryGetSlot(35, 0, out _, out _), Is.False);
        }

        /// <summary>한 바퀴 돌 때마다 체력 배율 +35%·공격 배율 +20%(2026-10-28 사용자 확정). 첫 바퀴는 정의 그대로.</summary>
        [Test]
        public void RotatingBoss_GrowsEachLap()
        {
            Assert.That(RotatingBossRules.HealthMultiplier(6f, 0), Is.EqualTo(6f).Within(0.0001f));
            Assert.That(RotatingBossRules.HealthMultiplier(6f, 1), Is.EqualTo(8.1f).Within(0.0001f));
            Assert.That(RotatingBossRules.HealthMultiplier(6f, 2), Is.EqualTo(10.2f).Within(0.0001f));
            Assert.That(RotatingBossRules.AttackMultiplier(2.5f, 0), Is.EqualTo(2.5f).Within(0.0001f));
            Assert.That(RotatingBossRules.AttackMultiplier(2.5f, 1), Is.EqualTo(3f).Within(0.0001f));
        }

        /// <summary>
        /// 날짜 정의가 없는 29일 이후 보스 날은 순환 목록에서 차례로 꺼내고, 바퀴 배율은 사본에만 곱한다.
        /// 목록 원본에 곱하면 에셋이 오염되고 같은 날을 다시 물을 때마다 배율이 커진다.
        /// </summary>
        [Test]
        public void RotatingBoss_FillsBossDaysAfter28WithoutTouchingTheRoster()
        {
            var config = UnityEngine.ScriptableObject.CreateInstance<WaveConfigSO>();
            try
            {
                var roster = new[]
                {
                    new WaveConfigSO.BossEntry { title = "A", healthMultiplier = 6f, attackMultiplier = 2.5f },
                    new WaveConfigSO.BossEntry { title = "B", healthMultiplier = 7.5f, attackMultiplier = 2.2f },
                    new WaveConfigSO.BossEntry { title = "C", healthMultiplier = 5f, attackMultiplier = 2.5f }
                };
                SetNarrativeField(config, "rotatingBosses", roster);
                SetNarrativeField(config, "bossEntries", new[] { new WaveConfigSO.BossEntry { targetDay = 63, title = "날짜 정의" } });

                Assert.That(config.GetBossForDay(28), Is.Null, "28일 이하는 순환하지 않는다.");
                Assert.That(config.GetBossForDay(36), Is.Null, "보스 날이 아닌 날.");
                Assert.That(config.GetBossForDay(35).title, Is.EqualTo("A"));
                Assert.That(config.GetBossForDay(42).title, Is.EqualTo("B"));
                Assert.That(config.GetBossForDay(49).title, Is.EqualTo("C"));
                Assert.That(config.GetBossForDay(63).title, Is.EqualTo("날짜 정의"), "날짜 정의가 순환보다 먼저다.");

                var secondLap = config.GetBossForDay(56);
                Assert.That(secondLap.title, Is.EqualTo("A"));
                Assert.That(secondLap.targetDay, Is.EqualTo(56));
                Assert.That(secondLap.healthMultiplier, Is.EqualTo(8.1f).Within(0.0001f));
                Assert.That(secondLap.attackMultiplier, Is.EqualTo(3f).Within(0.0001f));
                Assert.That(config.GetBossForDay(56).healthMultiplier, Is.EqualTo(8.1f).Within(0.0001f), "다시 물어도 같은 배율.");
                Assert.That(roster[0].healthMultiplier, Is.EqualTo(6f), "목록 원본은 그대로다.");
                Assert.That(config.IsBossDay(56), Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(config);
            }
        }

        /// <summary>
        /// 실제 WaveConfig의 순환 보스 셋: 순서·패배 조건·증원·교회 부활. 기마 두목은 금고 침입에 시간 제한을 함께 둔다 —
        /// 금고가 없는 판에서 14일 탐욕의 기사처럼 패배 조건이 사라졌기 때문이다(2026-10-28 실측).
        /// </summary>
        [Test]
        public void RotatingBossDays_UseTheRosterWithDefeatConditions()
        {
            var config = AssetDatabase.LoadAssetAtPath<WaveConfigSO>("Assets/07.SO/WaveConfig.asset");
            Assert.That(config, Is.Not.Null, "WaveConfig.asset 을 찾지 못했습니다.");

            foreach (var (day, boss) in new[] { (35, "Boss_Archmage"), (42, "Boss_OrcWarchief"), (49, "Boss_BanditRider"), (56, "Boss_Archmage") })
            {
                var entry = config.GetBossForDay(day);
                Assert.That(entry, Is.Not.Null, $"{day}일 순환 보스가 없습니다.");
                Assert.That(config.GetBossPartyForDay(day).Members[0].name, Is.EqualTo(boss), $"{day}일");
                Assert.That(entry.timeLimitSeconds > 0f || entry.loseOnTreasuryBreach || entry.loseAfterUnitsDowned > 0,
                    Is.True, $"{day}일 보스({entry.title})에 패배 조건이 없습니다.");
                Assert.That(entry.title, Is.Not.Empty, $"{day}일 배너 제목");
            }

            var archmage = config.GetBossForDay(35);
            Assert.That(archmage.timeLimitSeconds, Is.EqualTo(180f));
            Assert.That(archmage.enableFinalPhase, Is.True);
            Assert.That(archmage.allowChurchRevival, Is.False);

            var warchief = config.GetBossForDay(42);
            Assert.That(warchief.loseAfterUnitsDowned, Is.EqualTo(4));
            Assert.That(warchief.enableReinforcementPhase, Is.True);
            Assert.That(warchief.reinforcementParty.name, Is.EqualTo("BossReinforcement_OrcWarriors"));
            Assert.That(warchief.allowChurchRevival, Is.True);

            var rider = config.GetBossForDay(49);
            Assert.That(rider.loseOnTreasuryBreach, Is.True);
            Assert.That(rider.timeLimitSeconds, Is.GreaterThan(0f), "금고가 없는 판의 대체 조건");
            Assert.That(rider.allowChurchRevival, Is.False);

            Assert.That(config.GetBossForDay(56).healthMultiplier, Is.EqualTo(archmage.healthMultiplier * 1.35f).Within(0.001f));
        }

        /// <summary>
        /// 순환 보스 셋은 "무력화하는 배치"가 서로 달라야 한다: 대마법사는 함정 해체(기동성 0으로 함정을 더 자주 밟음),
        /// 오크 대족장은 전열, 기마 두목은 빠른 돌파(기동성 9). 파티 첫 멤버가 보스이고, 증원은 오크 전사 셋이다.
        /// </summary>
        [Test]
        public void RotatingBossRoster_KeepsEachBossIdentity()
        {
            const string enemies = "Assets/07.SO/Enemies/Bosses/";
            const string parties = "Assets/07.SO/Waves/Bosses/";
            var archmage = AssetDatabase.LoadAssetAtPath<Code.Enemies.EnemyDataSO>(enemies + "Boss_Archmage.asset");
            var warchief = AssetDatabase.LoadAssetAtPath<Code.Enemies.EnemyDataSO>(enemies + "Boss_OrcWarchief.asset");
            var rider = AssetDatabase.LoadAssetAtPath<Code.Enemies.EnemyDataSO>(enemies + "Boss_BanditRider.asset");
            Assert.That(archmage, Is.Not.Null, "Boss_Archmage 를 찾지 못했습니다.");
            Assert.That(warchief, Is.Not.Null, "Boss_OrcWarchief 를 찾지 못했습니다.");
            Assert.That(rider, Is.Not.Null, "Boss_BanditRider 를 찾지 못했습니다.");

            Assert.That(archmage.Role, Is.EqualTo(Code.BT.BattleRole.Ranged));
            Assert.That(archmage.DisarmsTraps, Is.True, "대마법사는 밟는 함정을 해체한다.");
            Assert.That(archmage.Mobility, Is.EqualTo(0));
            Assert.That(warchief.Role, Is.EqualTo(Code.BT.BattleRole.Tank));
            Assert.That(rider.Role, Is.EqualTo(Code.BT.BattleRole.Melee));
            Assert.That(rider.Mobility, Is.EqualTo(9));

            foreach (var (boss, party, escorts) in new[]
                     {
                         (archmage, "BossParty_Rotation_Archmage", 2),
                         (warchief, "BossParty_Rotation_OrcWarchief", 3),
                         (rider, "BossParty_Rotation_BanditRider", 3)
                     })
            {
                Assert.That(boss.AnimationSet, Is.Not.Null, $"{boss.name} 애니메이션 묶음");
                Assert.That(boss.Prefab, Is.Not.Null, $"{boss.name} 프리팹");
                var loaded = AssetDatabase.LoadAssetAtPath<AdventurerPartySO>(parties + party + ".asset");
                Assert.That(loaded, Is.Not.Null, $"{party} 를 찾지 못했습니다.");
                Assert.That(loaded.Members[0], Is.SameAs(boss), $"{party} 첫 멤버가 보스여야 합니다.");
                Assert.That(loaded.Members.Length, Is.EqualTo(1 + escorts), $"{party} 호위 수");
            }

            var reinforcement = AssetDatabase.LoadAssetAtPath<AdventurerPartySO>(parties + "BossReinforcement_OrcWarriors.asset");
            Assert.That(reinforcement, Is.Not.Null, "BossReinforcement_OrcWarriors 를 찾지 못했습니다.");
            Assert.That(reinforcement.Members, Has.Length.EqualTo(3).And.All.Property("name").EqualTo("OrcWarriorEnemy"));
        }
    }
}
