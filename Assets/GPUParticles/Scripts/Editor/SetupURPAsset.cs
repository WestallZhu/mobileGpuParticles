#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering;

namespace GPUParticles.Editor
{
    public static class SetupURPAsset
    {
        [MenuItem("Tools/GPU Particles/Setup URP Pipeline")]
        public static void Setup()
        {
            // Create URP Asset
            var urp = ScriptableObject.CreateInstance<UniversalRenderPipelineAsset>();
            var urpPath = AssetDatabase.GenerateUniqueAssetPath("Assets/UniversalRenderPipelineAsset.asset");
            AssetDatabase.CreateAsset(urp, urpPath);

            // Create Renderer Data
            var rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
            var rdPath = AssetDatabase.GenerateUniqueAssetPath("Assets/UniversalRendererData.asset");
            AssetDatabase.CreateAsset(rendererData, rdPath);

            // Assign renderer to URP Asset
            var so = new SerializedObject(urp);
            so.Update();
            var m_RendererDataList = so.FindProperty("m_RendererDataList");
            m_RendererDataList.arraySize = 1;
            m_RendererDataList.GetArrayElementAtIndex(0).objectReferenceValue = rendererData;
            so.FindProperty("m_DefaultRendererIndex").intValue = 0;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(urp);

            // Add our Renderer Feature
            var feature = ScriptableObject.CreateInstance<GPUParticlesRendererFeature>();
            AssetDatabase.CreateAsset(feature, AssetDatabase.GenerateUniqueAssetPath("Assets/GPUParticlesRendererFeature.asset"));
            rendererData.rendererFeatures.Add(feature);
            feature.Create();
            EditorUtility.SetDirty(rendererData);

            // Assign to Graphics Settings
            GraphicsSettings.renderPipelineAsset = urp;
            QualitySettings.renderPipeline = urp;

            AssetDatabase.SaveAssets();
            Selection.activeObject = urp;
            Debug.Log("URP asset + renderer created, GPUParticlesRendererFeature added and assigned to GraphicsSettings.");
        }
    }
}
#endif
