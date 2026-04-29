using UnityEngine;

namespace _01.Code.Buildings
{
    public class Building : MonoBehaviour
    {
        [field: SerializeField, Min(0)] public int DangerRating { get; private set; }

        public virtual void Initialize(BuildingDataSO data)
        {
            DangerRating = data.BaseDanger;
        }
    }
}
