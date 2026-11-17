namespace Code.Progression
{
    /// <summary>판이 끝나며 정산된 메타 결과. 결과 화면이 읽는다.</summary>
    public readonly struct MetaRunResult
    {
        public MetaRunResult(int pointsEarned, int totalProgress, int daysSurvived, bool newBestDay, int slotsBefore, int slotsAfter,
            int bossesDefeated = 0, int previousBestDay = 0)
        {
            PointsEarned = pointsEarned;
            TotalProgress = totalProgress;
            DaysSurvived = daysSurvived;
            NewBestDay = newBestDay;
            SlotsBefore = slotsBefore;
            SlotsAfter = slotsAfter;
            BossesDefeated = bossesDefeated;
            PreviousBestDay = previousBestDay;
        }

        public int PointsEarned { get; }
        public int TotalProgress { get; }
        public int DaysSurvived { get; }
        public bool NewBestDay { get; }
        public int SlotsBefore { get; }
        public int SlotsAfter { get; }

        /// <summary>이번 판에 잡은 주간 보스 수. 정산 뒤에는 이번 판 기록이 비워지므로 여기 남긴다.</summary>
        public int BossesDefeated { get; }

        /// <summary>이번 판 전의 최고 버틴 날. 새 기록이면 기록이 이미 덮였으므로 여기 남긴다.</summary>
        public int PreviousBestDay { get; }

        /// <summary>이번 판을 더하기 전의 누적 진행도.</summary>
        public int ProgressBefore => TotalProgress - PointsEarned;
    }
}
