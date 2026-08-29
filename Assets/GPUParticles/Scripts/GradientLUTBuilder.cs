using UnityEngine;

namespace GPUParticles
{
    public static class GradientLUTBuilder
    {
        static Texture2D s_DefaultWhiteLut;

        // RGBA gradient
        public static Texture2D Build(Gradient gradient, int resolution = 256)
        {
            if (gradient == null) gradient = new Gradient();
            resolution = Mathf.Max(2, resolution);
            var tex = new Texture2D(resolution, 1, TextureFormat.RGBA32, false, true);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;

            for (int i = 0; i < resolution; i++)
            {
                float t = i / (float)(resolution - 1);
                Color c = gradient.Evaluate(t);
                tex.SetPixel(i, 0, c);
            }
            tex.Apply();
#if UNITY_EDITOR
            tex.name = "ColorOverLife_LUT";
            UnityEditor.AssetDatabase.CreateAsset(tex, UnityEditor.AssetDatabase.GenerateUniqueAssetPath("Assets/ColorOverLife_LUT.asset"));
            UnityEditor.AssetDatabase.SaveAssets();
#endif
            return tex;
        }

        public static Texture2D GetDefaultWhiteLUT(int resolution = 256)
        {
            if (s_DefaultWhiteLut != null) return s_DefaultWhiteLut;

            resolution = Mathf.Max(2, resolution);
            var tex = new Texture2D(resolution, 1, TextureFormat.RGBA32, false, true);
            tex.name = "ColorOverLife_LUT_Default";
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            tex.hideFlags = HideFlags.HideAndDontSave;

            for (int i = 0; i < resolution; i++)
            {
                tex.SetPixel(i, 0, Color.white);
            }

            tex.Apply();
            s_DefaultWhiteLut = tex;
            return tex;
        }
    }
}
