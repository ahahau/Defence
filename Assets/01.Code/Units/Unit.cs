using UnityEngine;

namespace _01.Code.Units
{
    public class Unit : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer spriteRenderer;

        public void Initialize(UnitDataSO unitData)
        {
            spriteRenderer.sprite = unitData.Sprite;
        }
    }
}