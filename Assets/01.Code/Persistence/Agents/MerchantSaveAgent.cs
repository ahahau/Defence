using System;
using System.Collections.Generic;
using Code.Artifacts;
using Code.Core;
using Code.Manager;
using Code.UI;
using UnityEngine;

namespace Code.Persistence.Agents
{
    [Serializable]
    public struct MerchantSaveState
    {
        public List<string> artifacts;
        public int purchaseCount;

        /// <summary>상인의 방문 주기를 되돌리려면 며칠차인지 알아야 한다.</summary>
        public int completedDay;
    }

    /// <summary>
    /// 상인과 손에 든 유물.
    ///
    /// 유물 구매 이력과 보유 목록이 한 덩어리인 이유는 값이 서로 물려 있기 때문이다 —
    /// 구매 횟수만 되돌아오고 유물이 안 오면 가격만 오른 채 물건이 없다.
    /// </summary>
    public sealed class MerchantSaveAgent : MonoBehaviour, ISaveable
    {
        [SerializeField] private string saveKey = "merchant.state";
        [SerializeField, Tooltip("씬의 상인. 비어 있으면 찾으며 경고한다(배선 뒤 폴백 제거).")] private MerchantPanelView merchant;
        [SerializeField, Tooltip("씬의 유물 효과. 비어 있으면 찾으며 경고한다(배선 뒤 폴백 제거).")] private ArtifactEffectController artifacts;

        public string SaveKey => saveKey;

        public string GetSaveData()
        {
            var panel = ResolveMerchant();
            var obtained = new List<string>();

            var controller = ResolveArtifacts();
            if (controller?.Inventory != null)
                foreach (var artifact in controller.Inventory.ObtainedArtifacts)
                    if (artifact != null)
                        obtained.Add(artifact.name);

            return JsonUtility.ToJson(new MerchantSaveState
            {
                artifacts = obtained,
                purchaseCount = panel != null ? panel.PurchaseCount : 0,
                completedDay = DayManager.Current != null ? DayManager.Current.CurrentDay : 0
            });
        }

        public void RestoreData(string savedData)
        {
            if (string.IsNullOrWhiteSpace(savedData))
                return;

            var state = JsonUtility.FromJson<MerchantSaveState>(savedData);
            ResolveMerchant()?.RestoreCheckpoint(
                state.artifacts ?? new List<string>(),
                state.purchaseCount,
                state.completedDay);
        }

        private MerchantPanelView ResolveMerchant() =>
            merchant != null ? merchant : merchant = SceneReferenceFallback.Find<MerchantPanelView>(this, nameof(merchant));

        private ArtifactEffectController ResolveArtifacts() =>
            artifacts != null ? artifacts : artifacts = SceneReferenceFallback.Find<ArtifactEffectController>(this, nameof(artifacts));
    }
}
