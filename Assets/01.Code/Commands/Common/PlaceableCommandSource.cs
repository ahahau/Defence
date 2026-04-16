using System.Collections.Generic;
using _01.Code.Tiles;
using UnityEngine;

namespace _01.Code.Commands
{
    public class PlaceableCommandSource : MonoBehaviour
    {
        [field: SerializeField] public BattleObstacleDataSO Data { get; private set; }
        [field: SerializeField] public string FallbackCommandTitle { get; private set; } = "COMMAND";
        [field: SerializeField] public List<BaseCommandSO> FallbackCommands { get; private set; } = new();

        public string CommandTitle
        {
            get { return Data != null && !string.IsNullOrWhiteSpace(Data.CommandTitle) ? Data.CommandTitle : FallbackCommandTitle; }
        }

        public List<BaseCommandSO> Commands
        {
            get { return Data != null ? Data.Commands : FallbackCommands; }
        }

        public bool HasCommands
        {
            get { return Commands != null && Commands.Count > 0; }
        }
    }
}
