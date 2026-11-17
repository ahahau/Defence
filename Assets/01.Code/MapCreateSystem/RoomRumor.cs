using UnityEngine;

namespace Code.MapCreateSystem
{
    /// <summary>
    /// 모험가 사이에 도는 방 하나의 소문. 살아 나간 파티가 본 것만 쌓이고 하루마다 절반으로 사그라든다.
    ///
    /// 악명(<see cref="Node.DangerLevel"/>)은 판 내내 쌓이기만 해서 후반엔 모든 방이 위험해 보인다.
    /// 길 판단에는 줄어드는 이 값을 쓴다.
    /// </summary>
    public readonly struct RoomRumor
    {
        public RoomRumor(int traps, int downs, int retreats)
        {
            Traps = Mathf.Max(0, traps);
            Downs = Mathf.Max(0, downs);
            Retreats = Mathf.Max(0, retreats);
        }

        /// <summary>이 방에서 함정이 모험가에게 터진 횟수.</summary>
        public int Traps { get; }

        /// <summary>이 방에서 모험가가 쓰러진 횟수.</summary>
        public int Downs { get; }

        /// <summary>이 방에서 모험가가 겁먹고 돌아선 횟수.</summary>
        public int Retreats { get; }

        public bool IsEmpty => Traps == 0 && Downs == 0 && Retreats == 0;

        public RoomRumor Add(in RoomRumor other) =>
            new(Traps + other.Traps, Downs + other.Downs, Retreats + other.Retreats);

        /// <summary>하루가 지나 절반만 남는다(내림). 1은 0이 된다.</summary>
        public RoomRumor Halved() => new(Traps / 2, Downs / 2, Retreats / 2);

        public static RoomRumor Of(RumorKind kind) => kind switch
        {
            RumorKind.Trap => new RoomRumor(1, 0, 0),
            RumorKind.Down => new RoomRumor(0, 1, 0),
            _ => new RoomRumor(0, 0, 1)
        };
    }
}
