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

        /// <summary>받은 전투 연출 표. 아직 못 받았으면 null(점멸·베기 섬광은 그때 건너뛴다).</summary>
        public static CombatFxCatalogSO Catalog => catalog;

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
            var frames = isIntruder ? config.IntruderDeathFrames : config.MinionDeathFrames;
            if (frames == null || frames.Length == 0)
                return;

            // 그림은 캐릭터 그림과 같은 PPU로 들어오므로 몸통 크기를 따라야 비율이 맞는다.
            // 혼불은 발밑 기준점이라 발에서 솟고, 폭발은 가운데 기준점이라 몸 높이로 올린다.
            var owner = health.GetComponentInParent<Combatant>();
            var render = owner != null
                ? owner.GetComponentInChildren<Code.Entities.EntityRender>(true)
                : health.GetComponentInChildren<Code.Entities.EntityRender>(true);
            var bodyScale = render != null ? Mathf.Abs(render.transform.lossyScale.y) : 1f;
            SpriteFramePlayer.Spawn(
                isIntruder ? "IntruderDeathFx" : "MinionDeathFx",
                frames,
                config.DeathFramesPerSecond,
                isIntruder ? position + Vector3.up * IntruderBurstHeight : position,
                bodyScale * (isIntruder ? config.IntruderDeathScale : config.MinionDeathScale),
                config.SortingOrder);
        }

        /// <summary>치명타가 들어간 자리에서 붉은 고리를 한 번 터뜨린다(<see cref="DamageFeedback"/>이 부른다).</summary>
        public static void PlayCriticalHit(Vector3 position)
        {
            var config = catalog;
            if (config == null)
                return;

            SpriteFramePlayer.Spawn("CriticalHitFx", config.CriticalHitFrames, config.HitFramesPerSecond, position, config.CriticalHitScale, config.SortingOrder + 2);
        }

        /// <summary>모험가 사망 폭발을 몸 가운데(발밑 기준점 위)에서 터뜨리는 높이.</summary>
        private const float IntruderBurstHeight = 0.35f;

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
