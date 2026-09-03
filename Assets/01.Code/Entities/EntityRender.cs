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

        public SpriteRenderer SpriteRenderer => spriteRenderer;

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
                    Apply(defeatedSprite);
                    break;
                case EntityState.Attack:
                    Apply(attackSprite);
                    break;
                case EntityState.Idle:
                    Apply(idleSprite);
                    break;
            }
        }

        /// <summary>
        /// 비어 있는 포즈로는 갈아끼우지 않는다. 그대로 대입하면 스프라이트가 null이 되어
        /// 캐릭터가 화면에서 사라진다 — 포즈 그림이 아직 없는 쪽은 있던 모습을 유지하는 편이 낫다.
        /// </summary>
        private void Apply(Sprite sprite)
        {
            if (spriteRenderer != null && sprite != null)
                spriteRenderer.sprite = sprite;
        }
    }
}
