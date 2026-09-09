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

        /// <summary>
        /// 지금 아무도 쓰지 않는다. 물고 있는 소리가 Fire Spell_Sorcerer 라서 사제가 쓰면
        /// 성직자가 불 마법을 쓰는 것처럼 들렸고, 사제 둘을 SkillCast 로 옮기면서 비었다.
        /// 값을 지우면 에셋에 저장된 열거 번호가 밀리므로 자리는 남겨 둔다.
        /// 다시 쓰려면 카탈로그의 소리부터 마법사용으로 바꿔야 한다.
        /// </summary>
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
