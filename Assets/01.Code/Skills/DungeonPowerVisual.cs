using _01.Code.Core;
using DG.Tweening;
using UnityEngine;

namespace _01.Code.Skills
{
    /// <summary>
    /// 권능이 구역에 꽂히는 순간의 연출.
    /// 피해 숫자와 피격 모션은 <c>DamageFeedback</c>이 체력 이벤트를 듣고 알아서 띄우므로,
    /// 여기서는 '던전이 뭔가 했다'는 원인 쪽만 그린다. 그게 없으면 적이 저절로 죽는 것처럼 보인다.
    /// </summary>
    public static class DungeonPowerVisual
    {
        /// <summary>유닛 스프라이트(20) 위에 그린다. 아래로 두면 파티클이 전투원 뒤에 깔린다.</summary>
        private const int ParticleSortingOrder = 30;

        /// <summary>파티클 길이를 못 읽었을 때 쓰는 정리 시간. 남겨두면 씬에 계속 쌓인다.</summary>
        private const float FallbackLifetime = 2f;

        /// <summary>
        /// 권능 하나를 구역에 터뜨린다.
        /// 고유 파티클이 지정돼 있으면 그것을, 없으면 색만 다른 원형 파동을 쓴다.
        /// </summary>
        public static void Play(DungeonPowerSO power, Vector3 center, float radius)
        {
            if (power == null)
                return;

            if (power.ImpactEffect != null)
                SpawnEffect(power, center);
            else
                PlayBurst(center, power.FlashColor, radius, power.Damage > 0);

            DungeonPowerFeedbacks.Play(center, power.ShakeStrength);
        }

        private static void SpawnEffect(DungeonPowerSO power, Vector3 center)
        {
            var instance = Object.Instantiate(power.ImpactEffect, center, Quaternion.identity);
            instance.transform.localScale = Vector3.one * Mathf.Max(0.1f, power.EffectScale);

            var lifetime = 0f;
            foreach (var system in instance.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = system.main;
                // 루프가 걸린 프리팹을 그대로 두면 영원히 남는다.
                main.loop = false;
                lifetime = Mathf.Max(lifetime, main.duration + main.startLifetime.constantMax);
            }

            foreach (var renderer in instance.GetComponentsInChildren<ParticleSystemRenderer>(true))
            {
                renderer.sortingLayerName = "Default";
                renderer.sortingOrder = ParticleSortingOrder;
            }

            var root = instance.GetComponent<ParticleSystem>();
            if (root != null)
                root.Play(true);

            Object.Destroy(instance, lifetime > 0f ? lifetime : FallbackLifetime);
        }

        /// <summary>구역에 원형 파동을 터뜨린다. 퍼지면서 옅어진 뒤 스스로 사라진다.</summary>
        public static void PlayBurst(Vector3 center, Color color, float radius, bool shakeScreen)
        {
            var startColor = color;
            startColor.a = 0.55f;

            var zone = SkillZoneVisual.CreateZone("DungeonPowerBurst", center, Mathf.Max(0.5f, radius) * 0.35f, startColor, 8);
            if (zone == null)
                return;

            var root = zone.gameObject;
            var target = Vector3.one * (Mathf.Max(0.5f, radius) * 2f);
            var fade = startColor;
            fade.a = 0f;

            DOTween.Sequence().SetLink(root)
                .Append(root.transform.DOScale(target, 0.28f).SetEase(Ease.OutQuad))
                .Join(zone.DOColor(fade, 0.34f).SetEase(Ease.InQuad))
                .OnComplete(() =>
                {
                    if (root != null)
                        Object.Destroy(root);
                });

            // 때리는 권능만 화면을 흔든다. 회복까지 흔들면 무슨 일이 났는지 구분이 안 된다.
            if (shakeScreen)
                ScenePostProcessing.PulseVignette(0.12f, 0.35f);
        }
    }
}
