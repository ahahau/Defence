using UnityEngine;

namespace Code.Manager
{
    /// <summary>
    /// 하루 방문객이 들어오는 박자. 하루 인원은 등급이 정하고, 여기서는 파티 사이 간격과 밤 손님의 세기만 정한다.
    ///
    /// 기획서는 "파티가 확률적으로 하나씩, 낮은 촘촘히, 밤은 드물고 약하게"다. 인원을 확률로 바꾸면 하루 끝·정산·
    /// 목표·보스 조건이 모두 흔들리므로, 인원은 그대로 두고 간격만 평균 주위로 들쭉날쭉하게 한다.
    /// </summary>
    public static class SpawnPacingRules
    {
        /// <summary>파티 간격 배율의 범위. 평균이 1이라 하루 전체 길이는 그대로다.</summary>
        public const float MinGapJitter = 0.5f;
        public const float MaxGapJitter = 1.5f;

        /// <summary>밤에 들어오는 모험가가 낮보다 낮은 레벨.</summary>
        public const int NightLevelPenalty = 1;

        /// <summary>0 이상 1 미만의 난수를 간격 배율로.</summary>
        public static float GapJitter(double roll) =>
            Mathf.Lerp(MinGapJitter, MaxGapJitter, Mathf.Clamp01((float)roll));

        /// <summary>
        /// 다음 파티까지의 간격. 그룹 스폰이면 기본 간격에 실제로 내보낸 인원을 곱한 값이 기준이다 —
        /// 한 파티가 한 사람 몫의 시간에 통째로 들어오면 하루 방문이 그룹 크기만큼 빨라진다.
        /// </summary>
        public static float GapSeconds(float baseGap, float jitter, bool isNight, float nightMultiplier) =>
            Mathf.Max(0f, baseGap) * Mathf.Max(0f, jitter) * (isNight ? Mathf.Max(1f, nightMultiplier) : 1f);

        /// <summary>밤 손님은 같은 종류의 약한 파티다. 보스는 예외, 레벨은 1 아래로 내려가지 않는다.</summary>
        public static int VisitorLevel(int baseLevel, bool isNight, bool isBoss) =>
            isBoss || !isNight ? baseLevel : Mathf.Max(1, baseLevel - NightLevelPenalty);
    }
}
