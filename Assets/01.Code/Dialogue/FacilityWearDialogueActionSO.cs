using Code.Manager;
using UnityEngine;

namespace Code.Dialogue
{
    /// <summary>
    /// 사건이 던전의 모든 수익 시설 내구도를 깎는다(화재·붕괴 같은 사고).
    /// 사건이 금화·사기만 바꾸면 시설을 아무리 지어도 사건과 상관이 없어서, 시설을 가진 만큼 아픈 결과를 만든다.
    /// </summary>
    [CreateAssetMenu(menuName = "SO/Dialogue/Actions/Facility Wear", fileName = "FacilityWearDialogueAction", order = 2)]
    public class FacilityWearDialogueActionSO : DialogueActionSO
    {
        [SerializeField, Min(0), Tooltip("수익 시설마다 깎을 내구도.")]
        private int amount = 3;

        public int Amount => amount;

        public override void Execute(DialogueActionContext context)
        {
            if (amount <= 0)
                return;

            FacilityEconomyRules.WearAllFacilities(amount);
        }
    }
}
