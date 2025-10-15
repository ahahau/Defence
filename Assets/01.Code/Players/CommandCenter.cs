using System;
using _01.Code.Entities;
using _01.Code.PlaceableObjects;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace _01.Code.Players
{
    public class CommandCenter : PlaceableEntity, IDamageable
    {
        public void ApplyDamage(int damage, Entity dealer)
        {
            maxHp -= damage;
            dealer.OnDeath?.Invoke();
        }
        public override void Initialize(Vector2Int position = default)
        {
            position = new Vector2Int((int)transform.position.x, (int)transform.position.y);
            base.Initialize(position);
        }
    }
}