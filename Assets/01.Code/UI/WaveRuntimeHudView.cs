using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Serialization;

namespace _01.Code.UI
{
    public sealed class WaveRuntimeHudView : MonoBehaviour
    {
        [Header("Wave Banner")]
        [SerializeField] private GameObject bannerRoot;
        [SerializeField] private CanvasGroup bannerGroup;
        [SerializeField] private TMP_Text bannerTitle;
        [SerializeField] private TMP_Text bannerSubtitle;

        [Header("Wave Progress")]
        [SerializeField] private GameObject progressRoot;
        [SerializeField] private CanvasGroup progressGroup;
        [SerializeField] private TMP_Text progressTitle;
        [SerializeField] private TMP_Text progressStats;
        [SerializeField] private Image progressFill;

        [Header("Dungeon Core Status")]
        [FormerlySerializedAs("nestStatusRoot"), SerializeField] private GameObject coreStatusRoot;
        [FormerlySerializedAs("nestPhase"), SerializeField] private TMP_Text corePhase;
        [FormerlySerializedAs("nestGoal"), SerializeField] private TMP_Text coreGoal;
        [FormerlySerializedAs("nestForecast"), SerializeField] private TMP_Text coreForecast;

        public GameObject BannerRoot => bannerRoot;
        public CanvasGroup BannerGroup => bannerGroup;
        public TMP_Text BannerTitle => bannerTitle;
        public TMP_Text BannerSubtitle => bannerSubtitle;
        public GameObject ProgressRoot => progressRoot;
        public CanvasGroup ProgressGroup => progressGroup;
        public TMP_Text ProgressTitle => progressTitle;
        public TMP_Text ProgressStats => progressStats;
        public Image ProgressFill => progressFill;
        public GameObject CoreStatusRoot => coreStatusRoot;
        public TMP_Text CorePhase => corePhase;
        public TMP_Text CoreGoal => coreGoal;
        public TMP_Text CoreForecast => coreForecast;
    }
}
