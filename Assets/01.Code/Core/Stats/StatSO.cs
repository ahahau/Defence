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

        private readonly Dictionary<object, float> _modifierByKey = new();
        private float _modifierTotal;

        public string Description => description;
        public float MinValue => minValue;
        public float MaxValue => maxValue;

        /// <summary>기본값에 모든 가감치를 더하고 상·하한으로 자른 최종값.</summary>
        public float Value => Mathf.Clamp(baseValue + _modifierTotal, minValue, maxValue);

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
        public void AddModifier(object key, float value)
        {
            if (key == null || _modifierByKey.ContainsKey(key))
                return;

            var previous = Value;
            _modifierByKey.Add(key, value);
            _modifierTotal += value;
            RaiseIfChanged(Value, previous);
        }

        public void RemoveModifier(object key)
        {
            if (key == null || !_modifierByKey.TryGetValue(key, out var value))
                return;

            var previous = Value;
            _modifierByKey.Remove(key);
            _modifierTotal -= value;
            RaiseIfChanged(Value, previous);
        }

        public bool HasModifier(object key) => key != null && _modifierByKey.ContainsKey(key);

        public void ClearModifiers()
        {
            if (_modifierByKey.Count == 0)
                return;

            var previous = Value;
            _modifierByKey.Clear();
            _modifierTotal = 0f;
            RaiseIfChanged(Value, previous);
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
