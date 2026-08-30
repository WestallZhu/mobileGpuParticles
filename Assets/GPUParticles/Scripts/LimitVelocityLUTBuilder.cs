using UnityEngine;

namespace GPUParticles
{
    /// <summary>
    /// Packs Limit Velocity over Lifetime into a two-row RGBA LUT.
    /// RGB stores the scalar/axis limits and A stores Drag.
    /// </summary>
    public static class LimitVelocityLUTBuilder
    {
        static Texture2D s_DefaultZeroLut;

        public static Texture2D Build(
            ParticleSystem.LimitVelocityOverLifetimeModule module,
            int resolution = 256,
            bool saveAsAsset = false,
            string assetName = "LimitVelocityOverLifetime_LUT")
        {
            resolution = Mathf.Max(2, resolution);
            if (string.IsNullOrEmpty(assetName))
            {
                assetName = "LimitVelocityOverLifetime_LUT";
            }

            var texture = new Texture2D(
                resolution, 2, TextureFormat.RGBAHalf, false, true)
            {
                name = assetName,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

#if UNITY_EDITOR
            if (!saveAsAsset || Application.isPlaying)
            {
                texture.hideFlags =
                    HideFlags.DontSaveInBuild | HideFlags.DontSaveInEditor;
            }
#endif

            ParticleSystem.MinMaxCurve x = module.separateAxes
                ? module.limitX
                : module.limit;
            ParticleSystem.MinMaxCurve y = module.separateAxes
                ? module.limitY
                : module.limit;
            ParticleSystem.MinMaxCurve z = module.separateAxes
                ? module.limitZ
                : module.limit;
            ParticleSystem.MinMaxCurve drag = module.drag;

            var pixels = new Color[resolution * 2];
            for (int i = 0; i < resolution; i++)
            {
                float normalizedAge = i / (resolution - 1f);
                pixels[i] = Evaluate(x, y, z, drag, normalizedAge, 0f);
                pixels[resolution + i] =
                    Evaluate(x, y, z, drag, normalizedAge, 1f);
            }

            texture.SetPixels(pixels);
            texture.Apply(false, false);

#if UNITY_EDITOR
            if (saveAsAsset && !Application.isPlaying)
            {
                string path = UnityEditor.AssetDatabase.GenerateUniqueAssetPath(
                    $"Assets/{assetName}.asset");
                UnityEditor.AssetDatabase.CreateAsset(texture, path);
                UnityEditor.AssetDatabase.SaveAssets();
            }
#endif

            return texture;
        }

        public static Texture2D GetDefaultZeroLUT(int resolution = 2)
        {
            if (s_DefaultZeroLut != null) return s_DefaultZeroLut;

            resolution = Mathf.Max(2, resolution);
            var texture = new Texture2D(
                resolution, 2, TextureFormat.RGBAHalf, false, true)
            {
                name = "LimitVelocityOverLifetime_LUT_DefaultZero",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave
            };

            texture.SetPixels(new Color[resolution * 2]);
            texture.Apply(false, false);
            s_DefaultZeroLut = texture;
            return texture;
        }

        static Color Evaluate(
            ParticleSystem.MinMaxCurve x,
            ParticleSystem.MinMaxCurve y,
            ParticleSystem.MinMaxCurve z,
            ParticleSystem.MinMaxCurve drag,
            float normalizedAge,
            float lerpFactor)
        {
            return new Color(
                Mathf.Max(0f, x.Evaluate(normalizedAge, lerpFactor)),
                Mathf.Max(0f, y.Evaluate(normalizedAge, lerpFactor)),
                Mathf.Max(0f, z.Evaluate(normalizedAge, lerpFactor)),
                Mathf.Max(0f, drag.Evaluate(normalizedAge, lerpFactor)));
        }
    }
}
