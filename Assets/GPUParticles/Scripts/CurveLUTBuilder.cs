using UnityEngine;

namespace GPUParticles
{
    public static class CurveLUTBuilder
    {
        static Texture2D s_DefaultUnitLut;

        public static Texture2D Build(
            ParticleSystem.MinMaxCurve curve,
            int resolution = 256,
            bool saveAsAsset = false,
            string assetName = "SizeOverLife_LUT")
        {
            resolution = Mathf.Max(2, resolution);
            if (string.IsNullOrEmpty(assetName)) assetName = "SizeOverLife_LUT";
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
            for (int i = 0; i < resolution; i++)
            {
                float t = i / (float)(resolution - 1);
                pixels[i] = new Color(Mathf.Max(0f, curve.Evaluate(t, 0f)), 0f, 0f, 1f);
                pixels[resolution + i] = new Color(
                    Mathf.Max(0f, curve.Evaluate(t, 1f)), 0f, 0f, 1f);
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
    }
}
