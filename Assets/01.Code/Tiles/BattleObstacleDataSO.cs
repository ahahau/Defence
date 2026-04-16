using System.Collections.Generic;
using _01.Code.Commands;
using _01.Code.Entities;
using UnityEngine;

namespace _01.Code.Tiles
{
    public class BattleObstacleDataSO : ScriptableObject
    {
        [field: SerializeField] public string SaveKey { get; private set; }
        [field: SerializeField] public PlaceableEntity Prefab { get; private set; }
        [field: SerializeField] public string DisplayName { get; private set; }
        [field: SerializeField] public string Description { get; private set; }
        [field: SerializeField] public Sprite Icon { get; private set; }
        [field: SerializeField] public string CommandTitle { get; private set; } = "BUILD";
        [field: SerializeField] public List<BaseCommandSO> Commands { get; private set; } = new();
    }
}
