using Code.Core;
using Code.Events;
using UnityEngine;

namespace Code.UI
{
    /// <summary>
    /// 씬에 배선된 노드 채널에서 모험가 상태 요청을 듣고 UI Toolkit 화면(<c>EnemyStatusToolkitView</c>)으로 넘긴다.
    /// 화면은 Toolkit이 그리고, 이 컴포넌트는 채널을 잇는 다리만 맡는다.
    /// </summary>
    public class EnemyStatusPanelView : MonoBehaviour
    {
        public static EnemyStatusPanelView ActiveInstance { get; private set; }

        [SerializeField] private GameEventChannelSO nodeEventChannel;

        private bool isSubscribed;

        public void HidePanel() => Code.UI.Toolkit.EnemyStatusToolkitView.HideCurrent();

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
            Code.UI.Toolkit.EnemyStatusToolkitView.Show(evt.Enemy, evt.ScreenPosition);
        }
    }
}
