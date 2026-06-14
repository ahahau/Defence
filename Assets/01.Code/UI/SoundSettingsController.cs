using _01.Code.Audio;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace _01.Code.UI
{
    public class SoundSettingsController : MonoBehaviour
    {
        private const string MasterVolumeKey = "Settings.MasterVolume";
        private const string SfxVolumeKey = "Settings.SfxVolume";

        [SerializeField] private Slider masterSlider;
        [SerializeField] private Slider sfxSlider;
        [SerializeField] private TMP_Text masterValueText;
        [SerializeField] private TMP_Text sfxValueText;

        private bool isApplying;

        private void Awake()
        {
            BindSlider(masterSlider, HandleMasterChanged);
            BindSlider(sfxSlider, HandleSfxChanged);
            Load();
        }

        private void OnEnable()
        {
            Load();
        }

        public void Load()
        {
            isApplying = true;

            var masterVolume = PlayerPrefs.GetFloat(MasterVolumeKey, 1f);
            var sfxVolume = PlayerPrefs.GetFloat(SfxVolumeKey, GameSfxPlayer.Volume);

            SetSliderValue(masterSlider, masterVolume);
            SetSliderValue(sfxSlider, sfxVolume);

            isApplying = false;
            ApplyMasterVolume(masterVolume);
            ApplySfxVolume(sfxVolume);
        }

        public void ResetDefaults()
        {
            SetMasterVolume(1f);
            SetSfxVolume(0.85f);
        }

        private void BindSlider(Slider slider, UnityEngine.Events.UnityAction<float> handler)
        {
            if (slider == null)
                return;

            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.wholeNumbers = false;
            slider.onValueChanged.RemoveListener(handler);
            slider.onValueChanged.AddListener(handler);
        }

        private void SetSliderValue(Slider slider, float value)
        {
            if (slider != null)
                slider.SetValueWithoutNotify(Mathf.Clamp01(value));
        }

        private void HandleMasterChanged(float value)
        {
            if (!isApplying)
                SetMasterVolume(value);
        }

        private void HandleSfxChanged(float value)
        {
            if (!isApplying)
                SetSfxVolume(value);
        }

        private void SetMasterVolume(float value)
        {
            var clamped = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(MasterVolumeKey, clamped);
            SetSliderValue(masterSlider, clamped);
            ApplyMasterVolume(clamped);
        }

        private void SetSfxVolume(float value)
        {
            var clamped = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(SfxVolumeKey, clamped);
            SetSliderValue(sfxSlider, clamped);
            ApplySfxVolume(clamped);
        }

        private void ApplyMasterVolume(float value)
        {
            AudioListener.volume = Mathf.Clamp01(value);
            SetPercentText(masterValueText, value);
        }

        private void ApplySfxVolume(float value)
        {
            GameSfxPlayer.Volume = Mathf.Clamp01(value);
            SetPercentText(sfxValueText, value);
        }

        private void SetPercentText(TMP_Text target, float value)
        {
            if (target != null)
                target.text = $"{Mathf.RoundToInt(Mathf.Clamp01(value) * 100f)}%";
        }
    }
}
