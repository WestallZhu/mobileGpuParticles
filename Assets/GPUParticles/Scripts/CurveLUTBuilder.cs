using UnityEngine;

namespace GPUParticles
{
    public static class CurveLUTBuilder
    {
        // Single-channel (R) LUT in 0..1
        public static Texture2D Build(AnimationCurve curve, int resolution = 256)
        {
            if (curve == null) curve = AnimationCurve.Linear(0, 1, 1, 1);
            resolution = Mathf.Max(2, resolution);
            var tex = new Texture2D(resolution, 1, TextureFormat.R8, false, true);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;

            for (int i = 0; i < resolution; i++)
            {
                float t = i / (float)(resolution - 1);
                float v = Mathf.Clamp01(curve.Evaluate(t));
                tex.SetPixel(i, 0, new Color(v, 0, 0, 1));
            }
            tex.Apply();
#if UNITY_EDITOR
            tex.name = "SizeOverLife_LUT";
            UnityEditor.AssetDatabase.CreateAsset(tex, UnityEditor.AssetDatabase.GenerateUniqueAssetPath("Assets/SizeOverLife_LUT.asset"));
            UnityEditor.AssetDatabase.SaveAssets();
#endif
            return tex;
        }
    }
}
