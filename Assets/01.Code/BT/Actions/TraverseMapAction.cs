using System;
using _01.Code.Enemies;
using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;

namespace _01.Code.BT
{
    /// <summary>적이 던전 노드를 순회한다(이동/공포·탐욕 무드/귀환/함정/약탈 — 기존 Enemy 로직 재사용).
    /// 매 평가마다 한 스텝 처리 후 Success를 반환해, 루프가 다시 돌며 전투 분기를 재검사하게 한다.</summary>
    [Serializable, Unity.Properties.GeneratePropertyBag]
    [NodeDescription(
        name: "Traverse Map",
        description: "Roams dungeon nodes (movement, mood, return, traps, looting). Enemy only.",
        story: "[Agent] traverses the map",
        category: "Action/Battle",
        id: "a2d6b8f40c1e4a7c9b3f5e1d70428c63")]
    public partial class TraverseMapAction : Action
    {
        private Enemy _enemy;

        protected override Status OnStart()
        {
            _enemy = GameObject?.GetComponentInParent<Enemy>();
            if (_enemy == null) return Status.Failure;

            _enemy.TickTraversal(Time.deltaTime);
            return Status.Success;
        }
    }
}
