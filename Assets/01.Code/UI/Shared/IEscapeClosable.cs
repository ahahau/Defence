namespace Code.UI
{
    /// <summary>
    /// ESC로 닫을 수 있는 창. <see cref="UiEscapeRouter"/>가 위에 있는 창부터 하나씩 닫는다.
    /// 창마다 ESC를 따로 받으면 한 번 눌러 둘이 같이 닫혔다.
    /// </summary>
    public interface IEscapeClosable
    {
        bool IsOpen { get; }

        /// <summary>한 단계만 닫는다. 단계가 있는 창(방 관리)은 바깥쪽부터 하나씩 걷는다.</summary>
        void Close();
    }
}
