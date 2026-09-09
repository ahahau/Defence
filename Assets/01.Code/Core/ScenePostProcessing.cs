using DG.Tweening;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace _01.Code.Core
{
    /// <summary>URP 포스트 프로세싱 전역 셋업 — 씬 배선 없이 런타임에 글로벌 볼륨/프로파일을 구성하고
    /// 카메라의 포스트 프로세싱을 켠다. 블룸(타격 플래시·파티클 글로우), 비네트(던전 분위기),
    /// 컬러 보정(대비·채도), 미세 색수차. 보스 시네마틱 등에서 PulseVignette로 일시 강조 가능.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Volume))]
    public class ScenePostProcessing : MonoBehaviour
    {
        private static ScenePostProcessing _instance;

        [Header("Bloom")]
        [SerializeField, Min(0f)] private float bloomIntensity = 0.7f;
        [SerializeField, Min(0f)] private float bloomThreshold = 0.85f;
        [SerializeField, Range(0f, 1f)] private float bloomScatter = 0.7f;

        [Header("Vignette")]
        [SerializeField, Range(0f, 1f)] private float vignetteIntensity = 0.27f;
        [SerializeField, Range(0.01f, 1f)] private float vignetteSmoothness = 0.42f;

        [Header("Color")]
        [SerializeField, Range(-100f, 100f)] private float contrast = 8f;
        [SerializeField, Range(-100f, 100f)] private float saturation = 6f;

        [Header("Exposure & Tint")]
        [SerializeField, Range(-2f, 2f), Tooltip("화면 전체 밝기(EV). 조명을 키우는 것과 달리 어두운 곳도 함께 올라온다.")]
        private float postExposure = 0.35f;

        [SerializeField, Tooltip("화면 전체에 옅게 까는 색. 흰색에서 살짝만 기울인다.")]
        private Color colorFilter = new(1f, 0.985f, 0.955f, 1f);

        [Header("Chromatic Aberration")]
        [SerializeField, Range(0f, 1f)] private float chromaticAberration = 0.05f;

        private Volume _volume;
        private Vignette _vignette;
        private Tween _vignettePulse;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            BuildVolume();
            EnableCameraPostProcessing();
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;

            _vignettePulse?.Kill();
            SceneManager.sceneLoaded -= HandleSceneLoaded;
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            EnableCameraPostProcessing();
        }

        private void BuildVolume()
        {
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();

            var bloom = profile.Add<Bloom>();
            bloom.intensity.Override(bloomIntensity);
            bloom.threshold.Override(bloomThreshold);
            bloom.scatter.Override(bloomScatter);

            _vignette = profile.Add<Vignette>();
            _vignette.intensity.Override(vignetteIntensity);
            _vignette.smoothness.Override(vignetteSmoothness);
            _vignette.color.Override(Color.black);

            // 톤매핑이 없으면 밝은 값이 그냥 1.0 에서 잘린다. 조명을 조금만 올려도 건물이
            // 흰 덩어리가 되던 이유다. Neutral 은 색을 비틀지 않고 위쪽만 완만하게 눕힌다 —
            // 덕분에 노출을 올려도 하얗게 타지 않는다. (URP 에 HDR 이 켜져 있어야 일한다.)
            var tonemapping = profile.Add<Tonemapping>();
            tonemapping.mode.Override(TonemappingMode.Neutral);

            var colorAdjustments = profile.Add<ColorAdjustments>();
            colorAdjustments.contrast.Override(contrast);
            colorAdjustments.saturation.Override(saturation);

            // 밝기는 조명이 아니라 여기서 올린다. 조명을 키우면 밝은 곳만 먼저 타지만,
            // 노출은 화면 전체를 같은 비율로 들어 올려 어두운 구석이 같이 살아난다.
            colorAdjustments.postExposure.Override(postExposure);

            // 던전의 불빛이 따뜻하니 화면 전체에도 아주 옅게 그 색을 깐다. 세게 넣으면
            // 갈색 일색이 되므로 흰색에서 살짝만 기울인다.
            colorAdjustments.colorFilter.Override(colorFilter);

            if (chromaticAberration > 0f)
            {
                var aberration = profile.Add<ChromaticAberration>();
                aberration.intensity.Override(chromaticAberration);
            }

            _volume = GetComponent<Volume>();
            if (_volume == null)
            {
                Debug.LogError($"{nameof(ScenePostProcessing)} requires a {nameof(Volume)} component.", this);
                enabled = false;
                return;
            }
            _volume.isGlobal = true;
            _volume.priority = 10f;
            _volume.profile = profile;
        }

        /// <summary>씬의 카메라에 URP 포스트 프로세싱 렌더링을 켠다(씬 에셋 수정 불필요).</summary>
        private static void EnableCameraPostProcessing()
        {
            foreach (var cam in Camera.allCameras)
            {
                var data = cam.GetUniversalAdditionalCameraData();
                if (data != null)
                    data.renderPostProcessing = true;
            }
        }

        /// <summary>비네트를 잠시 조였다가 원래대로 — 보스 처치 시네마틱 등 극적인 순간 강조용(비스케일 시간).</summary>
        public static void PulseVignette(float extraIntensity, float duration)
        {
            if (_instance == null || _instance._vignette == null || duration <= 0f)
                return;

            var vignette = _instance._vignette;
            var baseIntensity = _instance.vignetteIntensity;
            var peak = Mathf.Clamp01(baseIntensity + Mathf.Max(0f, extraIntensity));

            _instance._vignettePulse?.Kill();
            _instance._vignettePulse = DOTween.Sequence().SetUpdate(true)
                .Append(DOTween.To(() => vignette.intensity.value,
                    value => vignette.intensity.Override(value), peak, duration * 0.25f))
                .AppendInterval(duration * 0.5f)
                .Append(DOTween.To(() => vignette.intensity.value,
                    value => vignette.intensity.Override(value), baseIntensity, duration * 0.25f));
        }
    }
}
