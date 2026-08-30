using System;
using System.Collections.Generic;
using _01.Code.Artifacts;
using _01.Code.Manager;
using _01.Code.UI;
using UnityEngine;

namespace _01.Code.Persistence.Agents
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
        [SerializeField, Tooltip("비우면 씬에서 찾는다.")] private MerchantPanelView merchant;
        [SerializeField, Tooltip("비우면 씬에서 찾는다.")] private ArtifactEffectController artifacts;

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
            merchant != null ? merchant : merchant = FindAnyObjectByType<MerchantPanelView>(FindObjectsInactive.Include);

        private ArtifactEffectController ResolveArtifacts() =>
            artifacts != null ? artifacts : artifacts = FindAnyObjectByType<ArtifactEffectController>(FindObjectsInactive.Include);
    }
}
