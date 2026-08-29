using UnityEngine;

namespace GPUParticles
{
    public static class GradientLUTBuilder
    {
        static Texture2D s_DefaultWhiteLut;

        public static Texture2D Build(
            ParticleSystem.MinMaxGradient gradient,
            int resolution = 256,
            bool saveAsAsset = false)
        {
            resolution = Mathf.Max(2, resolution);
            var tex = new Texture2D(resolution, 2, TextureFormat.RGBAHalf, false, true)
            {
                name = "ColorOverLife_LUT",
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
                EvaluateBounds(gradient, t, out Color minimum, out Color maximum);
                pixels[i] = minimum;
                pixels[resolution + i] = maximum;
            }
            tex.SetPixels(pixels);
            tex.Apply(false, false);

#if UNITY_EDITOR
            if (saveAsAsset && !Application.isPlaying)
            {
                string path = UnityEditor.AssetDatabase.GenerateUniqueAssetPath(
                    "Assets/ColorOverLife_LUT.asset");
                UnityEditor.AssetDatabase.CreateAsset(tex, path);
                UnityEditor.AssetDatabase.SaveAssets();
            }
#endif
            return tex;
        }

        public static Texture2D Build(
            Gradient gradient,
            int resolution = 256,
            bool saveAsAsset = false)
        {
            return Build(new ParticleSystem.MinMaxGradient(
                gradient ?? CreateWhiteGradient()), resolution, saveAsAsset);
        }

        public static Texture2D GetDefaultWhiteLUT(int resolution = 256)
        {
            if (s_DefaultWhiteLut != null) return s_DefaultWhiteLut;

            resolution = Mathf.Max(2, resolution);
            var tex = new Texture2D(resolution, 2, TextureFormat.RGBAHalf, false, true)
            {
                name = "ColorOverLife_LUT_Default",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave
            };

            var pixels = new Color[resolution * 2];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = Color.white;
            }

            tex.SetPixels(pixels);
            tex.Apply(false, false);
            s_DefaultWhiteLut = tex;
            return tex;
        }

        static void EvaluateBounds(
            ParticleSystem.MinMaxGradient gradient,
            float time,
            out Color minimum,
            out Color maximum)
        {
            switch (gradient.mode)
            {
                case ParticleSystemGradientMode.Color:
                    minimum = gradient.color;
                    maximum = minimum;
                    break;

                case ParticleSystemGradientMode.TwoColors:
                    minimum = gradient.colorMin;
                    maximum = gradient.colorMax;
                    break;

                case ParticleSystemGradientMode.TwoGradients:
                    minimum = EvaluateGradient(gradient.gradientMin, time);
                    maximum = EvaluateGradient(gradient.gradientMax, time);
                    break;

                case ParticleSystemGradientMode.RandomColor:
                case ParticleSystemGradientMode.Gradient:
                default:
                    minimum = EvaluateGradient(
                        gradient.gradient ?? gradient.gradientMax, time);
                    maximum = minimum;
                    break;
            }
        }

        static Color EvaluateGradient(Gradient gradient, float time)
        {
            return (gradient ?? CreateWhiteGradient()).Evaluate(Mathf.Clamp01(time));
        }

        static Gradient CreateWhiteGradient()
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(Color.white, 1f)
                },
                new[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(1f, 1f)
                });
            return gradient;
        }
    }
}
