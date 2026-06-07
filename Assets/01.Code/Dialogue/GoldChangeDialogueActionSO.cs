using _01.Code.Events;
using _01.Code.Core;
using UnityEngine;

namespace _01.Code.Dialogue
{
    [CreateAssetMenu(menuName = "SO/Dialogue/Actions/Gold Change", fileName = "GoldChangeDialogueAction", order = 0)]
    public class GoldChangeDialogueActionSO : DialogueActionSO
    {
        [SerializeField] private GameEventChannelSO costEventChannel;
        [SerializeField] private int amount;

        public override void Execute(DialogueActionContext context)
        {
            var channel = costEventChannel != null ? costEventChannel : context.CostEventChannel;
            if (amount == 0 || channel == null)
                return;

            if (amount > 0)
                channel.RaiseEvent(new GoldEarnedEvent(amount, GoldChangeSource.Dialogue));
            else
                channel.RaiseEvent(new GoldLostEvent(-amount, GoldChangeSource.Dialogue));
        }
    }
}
