using DG.Tweening;
using UnityEngine;

namespace Code.Skills
{
    /// <summary>
    /// 시전 자리에서 퍼져 나가는 고리 하나.
    ///
    /// 지금까지 스킬 연출은 시전자 몸에 색을 입히는 섬광뿐이라, 스물여덟 가지 기술이 전부
    /// 같아 보였다. 플레이어가 스킬을 직접 고르는 게 아니라 목록을 볼 일이 없으므로,
    /// 아이콘을 스물여덟 장 그리는 대신 <b>판 위에서 무슨 일이 일어났는지</b>로 구분한다.
    ///
    /// 색은 <see cref="SkillDataSO.CastColor"/>가 효과 조각에서 뽑아 준다 — 회복은 초록,
    /// 독 지대는 그 지대의 색, 낙하물은 터질 때의 색으로 저절로 갈린다.
    ///
    /// 고리는 <see cref="SkillZoneVisual.CircleSprite"/>를 다시 쓴다. 원 텍스처를 한 장 더
    /// 만들 이유가 없고, 지대와 같은 원이라 두 연출이 한 세트로 읽힌다.
    /// </summary>
    public static class SkillCastBurst
    {
        private const float StartRadius = 0.18f;
        private const float EndRadius = 1.15f;
        private const float Duration = 0.34f;
        private const float StartAlpha = 0.55f;

        /// <summary>궁극기는 이 배수만큼 크게 퍼지고 화면도 함께 흔든다.</summary>
        private const float UltimateScale = 1.9f;

        /// <summary>궁극기 한 방에 실어 보낼 화면 흔들림 세기. 권능 쪽과 같은 자를 쓴다.</summary>
        private const float UltimateShakeStrength = 0.7f;

        public static void Play(Vector3 position, Color color, bool isUltimate)
        {
            var scale = isUltimate ? UltimateScale : 1f;
            var renderer = SkillZoneVisual.CreateZone(
                "Skill Cast Burst", position, StartRadius * scale, WithAlpha(color, StartAlpha), 8);
            if (renderer == null)
                return;

            var ring = renderer.transform;
            var end = Vector3.one * (EndRadius * scale * 2f);

            // 커지면서 사라진다. 두 트윈을 한 시퀀스에 묶어야 중간에 끊겨도 함께 정리된다.
            DOTween.Sequence()
                // 웨이브가 끝나거나 씬이 바뀌면 고리도 함께 사라진다. 묶어 두지 않으면
                // 그 뒤에도 트윈이 돌며 없어진 Transform 을 건드려 예외가 난다.
                .SetLink(renderer.gameObject)
                .Append(ring.DOScale(end, Duration).SetEase(Ease.OutCubic))
                .Join(renderer.DOFade(0f, Duration).SetEase(Ease.InQuad))
                .SetTarget(renderer)
                .OnComplete(() =>
                {
                    if (renderer != null)
                        Object.Destroy(renderer.gameObject);
                });

            if (isUltimate)
                CombatShakeFeedbacks.Play(position, UltimateShakeStrength);
        }

        private static Color WithAlpha(Color color, float alpha) => new(color.r, color.g, color.b, alpha);
    }
}
