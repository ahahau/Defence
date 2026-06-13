using System;
using System.Collections.Generic;
using _01.Code.Core;
using _01.Code.Events;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace _01.Code.Audio
{
    [RequireComponent(typeof(AudioSource))]
    public class GameSfxPlayer : MonoBehaviour
    {
        private static GameSfxPlayer instance;

        [Header("Channels")]
        [SerializeField] private GameEventChannelSO nodeEventChannel;
        [SerializeField] private GameEventChannelSO costEventChannel;
        [SerializeField] private GameEventChannelSO waveEventChannel;

        [Header("UI")]
        [SerializeField] private AudioClip[] uiClickClips;
        [SerializeField] private AudioClip[] uiConfirmClips;
        [SerializeField] private AudioClip[] uiOpenClips;
        [SerializeField] private AudioClip[] uiRewardClips;

        [Header("Game")]
        [SerializeField] private AudioClip[] buildInstallClips;
        [SerializeField] private AudioClip[] unitPlaceClips;
        [SerializeField] private AudioClip[] waveStartClips;
        [SerializeField] private AudioClip[] waveClearClips;

        [Header("Combat")]
        [SerializeField] private AudioClip[] attackClips;
        [SerializeField] private AudioClip[] hitClips;
        [SerializeField] private AudioClip[] dodgeClips;
        [SerializeField] private AudioClip[] trapClips;

        [SerializeField, Range(0f, 1f)] private float volume = 0.85f;
        [SerializeField, Range(0.8f, 1.2f)] private float minPitch = 0.96f;
        [SerializeField, Range(0.8f, 1.2f)] private float maxPitch = 1.04f;

        public static float Volume
        {
            get => instance != null ? instance.volume : PlayerPrefs.GetFloat("Settings.SfxVolume", 0.85f);
            set
            {
                var clamped = Mathf.Clamp01(value);
                PlayerPrefs.SetFloat("Settings.SfxVolume", clamped);

                if (instance != null)
                    instance.volume = clamped;
            }
        }

        private readonly HashSet<Button> boundButtons = new();
        private AudioSource audioSource;

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
                audioSource = gameObject.AddComponent<AudioSource>();

            audioSource.playOnAwake = false;
            audioSource.loop = false;
            audioSource.spatialBlend = 0f;
            volume = PlayerPrefs.GetFloat("Settings.SfxVolume", volume);
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
            nodeEventChannel?.AddListener<UnitAssignedToNodeEvent>(HandleUnitAssigned);
            nodeEventChannel?.AddListener<BuildingInstalledEvent>(HandleBuildingInstalled);
            nodeEventChannel?.AddListener<PortalInstalledEvent>(HandlePortalInstalled);
            costEventChannel?.AddListener<RosterHirePaidEvent>(HandleRosterHirePaid);
            costEventChannel?.AddListener<BuildCostRejectedEvent>(HandleBuildCostRejected);
            waveEventChannel?.AddListener<WaveStartedEvent>(HandleWaveStarted);
            waveEventChannel?.AddListener<WaveEndedEvent>(HandleWaveEnded);
            BindSceneButtons();
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            nodeEventChannel?.RemoveListener<UnitAssignedToNodeEvent>(HandleUnitAssigned);
            nodeEventChannel?.RemoveListener<BuildingInstalledEvent>(HandleBuildingInstalled);
            nodeEventChannel?.RemoveListener<PortalInstalledEvent>(HandlePortalInstalled);
            costEventChannel?.RemoveListener<RosterHirePaidEvent>(HandleRosterHirePaid);
            costEventChannel?.RemoveListener<BuildCostRejectedEvent>(HandleBuildCostRejected);
            waveEventChannel?.RemoveListener<WaveStartedEvent>(HandleWaveStarted);
            waveEventChannel?.RemoveListener<WaveEndedEvent>(HandleWaveEnded);
        }

        public static void Play(GameSfxCue cue)
        {
            if (instance == null)
                return;

            instance.PlayInternal(cue);
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            BindSceneButtons();
        }

        private void BindSceneButtons()
        {
            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid())
                return;

            var roots = scene.GetRootGameObjects();
            foreach (var root in roots)
            {
                var buttons = root.GetComponentsInChildren<Button>(true);
                foreach (var button in buttons)
                    BindButton(button);
            }
        }

        private void BindButton(Button button)
        {
            if (button == null || !boundButtons.Add(button))
                return;

            button.onClick.AddListener(() => PlayInternal(ResolveButtonCue(button)));
        }

        private static GameSfxCue ResolveButtonCue(Button button)
        {
            var name = button != null ? button.name : string.Empty;
            return name.Contains("Confirm", StringComparison.OrdinalIgnoreCase)
                   || name.Contains("Install", StringComparison.OrdinalIgnoreCase)
                   || name.Contains("Recover", StringComparison.OrdinalIgnoreCase)
                   || name.Contains("Reward", StringComparison.OrdinalIgnoreCase)
                ? GameSfxCue.UiConfirm
                : GameSfxCue.UiClick;
        }

        private void HandleUnitAssigned(UnitAssignedToNodeEvent evt) => PlayInternal(GameSfxCue.UnitPlace);
        private void HandleBuildingInstalled(BuildingInstalledEvent evt) => PlayInternal(GameSfxCue.BuildInstall);
        private void HandlePortalInstalled(PortalInstalledEvent evt) => PlayInternal(GameSfxCue.BuildInstall);
        private void HandleRosterHirePaid(RosterHirePaidEvent evt) => PlayInternal(GameSfxCue.UiConfirm);
        private void HandleBuildCostRejected(BuildCostRejectedEvent evt) => PlayInternal(GameSfxCue.UiClick);
        private void HandleWaveStarted(WaveStartedEvent evt) => PlayInternal(GameSfxCue.WaveStart);
        private void HandleWaveEnded(WaveEndedEvent evt) => PlayInternal(GameSfxCue.WaveClear);

        private void PlayInternal(GameSfxCue cue)
        {
            var clips = ResolveClips(cue);
            if (clips == null || clips.Length == 0 || audioSource == null)
                return;

            var clip = clips[UnityEngine.Random.Range(0, clips.Length)];
            if (clip == null)
                return;

            audioSource.pitch = UnityEngine.Random.Range(minPitch, maxPitch);
            audioSource.PlayOneShot(clip, volume);
        }

        private AudioClip[] ResolveClips(GameSfxCue cue)
        {
            return cue switch
            {
                GameSfxCue.UiClick => uiClickClips,
                GameSfxCue.UiConfirm => uiConfirmClips,
                GameSfxCue.UiOpen => uiOpenClips,
                GameSfxCue.UiReward => uiRewardClips,
                GameSfxCue.BuildInstall => buildInstallClips,
                GameSfxCue.UnitPlace => unitPlaceClips,
                GameSfxCue.WaveStart => waveStartClips,
                GameSfxCue.WaveClear => waveClearClips,
                GameSfxCue.Attack => attackClips,
                GameSfxCue.Hit => hitClips,
                GameSfxCue.Dodge => dodgeClips,
                GameSfxCue.Trap => trapClips,
                _ => null
            };
        }
    }
}
