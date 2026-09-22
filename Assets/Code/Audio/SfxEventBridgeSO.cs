using Code.Core;
using UnityEngine;

namespace Code.Audio
{
    /// <summary>
    /// 게임 이벤트를 효과음에 이어주는 다리.
    ///
    /// 이벤트 채널은 <c>Assets/03.SO/Event</c>에 흩어져 있어 코드에서 바로 못 집는다.
    /// 이 에셋만 Resources에 두고 채널들을 참조하게 하면, 씬을 건드리지 않고도
    /// 전역에서 구독할 수 있다 — 씬에 컴포넌트를 새로 붙이면 다른 작업과 충돌한다.
    /// </summary>
    [CreateAssetMenu(fileName = "SfxEventBridge", menuName = "Defence/Audio/SFX Event Bridge")]
    public sealed class SfxEventBridgeSO : ScriptableObject
    {
        [field: SerializeField, Tooltip("구독할 이벤트 채널들. 어느 채널로 오는지 확실치 않은 이벤트가 있어 전부 건다.")]
        public GameEventChannelSO[] Channels { get; private set; } = new GameEventChannelSO[0];
    }
}
