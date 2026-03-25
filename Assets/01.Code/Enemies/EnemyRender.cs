using System;
using _01.Code.Entities;
using _01.Code.Modules;
using UnityEngine;

namespace _01.Code.Enemies
{
    public class EnemyRender : EntityRender
    {
        private Entity _entity;
        public override void Initialize(ModuleOwner owner)
        {
            base.Initialize(owner);
            _entity = owner as Entity;
        }
        public void ChangeAnimation(Vector2 dir)
        {
            bool hasHorizontal = Mathf.Abs(dir.x) >= 0.01f;
            bool hasVertical = Mathf.Abs(dir.y) >= 0.01f;

            if (!hasHorizontal && !hasVertical)
            {
                return;
            }

            bool shouldFlip = dir.x < -0.01f || dir.y > 0.01f;
            bool isFlipped = FacingDirection < 0f;

            if (isFlipped != shouldFlip)
            {
                Flip();
            }
        }
    }
}
