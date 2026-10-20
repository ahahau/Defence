namespace Code.Progression
{
    /// <summary>판이 끝나며 정산된 메타 결과. 결과 화면이 읽는다.</summary>
    public readonly struct MetaRunResult
    {
        public MetaRunResult(int pointsEarned, int totalProgress, int daysSurvived, bool newBestDay, int slotsBefore, int slotsAfter)
        {
            PointsEarned = pointsEarned;
            TotalProgress = totalProgress;
            DaysSurvived = daysSurvived;
            NewBestDay = newBestDay;
            SlotsBefore = slotsBefore;
            SlotsAfter = slotsAfter;
        }

        public int PointsEarned { get; }
        public int TotalProgress { get; }
        public int DaysSurvived { get; }
        public bool NewBestDay { get; }
        public int SlotsBefore { get; }
        public int SlotsAfter { get; }
    }
}
