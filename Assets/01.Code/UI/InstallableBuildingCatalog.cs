using System.Collections.Generic;
using _01.Code.Buildings;
using _01.Code.Progression;
using UnityEngine;

namespace _01.Code.UI
{
    /// <summary>
    /// 설치 메뉴에 올릴 수 있는 건물 목록과 해금 상태를 들고 있는 장부.
    /// 화면 갱신은 하지 않는다. 노드 패널이 목록을 물어보고 스스로 다시 그린다.
    /// </summary>
    public sealed class InstallableBuildingCatalog
    {
        private const string TreasuryResourcePath = "Buildings/TreasuryBuildingData";

        private readonly List<BuildingDataSO> _unlocked = new();
        private IReadOnlyList<BuildingDataSO> _installable;
        private BuildingDataSO _treasuryData;
        private bool _hasLoadedTreasuryData;

        public IReadOnlyList<BuildingDataSO> Unlocked => _unlocked;

        /// <summary>
        /// 시작 해금 상태를 정한다. 해금 카탈로그가 있으면 그 기준을 따르고,
        /// 없으면 설치 가능 목록 전체를 열어 둔다.
        /// </summary>
        public void Initialize(IReadOnlyList<BuildingDataSO> installableBuildings, DungeonUnlockCatalogSO unlockCatalog)
        {
            _installable = installableBuildings;
            _unlocked.Clear();

            if (_installable == null)
                return;

            if (unlockCatalog != null)
            {
                foreach (var entry in unlockCatalog.Entries)
                {
                    if (entry?.Building != null && entry.StartsUnlocked)
                        AddUnlocked(entry.Building);
                }

                return;
            }

            foreach (var buildingData in _installable)
                AddUnlocked(buildingData);
        }

        /// <summary>새로 해금한다. 이미 열려 있으면 false.</summary>
        public bool TryUnlock(BuildingDataSO buildingData)
        {
            return buildingData != null && AddUnlocked(buildingData);
        }

        /// <summary>바깥에서 통보받은 해금 목록으로 통째로 맞춘다.</summary>
        public void ReplaceUnlocked(IEnumerable<BuildingDataSO> buildings)
        {
            _unlocked.Clear();
            if (buildings == null)
                return;

            foreach (var building in buildings)
                AddUnlocked(building);
        }

        /// <summary>설치 메뉴에 후보로 올릴 건물 전부. 중복은 걸러서 한 번씩만 나온다.</summary>
        public IEnumerable<BuildingDataSO> EnumerateOptions()
        {
            var yielded = new HashSet<BuildingDataSO>();

            if (_installable != null)
            {
                foreach (var buildingData in _installable)
                {
                    if (buildingData != null && yielded.Add(buildingData))
                        yield return buildingData;
                }
            }

            foreach (var buildingData in _unlocked)
            {
                if (buildingData != null && yielded.Add(buildingData))
                    yield return buildingData;
            }

            // 금고는 인스펙터 목록에 없어도 항상 지을 수 있어야 해서 리소스에서 직접 가져온다.
            var treasuryData = ResolveTreasuryData();
            if (treasuryData != null && yielded.Add(treasuryData))
                yield return treasuryData;
        }

        private BuildingDataSO ResolveTreasuryData()
        {
            if (_hasLoadedTreasuryData)
                return _treasuryData;

            _hasLoadedTreasuryData = true;
            _treasuryData = Resources.Load<BuildingDataSO>(TreasuryResourcePath);
            return _treasuryData;
        }

        private bool AddUnlocked(BuildingDataSO buildingData)
        {
            if (buildingData == null || _unlocked.Contains(buildingData))
                return false;

            _unlocked.Add(buildingData);
            return true;
        }
    }
}
