using System;
using UnityEngine;

namespace Code.Stages
{
    /// <summary>사전에 제작한 스테이지. 수치는 첫 실행용 임시 설정이며 런타임에는 쓰지 않는다.</summary>
    [CreateAssetMenu(menuName = "Defence/Stages/Stage")]
    public sealed class StageDefinitionSO : ScriptableObject
    {
        [field: SerializeField] public string Id { get; private set; }
        [field: SerializeField] public string Title { get; private set; }
        [field: SerializeField, TextArea] public string Gimmick { get; private set; }
        [field: SerializeField] public string[] Layout { get; private set; } = Array.Empty<string>();
        [field: SerializeField] public Vector2Int[] Doors { get; private set; } = Array.Empty<Vector2Int>();
        /// <summary>문마다 처음 열리는 웨이브 번호(1부터). 짧거나 비면 그 문은 처음부터 열려 있다.</summary>
        [field: SerializeField] public int[] DoorOpensAtWave { get; private set; } = Array.Empty<int>();
        /// <summary>강화 칸('+')을 덮은 함정의 피해 배율. 임시 값.</summary>
        [field: SerializeField, Min(1f)] public float EmpoweredTrapMultiplier { get; private set; } = 1.5f;
        [field: SerializeField] public StageDevice[] Devices { get; private set; } = Array.Empty<StageDevice>();
        [field: SerializeField] public Vector2Int Core { get; private set; }
        [field: SerializeField, Min(1)] public int CoreHealth { get; private set; }
        [field: SerializeField, Min(0)] public int StartingGold { get; private set; }
        [field: SerializeField, Min(0.1f)] public float CellSize { get; private set; }
        [field: SerializeField, Min(0.1f)] public float UnitSpeed { get; private set; }
        [field: SerializeField, Min(0.1f)] public float EnemySpeed { get; private set; }
        [field: SerializeField, Min(0.1f)] public float EngageRadius { get; private set; }
        [field: SerializeField, Min(0.1f)] public float LeashRadius { get; private set; }
        [field: SerializeField, Min(0)] public int BarricadeCost { get; private set; }
        [field: SerializeField, Min(1)] public int BarricadeHealth { get; private set; }
        [field: SerializeField, Min(0)] public float RecoveryDelay { get; private set; }
        [field: SerializeField, Min(0)] public float RecoveryPerSecond { get; private set; }
        [field: SerializeField, Range(0, 1)] public float DestroyRefund { get; private set; }
        [field: SerializeField, Range(0, 1)] public float DemolishRefund { get; private set; }
        [field: SerializeField, Min(0)] public int ClearReward { get; private set; }
        [field: SerializeField, Min(0)] public int KillReward { get; private set; }
        [field: SerializeField] public StageWave[] Waves { get; private set; } = Array.Empty<StageWave>();
        /// <summary>준비 단계 사건(선택). 선택지가 없으면 사건이 없다.</summary>
        [field: SerializeField] public StagePreparationEvent PreparationEvent { get; private set; }

        /// <summary>저장 ID와 실제 통행 바닥을 포함해 시작 전 설정을 확인한다.</summary>
        public bool TryValidate(out string reason)
        {
            reason = "스테이지 설정이 비어 있거나 유효하지 않습니다";
            if (string.IsNullOrWhiteSpace(Id) || Layout == null || Layout.Length == 0
                || Layout[0] == null || Layout[0].Length == 0 || CoreHealth <= 0 || CellSize <= 0
                || UnitSpeed <= 0 || EnemySpeed <= 0 || EngageRadius <= 0
                || LeashRadius <= 0 || BarricadeHealth <= 0 || Doors == null || Doors.Length == 0
                || Waves == null || Waves.Length == 0)
                return false;
            foreach (string row in Layout)
                if (row == null || row.Length != Layout[0].Length)
                    return false;
            if (!IsFloor(Core))
                return false;
            foreach (Vector2Int door in Doors)
                if (!IsFloor(door) || door == Core)
                    return false;
            if (OpenDoorCount(0) == 0)
                return false;
            // 웨이브 수보다 늦게 열리는 문은 끝까지 열리지 않는다. 문 수보다 긴 목록도 설정 실수다.
            if (DoorOpensAtWave != null)
            {
                if (DoorOpensAtWave.Length > Doors.Length)
                    return false;
                foreach (int wave in DoorOpensAtWave)
                    if (wave < 1 || wave > Waves.Length)
                        return false;
            }
            if (Devices != null)
                foreach (StageDevice device in Devices)
                {
                    if (device == null || !IsFloor(device.Cell) || device.Cell == Core || Array.IndexOf(Doors, device.Cell) >= 0)
                        return false;
                    if (device.Kind != StageDeviceKind.Rockfall && device.ActiveSeconds >= device.Period)
                        return false;
                }
            foreach (StageWave wave in Waves)
            {
                if (wave == null || wave.Enemies == null || wave.Count == 0 || wave.SpawnInterval <= 0)
                    return false;
                // 인스펙터에서 새로 더한 웨이브는 배율이 0으로 들어올 수 있다. 0이면 승격이 보스 체력을 지운다.
                if (wave.HasBoss && (wave.BossHealthMultiplier < 1f || wave.BossAttackMultiplier < 1f || wave.BossScale < 1f))
                    return false;
                for (int i = 0; i < wave.Count; i++)
                {
                    Code.Enemies.EnemyDataSO enemy = wave.At(i, out _);
                    if (enemy == null || enemy.Prefab == null || enemy.CoreArrivalDamage <= 0)
                        return false;
                }
            }
            if (PreparationEvent != null && !PreparationEvent.TryValidate(Waves.Length, out string eventReason))
            {
                reason = eventReason;
                return false;
            }
            reason = string.Empty;
            return true;
        }

        /// <summary>'#' 벽이 아니면 지나갈 수 있다. 'x'(설치 불가)·'+'(강화 칸)도 바닥이다.</summary>
        public bool IsFloor(Vector2Int cell) => Tile(cell) is char tile && tile != '#';

        /// <summary>바리케이드·부하·함정을 놓을 수 있는 바닥('x' 제외).</summary>
        public bool IsBuildable(Vector2Int cell) => Tile(cell) is char tile && tile != '#' && tile != 'x';

        public bool IsEmpowered(Vector2Int cell) => Tile(cell) == '+';

        public StageDevice DeviceAt(Vector2Int cell)
        {
            if (Devices != null)
                foreach (StageDevice device in Devices)
                    if (device != null && device.Cell == cell)
                        return device;
            return null;
        }

        /// <param name="waveIndex">0부터 센 웨이브 순서.</param>
        public bool IsDoorOpen(int doorIndex, int waveIndex) =>
            DoorOpensAtWave == null || doorIndex >= DoorOpensAtWave.Length || waveIndex + 1 >= DoorOpensAtWave[doorIndex];

        public int OpenDoorCount(int waveIndex)
        {
            int count = 0;
            for (int i = 0; i < Doors.Length; i++)
                if (IsDoorOpen(i, waveIndex))
                    count++;
            return count;
        }

        /// <summary>이 웨이브에 처음 열리는 문이 있는가(첫 웨이브 제외).</summary>
        public bool OpensDoorAt(int waveIndex)
        {
            if (waveIndex <= 0)
                return false;
            for (int i = 0; i < Doors.Length; i++)
                if (IsDoorOpen(i, waveIndex) && !IsDoorOpen(i, waveIndex - 1))
                    return true;
            return false;
        }

        /// <summary>열린 문 중 순서상 slot번째 문의 원래 번호.</summary>
        public int OpenDoorIndex(int waveIndex, int slot)
        {
            for (int i = 0; i < Doors.Length; i++)
            {
                if (!IsDoorOpen(i, waveIndex))
                    continue;
                if (slot == 0)
                    return i;
                slot--;
            }
            return -1;
        }

        private char? Tile(Vector2Int cell)
        {
            if (Layout == null || cell.y < 0 || cell.y >= Layout.Length || Layout[cell.y] == null
                || cell.x < 0 || cell.x >= Layout[cell.y].Length)
                return null;
            return Layout[cell.y][cell.x];
        }
    }
}
