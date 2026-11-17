namespace Code.Entities
{
    /// <summary>캐릭터 프레임 애니메이션 한 묶음 안의 동작.</summary>
    public enum CharacterClip
    {
        Idle,
        Walk,
        Attack,
        Defeated,
        /// <summary>둘째 공격 동작. 그림이 있는 캐릭터만 평타마다 <see cref="Attack"/>과 번갈아 튼다.</summary>
        AttackAlt,
        /// <summary>맞고 움찔하는 동작. 그림이 없는 캐릭터(모험가 팩)는 흰 점멸만 한다.</summary>
        Hurt
    }
}
