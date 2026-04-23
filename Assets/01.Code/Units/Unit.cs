using _01.Code.Combat;
using UnityEngine;

namespace _01.Code.Units
{
    public class Unit : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer spriteRenderer;

        public Combatant Combatant { get; private set; }

        public void Initialize(UnitDataSO unitData)
        {
            spriteRenderer.sprite = unitData.Sprite;
            Combatant = GetComponent<Combatant>();
            if (Combatant == null)
                Combatant = gameObject.AddComponent<Combatant>();
        }
    }
}
