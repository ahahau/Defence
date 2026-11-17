using UnityEngine;

namespace Code.Manager
{
    /// <summary>판 난수가 갈라지는 줄기. 값을 바꾸면 같은 시드의 결과가 달라지므로 끝에만 더한다.</summary>
    public enum RunRandomStream
    {
        RoomSize = 1,
        RoomFeatureChance = 2,
        RoomFeatureKind = 3,
        Events = 4,
        Wave = 5,
        WavePacing = 6,
        RoomLayout = 7
    }

    /// <summary>
    /// 판 시드와 시스템별 난수(기획서: 런 시작 때 모든 난수를 고정하고, 저장을 다시 불러도 결과가 바뀌지 않는다).
    ///
    /// 전역 난수를 쓰면 불러올 때마다 방 후보·사건 순서·파티 편성이 다시 뽑혀, 저장·불러오기로 원하는 결과가
    /// 나올 때까지 굴릴 수 있다. 시드에 자리·일차 같은 고정 값을 섞어 뽑으면 같은 판에서는 늘 같은 값이 나온다.
    /// 유닛 판매·계약 난수는 주간 계약 작업이 맡는다.
    /// </summary>
    public static class RunRandom
    {
        /// <summary>지금 판의 시드. 0이면 아직 정하지 않았다.</summary>
        public static int Seed { get; private set; }

        /// <summary>새 판을 시작한다. 시드를 새로 뽑는다.</summary>
        public static void BeginNewRun(int seed = 0)
        {
            Seed = seed != 0 ? seed : NewSeed();
        }

        /// <summary>저장에서 시드를 되돌린다. 옛 저장(0)이면 새로 뽑는다.</summary>
        public static void Restore(int seed)
        {
            Seed = seed != 0 ? seed : NewSeed();
        }

        /// <summary>시드·줄기·두 값으로 정해지는 32비트 해시.</summary>
        public static int Hash(RunRandomStream stream, int a, int b = 0)
        {
            unchecked
            {
                var h = (uint)EnsureSeed() * 0x9E3779B1u;
                h ^= (uint)stream * 0x85EBCA77u;
                h = Mix(h ^ (uint)a * 0xC2B2AE3Du);
                h = Mix(h ^ (uint)b * 0x27D4EB2Fu);
                return (int)h;
            }
        }

        /// <summary>0 이상 1 미만의 정해진 값.</summary>
        public static float Value(RunRandomStream stream, int a, int b = 0) =>
            ((uint)Hash(stream, a, b) >> 8) / 16777216f;

        /// <summary>그 줄기·값으로 시작하는 난수 생성기. 같은 입력이면 같은 순서를 낸다.</summary>
        public static System.Random Create(RunRandomStream stream, int a, int b = 0) =>
            new(Hash(stream, a, b));

        private static int EnsureSeed()
        {
            if (Seed == 0)
                Seed = NewSeed();
            return Seed;
        }

        private static int NewSeed()
        {
            var seed = System.Environment.TickCount ^ Random.Range(int.MinValue, int.MaxValue);
            return seed != 0 ? seed : 1;
        }

        private static uint Mix(uint h)
        {
            unchecked
            {
                h ^= h >> 16;
                h *= 0x7FEB352Du;
                h ^= h >> 15;
                h *= 0x846CA68Bu;
                h ^= h >> 16;
                return h;
            }
        }
    }
}
