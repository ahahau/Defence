using UnityEngine;

namespace _01.Code.Manager
{
    /// <summary>
    /// 던전 등급이 기억해야 할 것 하나 — 여기서 죽어 나간 모험가의 누적 수.
    ///
    /// 금고 금화와 건물은 지도를 세면 언제든 다시 구할 수 있지만 처치 수는 그렇지 않다.
    /// 저장하지 않으면 불러올 때마다 등급이 주저앉아, 같은 던전이 갑자기 조용해지고
    /// 벌이가 준다.
    /// </summary>
    public sealed class DungeonGradeManager : MonoBehaviour
    {
        public static DungeonGradeManager Current { get; private set; }

        [SerializeField, Min(0)] private int totalKills;

        /// <summary>이 판에서 여기서 죽은 모험가의 총수.</summary>
        public int TotalKills => Mathf.Max(0, totalKills);

        /// <summary>지금 등급. 처치·금고·건물을 합친 값이다.</summary>
        public int Grade => DungeonGradeRules.CalculateGrade();

        /// <summary>등급을 한 낱말로.</summary>
        public string GradeLabel => DungeonGradeRules.GetGradeLabel(Grade);

        /// <summary>오늘 영업으로 오른 등급. 정산 보고가 하루의 성과로 보여 준다.</summary>
        public int LastGradeDelta { get; private set; }

        /// <summary>오늘 막아낸 수.</summary>
        public int LastDayKills { get; private set; }

        private int _gradeAtDayStart;
        private int _killsAtDayStart;

        private void Awake()
        {
            Current = this;
        }

        private void OnDestroy()
        {
            if (Current == this)
                Current = null;
        }

        public void BeginBusinessDay()
        {
            _gradeAtDayStart = Grade;
            _killsAtDayStart = TotalKills;
        }

        /// <summary>모험가 하나를 막았다. 위험하다는 소문이 그만큼 퍼진다.</summary>
        public void RecordKill()
        {
            totalKills = TotalKills + 1;
        }

        public void FinalizeBusinessDay()
        {
            LastDayKills = TotalKills - _killsAtDayStart;
            LastGradeDelta = Grade - _gradeAtDayStart;
            BeginBusinessDay();
        }

        public void RestoreTotalKills(int value)
        {
            totalKills = Mathf.Max(0, value);
            LastGradeDelta = 0;
            LastDayKills = 0;
            BeginBusinessDay();
        }
    }
}
