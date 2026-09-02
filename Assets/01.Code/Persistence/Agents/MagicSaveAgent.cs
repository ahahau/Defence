using System;
using _01.Code.Manager;
using UnityEngine;

namespace _01.Code.Persistence.Agents
{
    [Serializable]
    public struct MagicSaveState
    {
        public int maxMagic;
        public int usedMagic;
    }

    /// <summary>
    /// 주둔 마력. 상한과 사용량을 함께 담는다.
    ///
    /// 상한은 마을을 완전히 장악하면 오르므로 판마다 달라지고,
    /// 사용량은 복원이 유닛을 직접 세우느라 마력 시스템을 거치지 않아 저절로 돌아오지 않는다.
    /// 둘 중 하나만 담으면 불러온 판에서 마력이 어긋난다.
    /// </summary>
    public sealed class MagicSaveAgent : MonoBehaviour, ISaveable
    {
        [SerializeField] private string saveKey = "magic.state";
        [SerializeField, Tooltip("비우면 씬에서 찾는다.")] private MagicManager magicManager;

        public string SaveKey => saveKey;

        public string GetSaveData()
        {
            var magic = Resolve();
            if (magic == null)
                return string.Empty;

            return JsonUtility.ToJson(new MagicSaveState
            {
                maxMagic = magic.MaxMagic,
                usedMagic = magic.UsedMagic
            });
        }

        public void RestoreData(string savedData)
        {
            if (string.IsNullOrWhiteSpace(savedData))
                return;

            var magic = Resolve();
            if (magic == null)
                return;

            var state = JsonUtility.FromJson<MagicSaveState>(savedData);
            magic.RestoreState(state.maxMagic, state.usedMagic);
        }

        private MagicManager Resolve() =>
            magicManager != null ? magicManager : magicManager = FindAnyObjectByType<MagicManager>(FindObjectsInactive.Include);
    }
}
