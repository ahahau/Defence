using UnityEngine;

namespace Code.MapCreateSystem
{
    /// <summary>
    /// 방 위·아래 벽의 문. 평소엔 닫혀 있다가 모험가가 지나가면 열리고,
    /// 아무도 지나가지 않으면 잠시 뒤 닫힌다. 그림은 Dark Castle 문 애니메이션(닫힘 → 열림 16장)이다.
    /// 옆 벽은 3/4 시점에서 문 면이 보이지 않아 문 대신 양쪽에 기둥을 세운다(<see cref="RoomTilePlan"/>).
    /// </summary>
    public sealed class RoomDoorView : MonoBehaviour
    {
        /// <summary>한 장을 넘기는 시간(초, 실제 시간이 아니라 게임 시간).</summary>
        private const float FrameSeconds = 0.025f;

        /// <summary>마지막으로 열린 뒤 닫히기 시작할 때까지(초).</summary>
        private const float HoldOpenSeconds = 1.2f;

        private SpriteRenderer _renderer;
        private Sprite[] _frames;
        private float _frame;
        private float _openUntil;

        public bool IsOpen => _frames != null && _frame >= _frames.Length - 1;

        /// <summary>그림·그리기 순서를 정한다. 방 노드가 문을 만들 때 부른다.</summary>
        public void Configure(Sprite[] frames, int sortingLayerId, int sortingOrder)
        {
            _frames = frames;
            _renderer ??= gameObject.AddComponent<SpriteRenderer>();
            _renderer.sortingLayerID = sortingLayerId;
            _renderer.sortingOrder = sortingOrder;
            _frame = 0f;
            ApplyFrame();
        }

        /// <summary>문을 연다. 지나가는 동안 여러 번 불러도 되고, 마지막으로 부른 뒤 잠시 열어 둔다.</summary>
        public void Open() => _openUntil = Time.time + HoldOpenSeconds;

        private void Update()
        {
            if (_frames == null || _frames.Length == 0)
                return;

            var target = Time.time < _openUntil ? _frames.Length - 1 : 0f;
            if (Mathf.Approximately(_frame, target))
                return;

            _frame = Mathf.MoveTowards(_frame, target, Time.deltaTime / FrameSeconds);
            ApplyFrame();
        }

        private void ApplyFrame()
        {
            if (_renderer == null || _frames == null || _frames.Length == 0)
                return;

            _renderer.sprite = _frames[Mathf.Clamp(Mathf.RoundToInt(_frame), 0, _frames.Length - 1)];
        }
    }
}
