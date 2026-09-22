using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Code.UI
{
    public class WaveRuntimeHudView : MonoBehaviour
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

        public GameObject BannerRoot => bannerRoot;
        public CanvasGroup BannerGroup => bannerGroup;
        public TMP_Text BannerTitle => bannerTitle;
        public TMP_Text BannerSubtitle => bannerSubtitle;
        public GameObject ProgressRoot => progressRoot;
        public CanvasGroup ProgressGroup => progressGroup;
        public TMP_Text ProgressTitle => progressTitle;
        public TMP_Text ProgressStats => progressStats;
        public Image ProgressFill => progressFill;
    }
}
