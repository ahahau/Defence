using _01.Code.Units;
using TMPro;
using UnityEngine;

namespace _01.Code.UI
{
    public sealed class PersonalityTooltipView : MonoBehaviour
    {
        private static PersonalityTooltipView instance;

        [SerializeField] private GameObject panelRoot;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text descriptionText;
        [SerializeField] private Vector2 screenOffset = new(16f, -16f);

        public static void ShowFor(Unit unit, UnitDetailTooltipKind kind, Vector2 screenPosition, Canvas ownerCanvas)
        {
            if (unit == null)
                return;

            var view = EnsureInstance(ownerCanvas);
            if (view == null)
                return;

            if (kind == UnitDetailTooltipKind.Trait)
            {
                view.titleText.text = $"특성 · {unit.TraitLabel}";
                view.descriptionText.text = unit.TraitDescription;
            }
            else
            {
                view.titleText.text = $"성격 · {unit.PersonalityLabel}";
                view.descriptionText.text = unit.PersonalityDescription;
            }
            if (view.panelRoot != null)
            {
                view.panelRoot.SetActive(true);
                view.panelRoot.transform.SetAsLastSibling();
                if (view.panelRoot.transform is RectTransform rect)
                    view.PlaceAt(rect, screenPosition + view.screenOffset, ownerCanvas);
            }
        }

        public static void HideCurrent()
        {
            if (instance?.panelRoot != null)
                instance.panelRoot.SetActive(false);
        }

        private static PersonalityTooltipView EnsureInstance(Canvas ownerCanvas)
        {
            if (instance != null)
                return instance;

            // The tooltip is part of the authored HUD hierarchy.  Prefer the inactive
            // scene instance so Play mode never adds a second runtime-only panel.
            foreach (var sceneView in SceneUiRegistry.EnumerateLoaded<PersonalityTooltipView>())
            {
                if (sceneView == null)
                    continue;

                instance = sceneView;
                return instance;
            }

            // Tooltip UI is authored as part of the scene HUD. Runtime fallback creation
            // hides broken prefab/scene bindings and makes the hierarchy diverge in Play mode.
            Debug.LogWarning("Personality tooltip scene prefab is missing.");
            return null;
        }

        private void PlaceAt(RectTransform rect, Vector2 screenPosition, Canvas ownerCanvas)
        {
            if (ownerCanvas == null || ownerCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                rect.position = screenPosition;
                return;
            }

            var canvasRect = ownerCanvas.transform as RectTransform;
            if (canvasRect == null)
                return;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect,
                screenPosition,
                ownerCanvas.worldCamera,
                out var localPosition);
            rect.anchoredPosition = localPosition;
        }

        private void Awake()
        {
            instance = this;
            panelRoot ??= gameObject;
            panelRoot.SetActive(false);
        }
    }
}
