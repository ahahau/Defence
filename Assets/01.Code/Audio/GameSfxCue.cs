namespace _01.Code.Audio
{
    public enum GameSfxCue
    {
        UiClick,
        UiConfirm,
        UiOpen,
        UiReward,
        BuildInstall,
        UnitPlace,
        WaveStart,
        WaveClear,
        Attack,
        AttackBow,
        AttackMagic,
        Hit,
        Dodge,
        Trap,
        SkillCast,
        Explosion,

        /// <summary>전투원이 쓰러질 때. 타격음과 겹치지 않게 별도로 둔다.</summary>
        Death,

        /// <summary>회복 권능처럼 아군에게 좋은 일이 일어날 때.</summary>
        Heal,

        /// <summary>돈이나 마력이 모자라 눌러도 안 될 때. 아무 반응이 없으면 버그로 읽힌다.</summary>
        UiFail,

        /// <summary>패널을 닫을 때. 여는 소리와 달라야 방향이 느껴진다.</summary>
        UiClose,

        /// <summary>발톱과 이빨. 슬라임·거미·늑대·박쥐에 쇠붙이 소리는 어울리지 않는다.</summary>
        AttackClaw,
    }
}
