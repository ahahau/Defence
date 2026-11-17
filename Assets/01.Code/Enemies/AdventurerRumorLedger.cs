using System.Collections.Generic;
using Code.MapCreateSystem;

namespace Code.Enemies
{
    /// <summary>
    /// 파티마다 던전에서 겪은 일(견문)과, 그것이 던전 소문이 되는 순간.
    ///
    /// 살아 나간 자만 말을 옮긴다. 전멸한 파티의 견문은 함께 사라지므로 "다 잡으면 소문이 안 난다".
    /// 큰돈을 들고 나간 파티가 있으면 "돈 된다더라"가 겁을 덮어 던전 소문이 지워진다.
    /// 파티 번호는 <see cref="EnemyMover.PartyId"/>를 그대로 쓴다.
    /// </summary>
    public sealed class AdventurerRumorLedger
    {
        private readonly Dictionary<int, Dictionary<Node, RoomRumor>> _sightings = new();
        private readonly HashSet<int> _silentParties = new();

        /// <summary>지금 영업에서 쓰는 장부. 영업이 시작될 때 <see cref="BeginBusinessDay"/>로 비운다.</summary>
        public static AdventurerRumorLedger Active { get; } = new();

        /// <summary>보스와 함께 온 파티는 소문을 남기지 않는다. 보스날의 결과가 다음 주 길을 바꾸면 안 된다.</summary>
        public void Silence(int partyId) => _silentParties.Add(partyId);

        /// <summary>파티가 이 방에서 겪은 일을 기억한다. 통로 위 일처럼 방이 없으면 남기지 않는다.</summary>
        public void Note(int partyId, Node node, RumorKind kind)
        {
            if (node == null || _silentParties.Contains(partyId))
                return;

            if (!_sightings.TryGetValue(partyId, out var rooms))
            {
                rooms = new Dictionary<Node, RoomRumor>();
                _sightings[partyId] = rooms;
            }

            rooms.TryGetValue(node, out var rumor);
            rooms[node] = rumor.Add(RoomRumor.Of(kind));
        }

        /// <summary>
        /// 파티원이 살아서 나갔다. 그때까지의 견문을 던전 소문에 더하고 비운다.
        /// 남은 파티원이 이후에 겪는 일은 다음에 나가는 사람이 옮긴다.
        /// </summary>
        /// <param name="bigReward">큰돈을 들고 나갔는가. 그러면 견문 대신 던전 소문을 지운다.</param>
        public void Report(int partyId, bool bigReward)
        {
            if (_silentParties.Contains(partyId))
            {
                _sightings.Remove(partyId);
                return;
            }

            if (bigReward)
            {
                _sightings.Remove(partyId);
                ClearDungeonRumor();
                return;
            }

            if (!_sightings.TryGetValue(partyId, out var rooms))
                return;

            foreach (var pair in rooms)
            {
                // 그사이 철거·공사 취소로 사라진 방이면 옮길 곳이 없다.
                if (pair.Key != null)
                    pair.Key.AddRumor(pair.Value);
            }

            _sightings.Remove(partyId);
        }

        /// <summary>파티가 전멸했다. 아무도 돌아가 말하지 못한다.</summary>
        public void Discard(int partyId) => _sightings.Remove(partyId);

        /// <summary>새 영업. 어제 파티의 견문을 비우고, 던전 소문은 절반만 남긴다.</summary>
        public void BeginBusinessDay()
        {
            _sightings.Clear();
            _silentParties.Clear();
            foreach (var node in Node.ActiveNodes)
            {
                if (node != null)
                    node.HalveRumor();
            }
        }

        private static void ClearDungeonRumor()
        {
            foreach (var node in Node.ActiveNodes)
            {
                if (node != null)
                    node.RestoreRumor(default);
            }
        }
    }
}
