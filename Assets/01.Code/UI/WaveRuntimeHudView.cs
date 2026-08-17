using TMPro;
using UnityEngine;
using UnityEngine.UI;

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

        [Header("Nest Status")]
        [SerializeField] private GameObject nestStatusRoot;
        [SerializeField] private TMP_Text nestPhase;
        [SerializeField] private TMP_Text nestGoal;
        [SerializeField] private TMP_Text nestForecast;

        public GameObject BannerRoot => bannerRoot;
        public CanvasGroup BannerGroup => bannerGroup;
        public TMP_Text BannerTitle => bannerTitle;
        public TMP_Text BannerSubtitle => bannerSubtitle;
        public GameObject ProgressRoot => progressRoot;
        public CanvasGroup ProgressGroup => progressGroup;
        public TMP_Text ProgressTitle => progressTitle;
        public TMP_Text ProgressStats => progressStats;
        public Image ProgressFill => progressFill;
        public GameObject NestStatusRoot => nestStatusRoot;
        public TMP_Text NestPhase => nestPhase;
        public TMP_Text NestGoal => nestGoal;
        public TMP_Text NestForecast => nestForecast;
    }
}
