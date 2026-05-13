using _01.Code.Core;
using UnityEngine;

namespace _01.Code.Dialogue
{
    public abstract class DialogueActionSO : ScriptableObject
    {
        public abstract void Execute(DialogueActionContext context);
    }

    public readonly struct DialogueActionContext
    {
        public DialogueActionContext(
            DialogueRunner runner,
            GameEventChannelSO costEventChannel,
            DialogueValueTableSO valueTable)
        {
            Runner = runner;
            CostEventChannel = costEventChannel;
            ValueTable = valueTable;
        }

        public DialogueRunner Runner { get; }
        public GameEventChannelSO CostEventChannel { get; }
        public DialogueValueTableSO ValueTable { get; }
    }
}
