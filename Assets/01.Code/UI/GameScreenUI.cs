using UnityEngine;

namespace _01.Code.UI
{
    public class GameScreenUI : MonoBehaviour
    {
        [SerializeField] private HudSectionUIController hudSection;
        [SerializeField] private BuildSectionUIController buildSection;

        private void Awake()
        {
            CacheReferences();
            hudSection?.InitializeSection();
        }

        private void CacheReferences()
        {
            hudSection ??= GetComponent<HudSectionUIController>();
            if (buildSection == null)
            {
                buildSection = GetComponentInChildren<BuildSectionUIController>(true);
            }
        }
    }
}
