using Code.Buildings;

namespace Code.UI.RoomManagement
{
    /// <summary>설치 창의 건물·함정 후보 하나. 막힌 항목도 이유와 함께 내보낸다.</summary>
    public readonly struct InstallOption
    {
        public InstallOption(BuildingDataSO data, bool canInstall, string blockedReason)
        {
            Data = data;
            CanInstall = canInstall;
            BlockedReason = blockedReason ?? string.Empty;
        }

        public BuildingDataSO Data { get; }
        public bool CanInstall { get; }
        public string BlockedReason { get; }
    }
}
