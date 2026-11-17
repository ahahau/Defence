namespace Code.UI.Run
{
    /// <summary>평면도 한 칸이 어떤 타일인지(<see cref="RunFloorTileRules"/>).</summary>
    public enum RunFloorCellKind
    {
        /// <summary>벽 윗면(테두리 조각).</summary>
        Wall,
        Floor,
        /// <summary>벽돌 면 바로 아래 바닥.</summary>
        LitFloor,
        /// <summary>바닥 바로 위 벽의 벽돌 면.</summary>
        Face,
    }
}
