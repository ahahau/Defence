using UnityEngine;

namespace Code.Stages
{
    /// <summary>조회만으로 얻는 배치/철거 표시 값. 기본값은 불가 상태다.</summary>
    public struct StagePlacementCheck
    {
        public bool Allowed { get; private set; }
        public Vector2Int Anchor { get; private set; }
        public Vector2Int Footprint { get; private set; }
        public int Cost { get; private set; }
        public string Reason { get; private set; }

        public StagePlacementCheck(Vector2Int anchor, Vector2Int footprint, int cost, string reason = "")
        {
            Anchor = anchor;
            Footprint = footprint;
            Cost = cost;
            Reason = reason;
            Allowed = string.IsNullOrEmpty(reason);
        }
    }
}
