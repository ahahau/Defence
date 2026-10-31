using System.Collections.Generic;
using UnityEngine;

namespace Code.Stages
{
    /// <summary>
    /// 같은 문으로 연달아 나온 침입자 묶음(2026-11-01 사용자 결정). 리더는 처음 나온 생존자이고, 쓰러지거나 코어에 닿으면 다음 생존자가 잇는다.
    /// 파티는 가장 느린 생존자 속도로 걷고, 리더는 처진 멤버를 잠깐 기다린다. 표식 적은 넣지 않는다.
    /// 거리·시간은 모두 임시 값이며 칸 크기 배수다.
    /// </summary>
    public sealed class StageParty
    {
        /// <summary>파티 안 입장 간격(초).</summary>
        public const float SpawnGap = 0.4f;
        /// <summary>멤버가 이 칸 수보다 멀면 리더를 따라간다.</summary>
        public const float FollowCells = 1f;
        /// <summary>멤버가 이 칸 수보다 처지면 리더가 기다린다.</summary>
        public const float WaitCells = 2.5f;
        /// <summary>
        /// 리더가 처진 멤버를 기다리는 최대 시간. 구 방 경로는 제한이 없지만, 막힌 길에 갇힌 멤버 때문에 파티 전체가 서지 않게 둔다.
        /// </summary>
        public const float MaxWaitSeconds = 3f;

        private readonly List<StageActor> _members = new();
        private readonly List<Vector2Int> _trail = new();
        private StageActor _trailOwner;

        /// <summary>리더가 밟은 칸 순서. 멤버가 이 칸을 차례로 따라가 리더와 같은 길로 간다.</summary>
        public IReadOnlyList<Vector2Int> Trail => _trail;
        /// <summary>리더가 바뀌어 발자국을 새로 시작할 때마다 오른다.</summary>
        public int TrailVersion { get; private set; }

        /// <summary>리더가 매 프레임 지금 칸을 남긴다. 리더가 바뀌면 새 리더 자리부터 다시 쌓는다.</summary>
        public void RecordLeaderCell(StageActor leader, Vector2Int cell)
        {
            if (leader != _trailOwner)
            {
                _trailOwner = leader;
                _trail.Clear();
                TrailVersion++;
            }
            if (_trail.Count == 0 || _trail[_trail.Count - 1] != cell)
                _trail.Add(cell);
        }

        public void Add(StageActor actor)
        {
            if (actor != null && !_members.Contains(actor))
                _members.Add(actor);
        }

        public StageActor Leader
        {
            get
            {
                foreach (StageActor member in _members)
                    if (member != null && member.IsAlive)
                        return member;
                return null;
            }
        }

        /// <summary>살아 있는 멤버 중 가장 느린 기본 속도.</summary>
        public float Speed(StageActor self)
        {
            float speed = self.Speed;
            foreach (StageActor member in _members)
                if (member != null && member.IsAlive)
                    speed = Mathf.Min(speed, member.Speed);
            return speed;
        }

        /// <summary>리더에게서 이 거리보다 처진 생존 멤버가 있는가.</summary>
        public bool HasStraggler(StageActor leader, float distance)
        {
            float limit = distance * distance;
            foreach (StageActor member in _members)
                if (member != null && member != leader && member.IsAlive
                    && ((Vector2)(member.transform.position - leader.transform.position)).sqrMagnitude > limit)
                    return true;
            return false;
        }

        /// <summary>이 거리 안에서 싸우고 있는 다른 멤버의 상대. 합류할 대상이다.</summary>
        public StageActor AssistTarget(StageActor self, float distance)
        {
            float limit = distance * distance;
            foreach (StageActor member in _members)
            {
                if (member == null || member == self || !member.IsAlive)
                    continue;
                StageActor opponent = member.Opponent;
                if (opponent != null && opponent.IsAlive
                    && ((Vector2)(member.transform.position - self.transform.position)).sqrMagnitude <= limit)
                    return opponent;
            }
            return null;
        }
    }
}
