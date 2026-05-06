using _01.Code.Core;
using UnityEngine;

namespace _01.Code.Entities
{
    public enum EntityState
    {
        Idle,
        Hit,
        Defeated
    }
    public class EntityRender : MonoBehaviour
    {
        [SerializeField] private GameEventChannelSO gameStateEventChannel;
        [SerializeField] private Sprite idleSprite;
        [SerializeField] private Sprite hitSprite;
        [SerializeField] private Sprite defeatedSprite;
        [SerializeField] private SpriteRenderer spriteRenderer;
        
        
        public void SetUnitSprite(EntityState state = EntityState.Defeated)
        {
            switch (state)
            {
                case EntityState.Defeated:
                    spriteRenderer.sprite = defeatedSprite;
                    break;
                case EntityState.Hit:
                    spriteRenderer.sprite = hitSprite;
                    break;
                case EntityState.Idle:
                    spriteRenderer.sprite = idleSprite;
                    break;
            }
        }
    }
}