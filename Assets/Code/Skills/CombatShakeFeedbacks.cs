using Code.Combat;
using MoreMountains.Feedbacks;
using UnityEngine;

namespace Code.Skills
{
    /// <summary>
    /// 권능 시전의 화면 단위 반응(카메라 흔들림 · 순간 정지).
    /// 전투원마다 붙는 <see cref="FeelCombatFeedbacks"/>와 달리 시전자는 화면 밖에 있으므로
    /// 씬에 하나만 두고 세기만 바꿔 재생한다.
    ///
    /// 가벼운 권능과 무거운 권능을 다른 플레이어로 나눈다. MMF_Player는 재생 시점에
    /// 세기만 넘길 수 있고 피드백 구성은 못 바꾸는데, 회복이나 보조까지 화면을 멈추면
    /// 무슨 일이 났는지 구분이 안 되기 때문이다.
    /// </summary>
    public static class CombatShakeFeedbacks
    {
        /// <summary>이 세기 이상이면 화면을 잠깐 멈춘다. 낙반이나 범람 같은 한 방짜리만 해당한다.</summary>
        private const float HitStopThreshold = 0.6f;

        private const float ShakeDuration = 0.22f;
        private const float ShakeFrequency = 22f;
        private const float ShakeAmplitude = 0.26f;
        private const float HitStopDuration = 0.05f;

        private static MMF_Player lightPlayer;
        private static MMF_Player heavyPlayer;

        /// <param name="strength">0이면 흔들지 않는다. 호출부가 정하는 흔들림 세기다.</param>
        public static void Play(Vector3 position, float strength)
        {
            strength = Mathf.Clamp01(strength);
            if (strength <= 0f)
                return;

            var player = strength >= HitStopThreshold
                ? heavyPlayer != null ? heavyPlayer : heavyPlayer = Build("Dungeon Power Feedbacks (Heavy)", true)
                : lightPlayer != null ? lightPlayer : lightPlayer = Build("Dungeon Power Feedbacks (Light)", false);

            player?.PlayFeedbacks(position, strength);
        }

        private static MMF_Player Build(string playerName, bool withHitStop)
        {
            FeelCombatSceneSetup.EnsureCameraShaker();

            var host = new GameObject(playerName);
            var player = host.AddComponent<MMF_Player>();
            player.InitializationMode = MMFeedbacks.InitializationModes.Script;
            player.AutoPlayOnStart = false;
            player.AutoPlayOnEnable = false;

            var shake = player.AddFeedback(typeof(MMF_CameraShake)) as MMF_CameraShake;
            if (shake != null)
            {
                shake.CameraShakeProperties = new MMCameraShakeProperties(
                    ShakeDuration, 0f, ShakeFrequency, ShakeAmplitude, ShakeAmplitude, 0f);
                shake.Timing.TimescaleMode = TimescaleModes.Unscaled;
            }
            else
            {
                Debug.LogWarning($"{nameof(CombatShakeFeedbacks)}가 {nameof(MMF_CameraShake)}를 만들지 못했습니다. 시전이 화면을 흔들지 않습니다.");
            }

            if (withHitStop)
            {
                var hitStop = player.AddFeedback(typeof(MMF_HitStop)) as MMF_HitStop;
                if (hitStop != null)
                {
                    hitStop.Duration = HitStopDuration;
                    // 권능은 플레이어가 직접 누른 결과라 드물다. 전투 타격 쿨다운에 먹히면 안 된다.
                    hitStop.IgnoreGlobalCooldown = true;
                    hitStop.Timing.TimescaleMode = TimescaleModes.Unscaled;
                }
            }

            player.Initialization();
            return player;
        }
    }
}
