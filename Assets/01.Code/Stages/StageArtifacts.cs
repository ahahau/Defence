using System;
using System.Collections.Generic;
using Code.Artifacts;
using Code.Combat;
using Code.Core;
using Code.Enemies;
using Code.Events;
using UnityEngine;

namespace Code.Stages
{
    /// <summary>
    /// 스테이지에 장착한 유물을 부하에게 건다. 능력치·조합은 배치/장착 변경 때, 조건부 피해는 공격할 때 계산한다.
    /// 스테이지에는 메인 유닛이 없어 유물·조합의 대상 구분 없이 모든 부하에게 적용한다(2026-10-29 사용자 결정).
    /// 피해 이벤트는 이 세션만 쓰는 채널로 받아 구 방 경로의 유물 처리와 섞이지 않게 한다.
    /// </summary>
    public sealed class StageArtifacts : IArtifactField, IDisposable
    {
        private readonly StageSession _session;
        private readonly GameEventChannelSO _channel;
        private readonly List<StageArtifact> _equipped = new();
        private readonly List<ArtifactDataSO> _active = new();
        private readonly List<EnemyMover> _noEnemies = new();
        public IReadOnlyList<StageArtifact> Equipped => _equipped;

        public StageArtifacts(StageSession session)
        {
            _session = session;
            _channel = ScriptableObject.CreateInstance<GameEventChannelSO>();
            _channel.hideFlags = HideFlags.HideAndDontSave;
            _channel.AddListener<CombatDamageCalculatedEvent>(HandleDamageCalculated);
        }

        public bool IsEquipped(StageArtifact artifact) => artifact != null && _equipped.Contains(artifact);

        internal void Equip(StageArtifact artifact)
        {
            if (artifact == null || artifact.Artifact == null || _equipped.Contains(artifact))
                return;
            _equipped.Add(artifact);
            _active.Add(artifact.Artifact);
            ApplyAll();
        }

        internal void Unequip(StageArtifact artifact)
        {
            int index = _equipped.IndexOf(artifact);
            if (index < 0)
                return;
            _equipped.RemoveAt(index);
            _active.RemoveAt(index);
            ApplyAll();
        }

        internal void Clear()
        {
            _equipped.Clear();
            _active.Clear();
        }

        /// <summary>둘 다 장착해서 성립한 조합. 화면 표시에 쓴다.</summary>
        public void CollectCombos(List<ArtifactCombo> target)
        {
            target.Clear();
            ArtifactComboCatalogSO catalog = _session.Catalog.ArtifactCombos;
            if (catalog == null || _active.Count < 2)
                return;
            foreach (ArtifactCombo combo in catalog.Combos)
                if (combo != null && combo.IsOwnedBy(_active))
                    target.Add(combo);
        }

        /// <summary>배치·복원된 부하에 능력치를 다시 얹고 피해 이벤트 채널을 건다.</summary>
        public void Apply(StageActor actor)
        {
            if (actor == null || actor.IsEnemy || actor.Unit == null)
                return;
            // 조건부 효과가 없으면 채널을 떼어 공격마다 피해 이벤트를 만들지 않는다.
            actor.Combat.SetArtifactEventChannel(HasDamageEffects() ? _channel : null);
            actor.Unit.ApplyArtifactBonus(Bonus(actor));
        }

        private bool HasDamageEffects()
        {
            foreach (ArtifactDataSO artifact in _active)
                if (artifact.Effects != null)
                    foreach (ArtifactEffectSO effect in artifact.Effects)
                        if (effect != null)
                            return true;
            return false;
        }

        private void ApplyAll()
        {
            if (_session.Board == null)
                return;
            foreach (StageActor actor in _session.Board.Actors)
                Apply(actor);
        }

        private ArtifactStatBonus Bonus(StageActor actor)
        {
            ArtifactStatBonus bonus = new(0, 1f, 0, 1f);
            foreach (ArtifactDataSO artifact in _active)
                bonus.Add(artifact.CalculateStatBonus(new ArtifactEffectContext(artifact, actor.Unit)));
            // 조합은 개별 유물을 다 더한 뒤에 얹는다(구 ArtifactInventorySO와 같은 순서).
            ArtifactComboCatalogSO catalog = _session.Catalog.ArtifactCombos;
            if (catalog != null && _active.Count >= 2)
                foreach (ArtifactCombo combo in catalog.Combos)
                    if (combo != null && combo.IsOwnedBy(_active))
                        bonus.Add(combo.StatBonus);
            return bonus;
        }

        private void HandleDamageCalculated(CombatDamageCalculatedEvent evt)
        {
            if (_active.Count == 0 || !TryFindUnit(evt.Attacker, out StageActor attacker))
                return;
            foreach (ArtifactDataSO artifact in _active)
            {
                ArtifactEffectContext context = new(artifact, attacker.Unit, null, evt.Attacker, evt.Target,
                    evt.Damage, _noEnemies, this);
                artifact.ApplyEffects(context);
                evt.Damage = context.Damage;
            }
        }

        public bool HasAllyNear(Combatant attacker)
        {
            if (_session.Board == null || _session.Definition == null)
                return false;
            float radius = _session.Catalog.LoneAllyRadius * _session.Definition.CellSize;
            float limit = radius * radius;
            Vector3 origin = attacker.transform.position;
            foreach (StageActor actor in _session.Board.Actors)
            {
                if (actor == null || actor.IsEnemy || !actor.IsAlive || actor.Combat == attacker)
                    continue;
                if (((Vector2)(actor.transform.position - origin)).sqrMagnitude <= limit)
                    return true;
            }
            return false;
        }

        private bool TryFindUnit(Combatant combatant, out StageActor found)
        {
            found = null;
            if (combatant == null || _session.Board == null)
                return false;
            foreach (StageActor actor in _session.Board.Actors)
                if (actor != null && !actor.IsEnemy && actor.Combat == combatant && actor.Unit != null)
                {
                    found = actor;
                    return true;
                }
            return false;
        }

        public void Dispose()
        {
            if (_channel == null)
                return;
            _channel.RemoveListener<CombatDamageCalculatedEvent>(HandleDamageCalculated);
            UnityEngine.Object.Destroy(_channel);
        }
    }
}
