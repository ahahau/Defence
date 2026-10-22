using Code.Entities;
using UnityEngine;

namespace Code.Units
{
    [CreateAssetMenu(menuName = "SO/Unit/Data", fileName = "UnitData", order = 0)]
    public class UnitDataSO : EntityDataSO
    {
        [field:SerializeField] public Sprite Sprite { get; private set; }
        [field: SerializeField] public Unit Prefab { get; private set; }
        [field:SerializeField] public string Name { get; private set; }
        [field:SerializeField] public int Cost { get; private set; }
        [field: SerializeField, Min(1)] public int MagicCost { get; private set; } = 1;
        [field:SerializeField] public bool Locked { get; private set; }
        [field: SerializeField, Min(0)] public int BaseDanger { get; private set; } = 1;
        [field: SerializeField, Min(0)] public int DangerIncreaseOnCombat { get; private set; } = 1;

        [System.NonSerialized] private Sprite _icon;

        /// <summary>
        /// 화면에 쓰는 유닛 아이콘: 프리팹 애니메이션의 대기 첫 프레임 → 고용 그림 → 보드 그림 순.
        /// 보드 그림이 부하 모두 임시 네모라 배치 카드에 빈 상자가 보였다. 새 유닛도 그림을 따로 넣지 않아도 맞게 나온다.
        /// </summary>
        public Sprite IconSprite
        {
            get
            {
                if (_icon != null)
                    return _icon;

                var render = Prefab != null ? Prefab.GetComponentInChildren<EntityRender>(true) : null;
                var idle = render != null && render.AnimationSet != null ? render.AnimationSet.FirstIdleFrame : null;
                _icon = idle != null ? idle : Sprite != null ? Sprite : BoardSprite;
                return _icon;
            }
        }
    }
}
