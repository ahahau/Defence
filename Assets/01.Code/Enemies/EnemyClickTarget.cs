using _01.Code.Core;
using _01.Code.Events;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace _01.Code.Enemies
{
    public class EnemyClickTarget : MonoBehaviour
    {
        [SerializeField] private Enemy enemy;
        [SerializeField] private GameEventChannelSO nodeEventChannel;

        public void Initialize(Enemy targetEnemy, GameEventChannelSO eventChannel)
        {
            enemy = targetEnemy;
            nodeEventChannel = eventChannel;
            EnsureCollider();
        }

        private void Awake()
        {
            if (enemy == null)
                enemy = GetComponent<Enemy>();

            EnsureCollider();
        }

        private void OnMouseOver()
        {
            if (Mouse.current == null || !Mouse.current.rightButton.wasPressedThisFrame)
                return;

            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                return;

            if (enemy == null || nodeEventChannel == null)
                return;

            nodeEventChannel.RaiseEvent(new EnemyStatusRequestedEvent(enemy));
        }

        private void EnsureCollider()
        {
            if (TryGetComponent<Collider2D>(out _))
                return;

            var boxCollider = gameObject.AddComponent<BoxCollider2D>();
            boxCollider.size = new Vector2(0.8f, 0.8f);
            boxCollider.offset = new Vector2(0f, 0.25f);
            boxCollider.isTrigger = true;
        }
    }
}
