using System.Collections.Generic;
using UnityEngine;

namespace _01.Code.Combat
{
    public class CombatStatusController : MonoBehaviour
    {
        private readonly List<TimedModifier> _modifiers = new();

        public float MoveSpeedMultiplier
        {
            get
            {
                var value = 1f;
                for (var i = 0; i < _modifiers.Count; i++)
                    value *= _modifiers[i].MoveSpeedMultiplier;
                return Mathf.Clamp(value, 0.05f, 3f);
            }
        }

        public float DamageTakenMultiplier
        {
            get
            {
                var value = 1f;
                for (var i = 0; i < _modifiers.Count; i++)
                    value *= _modifiers[i].DamageTakenMultiplier;
                return Mathf.Clamp(value, 0.05f, 5f);
            }
        }

        public void Apply(string id, float duration, float moveSpeedMultiplier = 1f, float damageTakenMultiplier = 1f)
        {
            if (string.IsNullOrWhiteSpace(id) || duration <= 0f)
                return;

            var modifier = new TimedModifier(
                id,
                duration,
                Mathf.Max(0.05f, moveSpeedMultiplier),
                Mathf.Max(0.05f, damageTakenMultiplier));

            for (var i = 0; i < _modifiers.Count; i++)
            {
                if (_modifiers[i].Id != id)
                    continue;

                _modifiers[i] = modifier;
                return;
            }

            _modifiers.Add(modifier);
        }

        private void Update()
        {
            for (var i = _modifiers.Count - 1; i >= 0; i--)
            {
                var modifier = _modifiers[i];
                modifier.RemainingTime -= Time.deltaTime;
                if (modifier.RemainingTime <= 0f)
                    _modifiers.RemoveAt(i);
                else
                    _modifiers[i] = modifier;
            }
        }

        private struct TimedModifier
        {
            public readonly string Id;
            public readonly float MoveSpeedMultiplier;
            public readonly float DamageTakenMultiplier;
            public float RemainingTime;

            public TimedModifier(string id, float remainingTime, float moveSpeedMultiplier, float damageTakenMultiplier)
            {
                Id = id;
                RemainingTime = remainingTime;
                MoveSpeedMultiplier = moveSpeedMultiplier;
                DamageTakenMultiplier = damageTakenMultiplier;
            }
        }
    }
}
