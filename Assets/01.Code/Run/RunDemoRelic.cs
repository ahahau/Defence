using System;

namespace Code.Run
{
    /// <summary>시연 런 유물(2026-11-11 사용자 결정, 시연 런 기획 "지도와 경제"). 런 동안 유지한다. 저장 값이므로 번호를 바꾸지 않는다.</summary>
    [Flags]
    public enum RunDemoRelic
    {
        None = 0,

        /// <summary>빠른 태엽: 함정 충전 4초 → 3초.</summary>
        QuickTrap = 1,

        /// <summary>두꺼운 뼈: 모든 부하 최대 체력 +4.</summary>
        ThickBones = 2,

        /// <summary>마력 결정: 마력 용량 +1(상인의 용량 구매와 따로 쌓인다).</summary>
        ManaCrystal = 4,

        /// <summary>무거운 자물쇠: 도적이 한 번에 들 수 있는 금화 절반.</summary>
        HeavyLock = 8,

        All = QuickTrap | ThickBones | ManaCrystal | HeavyLock,
    }
}
