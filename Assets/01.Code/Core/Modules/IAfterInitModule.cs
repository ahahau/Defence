namespace _01.Code.Core.Modules
{
    /// <summary>
    /// 다른 모듈이 다 물린 뒤에 한 번 더 불리는 모듈.
    ///
    /// Initialize 단계에서는 형제 모듈이 아직 준비되지 않았을 수 있다.
    /// 예를 들어 체력 모듈이 스탯 모듈의 값을 읽으려면, 스탯 모듈이 먼저
    /// 자기 표를 다 만든 뒤여야 한다. 그 "그다음"이 이 단계다.
    /// </summary>
    public interface IAfterInitModule
    {
        void AfterInitialize();
    }
}
