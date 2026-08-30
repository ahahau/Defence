namespace _01.Code.Persistence
{
    /// <summary>
    /// 자기 상태를 스스로 담고 되돌리는 것.
    ///
    /// 예전에는 RunSaveSystem 하나가 금화·민심·명부·유물·상인·정복·던전을 전부
    /// 직접 찾아가 값을 꺼내 왔다. 시스템이 하나 늘 때마다 저장 코드가 같이 자랐고,
    /// 무엇보다 저장이 통짜 파일 하나라 복원이 한 군데서 실패해도 파일 전체가
    /// 의심스러워졌다 — 실제로 그것 때문에 저장 기능을 꺼 두어야 했다.
    ///
    /// 각자 자기 몫만 문자열로 내놓으면 실패가 그 열쇠 하나에 갇힌다.
    /// </summary>
    public interface ISaveable
    {
        /// <summary>저장 파일에서 이 조각을 찾는 열쇠. 시스템마다 고유해야 한다.</summary>
        string SaveKey { get; }

        /// <summary>지금 상태를 문자열로. 담을 것이 없으면 빈 문자열을 돌려준다.</summary>
        string GetSaveData();

        /// <summary>저장해 둔 문자열로 되돌린다. 빈 문자열이면 아무것도 하지 않는다.</summary>
        void RestoreData(string savedData);
    }
}
