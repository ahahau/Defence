using System;
using UnityEngine;

namespace _01.Code.Units
{
    public enum InjurySeverity
    {
        None,
        Minor,
        Severe
    }

    public enum UnitTrait
    {
        None,
        Aggressive,
        Guardian,
        Cautious,
        Tireless,
        FieldMedic
    }

    public enum UnitPersonality
    {
        None,
        Calm,
        HotBlooded,
        Timid,
        Perfectionist,
        Sociable
    }

    public enum UnitCommand
    {
        Standby,
        Guard,
        Assault,
        Rest
    }

    public static class UnitTraitUtility
    {
        public static string GetLabel(UnitTrait trait) => trait switch
        {
            UnitTrait.Aggressive => "공격적",
            UnitTrait.Guardian => "수호자",
            UnitTrait.Cautious => "신중함",
            UnitTrait.Tireless => "강인함",
            UnitTrait.FieldMedic => "야전 의무병",
            _ => "특성 없음"
        };

        public static string GetDescription(UnitTrait trait) => trait switch
        {
            UnitTrait.Aggressive => "공격력이 높지만 피로가 빠르게 누적됩니다.",
            UnitTrait.Guardian => "방어력이 높지만 공격력이 낮습니다.",
            UnitTrait.Cautious => "회피율이 높지만 공격 속도가 느립니다.",
            UnitTrait.Tireless => "피로가 천천히 누적되지만 공격력이 조금 낮습니다.",
            UnitTrait.FieldMedic => "웨이브 종료 시 자신의 체력을 일부 회복합니다.",
            _ => "전투에 영향을 주는 개인 특성이 없습니다."
        };
    }

    public static class UnitPersonalityUtility
    {
        public static string GetLabel(UnitPersonality personality) => personality switch
        {
            UnitPersonality.Calm => "침착함",
            UnitPersonality.HotBlooded => "열혈",
            UnitPersonality.Timid => "겁이 많음",
            UnitPersonality.Perfectionist => "완벽주의",
            UnitPersonality.Sociable => "사교적",
            _ => "성격 없음"
        };

        public static string GetDescription(UnitPersonality personality) => personality switch
        {
            UnitPersonality.Calm => "피로가 천천히 누적됩니다.",
            UnitPersonality.HotBlooded => "공격력이 높지만 피로가 빠르게 누적됩니다.",
            UnitPersonality.Timid => "공격력이 낮지만 회피율이 높습니다.",
            UnitPersonality.Perfectionist => "치명타 확률이 높지만 공격 준비가 조금 느립니다.",
            UnitPersonality.Sociable => "같은 노드에 동료가 있으면 웨이브 피로가 감소합니다.",
            _ => "전투 태도에 영향을 주는 성격이 없습니다."
        };
    }

    public static class UnitCommandUtility
    {
        public static string GetLabel(UnitCommand command) => command switch
        {
            UnitCommand.Guard => "경계",
            UnitCommand.Assault => "공격",
            UnitCommand.Rest => "휴식",
            _ => "대기"
        };

        public static string GetDescription(UnitCommand command) => command switch
        {
            UnitCommand.Guard => "방어를 우선해 버티지만 공격 속도가 조금 느려집니다.",
            UnitCommand.Assault => "공격을 우선해 피해량과 속도가 오르지만 피로가 더 쌓입니다.",
            UnitCommand.Rest => "피로 누적을 줄이고 회복을 우선하지만 전투 성능이 크게 낮아집니다.",
            _ => "기본 행동입니다. 별도 보정 없이 노드 안에서 대기합니다."
        };
    }

    [Serializable]
    public struct UnitConditionState
    {
        [SerializeField, Range(0f, 100f)] private float fatigue;
        [SerializeField] private InjurySeverity injury;
        [SerializeField, Range(0f, 1f)] private float healthRatio;
        [SerializeField] private UnitTrait trait;
        [SerializeField] private UnitPersonality personality;
        [SerializeField] private UnitCommand command;

        public UnitConditionState(
            float fatigue,
            InjurySeverity injury,
            float healthRatio = 1f,
            UnitTrait trait = UnitTrait.None,
            UnitPersonality personality = UnitPersonality.None,
            UnitCommand command = UnitCommand.Standby)
        {
            this.fatigue = Mathf.Clamp(fatigue, 0f, 100f);
            this.injury = injury;
            this.healthRatio = Mathf.Clamp01(healthRatio);
            this.trait = trait;
            this.personality = personality;
            this.command = command;
        }

        public float Fatigue => fatigue;
        public InjurySeverity Injury => injury;
        public float HealthRatio => healthRatio;
        public UnitTrait Trait => trait;
        public UnitPersonality Personality => personality;
        public UnitCommand Command => command;
        public string TraitLabel => UnitTraitUtility.GetLabel(trait);
        public string PersonalityLabel => UnitPersonalityUtility.GetLabel(personality);
        public string Summary => $"{TraitLabel} · {PersonalityLabel} · 피로 {Mathf.RoundToInt(fatigue)}/100";
        public bool IsExhausted => fatigue >= 100f;
        public static UnitConditionState Fresh => new(0f, InjurySeverity.None);

        public UnitConditionState Rest(float fatigueRecovery, float healthRecoveryRatio)
        {
            var recoveredInjury = injury switch
            {
                InjurySeverity.Severe => InjurySeverity.Minor,
                InjurySeverity.Minor => InjurySeverity.None,
                _ => InjurySeverity.None
            };

            return new UnitConditionState(
                fatigue - Mathf.Max(0f, fatigueRecovery),
                recoveredInjury,
                healthRatio + Mathf.Max(0f, healthRecoveryRatio),
                trait,
                personality,
                command);
        }
    }
}
