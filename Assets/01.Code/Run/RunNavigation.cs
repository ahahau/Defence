using System;

namespace Code.Run
{
    /// <summary>방 중앙과 네 문, 복도, 별도 입구 굴을 잇는 그래프. 탐색 배열은 준비 때만 만든다.</summary>
    public class RunNavigation
    {
        private const float Epsilon = 0.0005f;
        private const int NodesPerRoom = 5;
        private const int EntranceEdge = -2;
        private readonly RunBattle _battle;
        private readonly float[,] _edges;
        private readonly int[,] _links;
        private readonly float[] _distance;
        private readonly bool[] _visited;
        private readonly int[] _next;
        private readonly int _count;
        private readonly int _entranceNode;

        public RunNavigation(RunBattle battle)
        {
            _battle = battle;
            _entranceNode = battle.Rooms.Count * NodesPerRoom;
            _count = _entranceNode + 1;
            _edges = new float[_count, _count];
            _links = new int[_count, _count];
            _distance = new float[_count];
            _visited = new bool[_count];
            _next = new int[_count];
            for (int i = 0; i < _count; i++)
            {
                for (int j = 0; j < _count; j++)
                {
                    _edges[i, j] = float.PositiveInfinity;
                    _links[i, j] = -1;
                }
            }
            for (int i = 0; i < battle.Rooms.Count; i++)
            {
                for (int port = 1; port < NodesPerRoom; port++)
                    Connect(i * NodesPerRoom, i * NodesPerRoom + port, battle.Rooms[i].Length * 0.5f, -1);
            }
            for (int i = 0; i < battle.Links.Count; i++)
            {
                RunLink link = battle.Links[i];
                Connect(Node(link.From, link.Axis, link.FromOffset), Node(link.To, link.Axis, link.ToOffset), link.Length, link.Index);
            }
            Connect(_entranceNode, Node(battle.EntryRoom, RunAxis.Vertical, 0f), battle.Settings.EntranceLength, EntranceEdge);
        }

        /// <summary>중심선과 복도를 따른 거리. 닿을 수 없는 곳은 무한대다.</summary>
        public float Distance(RunPosition from, RunPosition to, bool tactical = false)
        {
            Search(to, tactical);
            Endpoints(from, out int a, out int b, out float da, out float db);
            float best = Math.Min(da + _distance[a] + PartialCost(from, a, tactical), db + _distance[b] + PartialCost(from, b, tactical));
            if (SameSegment(from, to)) best = Math.Min(best, Math.Abs(from.Offset - to.Offset));
            return best;
        }

        /// <summary>다음에 걸을 한 중심선 또는 통로 구간을 돌려준다.</summary>
        public bool NextStep(RunPosition from, RunPosition to, bool tactical, out RunPosition start, out RunPosition end)
        {
            Search(to, tactical);
            Endpoints(from, out int a, out int b, out float da, out float db);
            float ca = da + _distance[a] + PartialCost(from, a, tactical);
            float cb = db + _distance[b] + PartialCost(from, b, tactical);
            float best = Math.Min(ca, cb);
            start = from;
            end = to;
            if (SameSegment(from, to) && Math.Abs(from.Offset - to.Offset) <= best + Epsilon)
                return Math.Abs(from.Offset - to.Offset) > Epsilon;
            if (float.IsPositiveInfinity(best)) return false;
            int node = ca <= cb ? a : b;
            if ((ca <= cb ? da : db) > Epsilon)
            {
                end = EndpointPosition(from, node);
                return true;
            }
            int next = _next[node];
            if (next < 0)
            {
                start = EndpointPosition(to, node);
                end = to;
                return Math.Abs(start.Offset - end.Offset) > Epsilon;
            }
            int linkIndex = _links[node, next];
            if (linkIndex >= 0)
            {
                RunLink link = _battle.Links[linkIndex];
                bool forward = node / NodesPerRoom == link.From;
                start = RunPosition.OnLink(linkIndex, forward ? 0f : link.Length);
                end = RunPosition.OnLink(linkIndex, forward ? link.Length : 0f);
            }
            else if (linkIndex == EntranceEdge)
            {
                start = RunPosition.AtEntrance(node == _entranceNode ? 0f : _battle.Settings.EntranceLength);
                end = RunPosition.AtEntrance(node == _entranceNode ? _battle.Settings.EntranceLength : 0f);
            }
            else
            {
                RunAxis axis = node % NodesPerRoom >= 3 || next % NodesPerRoom >= 3 ? RunAxis.Vertical : RunAxis.Horizontal;
                start = RunPosition.InRoom(node / NodesPerRoom, axis, NodeOffset(node));
                end = RunPosition.InRoom(next / NodesPerRoom, axis, NodeOffset(next));
            }
            return true;
        }

        /// <summary>굴은 전력 노드가 아니다. 고장 나지 않은 방 연결만 심장에서 순회한다.</summary>
        public void Connected(bool[] connected, int[] queue)
        {
            Array.Clear(connected, 0, connected.Length);
            int head = 0;
            int tail = 0;
            queue[tail++] = _battle.HeartRoom;
            connected[_battle.HeartRoom] = true;
            while (head < tail)
            {
                int room = queue[head++];
                for (int i = 0; i < _battle.Links.Count; i++)
                {
                    RunLink link = _battle.Links[i];
                    int neighbor = link.From == room ? link.To : link.To == room ? link.From : -1;
                    if (neighbor < 0 || connected[neighbor] || _battle.Rooms[neighbor].IsBroken) continue;
                    connected[neighbor] = true;
                    queue[tail++] = neighbor;
                }
            }
        }

        private void Search(RunPosition target, bool tactical)
        {
            for (int i = 0; i < _count; i++)
            {
                _distance[i] = float.PositiveInfinity;
                _visited[i] = false;
                _next[i] = -1;
            }
            Endpoints(target, out int a, out int b, out float da, out float db);
            _distance[a] = da;
            _distance[b] = db;
            for (int iteration = 0; iteration < _count; iteration++)
            {
                int current = -1;
                for (int i = 0; i < _count; i++)
                    if (!_visited[i] && (current < 0 || _distance[i] < _distance[current])) current = i;
                if (current < 0 || float.IsPositiveInfinity(_distance[current])) break;
                _visited[current] = true;
                for (int source = 0; source < _count; source++)
                {
                    float cost = _edges[source, current];
                    int link = _links[source, current];
                    if (tactical && link >= 0)
                    {
                        cost += EntryCost(current / NodesPerRoom);
                        if (_battle.IsDoorLocked(link)) cost += Penalty;
                    }
                    else if (tactical && link == EntranceEdge && current != _entranceNode)
                        cost += EntryCost(_battle.EntryRoom);
                    float candidate = cost + _distance[current];
                    if (candidate + Epsilon >= _distance[source]) continue;
                    _distance[source] = candidate;
                    _next[source] = current;
                }
            }
        }

        private float Penalty => _battle.Settings.RoomLength * 2f;
        private float EntryCost(int room) => _battle.IsDefended(room) ? Penalty : 0f;

        private float PartialCost(RunPosition from, int endpoint, bool tactical)
        {
            if (!tactical || from.IsInRoom) return 0f;
            if (from.IsAtEntrance) return endpoint == _entranceNode ? 0f : EntryCost(_battle.EntryRoom);
            RunLink link = _battle.Links[from.Link];
            float end = endpoint / NodesPerRoom == link.From ? 0f : link.Length;
            float cost = EntryCost(endpoint / NodesPerRoom);
            if (_battle.IsDoorLocked(link.Index) && (from.Offset - link.Length * 0.5f) * (end - link.Length * 0.5f) <= 0f) cost += Penalty;
            return cost;
        }

        private void Connect(int a, int b, float length, int link)
        {
            _edges[a, b] = _edges[b, a] = length;
            _links[a, b] = _links[b, a] = link;
        }

        private int Node(int room, RunAxis axis, float offset)
        {
            int port = Math.Abs(offset - _battle.Rooms[room].Center) < Epsilon ? 0
                : axis == RunAxis.Horizontal ? (offset < _battle.Rooms[room].Center ? 1 : 2)
                : offset < _battle.Rooms[room].Center ? 3 : 4;
            return room * NodesPerRoom + port;
        }

        private float NodeOffset(int node) => (node % NodesPerRoom) switch
        {
            1 or 3 => 0f,
            2 or 4 => _battle.Rooms[node / NodesPerRoom].Length,
            _ => _battle.Rooms[node / NodesPerRoom].Center,
        };

        private void Endpoints(RunPosition position, out int a, out int b, out float da, out float db)
        {
            if (position.IsInRoom)
            {
                float half = _battle.Rooms[position.Room].Center;
                bool first = position.Offset <= half;
                a = first ? Node(position.Room, position.Axis, 0f) : position.Room * NodesPerRoom;
                b = first ? position.Room * NodesPerRoom : Node(position.Room, position.Axis, half * 2f);
                da = position.Offset - (first ? 0f : half);
                db = half - da;
            }
            else if (position.IsAtEntrance)
            {
                a = _entranceNode;
                b = Node(_battle.EntryRoom, RunAxis.Vertical, 0f);
                da = position.Offset;
                db = _battle.Settings.EntranceLength - position.Offset;
            }
            else
            {
                RunLink link = _battle.Links[position.Link];
                a = Node(link.From, link.Axis, link.FromOffset);
                b = Node(link.To, link.Axis, link.ToOffset);
                da = position.Offset;
                db = link.Length - position.Offset;
            }
        }

        private RunPosition EndpointPosition(RunPosition position, int node)
        {
            if (position.IsInRoom) return RunPosition.InRoom(position.Room, position.Axis, NodeOffset(node));
            if (position.IsAtEntrance) return RunPosition.AtEntrance(node == _entranceNode ? 0f : _battle.Settings.EntranceLength);
            RunLink link = _battle.Links[position.Link];
            return RunPosition.OnLink(position.Link, node / NodesPerRoom == link.From ? 0f : link.Length);
        }

        private static bool SameSegment(RunPosition a, RunPosition b) => a.IsInRoom == b.IsInRoom && (a.IsInRoom
            ? a.Room == b.Room && a.Axis == b.Axis : a.Link == b.Link);
    }
}
