using UnityEngine;

namespace Code.Manager
{
    /// <summary>
    /// 보스별 패배 조건.
    ///
    /// 보스는 탈출하지 않고 버티면 결국 쓰러뜨릴 수 있어서, 보스날이 "오래 걸리는 날"일 뿐이었다.
    /// 보스마다 제한 시간·금고 방 침입·부하 손실 같은 다른 패배 조건이 있으면, 그 보스를 어떻게 막을지
    /// 미리 배치를 바꿔야 한다. 조건은 전날 정산에서 예고하고 보스 날 HUD에 남는다.
    /// </summary>
    public static class BossDefeatRules
    {
        /// <summary>패배 조건을 만족했으면 패배 화면에 적을 이유를, 아니면 null을 돌려준다.</summary>
        public static string Check(
            string bossTitle,
            float elapsedSeconds,
            float timeLimitSeconds,
            bool treasuryBreached,
            bool loseOnTreasuryBreach,
            int unitsDowned,
            int loseAfterUnitsDowned)
        {
            var name = string.IsNullOrWhiteSpace(bossTitle) ? "보스" : bossTitle;
            if (timeLimitSeconds > 0f && elapsedSeconds >= timeLimitSeconds)
                return $"{name}을(를) {Mathf.RoundToInt(timeLimitSeconds)}초 안에 쓰러뜨리지 못해 던전이 무너졌습니다";
            if (loseOnTreasuryBreach && treasuryBreached)
                return $"{name}이(가) 금고 방에 들어와 던전이 무너졌습니다";
            if (loseAfterUnitsDowned > 0 && unitsDowned >= loseAfterUnitsDowned)
                return $"{name} 앞에서 부하 {loseAfterUnitsDowned}명이 쓰러져 던전이 무너졌습니다";
            return null;
        }

        /// <summary>예고·HUD에 적을 조건 목록. 조건이 없으면 빈 문자열.</summary>
        public static string Describe(float timeLimitSeconds, bool loseOnTreasuryBreach, int loseAfterUnitsDowned)
        {
            var text = string.Empty;
            if (timeLimitSeconds > 0f)
                text = Join(text, $"{Mathf.RoundToInt(timeLimitSeconds)}초 안에 처치");
            if (loseOnTreasuryBreach)
                text = Join(text, "금고 방 침입 시 패배");
            if (loseAfterUnitsDowned > 0)
                text = Join(text, $"부하 {loseAfterUnitsDowned}명 쓰러지면 패배");
            return text;
        }

        /// <summary>보스 날 HUD에 적을 진행 상황. 남은 시간과 쓰러진 부하 수를 같이 보여 준다.</summary>
        public static string DescribeProgress(
            float elapsedSeconds, float timeLimitSeconds, bool loseOnTreasuryBreach, int unitsDowned, int loseAfterUnitsDowned)
        {
            var text = string.Empty;
            if (timeLimitSeconds > 0f)
                text = Join(text, $"남은 시간 {Mathf.Max(0, Mathf.CeilToInt(timeLimitSeconds - elapsedSeconds))}초");
            if (loseOnTreasuryBreach)
                text = Join(text, "금고 방 침입 금지");
            if (loseAfterUnitsDowned > 0)
                text = Join(text, $"부하 손실 {unitsDowned}/{loseAfterUnitsDowned}");
            return text;
        }

        private static string Join(string current, string next) =>
            string.IsNullOrEmpty(current) ? next : current + " · " + next;
    }
}
