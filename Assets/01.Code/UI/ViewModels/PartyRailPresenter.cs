using System.Collections.Generic;
using Code.Enemies;
using Code.Manager;

namespace Code.UI.Toolkit
{
    /// <summary>
    /// 던전 안 모험가를 파티별로 묶어 HUD 우측 목록 줄로 만든다. 목록에는 이름과 위험도만 싣고,
    /// 자세한 것은 줄을 눌러 리더 상태 패널에서 본다(기획서 "우측은 현재 파티 목록").
    /// </summary>
    public sealed class PartyRailPresenter
    {
        private sealed class PartyGroup
        {
            public int PartyId;
            public Enemy Leader;
            public Enemy FirstAlive;
            public int Alive;
            public int MaxTier;
            public bool HasBoss;
        }

        private readonly Dictionary<int, PartyGroup> _groups = new();
        private readonly List<PartyGroup> _ordered = new();
        private readonly Stack<PartyGroup> _pool = new();

        public void Build(WaveManager wave, int nextWaveDay, List<PartyRailRowViewModel> rows)
        {
            rows.Clear();
            if (wave == null || !wave.IsWaveRunning)
            {
                rows.Add(new PartyRailRowViewModel("들어온 파티 없음", "대기"));
                // 보스 날 준비 중에는 오늘 올 보스와 패배 조건을 계속 띄워 둔다.
                if (wave != null && wave.TryGetBossPreview(nextWaveDay, out var bossTitle, out var conditions))
                    rows.Add(new PartyRailRowViewModel($"오늘 보스 · {bossTitle}",
                        string.IsNullOrEmpty(conditions) ? "조건 없음" : conditions, null, true));
                return;
            }

            CollectParties(wave.ActiveEnemies);
            foreach (var group in _ordered)
                rows.Add(BuildPartyRow(group));

            if (_ordered.Count == 0)
                rows.Add(new PartyRailRowViewModel("던전 안 파티 없음", "대기"));

            var pending = wave.PendingSpawnCount;
            if (pending > 0)
                rows.Add(new PartyRailRowViewModel("다음 파티", $"접근 · {pending}명"));

            // 보스가 있는 동안은 남은 시간·부하 손실 같은 패배 조건 진행을 보여 준다.
            var bossProgress = wave.BossDefeatProgressText;
            if (!string.IsNullOrEmpty(bossProgress))
                rows.Add(new PartyRailRowViewModel("보스 패배 조건", bossProgress, null, true));
        }

        private void CollectParties(IReadOnlyList<Enemy> enemies)
        {
            foreach (var group in _ordered)
                _pool.Push(group);
            _groups.Clear();
            _ordered.Clear();

            if (enemies == null)
                return;

            foreach (var enemy in enemies)
            {
                if (enemy == null || !enemy.IsAlive)
                    continue;

                if (!_groups.TryGetValue(enemy.PartyId, out var group))
                {
                    group = _pool.Count > 0 ? _pool.Pop() : new PartyGroup();
                    group.PartyId = enemy.PartyId;
                    group.Leader = null;
                    group.FirstAlive = null;
                    group.Alive = 0;
                    group.MaxTier = 0;
                    group.HasBoss = false;
                    _groups[enemy.PartyId] = group;
                    _ordered.Add(group);
                }

                group.Alive++;
                group.FirstAlive ??= enemy;
                if (enemy.IsBoss)
                {
                    group.HasBoss = true;
                    group.Leader = enemy;
                }
                else if (group.Leader == null && enemy.IsPartyLeader)
                {
                    group.Leader = enemy;
                }

                if (enemy.StrengthTier > group.MaxTier)
                    group.MaxTier = enemy.StrengthTier;
            }

            // 보스 파티를 맨 위에, 나머지는 들어온 순서(파티 번호)대로.
            _ordered.Sort((a, b) => a.HasBoss != b.HasBoss ? (a.HasBoss ? -1 : 1) : a.PartyId.CompareTo(b.PartyId));
        }

        private static PartyRailRowViewModel BuildPartyRow(PartyGroup group)
        {
            var leader = group.Leader != null ? group.Leader : group.FirstAlive;
            var coffins = PartyCoffin.CountFor(group.PartyId);
            var name = group.HasBoss
                ? $"보스 · {leader.DisplayName} · {group.Alive}명"
                : $"{leader.DisplayName} 파티 · {group.Alive}명";
            if (coffins > 0)
                name = $"{name} · 관 {coffins}";

            var isElite = group.MaxTier >= PauseLockRules.EliteStrengthTier;
            var strength = group.HasBoss ? "보스" : isElite ? $"엘리트 · 강함 {group.MaxTier}" : $"강함 {group.MaxTier}";
            var fund = PartyFundLedger.TierLabel(PartyFundLedger.Active.TierOf(group.PartyId));
            return new PartyRailRowViewModel(name, $"{strength} · 자금 {fund}", leader, group.HasBoss || isElite);
        }
    }
}
