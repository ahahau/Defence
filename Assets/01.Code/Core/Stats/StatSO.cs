using System;
using System.Collections.Generic;
using UnityEngine;

namespace _01.Code.Core.Stats
{
    /// <summary>
    /// 기본값 하나와 출처별 가감치로 이루어진 스탯.
    ///
    /// 여태 Defence는 최대 체력·공격력을 SetMaxHealth·SetAttackDamage로 "덮어썼다".
    /// 그래서 일차 배율을 얹은 뒤 데이터를 한 번 더 바르면 배율이 통째로 지워졌고,
    /// 보스가 커 보이기만 하고 잡몹과 똑같이 약한 버그가 오래 숨어 있었다.
    /// 가감치를 출처(key)별로 따로 들고 있으면 덮어쓸 일이 없다 —
    /// 일차 배율은 일차 키로, 아티팩트는 아티팩트 키로 붙고 각자 떼어진다.
    /// </summary>
    [CreateAssetMenu(menuName = "SO/Stat/Stat", fileName = "Stat", order = 0)]
    public class StatSO : IndexedAsset, ICloneable
    {
        public delegate void ValueChangeHandler(StatSO stat, float currentValue, float previousValue);

        /// <summary>값이 실제로 달라졌을 때만 발생한다. 같은 값 재대입은 알리지 않는다.</summary>
        public event ValueChangeHandler ValueChanged;

        [field: SerializeField] public string StatName { get; private set; }
        [field: SerializeField] public string DisplayName { get; private set; }
        [field: SerializeField] public Sprite Icon { get; private set; }
        [field: SerializeField] public bool IsPercent { get; private set; }

        [SerializeField, TextArea] private string description;
        [SerializeField] private float baseValue;
        [SerializeField] private float minValue;
        [SerializeField] private float maxValue = 9999f;

        private readonly Dictionary<object, Modifier> _modifierByKey = new();
        private float _additiveTotal;
        private float _multiplierProduct = 1f;

        /// <summary>한 출처가 이 스탯에 미치는 영향. 더하는 몫과 곱하는 몫을 같이 들고 있다.</summary>
        private readonly struct Modifier
        {
            public readonly float Additive;
            public readonly float Multiplier;

            public Modifier(float additive, float multiplier)
            {
                Additive = additive;
                Multiplier = multiplier;
            }
        }

        public string Description => description;
        public float MinValue => minValue;
        public float MaxValue => maxValue;

        /// <summary>
        /// 기본값에 가감치를 더한 뒤 배율을 곱하고 상·하한으로 자른 최종값.
        ///
        /// 순서가 (기본+가감치)×배율인 이유는 기존 전투 계산이 그 순서였기 때문이다 —
        /// "(공격력 + 아티팩트 보너스) × 아티팩트 배율 × 컨디션 배율". 순서를 바꾸면
        /// 수치가 전부 미세하게 틀어진다.
        /// </summary>
        public float Value => Mathf.Clamp((baseValue + _additiveTotal) * _multiplierProduct, minValue, maxValue);

        public int IntValue => Mathf.RoundToInt(Value);

        public float BaseValue
        {
            get => baseValue;
            set
            {
                var previous = Value;
                baseValue = Mathf.Clamp(value, minValue, maxValue);
                RaiseIfChanged(Value, previous);
            }
        }

        /// <summary>같은 출처가 두 번 붙지 않는다. 중복 호출이 값을 두 배로 만들지 않게.</summary>
        public void AddModifier(object key, float value) => AddModifier(key, value, 1f);

        /// <summary>
        /// 곱하는 보정. 배율은 가감치로 옮겨 적을 수 없다 — 곱할 대상이
        /// 다른 출처가 붙고 떨어질 때마다 바뀌기 때문에, 환산해 두면 그 시점 값에 못이 박힌다.
        /// </summary>
        public void AddMultiplier(object key, float multiplier) => AddModifier(key, 0f, multiplier);

        public void AddModifier(object key, float additive, float multiplier)
        {
            if (key == null || _modifierByKey.ContainsKey(key))
                return;

            var previous = Value;
            _modifierByKey.Add(key, new Modifier(additive, multiplier));
            Recompute();
            RaiseIfChanged(Value, previous);
        }

        /// <summary>
        /// 이 출처의 보정을 최신값으로 갈아 끼운다. 피로·명령처럼 자주 다시 계산되는 쪽을 위한 창구다 —
        /// 뗐다 붙이면 그 사이에 값이 한 번 튀어 ValueChanged가 두 번 나간다.
        /// </summary>
        public void SetModifier(object key, float additive, float multiplier)
        {
            if (key == null)
                return;

            var previous = Value;
            _modifierByKey[key] = new Modifier(additive, multiplier);
            Recompute();
            RaiseIfChanged(Value, previous);
        }

        /// <summary>이 출처가 붙여 둔 것을 가감치·배율 가릴 것 없이 통째로 걷는다.</summary>
        public void RemoveModifier(object key)
        {
            if (key == null || !_modifierByKey.ContainsKey(key))
                return;

            var previous = Value;
            _modifierByKey.Remove(key);
            Recompute();
            RaiseIfChanged(Value, previous);
        }

        public bool HasModifier(object key) => key != null && _modifierByKey.ContainsKey(key);

        public void ClearModifiers()
        {
            if (_modifierByKey.Count == 0)
                return;

            var previous = Value;
            _modifierByKey.Clear();
            Recompute();
            RaiseIfChanged(Value, previous);
        }

        /// <summary>
        /// 총합을 통째로 다시 센다. 가감치는 ±로 누적해도 정확하지만 배율은 그렇지 않다 —
        /// 뗄 때 나누면 부동소수 오차가 남아, 붙였다 떼길 반복하면 1로 돌아오지 않는다.
        /// 출처가 많아야 대여섯이라 다시 세는 편이 싸고 정확하다.
        /// </summary>
        private void Recompute()
        {
            _additiveTotal = 0f;
            _multiplierProduct = 1f;

            foreach (var modifier in _modifierByKey.Values)
            {
                _additiveTotal += modifier.Additive;
                _multiplierProduct *= modifier.Multiplier;
            }
        }

        /// <summary>
        /// 인스턴스마다 자기 사본을 갖는다. 원본 자산에 직접 가감치를 붙이면
        /// 한 마리에게 건 효과가 같은 종류 전체에 걸리고, 플레이를 멈춰도 자산에 남는다.
        /// </summary>
        public virtual object Clone() => Instantiate(this);

        private void RaiseIfChanged(float current, float previous)
        {
            if (!Mathf.Approximately(current, previous))
                ValueChanged?.Invoke(this, current, previous);
        }
    }
}
