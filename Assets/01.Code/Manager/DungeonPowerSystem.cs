using System.Collections.Generic;
using _01.Code.BT;
using _01.Code.Combat;
using _01.Code.Core;
using _01.Code.Events;
using _01.Code.MapCreateSystem;
using _01.Code.Skills;
using UnityEngine;

namespace _01.Code.Manager
{
    /// <summary>
    /// 웨이브 도중 플레이어가 쓰는 던전의 힘.
    /// 이 게임에서 웨이브는 여태 관전이었다 — 배치·건설·명령이 전부 대기 중으로 잠겨 있고
    /// 부하의 스킬은 행동트리가 알아서 쓴다. 권능은 습격이 도는 동안 플레이어에게 남는
    /// 유일한 개입 수단이므로, 자원도 웨이브 중에만 차오른다.
    /// </summary>
    public sealed class DungeonPowerSystem : MonoBehaviour
    {
        public static DungeonPowerSystem Current { get; private set; }

        [SerializeField] private GameEventChannelSO waveEventChannel;

        [SerializeField, Tooltip("이번 판에서 쓸 수 있는 권능 목록")]
        private DungeonPowerSO[] powers = System.Array.Empty<DungeonPowerSO>();

        [SerializeField, Min(1)] private int maxPower = 100;

        [SerializeField, Min(0f), Tooltip("웨이브가 도는 동안 초당 차오르는 권능")]
        private float powerPerSecond = 6f;

        [SerializeField, Min(0f), Tooltip("웨이브를 시작할 때 들고 가는 권능")]
        private float startingPower = 30f;

        [SerializeField, Tooltip("침입자를 쓰러뜨릴 때마다 얻는 권능. 잘 막을수록 더 쓸 수 있다.")]
        private float powerPerKill = 8f;

        private readonly Dictionary<DungeonPowerSO, float> _readyTimeByPower = new();
        private float _power;
        private bool _isWaveRunning;

        public IReadOnlyList<DungeonPowerSO> Powers => powers;
        public int CurrentPower => Mathf.FloorToInt(_power);
        public int MaxPower => maxPower;
        public bool IsWaveRunning => _isWaveRunning;

        private void Awake()
        {
            if (Current != null && Current != this)
            {
                Debug.LogError($"Duplicate {nameof(DungeonPowerSystem)} detected. Keep exactly one scene instance.", this);
                enabled = false;
                return;
            }

            Current = this;
        }

        private void OnDestroy()
        {
            if (Current == this)
                Current = null;
        }

        private void OnEnable()
        {
            if (waveEventChannel == null)
                return;

            waveEventChannel.AddListener<WaveStartedEvent>(HandleWaveStarted);
            waveEventChannel.AddListener<WaveEndedEvent>(HandleWaveEnded);
        }

        private void OnDisable()
        {
            if (waveEventChannel == null)
                return;

            waveEventChannel.RemoveListener<WaveStartedEvent>(HandleWaveStarted);
            waveEventChannel.RemoveListener<WaveEndedEvent>(HandleWaveEnded);
        }

        private void HandleWaveStarted(WaveStartedEvent evt)
        {
            _isWaveRunning = true;
            _power = Mathf.Clamp(startingPower, 0f, maxPower);
            _readyTimeByPower.Clear();
            RaiseChanged();
        }

        private void HandleWaveEnded(WaveEndedEvent evt)
        {
            // 대기 중에 쟁여 두고 다음 습격을 시작하자마자 쏟아붓는 걸 막는다.
            _isWaveRunning = false;
            _power = 0f;
            RaiseChanged();
        }

        private void Update()
        {
            if (!_isWaveRunning || powerPerSecond <= 0f || _power >= maxPower)
                return;

            var before = CurrentPower;
            _power = Mathf.Min(maxPower, _power + powerPerSecond * Time.deltaTime);
            if (CurrentPower != before)
                RaiseChanged();
        }

        /// <summary>침입자를 쓰러뜨리면 권능이 붙는다. 잘 막아낼수록 더 개입할 수 있다.</summary>
        public void RewardKill()
        {
            if (!_isWaveRunning || powerPerKill <= 0f)
                return;

            _power = Mathf.Min(maxPower, _power + powerPerKill);
            RaiseChanged();
        }

        /// <summary>겨냥해 둔 권능. 이 상태에서 구역을 클릭하면 패널이 열리는 대신 시전된다.</summary>
        public DungeonPowerSO ArmedPower { get; private set; }

        public void Arm(DungeonPowerSO power)
        {
            ArmedPower = ArmedPower == power ? null : power;
            ArmedPowerChanged?.Invoke(ArmedPower);
        }

        public void Disarm()
        {
            if (ArmedPower == null)
                return;

            ArmedPower = null;
            ArmedPowerChanged?.Invoke(null);
        }

        /// <summary>
        /// 겨냥한 권능이 있으면 이 구역에 쏟는다. 구역 클릭을 가로챈 경우에만 true.
        /// 시전에 실패해도(권능 부족·빈 구역) 클릭은 소비한다 — 겨냥 중에 패널이 튀어나오면 흐름이 끊긴다.
        /// </summary>
        public bool TryCastArmed(Node node)
        {
            var power = ArmedPower;
            if (power == null)
                return false;

            var cast = TryCast(power, node, out var reason);
            CastAttempted?.Invoke(power, cast, reason);
            if (cast)
                Disarm();

            return true;
        }

        public bool IsOnCooldown(DungeonPowerSO power) => CooldownRemaining(power) > 0f;

        public float CooldownRemaining(DungeonPowerSO power)
        {
            if (power == null || !_readyTimeByPower.TryGetValue(power, out var readyTime))
                return 0f;

            return Mathf.Max(0f, readyTime - Time.time);
        }

        public bool CanCast(DungeonPowerSO power, Node node, out string reason)
        {
            if (power == null)
            {
                reason = "권능이 없습니다";
                return false;
            }
            if (!_isWaveRunning)
            {
                reason = "습격 중에만 쓸 수 있습니다";
                return false;
            }
            if (node == null)
            {
                reason = "겨냥할 구역을 고르세요";
                return false;
            }
            if (CurrentPower < power.Cost)
            {
                reason = $"권능 {power.Cost} 필요 (현재 {CurrentPower})";
                return false;
            }
            if (IsOnCooldown(power))
            {
                reason = $"재사용까지 {CooldownRemaining(power):F1}초";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        /// <summary>고른 노드에 권능을 쏟는다. 실제로 닿은 대상 수를 돌려준다.</summary>
        public bool TryCast(DungeonPowerSO power, Node node, out string reason)
        {
            if (!CanCast(power, node, out reason))
                return false;

            // 봉쇄는 노드 자체를 겨냥한다. 아래 전투 판정을 태우면 "적이 있는 구역"만 고를 수 있게 되는데,
            // 그러면 적이 도착하기 전에 길을 끊는다는 이 권능의 요점이 사라진다.
            if (power.TargetsNode)
                return TryCastBlock(power, node, out reason);

            var battlefield = node.GetComponent<NodeBattlefield>();
            if (battlefield == null)
            {
                reason = "이 구역에는 전투가 없습니다";
                return false;
            }

            var team = power.Target == DungeonPowerTarget.Intruders ? BattleTeam.Enemy : BattleTeam.Player;
            var affected = ApplyTo(battlefield.Allies(team), power);
            if (affected <= 0)
            {
                reason = power.Target == DungeonPowerTarget.Intruders
                    ? "이 구역에 침입자가 없습니다"
                    : "이 구역에 부하가 없습니다";
                return false;
            }

            _power -= power.Cost;
            if (power.Cooldown > 0f)
                _readyTimeByPower[power] = Time.time + power.Cooldown;

            DungeonPowerVisual.PlayBurst(
                node.transform.position,
                power.FlashColor,
                battlefield.ArenaRadius,
                power.Damage > 0);

            RaiseChanged();
            reason = $"{power.DisplayName} · {affected}명에게 적중";
            return true;
        }

        /// <summary>고른 노드를 한동안 통행 불가로 만든다. 침입자는 길을 새로 찾아야 한다.</summary>
        private bool TryCastBlock(DungeonPowerSO power, Node node, out string reason)
        {
            if (node.HasWall)
            {
                reason = "이미 벽으로 막혀 있습니다";
                return false;
            }

            if (node.IsTemporarilyBlocked)
            {
                reason = $"이미 막혀 있습니다 · {node.BlockRemaining:F1}초 남음";
                return false;
            }

            node.BlockTemporarily(power.BlockDuration);

            _power -= power.Cost;
            if (power.Cooldown > 0f)
                _readyTimeByPower[power] = Time.time + power.Cooldown;

            var battlefield = node.GetComponent<NodeBattlefield>();
            DungeonPowerVisual.PlayBurst(
                node.transform.position,
                power.FlashColor,
                battlefield != null ? battlefield.ArenaRadius : 1f,
                true);

            RaiseChanged();
            reason = $"{power.DisplayName} · {power.BlockDuration:0.#}초 동안 길이 끊겼습니다";
            return true;
        }

        /// <summary>권능을 명단 전체에 적용한다. 사본을 떠서 도는 이유는 피해로 명단이 줄기 때문이다.</summary>
        private static int ApplyTo(List<BattleAgent> agents, DungeonPowerSO power)
        {
            if (agents == null || agents.Count == 0)
                return 0;

            var targets = new List<BattleAgent>(agents);
            var affected = 0;

            foreach (var agent in targets)
            {
                if (agent == null || !agent.IsAlive)
                    continue;

                var combatant = agent.Combatant;
                if (combatant == null)
                    continue;

                if (power.Heal > 0)
                    combatant.Health?.Heal(power.Heal);

                if (power.StatusEffect != null)
                    power.StatusEffect.TryApplyTo(combatant);

                // 피해는 마지막에. 먼저 넣으면 죽은 대상에게 회복·상태이상을 거는 순서가 된다.
                if (power.Damage > 0)
                    combatant.Health?.TakeDamage(power.Damage);

                affected++;
            }

            return affected;
        }

        private void RaiseChanged()
        {
            DungeonPowerChanged?.Invoke(CurrentPower, maxPower);
        }

        /// <summary>권능 잔량이 바뀔 때. HUD가 구독한다.</summary>
        public event System.Action<int, int> DungeonPowerChanged;

        /// <summary>겨냥한 권능이 바뀔 때(해제는 null).</summary>
        public event System.Action<DungeonPowerSO> ArmedPowerChanged;

        /// <summary>시전을 시도했을 때 — 성공 여부와 사유. HUD가 안내 문구로 쓴다.</summary>
        public event System.Action<DungeonPowerSO, bool, string> CastAttempted;
    }
}
