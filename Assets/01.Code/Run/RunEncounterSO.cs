using UnityEngine;
using UnityEngine.Serialization;

namespace Code.Run
{
    /// <summary>
    /// 침입 방어 한 판의 구성: 층별 격자 배치, 부하 시작 자리,
    /// 입구로 들어오는 적, 수치.
    /// </summary>
    [CreateAssetMenu(menuName = "SO/Run/Encounter", fileName = "RunEncounter")]
    public class RunEncounterSO : ScriptableObject
    {
        [SerializeField, HideInInspector, FormerlySerializedAs("<Layout>k__BackingField")]
        private RunRoomKind[] legacyLayout;

        [field: SerializeField] public RunRoomLayout[] GridLayout { get; private set; } = System.Array.Empty<RunRoomLayout>();
        [field: SerializeField, Min(0)] public int EntryRoom { get; private set; }
        [field: SerializeField] public RunPlacement[] Minions { get; private set; } = System.Array.Empty<RunPlacement>();

        [SerializeField, HideInInspector, FormerlySerializedAs("<Intruders>k__BackingField")]
        private RunCombatantSO[] legacyIntruders;

        [field: SerializeField, Tooltip("입구에서 이 순서로 입장하며 파티별 상성을 덮어쓸 수 있다.")]
        public RunIntruderEntry[] Party { get; private set; } = System.Array.Empty<RunIntruderEntry>();

        [field: SerializeField] public RunBattleSettings Settings { get; private set; } = new();
        [field: SerializeField] internal Code.Enemies.EnemyDataSO DemoEliteAppearance { get; private set; }

        /// <summary>이 구성으로 새 전투를 만든다.</summary>
        public RunBattle CreateBattle() => CreateBattle(null);

        /// <summary>배치 화면에서 정한 자리와 마력을 새 전투에 복사한다. 원본 SO는 바꾸지 않는다.</summary>
        public RunBattle CreateBattle(RunDeployment deployment) => CreateBattle(deployment, Settings, Party);

        internal RunBattle CreateBattle(RunDeployment deployment, RunBattleSettings settings, RunIntruderEntry[] party)
        {
            RunBattle battle = new(settings, GridLayout, EntryRoom);
            for (int i = 0; i < Minions.Length; i++)
            {
                RunPlacement placement = Minions[i];
                if (placement.Combatant != null)
                    battle.AddCombatant(placement.Combatant, deployment != null ? deployment.RoomOf(i) : placement.Room);
            }
            if (deployment != null)
                for (int i = 0; i < GridLayout.Length; i++) battle.SetMana(i, deployment.ManaAt(i));
            // 무리마다 같은 파티가 입장 줄에 다시 선다. 다음 무리는 정한 간격에 맞춰 입장을 시작한다(2026-11-10 사용자 결정).
            for (int wave = 0; wave < System.Math.Max(1, settings.WaveCount); wave++)
            {
                if (wave > 0) battle.DelayNextEntryUntil(wave * settings.WaveGapSeconds);
                foreach (RunIntruderEntry intruder in party)
                    if (intruder.Combatant != null)
                        battle.AddIntruderAtEntrance(intruder.Combatant, intruder.OverrideAffinity ? intruder.Affinity : null);
            }
            return battle;
        }
    }
}
