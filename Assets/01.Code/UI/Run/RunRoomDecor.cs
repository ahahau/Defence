using System;
using Code.Run;
using UnityEngine;

namespace Code.UI.Run
{
    /// <summary>방 종류별 장치 그림 한 개. 자리는 방 왼쪽 위에서 잰 타일 칸(아래로 +)이다.</summary>
    [Serializable]
    public struct RunRoomDecor
    {
        [SerializeField] private RunRoomKind kind;
        [SerializeField] private RunArtPiece piece;
        [SerializeField] private Vector2 cell;

        public RunRoomDecor(RunRoomKind kind, RunArtPiece piece, Vector2 cell)
        {
            this.kind = kind;
            this.piece = piece;
            this.cell = cell;
        }

        public RunRoomKind Kind => kind;
        public RunArtPiece Piece => piece;
        public Vector2 Cell => cell;
    }
}
