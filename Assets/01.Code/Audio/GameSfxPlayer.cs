using System.Collections.Generic;
using UnityEngine;

namespace _01.Code.Audio
{
    /// <summary>
    /// 게임 전체의 효과음 재생 창구.
    ///
    /// MonoBehaviour로 남겨둔 것은 일부러다 — 예전 씬과 프리팹이 이 스크립트를 GUID로 물고 있어서,
    /// 클래스를 지우거나 static으로 바꾸면 그 참조가 전부 끊어진다.
    /// 컴포넌트로 붙어 있어도 하는 일은 없고, 실제 재생은 아래 static 경로가 맡는다.
    ///
    /// 소리는 <see cref="GameSfxCatalogSO"/> 하나에서만 나온다. 이펙트 프리팹에 딸려온
    /// AudioSource는 <see cref="StripEmbeddedAudio"/>로 걷어내는데, 그걸 놔두면
    /// 음량 설정이 안 먹는 소리가 따로 생겨서 어떤 건 줄고 어떤 건 안 줄게 된다.
    /// </summary>
    public sealed class GameSfxPlayer : MonoBehaviour
    {
        private const string CatalogResourcePath = "Audio/GameSfxCatalog";
        private const string VolumeKey = "audio.sfx.volume";
        private const int VoiceCount = 12;

        private static GameSfxCatalogSO catalog;
        private static bool catalogLoaded;
        private static AudioSource[] voices;
        private static int nextVoice;
        private static readonly Dictionary<GameSfxCue, float> NextAllowedTime = new();
        private static float volume = -1f;

        /// <summary>0이면 완전 무음. 설정은 PlayerPrefs에 남는다.</summary>
        public static float Volume
        {
            get
            {
                if (volume < 0f)
                    volume = Mathf.Clamp01(PlayerPrefs.GetFloat(VolumeKey, 0.5f));
                return volume;
            }
            set
            {
                volume = Mathf.Clamp01(value);
                PlayerPrefs.SetFloat(VolumeKey, volume);
                // 즉시 기록한다. Unity는 종료할 때 알아서 저장하지만, 그 전에 죽으면 설정이 날아간다.
                PlayerPrefs.Save();
            }
        }

        /// <summary>화면 어디서 났는지 상관없는 소리(UI, 웨이브 시작 등).</summary>
        public static void Play(GameSfxCue cue) => PlayInternal(cue, null);

        /// <summary>전투처럼 위치가 있는 소리. 카메라와 멀면 조금 작게 들린다.</summary>
        public static void Play(GameSfxCue cue, Vector3 position) => PlayInternal(cue, position);

        private static void PlayInternal(GameSfxCue cue, Vector3? position)
        {
            if (!Application.isPlaying || Volume <= 0f)
                return;

            var entry = EnsureCatalog()?.Find(cue);
            if (entry == null)
                return;

            var now = Time.unscaledTime;
            if (NextAllowedTime.TryGetValue(cue, out var allowedAt) && now < allowedAt)
                return;
            NextAllowedTime[cue] = now + entry.MinInterval;

            var clip = entry.PickVariant();
            if (clip == null)
                return;

            var source = EnsureVoice();
            if (source == null)
                return;

            source.pitch = 1f + Random.Range(-entry.PitchJitter, entry.PitchJitter);
            source.PlayOneShot(clip, Volume * entry.Volume * DistanceAttenuation(position));
        }

        /// <summary>
        /// 화면 밖에서 난 소리를 조금 줄인다. 3D 오디오를 쓰지 않는 것은
        /// 카메라가 정사영이라 거리 감쇠가 직관과 어긋나기 때문이다.
        /// </summary>
        private static float DistanceAttenuation(Vector3? position)
        {
            if (position == null)
                return 1f;

            var camera = Camera.main;
            if (camera == null)
                return 1f;

            var viewport = camera.WorldToViewportPoint(position.Value);
            var onScreen = viewport.z > 0f
                           && viewport.x > -0.15f && viewport.x < 1.15f
                           && viewport.y > -0.15f && viewport.y < 1.15f;
            return onScreen ? 1f : 0.35f;
        }

        private static GameSfxCatalogSO EnsureCatalog()
        {
            if (catalogLoaded)
                return catalog;

            catalogLoaded = true;
            catalog = Resources.Load<GameSfxCatalogSO>(CatalogResourcePath);
            if (catalog == null)
                Debug.LogWarning($"효과음 표를 찾지 못했습니다: Resources/{CatalogResourcePath}. 게임이 무음으로 돕니다.");

            return catalog;
        }

        private static AudioSource EnsureVoice()
        {
            if (voices == null || voices.Length == 0 || voices[0] == null)
            {
                var host = new GameObject("Game SFX Voices");
                Object.DontDestroyOnLoad(host);

                voices = new AudioSource[VoiceCount];
                for (var i = 0; i < VoiceCount; i++)
                {
                    var source = host.AddComponent<AudioSource>();
                    source.playOnAwake = false;
                    source.loop = false;
                    source.spatialBlend = 0f;
                    // 모달이 timeScale을 0으로 내려도 UI 소리는 들려야 한다.
                    source.ignoreListenerPause = true;
                    voices[i] = source;
                }

                nextVoice = 0;
            }

            var voice = voices[nextVoice];
            nextVoice = (nextVoice + 1) % voices.Length;
            return voice;
        }

        /// <summary>
        /// 이펙트 프리팹에 딸려온 AudioSource를 걷어낸다.
        /// 파티클 팩은 저마다 소리를 물고 오는데, 그대로 두면 우리 음량 설정 밖에서 울린다.
        /// </summary>
        public static void StripEmbeddedAudio(GameObject instance)
        {
            if (instance == null)
                return;

            foreach (var source in instance.GetComponentsInChildren<AudioSource>(true))
            {
                source.Stop();
                source.playOnAwake = false;
                source.enabled = false;
            }
        }

        /// <summary>플레이 모드를 나갔다 들어오면 정적 상태가 남아 죽은 AudioSource를 가리킨다.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            catalog = null;
            catalogLoaded = false;
            voices = null;
            nextVoice = 0;
            volume = -1f;
            NextAllowedTime.Clear();
        }
    }
}
