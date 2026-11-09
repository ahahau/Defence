using System;
using System.IO;
using UnityEngine;

namespace Code.Run
{
    /// <summary>시연 전용 칸 체크포인트. 기존 본편의 저장 파일과 분리한다.</summary>
    public static class RunDemoSave
    {
        public const string ScenePath = "Assets/00.Scenes/DungeonRun.unity";
        private const int Version = 1;
        private const string FileName = "defence-dungeon-demo-v1.json";

        [Serializable]
        internal sealed class Checkpoint
        {
            public int Version;
            public RunDemoRoute.Node Current;
            public RunDemoRoute.Result Result;
            public int Branch = -1;
            public RunDemoRoute.Result RaidResult;
            public int TrapBonus;
            public RunBattleCarryover Carryover;

            internal RunDemoRoute GetRoute() => RunDemoRoute.Restore(Current, Result,
                Branch == -1 ? null : (RunDemoRoute.Node)Branch, RaidResult);
        }

        public static bool CanContinue()
        {
            Checkpoint checkpoint = Load(out _);
            return checkpoint != null && !checkpoint.GetRoute().IsFinished;
        }

        public static bool BeginNew() => Write(new Checkpoint { Version = Version }, true);

        internal static bool Save(RunDemoCampaign campaign)
        {
            RunDemoRoute route = campaign.Route;
            return Write(new Checkpoint
            {
                Version = Version,
                Current = route.Current,
                Result = route.CurrentResult,
                Branch = route.ChosenBranch.HasValue ? (int)route.ChosenBranch.Value : -1,
                RaidResult = route.RaidResult,
                TrapBonus = campaign.TrapBonus,
                Carryover = campaign.Carryover,
            }, false);
        }

        internal static Checkpoint Load(out bool blocked)
        {
            string path = Path.Combine(Application.persistentDataPath, FileName);
            blocked = false;
            if (!File.Exists(path)) return null;
            Checkpoint checkpoint = Read(path);
            if (checkpoint != null) return checkpoint;
            blocked = true;
            Debug.LogWarning("시연 체크포인트를 읽을 수 없어 원본을 보존합니다.");
            return Read(path + ".bak");
        }

        private static Checkpoint Read(string path)
        {
            if (!File.Exists(path)) return null;
            try
            {
                Checkpoint value = JsonUtility.FromJson<Checkpoint>(File.ReadAllText(path));
                if (value == null || value.Version != Version || value.GetRoute() == null || value.TrapBonus is not (0 or 2)) return null;
                if (value.Carryover == null)
                    return value.Current == RunDemoRoute.Node.ThiefDefense && value.Result == RunDemoRoute.Result.Pending ? value : null;
                RunBattleCarryover state = value.Carryover;
                if (state.MinionHealth == null || state.RoomDurability == null || state.RoomBroken == null
                    || state.RoomDurability.Length != state.RoomBroken.Length || state.Gold < 0 || state.HeartHealth < 0) return null;
                foreach (int health in state.MinionHealth) if (health < 0) return null;
                foreach (float durability in state.RoomDurability)
                    if (durability < 0 || float.IsNaN(durability) || float.IsInfinity(durability)) return null;
                return value;
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException)
            {
                return null;
            }
        }

        private static bool Write(Checkpoint checkpoint, bool explicitNew)
        {
            string path = Path.Combine(Application.persistentDataPath, FileName);
            string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                Directory.CreateDirectory(Application.persistentDataPath);
                File.WriteAllText(temp, JsonUtility.ToJson(checkpoint));
                if (!File.Exists(path)) File.Move(temp, path);
                else if (Read(path) != null) File.Replace(temp, path, path + ".bak");
                else if (explicitNew) File.Replace(temp, path, path + "." + Guid.NewGuid().ToString("N") + ".invalid");
                else return false;
                return true;
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                Debug.LogWarning($"시연 체크포인트를 저장하지 못했습니다. {error.Message}");
                return false;
            }
            finally
            {
                try { File.Delete(temp); }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException)
                {
                    Debug.LogWarning($"시연 임시 저장 파일을 정리하지 못했습니다. {error.Message}");
                }
            }
        }
    }
}
