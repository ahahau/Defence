using UnityEngine;

namespace _01.Code.Progression
{
    /// <summary>
    /// 한 판 내내 쌓이는 전과. WaveManager의 집계는 웨이브마다 초기화되므로
    /// 판이 끝났을 때 돌아볼 숫자가 남지 않는다. 결과 화면이 이 값을 읽는다.
    ///
    /// 정적 클래스로 두면 도메인 리로드를 꺼 둔 에디터에서 지난 판의 숫자가 살아남는다.
    /// 컴포넌트로 두면 씬과 함께 사라지므로 한 판의 수명과 정확히 맞는다.
    /// </summary>
    public sealed class RunSummarySystem : MonoBehaviour
    {
        public static RunSummarySystem Current { get; private set; }

        public int WavesFought { get; private set; }
        public int Invaders { get; private set; }
        public int Kills { get; private set; }
        public int DamageDealt { get; private set; }
        public int DamageTaken { get; private set; }
        public int CriticalHits { get; private set; }

        /// <summary>이 판에서 빚이 가장 많았을 때의 액수. 얼마나 아슬아슬했는지가 남는다.</summary>
        public int PeakDebt { get; private set; }

        private void Awake()
        {
            if (Current != null && Current != this)
            {
                Debug.LogError($"Duplicate {nameof(RunSummarySystem)} detected. Keep exactly one scene instance.", this);
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

        public void ResetSummary()
        {
            WavesFought = 0;
            Invaders = 0;
            Kills = 0;
            DamageDealt = 0;
            DamageTaken = 0;
            CriticalHits = 0;
            PeakDebt = 0;
        }

        public void RecordWave(int invaders, int kills, int damageDealt, int damageTaken, int criticalHits)
        {
            // 포탈이 없어 웨이브가 서지 않은 날은 전과로 치지 않는다.
            if (invaders <= 0)
                return;

            WavesFought++;
            Invaders += Mathf.Max(0, invaders);
            Kills += Mathf.Max(0, kills);
            DamageDealt += Mathf.Max(0, damageDealt);
            DamageTaken += Mathf.Max(0, damageTaken);
            CriticalHits += Mathf.Max(0, criticalHits);
        }

        public void RecordDebt(int debt)
        {
            PeakDebt = Mathf.Max(PeakDebt, Mathf.Max(0, debt));
        }
    }
}
