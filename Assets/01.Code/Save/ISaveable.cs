namespace _01.Code.Save
{
    public interface ISaveable
    {
        string SaveKey { get; }
        int RestoreOrder { get; }
        string GetSaveData();
        void RestoreData(string savedData);
    }
}
