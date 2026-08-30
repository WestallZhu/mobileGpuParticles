using UnityEngine;

namespace GPUParticles
{
    public static class CurveLUTBuilder
    {
        static Texture2D s_DefaultUnitLut;
        static Texture2D s_DefaultZeroLut;
        static Texture2D s_DefaultLinear01Lut;

        public static Texture2D Build(
            ParticleSystem.MinMaxCurve curve,
            int resolution = 256,
            bool saveAsAsset = false,
            string assetName = "SizeOverLife_LUT")
        {
            return BuildSampled(
                curve,
                resolution,
                saveAsAsset,
                assetName,
                clampNonNegative: true,
                TextureFormat.RHalf);
        }

        public static Texture2D BuildHighPrecision(
            ParticleSystem.MinMaxCurve curve,
            int resolution = 256,
            bool saveAsAsset = false,
            string assetName = "StartLifetime_LUT")
        {
            return BuildSampled(
                curve,
                resolution,
                saveAsAsset,
                assetName,
                clampNonNegative: true,
                TextureFormat.RFloat);
        }

        public static Texture2D BuildSigned(
            ParticleSystem.MinMaxCurve curve,
            int resolution = 256,
            bool saveAsAsset = false,
            string assetName = "RotationBySpeed_LUT")
        {
            return BuildSampled(
                curve,
                resolution,
                saveAsAsset,
                assetName,
                clampNonNegative: false,
                TextureFormat.RHalf);
        }

        static Texture2D BuildSampled(
            ParticleSystem.MinMaxCurve curve,
            int resolution,
            bool saveAsAsset,
            string assetName,
            bool clampNonNegative,
            TextureFormat textureFormat)
        {
            resolution = Mathf.Max(2, resolution);
            if (string.IsNullOrEmpty(assetName))
            {
                assetName = clampNonNegative
                    ? "SizeOverLife_LUT"
                    : "RotationBySpeed_LUT";
            }
            var tex = new Texture2D(resolution, 2, textureFormat, false, true)
            {
                name = assetName,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

#if UNITY_EDITOR
            if (!saveAsAsset || Application.isPlaying)
            {
                tex.hideFlags = HideFlags.DontSaveInBuild | HideFlags.DontSaveInEditor;
            }
#endif

            var pixels = new Color[resolution * 2];
            for (int i = 0; i < resolution; i++)
            {
                float t = i / (float)(resolution - 1);
                float minimum = curve.Evaluate(t, 0f);
                float maximum = curve.Evaluate(t, 1f);
                if (clampNonNegative)
                {
                    minimum = Mathf.Max(0f, minimum);
                    maximum = Mathf.Max(0f, maximum);
                }
                pixels[i] = new Color(minimum, 0f, 0f, 1f);
                pixels[resolution + i] = new Color(maximum, 0f, 0f, 1f);
            }
            tex.SetPixels(pixels);
            tex.Apply(false, false);

#if UNITY_EDITOR
            if (saveAsAsset && !Application.isPlaying)
            {
                string path = UnityEditor.AssetDatabase.GenerateUniqueAssetPath(
                    $"Assets/{assetName}.asset");
                UnityEditor.AssetDatabase.CreateAsset(tex, path);
                UnityEditor.AssetDatabase.SaveAssets();
            }
#endif
            return tex;
        }

        public static Texture2D Build(
            AnimationCurve curve,
            int resolution = 256,
            bool saveAsAsset = false,
            string assetName = "SizeOverLife_LUT")
        {
            return Build(new ParticleSystem.MinMaxCurve(
                1f, curve ?? AnimationCurve.Linear(0f, 1f, 1f, 1f)),
                resolution,
                saveAsAsset,
                assetName);
        }

        public static Texture2D BuildIntegral(
            ParticleSystem.MinMaxCurve curve,
            int resolution = 256,
            bool saveAsAsset = false,
            string assetName = "RotationOverLife_IntegralLUT")
        {
            resolution = Mathf.Max(2, resolution);
            if (string.IsNullOrEmpty(assetName))
            {
                assetName = "RotationOverLife_IntegralLUT";
            }

            var tex = new Texture2D(resolution, 2, TextureFormat.RHalf, false, true)
            {
                name = assetName,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

#if UNITY_EDITOR
            if (!saveAsAsset || Application.isPlaying)
            {
                tex.hideFlags = HideFlags.DontSaveInBuild | HideFlags.DontSaveInEditor;
            }
#endif

            var pixels = new Color[resolution * 2];
            float step = 1f / (resolution - 1);
            float previousMinimum = curve.Evaluate(0f, 0f);
            float previousMaximum = curve.Evaluate(0f, 1f);
            float minimumIntegral = 0f;
            float maximumIntegral = 0f;
            pixels[0] = new Color(0f, 0f, 0f, 1f);
            pixels[resolution] = new Color(0f, 0f, 0f, 1f);

            for (int i = 1; i < resolution; i++)
            {
                float t = i * step;
                float minimum = curve.Evaluate(t, 0f);
                float maximum = curve.Evaluate(t, 1f);
                minimumIntegral += (previousMinimum + minimum) * 0.5f * step;
                maximumIntegral += (previousMaximum + maximum) * 0.5f * step;
                pixels[i] = new Color(minimumIntegral, 0f, 0f, 1f);
                pixels[resolution + i] = new Color(maximumIntegral, 0f, 0f, 1f);
                previousMinimum = minimum;
                previousMaximum = maximum;
            }

            tex.SetPixels(pixels);
            tex.Apply(false, false);

#if UNITY_EDITOR
            if (saveAsAsset && !Application.isPlaying)
            {
                string path = UnityEditor.AssetDatabase.GenerateUniqueAssetPath(
                    $"Assets/{assetName}.asset");
                UnityEditor.AssetDatabase.CreateAsset(tex, path);
                UnityEditor.AssetDatabase.SaveAssets();
            }
#endif
            return tex;
        }

        public static Texture2D GetDefaultUnitLUT(int resolution = 256)
        {
            if (s_DefaultUnitLut != null) return s_DefaultUnitLut;

            resolution = Mathf.Max(2, resolution);
            var tex = new Texture2D(resolution, 2, TextureFormat.RHalf, false, true)
            {
                name = "SizeOverLife_LUT_Default",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave
            };

            var pixels = new Color[resolution * 2];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = new Color(1f, 0f, 0f, 1f);
            }

            tex.SetPixels(pixels);
            tex.Apply(false, false);
            s_DefaultUnitLut = tex;
            return tex;
        }

        public static Texture2D GetDefaultZeroLUT(int resolution = 256)
        {
            if (s_DefaultZeroLut != null) return s_DefaultZeroLut;

            resolution = Mathf.Max(2, resolution);
            var tex = new Texture2D(resolution, 2, TextureFormat.RHalf, false, true)
            {
                name = "RotationOverLife_IntegralLUT_Default",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave
            };

            var pixels = new Color[resolution * 2];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = new Color(0f, 0f, 0f, 1f);
            }

            tex.SetPixels(pixels);
            tex.Apply(false, false);
            s_DefaultZeroLut = tex;
            return tex;
        }

        public static Texture2D GetDefaultLinear01LUT(int resolution = 256)
        {
            if (s_DefaultLinear01Lut != null) return s_DefaultLinear01Lut;

            resolution = Mathf.Max(2, resolution);
            var tex = new Texture2D(resolution, 2, TextureFormat.RHalf, false, true)
            {
                name = "Curve_LUT_DefaultLinear01",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave
            };

            var pixels = new Color[resolution * 2];
            for (int i = 0; i < resolution; i++)
            {
                float value = i / (float)(resolution - 1);
                pixels[i] = new Color(value, 0f, 0f, 1f);
                pixels[resolution + i] = pixels[i];
            }

            tex.SetPixels(pixels);
            tex.Apply(false, false);
            s_DefaultLinear01Lut = tex;
            return tex;
        }
    }
}
