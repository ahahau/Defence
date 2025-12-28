using System;
using _01.Code.Animation;
using _01.Code.Entities;
using UnityEngine;

namespace _01.Code.Enemies
{
    public class EnemyRender : EntityRender,  IEntityComponent
    {
        [SerializeField] private ParamSO backParam;
        [SerializeField] private ParamSO forwardParam;
        [SerializeField] private ParamSO sideParam;
        private Entity _entity;
        public void Initialize(Entity entity)
        {
            _entity = entity;
        }
        public void ChangeAnimation(Vector2 dir)
        {
            if (Mathf.Abs(dir.x) > Mathf.Abs(dir.y))
            {
                SetParameter(backParam, false);
                SetParameter(forwardParam, false); 
                SetParameter(sideParam, true);
                renderer.flipX = dir.x < 0;
            }
            else
            {
                if (dir.y > 0)
                {
                    SetParameter(backParam, false);
                    SetParameter(sideParam, false);
                    SetParameter(forwardParam, true);
                }
                else if (dir.y < 0)
                {
                    SetParameter(forwardParam, false);
                    SetParameter(sideParam, false);
                    SetParameter(backParam, true);
                }
            }
        }

    }
}