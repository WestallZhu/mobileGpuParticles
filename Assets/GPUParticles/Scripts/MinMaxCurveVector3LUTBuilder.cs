using UnityEngine;

namespace GPUParticles
{
    /// <summary>
    /// Packs three Shuriken MinMaxCurves into a two-row RGB LUT.
    /// Row 0 stores the minimum curve and row 1 stores the maximum curve.
    /// </summary>
    public static class MinMaxCurveVector3LUTBuilder
    {
        static Texture2D s_DefaultZeroLut;

        public static Texture2D Build(
            ParticleSystem.MinMaxCurve x,
            ParticleSystem.MinMaxCurve y,
            ParticleSystem.MinMaxCurve z,
            int resolution = 256,
            bool saveAsAsset = false)
        {
            resolution = Mathf.Max(2, resolution);
            var texture = new Texture2D(resolution, 2, TextureFormat.RGBAHalf, false, true)
            {
                name = "MinMaxCurveVector3_LUT",
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
                pixels[i] = Evaluate(x, y, z, t, 0f);
                pixels[resolution + i] = Evaluate(x, y, z, t, 1f);
            }

            texture.SetPixels(pixels);
            texture.Apply(false, false);

#if UNITY_EDITOR
            if (saveAsAsset && !Application.isPlaying)
            {
                string path = UnityEditor.AssetDatabase.GenerateUniqueAssetPath(
                    "Assets/ForceOverLife_LUT.asset");
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

        static Color Evaluate(
            ParticleSystem.MinMaxCurve x,
            ParticleSystem.MinMaxCurve y,
            ParticleSystem.MinMaxCurve z,
            float time,
            float lerpFactor)
        {
            return new Color(
                x.Evaluate(time, lerpFactor),
                y.Evaluate(time, lerpFactor),
                z.Evaluate(time, lerpFactor),
                0f);
        }
    }
}
