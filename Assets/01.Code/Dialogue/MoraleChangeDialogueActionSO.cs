using _01.Code.Core;
using _01.Code.Events;
using UnityEngine;

namespace _01.Code.Dialogue
{
    [CreateAssetMenu(menuName = "SO/Dialogue/Actions/Morale Change", fileName = "MoraleChangeDialogueAction", order = 1)]
    public class MoraleChangeDialogueActionSO : DialogueActionSO
    {
        [SerializeField] private GameEventChannelSO managementEventChannel;
        [SerializeField] private int amount;
        [SerializeField] private string reason = "이벤트";

        public override void Execute(DialogueActionContext context)
        {
            if (amount == 0 || managementEventChannel == null)
                return;

            managementEventChannel.RaiseEvent(new MoraleChangeRequestedEvent(amount, reason));
        }
    }
}
