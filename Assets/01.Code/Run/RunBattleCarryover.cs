using System;

namespace Code.Run
{
    /// <summary>전투 간에 이어지는 던전 자원, 부하 체력과 방 상태.</summary>
    [Serializable]
    internal sealed class RunBattleCarryover
    {
        public int HeartHealth;
        public int Gold;
        public int[] MinionHealth;
        public float[] RoomDurability;
        public bool[] RoomBroken;

        internal RunBattleCarryover(RunBattle battle)
        {
            HeartHealth = battle.HeartHealth;
            Gold = battle.VaultGold;
            RoomDurability = new float[battle.Rooms.Count];
            RoomBroken = new bool[battle.Rooms.Count];
            for (int i = 0; i < RoomDurability.Length; i++)
            {
                RoomDurability[i] = battle.Rooms[i].Durability;
                RoomBroken[i] = battle.Rooms[i].IsBroken;
            }

            int count = 0;
            foreach (RunCombatant actor in battle.Combatants)
                if (actor.Side == RunSide.Minion) count++;
            MinionHealth = new int[count];
            foreach (RunCombatant actor in battle.Combatants)
                if (actor.Side == RunSide.Minion) MinionHealth[actor.Id] = actor.Health;
        }

        internal void ApplyTo(RunBattle battle)
        {
            battle.SetInitialResources(HeartHealth, Gold);
            foreach (RunCombatant actor in battle.Combatants)
                if (actor.Side == RunSide.Minion) actor.SetInitialHealth(MinionHealth[actor.Id]);
            for (int i = 0; i < RoomDurability.Length; i++)
                battle.Rooms[i].SetInitialCondition(RoomDurability[i], RoomBroken[i]);
        }
    }
}
