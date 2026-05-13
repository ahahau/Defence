using _01.Code.Events;
using UnityEngine;

namespace _01.Code.Dialogue
{
    [CreateAssetMenu(menuName = "SO/Dialogue/Actions/Gold Change", fileName = "GoldChangeDialogueAction", order = 0)]
    public class GoldChangeDialogueActionSO : DialogueActionSO
    {
        [SerializeField] private int amount;

        public override void Execute(DialogueActionContext context)
        {
            if (amount == 0 || context.CostEventChannel == null)
                return;

            if (amount > 0)
                context.CostEventChannel.RaiseEvent(new GoldEarnedEvent(amount));
            else
                context.CostEventChannel.RaiseEvent(new GoldLostEvent(-amount));
        }
    }
}
