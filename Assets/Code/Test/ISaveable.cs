namespace Code.Test
{
    public interface ISaveable
    {
        SaveID SaveId { get; }
        string GetSaveData();
        void RestoreData(string savedData);
    }
}