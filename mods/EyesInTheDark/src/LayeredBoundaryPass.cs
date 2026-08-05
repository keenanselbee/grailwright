using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace EyesInTheDark
{
    internal struct BoundaryLayerFrame
    {
        public float Radius;
        public float Thickness;
        public Color Color;
    }

    [Serializable]
    internal sealed class LayeredBoundaryPass : CustomPass
    {
        private static readonly int PositionId =
            Shader.PropertyToID("_ObjectPosition");
        private static readonly int RadiusId =
            Shader.PropertyToID("_Radius");
        private static readonly int ThicknessId =
            Shader.PropertyToID("_Thickness");
        private static readonly int MaskIntensityId =
            Shader.PropertyToID("_MaskIntensity");
        private static readonly int ColorId =
            Shader.PropertyToID("_Color");
        private static readonly int NativeIntensityId =
            Shader.PropertyToID("_Intensity");

        private readonly Material[] _materials = new Material[3];
        private readonly BoundaryLayerFrame[] _layers =
            new BoundaryLayerFrame[3];

        private Transform _targetObject;
        private float _maskIntensity;
        private float _nativeIntensity;
        private bool _hasNativeIntensity;

        internal bool Initialize(
            Material sourceMaterial,
            Transform targetObject,
            float maskIntensity)
        {
            if (sourceMaterial == null || targetObject == null)
            {
                return false;
            }

            ReleaseMaterials();
            _targetObject = targetObject;
            _maskIntensity = maskIntensity;
            for (int index = 0; index < _materials.Length; index++)
            {
                Material material = new Material(sourceMaterial);
                material.name = sourceMaterial.name
                    + " (EITD Layer "
                    + index
                    + ")";
                _materials[index] = material;
            }

            _hasNativeIntensity = _materials[0].HasProperty(
                NativeIntensityId);
            return true;
        }

        internal void SetNativeIntensity(float value)
        {
            _nativeIntensity = float.IsNaN(value)
                || float.IsInfinity(value)
                    ? 0f
                    : Mathf.Max(0f, value);
        }

        internal void SetLayer(
            int index,
            float radius,
            float thickness,
            Color color)
        {
            if (index < 0 || index >= _layers.Length)
            {
                return;
            }

            _layers[index] = new BoundaryLayerFrame
            {
                Radius = Mathf.Clamp(radius, 0f, 100f),
                Thickness = Mathf.Clamp(thickness, 0f, 1f),
                Color = color
            };
        }

        internal void ReleaseMaterials()
        {
            for (int index = 0; index < _materials.Length; index++)
            {
                if (_materials[index] != null)
                {
                    CoreUtils.Destroy(_materials[index]);
                    _materials[index] = null;
                }
            }

            _targetObject = null;
        }

        protected override void Execute(CustomPassContext context)
        {
            if (_targetObject == null || _nativeIntensity <= 0.0001f)
            {
                return;
            }

            Vector3 position = _targetObject.position;
            for (int index = _materials.Length - 1;
                index >= 0;
                index--)
            {
                Material material = _materials[index];
                if (material == null)
                {
                    continue;
                }

                BoundaryLayerFrame layer = _layers[index];
                material.SetVector(PositionId, position);
                material.SetFloat(RadiusId, layer.Radius);
                material.SetFloat(ThicknessId, layer.Thickness);
                material.SetFloat(MaskIntensityId, _maskIntensity);
                material.SetColor(ColorId, layer.Color);
                if (_hasNativeIntensity)
                {
                    material.SetFloat(
                        NativeIntensityId,
                        _nativeIntensity);
                }
                CoreUtils.DrawFullScreen(context.cmd, material);
            }
        }

        protected override void Cleanup()
        {
            ReleaseMaterials();
        }
    }
}
