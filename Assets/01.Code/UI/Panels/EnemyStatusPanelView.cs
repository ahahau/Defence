using Code.Core;
using Code.Events;
using UnityEngine;

namespace Code.UI
{
    /// <summary>
    /// 씬에 배선된 노드 채널에서 모험가 상태 요청을 듣고 화면에 넘긴다. 이 컴포넌트는 채널을 잇는 다리만 맡는다.
    /// 그리는 화면은 <see cref="EnemyStatusView"/>(uGUI)다 — <see cref="ShowRequested"/>·<see cref="HideRequested"/>를 듣는다.
    /// </summary>
    public class EnemyStatusPanelView : MonoBehaviour
    {
        public static EnemyStatusPanelView ActiveInstance { get; private set; }

        [SerializeField] private GameEventChannelSO nodeEventChannel;

        private bool isSubscribed;

/// <summary>모험가 상태 창을 연다(대상, 누른 화면 좌표).</summary>
        public event System.Action<Code.Enemies.Enemy, Vector2> ShowRequested;

        public event System.Action HideRequested;

        public void HidePanel() => HideRequested?.Invoke();

        /// <summary>지도 밖(기록판 파티 줄)에서 모험가 상태 창을 연다. 화면 좌표는 왼쪽 아래 원점.</summary>
        public void RequestShow(Code.Enemies.Enemy enemy, Vector2 screenPosition)
        {
            if (enemy == null)
                return;

            UnitStatusPanelView.ActiveInstance?.HidePanel();
            ShowRequested?.Invoke(enemy, screenPosition);
        }

        private void Awake()
        {
            ActiveInstance = this;
        }

        private void OnEnable() => Subscribe();

        private void OnDisable() => Unsubscribe();

        private void OnDestroy()
        {
            if (ActiveInstance == this)
                ActiveInstance = null;
        }

        private void Subscribe()
        {
            if (isSubscribed || nodeEventChannel == null)
                return;

            nodeEventChannel.AddListener<EnemyStatusRequestedEvent>(HandleEnemyStatusRequested);
            isSubscribed = true;
        }

        private void Unsubscribe()
        {
            if (!isSubscribed || nodeEventChannel == null)
                return;

            nodeEventChannel.RemoveListener<EnemyStatusRequestedEvent>(HandleEnemyStatusRequested);
            isSubscribed = false;
        }

        private void HandleEnemyStatusRequested(EnemyStatusRequestedEvent evt)
        {
            if (evt.Enemy == null)
                return;

            UnitStatusPanelView.ActiveInstance?.HidePanel();
            ShowRequested?.Invoke(evt.Enemy, evt.ScreenPosition);
        }
    }
}
