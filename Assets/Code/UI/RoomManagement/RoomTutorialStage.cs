namespace Code.UI.RoomManagement
{
    /// <summary>튜토리얼이 설치 흐름에서 지금 비출 곳.</summary>
    public enum RoomTutorialStage
    {
        /// <summary>설치 흐름 밖(방을 아직 안 골랐거나 위치를 고르는 중).</summary>
        None,
        /// <summary>하단 행동 바의 갈래 버튼.</summary>
        PickCategory,
        /// <summary>설치 창의 카드.</summary>
        PickCard,
        /// <summary>설치 창의 설치(배치) 버튼.</summary>
        PressInstall,
    }
}
