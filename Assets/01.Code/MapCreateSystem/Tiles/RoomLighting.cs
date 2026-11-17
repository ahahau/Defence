using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Code.MapCreateSystem
{
    /// <summary>
    /// 열린 방의 빛: 방 전체를 옅게 비추는 차가운 빛 하나와 횃불마다 흔들리는 따뜻한 빛.
    /// 던전 전체 빛(씬의 Global Light 2D)을 낮춰 두었으므로 복도와 잠긴 후보는 어둡고, 방과 횃불 주변만 보인다.
    /// </summary>
    public sealed class RoomLighting : MonoBehaviour
    {
        private const float AmbientIntensity = 0.6f;
        private static readonly Color AmbientColor = new(0.72f, 0.76f, 0.95f);

        private const float TorchIntensity = 1.1f;
        private const float TorchRadius = 4.5f;
        private const float TorchFlicker = 0.18f;
        private static readonly Color TorchColor = new(1f, 0.6f, 0.32f);

        private Light2D _ambient;
        private readonly List<Light2D> _torches = new();
        private readonly List<float> _phases = new();

        /// <summary>방 크기와 횃불 자리(월드)에 맞춰 빛을 놓는다. 남는 횃불 빛은 끈다.</summary>
        public void Apply(RoomShape shape, IReadOnlyList<Vector3> torchPositions)
        {
            if (shape == null)
                return;

            _ambient ??= CreateLight("RoomLight");
            _ambient.transform.position = transform.position;
            var halfExtent = Mathf.Max(shape.Columns, shape.Rows) * RoomLayoutRules.CellWorldSize * 0.5f;
            // 방 구석(가운데에서 반 폭의 1.4배)까지 고르게 밝아야 구석에 세운 유닛이 어둠에 묻히지 않는다.
            _ambient.pointLightInnerRadius = halfExtent * 0.85f;
            _ambient.pointLightOuterRadius = halfExtent * 1.6f;
            _ambient.intensity = AmbientIntensity;
            _ambient.color = AmbientColor;

            for (var i = 0; i < torchPositions.Count; i++)
            {
                if (i >= _torches.Count)
                {
                    var torch = CreateLight("TorchLight");
                    torch.color = TorchColor;
                    torch.pointLightInnerRadius = TorchRadius * 0.15f;
                    torch.pointLightOuterRadius = TorchRadius;
                    _torches.Add(torch);
                    _phases.Add(Random.value * 100f);
                }

                _torches[i].transform.position = torchPositions[i];
                _torches[i].enabled = true;
            }

            for (var i = torchPositions.Count; i < _torches.Count; i++)
                _torches[i].enabled = false;
        }

        private void Update()
        {
            for (var i = 0; i < _torches.Count; i++)
            {
                if (!_torches[i].enabled)
                    continue;

                var noise = Mathf.PerlinNoise(_phases[i], Time.time * 4f) - 0.5f;
                _torches[i].intensity = TorchIntensity * (1f + noise * 2f * TorchFlicker);
            }
        }

        private Light2D CreateLight(string lightName)
        {
            var go = new GameObject(lightName);
            go.transform.SetParent(transform, true);
            var light = go.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Point;
            light.falloffIntensity = 0.6f;
            light.shadowsEnabled = false;
            return light;
        }
    }
}
