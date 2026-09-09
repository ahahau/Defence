using _01.Code.Events;
using UnityEngine;

namespace _01.Code.Audio
{
    /// <summary>
    /// 게임에서 뭔가 일어났을 때 나는 UI 소리를 건다.
    /// 버튼 클릭은 <see cref="UiSfxInstaller"/>가, 전투 소리는 <c>CombatFxHooks</c>가 맡고,
    /// 여기서는 '돈이 들어왔다 · 건물이 섰다 · 부하를 놓았다'처럼 결과를 알리는 쪽만 본다.
    /// </summary>
    public static class UiSfxHooks
    {
        private const string BridgeResourcePath = "Audio/SfxEventBridge";

        private static bool subscribed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() => subscribed = false;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (subscribed)
                return;

            var bridge = Resources.Load<SfxEventBridgeSO>(BridgeResourcePath);
            if (bridge == null || bridge.Channels == null)
            {
                Debug.LogWarning($"효과음 이벤트 다리를 찾지 못했습니다: Resources/{BridgeResourcePath}. UI 소리 일부가 나지 않습니다.");
                return;
            }

            subscribed = true;

            // 어떤 이벤트가 어느 채널로 오는지 코드만 봐서는 확실치 않아 전부 건다.
            // 채널이 중복 구독을 막아주고, 같은 큐는 최소 간격이 있어 두 번 울리지 않는다.
            foreach (var channel in bridge.Channels)
            {
                if (channel == null)
                    continue;

                channel.AddListener<GoldEarnedEvent>(OnGoldEarned);
                channel.AddListener<BuildingInstalledEvent>(OnBuildingInstalled);
                channel.AddListener<UnitDeployMagicPaidEvent>(OnUnitDeployed);
                channel.AddListener<UnitDeployMagicRejectedEvent>(OnDeployRejected);
                channel.AddListener<RosterHirePaidEvent>(OnUnitHired);
            }
        }

        private static void OnGoldEarned(GoldEarnedEvent evt) => GameSfxPlayer.Play(GameSfxCue.UiReward);

        private static void OnBuildingInstalled(BuildingInstalledEvent evt) => GameSfxPlayer.Play(GameSfxCue.BuildInstall);

        private static void OnUnitDeployed(UnitDeployMagicPaidEvent evt) => GameSfxPlayer.Play(GameSfxCue.UnitPlace);

        private static void OnDeployRejected(UnitDeployMagicRejectedEvent evt) => GameSfxPlayer.Play(GameSfxCue.UiFail);

        /// <summary>
        /// 영입은 돈이 나가는 확정 동작인데 소리가 없었다. 표에 준비돼 있으면서 부르는 곳이
        /// 한 군데도 없던 큐가 UiConfirm 이라, 둘을 맞붙인다 — 둘러보는 클릭과 갈려야 한다.
        /// </summary>
        private static void OnUnitHired(RosterHirePaidEvent evt) => GameSfxPlayer.Play(GameSfxCue.UiConfirm);
    }
}
