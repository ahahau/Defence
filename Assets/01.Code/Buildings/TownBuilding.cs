using _01.Code.Tiles;
using UnityEngine;
using UnityEngine.EventSystems;

namespace _01.Code.Buildings
{
    public class TownBuilding : TownTileObject
    {
        private MainBuildingRoomWorld _townWorld;

        protected override void Awake()
        {
            base.Awake();
            _townWorld = FindFirstObjectByType<MainBuildingRoomWorld>(FindObjectsInactive.Include);
        }

        private void OnMouseDown()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                return;
            }

            _townWorld ??= FindFirstObjectByType<MainBuildingRoomWorld>(FindObjectsInactive.Include);
            _townWorld?.TryHandleTileObjectClick(this);
        }
    }
}
