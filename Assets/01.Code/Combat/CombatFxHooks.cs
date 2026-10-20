using Code.Audio;
using UnityEngine;

namespace Code.Combat
{
    /// <summary>
    /// 전투의 소리와 사망 연출을 한 곳에서 건다.
    ///
    /// <see cref="Health.AnyDamaged"/>가 전역 방송이라 여기 한 번만 붙으면
    /// 프리팹 16종을 다시 배선하지 않고도 모든 전투원이 소리를 내고 사망 연출을 띄운다.
    /// 화면 흔들림과 스프라이트 점멸은 이미 <see cref="FeelCombatFeedbacks"/>가 맡고 있으므로
    /// 여기서는 건드리지 않는다.
    /// </summary>
    public static class CombatFxHooks
    {
        private const float FallbackLifetime = 2f;

        private static CombatFxCatalogSO catalog;
        private static bool missingCatalogWarned;
        private static bool subscribed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            catalog = null;
            missingCatalogWarned = false;
            subscribed = false;
        }

        /// <summary>씬의 <see cref="Code.Core.RuntimeAssetInstaller"/>가 전투 연출 표를 넘긴다.</summary>
        public static void Provide(CombatFxCatalogSO fxCatalog)
        {
            if (fxCatalog != null)
                catalog = fxCatalog;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (subscribed)
                return;

            subscribed = true;
            Health.AnyDamaged += HandleAnyDamaged;
        }

        private static void HandleAnyDamaged(Health health, int damage, bool isCritical)
        {
            if (health == null || damage <= 0)
                return;

            var position = health.transform.position;

            if (health.IsAlive)
            {
                GameSfxPlayer.Play(GameSfxCue.Hit, position);
                return;
            }

            GameSfxPlayer.Play(GameSfxCue.Death, position);
            SpawnDeathEffect(health, position);
        }

        private static void SpawnDeathEffect(Health health, Vector3 position)
        {
            var config = EnsureCatalog();
            if (config == null)
                return;

            // 침입자와 부하의 죽음이 같아 보이면 전황을 못 읽는다.
            var isIntruder = health.GetComponentInParent<Code.Enemies.Enemy>() != null;
            var prefab = isIntruder ? config.IntruderDeathEffect : config.MinionDeathEffect;
            if (prefab == null)
                return;

            var instance = Object.Instantiate(prefab, position, Quaternion.identity);
            instance.transform.localScale = Vector3.one * config.DeathEffectScale;

            // 파티클 팩이 물고 온 소리는 걷어낸다. 사망음은 위에서 이미 냈다.
            GameSfxPlayer.StripEmbeddedAudio(instance);

            var lifetime = 0f;
            foreach (var system in instance.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = system.main;
                main.loop = false;
                lifetime = Mathf.Max(lifetime, main.duration + main.startLifetime.constantMax);
            }

            foreach (var renderer in instance.GetComponentsInChildren<ParticleSystemRenderer>(true))
            {
                renderer.sortingLayerName = "Default";
                renderer.sortingOrder = config.SortingOrder;
            }

            var root = instance.GetComponent<ParticleSystem>();
            if (root != null)
                root.Play(true);

            Object.Destroy(instance, lifetime > 0f ? lifetime : FallbackLifetime);
        }

        private static CombatFxCatalogSO EnsureCatalog()
        {
            if (catalog == null && !missingCatalogWarned)
            {
                missingCatalogWarned = true;
                Debug.LogWarning("전투 연출 표를 받지 못했습니다. 씬의 RuntimeAssetInstaller를 확인하세요. 사망 연출이 나오지 않습니다.");
            }

            return catalog;
        }
    }
}
