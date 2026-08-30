using UnityEngine;

namespace GPUParticles
{
    /// <summary>
    /// Packs three Shuriken MinMaxCurves into a two-row RGB LUT, with an
    /// optional fourth scalar curve in Alpha. Row 0 stores minimum curves and
    /// row 1 stores maximum curves.
    /// </summary>
    public static class MinMaxCurveVector3LUTBuilder
    {
        static Texture2D s_DefaultZeroLut;
        static Texture2D s_DefaultVelocityLut;

        public static Texture2D Build(
            ParticleSystem.MinMaxCurve x,
            ParticleSystem.MinMaxCurve y,
            ParticleSystem.MinMaxCurve z,
            int resolution = 256,
            bool saveAsAsset = false,
            string assetName = "MinMaxCurveVector3_LUT")
        {
            return BuildInternal(
                x, y, z, default, false,
                resolution, saveAsAsset, assetName);
        }

        public static Texture2D Build(
            ParticleSystem.MinMaxCurve x,
            ParticleSystem.MinMaxCurve y,
            ParticleSystem.MinMaxCurve z,
            ParticleSystem.MinMaxCurve scalar,
            int resolution = 256,
            bool saveAsAsset = false,
            string assetName = "MinMaxCurveVector4_LUT")
        {
            return BuildInternal(
                x, y, z, scalar, true,
                resolution, saveAsAsset, assetName);
        }

        static Texture2D BuildInternal(
            ParticleSystem.MinMaxCurve x,
            ParticleSystem.MinMaxCurve y,
            ParticleSystem.MinMaxCurve z,
            ParticleSystem.MinMaxCurve scalar,
            bool includeScalar,
            int resolution,
            bool saveAsAsset,
            string assetName)
        {
            resolution = Mathf.Max(2, resolution);
            var texture = new Texture2D(resolution, 2, TextureFormat.RGBAHalf, false, true)
            {
                name = includeScalar
                    ? "MinMaxCurveVector4_LUT"
                    : "MinMaxCurveVector3_LUT",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

#if UNITY_EDITOR
            if (!saveAsAsset || Application.isPlaying)
            {
                texture.hideFlags = HideFlags.DontSaveInBuild | HideFlags.DontSaveInEditor;
            }
#endif

            var pixels = new Color[resolution * 2];
            for (int i = 0; i < resolution; i++)
            {
                float t = i / (resolution - 1f);
                pixels[i] = Evaluate(
                    x, y, z, scalar, includeScalar, t, 0f);
                pixels[resolution + i] = Evaluate(
                    x, y, z, scalar, includeScalar, t, 1f);
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
            var texture = new Texture2D(resolution, 2, TextureFormat.RGBAHalf, false, true)
            {
                name = "MinMaxCurveVector3_LUT_DefaultZero",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave
            };

            var pixels = new Color[resolution * 2];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.clear;
            texture.SetPixels(pixels);
            texture.Apply(false, false);
            s_DefaultZeroLut = texture;
            return texture;
        }

        public static Texture2D GetDefaultVelocityLUT(int resolution = 2)
        {
            if (s_DefaultVelocityLut != null) return s_DefaultVelocityLut;

            resolution = Mathf.Max(2, resolution);
            var texture = new Texture2D(resolution, 2, TextureFormat.RGBAHalf, false, true)
            {
                name = "MinMaxCurveVector4_LUT_DefaultVelocity",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave
            };

            var pixels = new Color[resolution * 2];
            Color defaultVelocity = new Color(0f, 0f, 0f, 1f);
            for (int i = 0; i < pixels.Length; i++) pixels[i] = defaultVelocity;
            texture.SetPixels(pixels);
            texture.Apply(false, false);
            s_DefaultVelocityLut = texture;
            return texture;
        }

        static Color Evaluate(
            ParticleSystem.MinMaxCurve x,
            ParticleSystem.MinMaxCurve y,
            ParticleSystem.MinMaxCurve z,
            ParticleSystem.MinMaxCurve scalar,
            bool includeScalar,
            float time,
            float lerpFactor)
        {
            return new Color(
                x.Evaluate(time, lerpFactor),
                y.Evaluate(time, lerpFactor),
                z.Evaluate(time, lerpFactor),
                includeScalar
                    ? scalar.Evaluate(time, lerpFactor)
                    : 0f);
        }
    }
}
