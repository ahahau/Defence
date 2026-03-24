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
            _entity = owner as Entity;
        }
        public void ChangeAnimation(Vector2 dir)
        {
            if (Mathf.Abs(dir.x) > Mathf.Abs(dir.y))
            {
                FlipController(dir.x);
            }
            else
            {
                Flip();
            }
        }
    }
}