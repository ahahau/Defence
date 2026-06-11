using _01.Code.Core;
using UnityEngine;

namespace _01.Code.Entities
{
    public enum EntityState
    {
        Idle,
        Attack,
        Defeated
    }
    public class EntityRender : MonoBehaviour
    {
        [SerializeField] private GameEventChannelSO gameStateEventChannel;
        [SerializeField] private Sprite idleSprite;
        [SerializeField] private Sprite attackSprite;
        [SerializeField] private Sprite defeatedSprite;
        [SerializeField] private SpriteRenderer spriteRenderer;

        public void ConfigureSprites(Sprite idle, Sprite attack, Sprite defeated)
        {
            if (idle != null)
                idleSprite = idle;

            if (attack != null)
                attackSprite = attack;

            if (defeated != null)
                defeatedSprite = defeated;

            SetUnitSprite(EntityState.Idle);
        }

        public void SetUnitSprite(EntityState state = EntityState.Defeated)
        {
            switch (state)
            {
                case EntityState.Defeated:
                    spriteRenderer.sprite = defeatedSprite;
                    break;
                case EntityState.Attack:
                    spriteRenderer.sprite = attackSprite;
                    break;
                case EntityState.Idle:
                    spriteRenderer.sprite = idleSprite;
                    break;
            }
        }
    }
}
