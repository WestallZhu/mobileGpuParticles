#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.Rendering.Universal.ShaderGUI;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GPUParticles.Editor
{
    public static class ParticleABValidationTool
    {
        public const string SampleScenePath = "Assets/Scenes/SampleScene.unity";
        public const string ValidationScenePath = "Assets/Scenes/ParticleABValidation.unity";
        public const string ShurikenMaterialPath = "Assets/GPUParticles/ParticleABShuriken.mat";

        [MenuItem("Tools/GPU Particles/Create A-B Validation Scene")]
        public static void CreateValidationSceneMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            CreateOrRefreshValidationScene(false);
            EditorSceneManager.OpenScene(ValidationScenePath, OpenSceneMode.Single);
            Selection.activeObject = Camera.main;
            Debug.Log($"Created particle A/B validation scene: {ValidationScenePath}");
        }

        [MenuItem("Tools/GPU Particles/Run A-B RT Capture")]
        public static void RunCaptureMenu()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("Stop Play Mode before starting a deterministic A/B capture.");
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            StartCapture(false);
        }

        // Command line entry point:
        // Unity -batchmode -projectPath . -executeMethod GPUParticles.Editor.ParticleABValidationTool.RunBatchCapture
        public static void RunBatchCapture()
        {
            StartCapture(true);
        }

        static void StartCapture(bool exitWhenComplete)
        {
            CreateOrRefreshValidationScene(exitWhenComplete);
            EditorSceneManager.OpenScene(ValidationScenePath, OpenSceneMode.Single);

            var controller = Object.FindObjectOfType<ParticleABValidationController>();
            if (controller == null)
            {
                throw new MissingComponentException("Particle A/B validation controller was not created.");
            }

            controller.captureOnPlay = true;
            controller.exitEditorWhenCaptureCompletes = exitWhenComplete;
            EditorUtility.SetDirty(controller);
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            EditorApplication.isPlaying = true;
        }

        static void CreateOrRefreshValidationScene(bool exitWhenComplete)
        {
            if (!File.Exists(SampleScenePath))
            {
                throw new FileNotFoundException("Sample scene not found.", SampleScenePath);
            }

            Scene sampleScene = EditorSceneManager.OpenScene(SampleScenePath, OpenSceneMode.Single);
            if (!EditorSceneManager.SaveScene(sampleScene, ValidationScenePath, true))
            {
                throw new IOException($"Failed to create {ValidationScenePath}.");
            }

            Scene validationScene = EditorSceneManager.OpenScene(ValidationScenePath, OpenSceneMode.Single);
            var shurikenObject = GameObject.Find("Particle System");
            var gpuObject = GameObject.Find("Particle System_GPU");
            Camera camera = Camera.main;

            if (shurikenObject == null || gpuObject == null || camera == null)
            {
                throw new MissingReferenceException(
                    "Validation scene requires 'Particle System', 'Particle System_GPU', and a Main Camera.");
            }

            var shuriken = shurikenObject.GetComponent<ParticleSystem>();
            var gpu = gpuObject.GetComponent<GPUParticleSystem>();
            if (shuriken == null || gpu == null)
            {
                throw new MissingComponentException("The A/B scene is missing a Shuriken or GPU particle component.");
            }

            shuriken.useAutoRandomSeed = false;
            shuriken.randomSeed = 12345;
            var main = shuriken.main;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;

            var shurikenRenderer = shuriken.GetComponent<ParticleSystemRenderer>();
            if (shurikenRenderer == null)
            {
                throw new MissingComponentException("The Shuriken system has no ParticleSystemRenderer.");
            }
            shurikenRenderer.sharedMaterial = GetOrCreateShurikenMaterial();

            var controller = camera.GetComponent<ParticleABValidationController>();
            if (controller == null)
            {
                controller = camera.gameObject.AddComponent<ParticleABValidationController>();
            }

            controller.shuriken = shuriken;
            controller.gpuParticles = gpu;
            controller.displayMode = ParticleABDisplayMode.Both;
            controller.randomSeed = 12345;
            controller.fixedFrameRate = 60;
            controller.captureOnPlay = true;
            controller.captureFrequency = 5f;
            controller.captureDuration = 10f;
            controller.captureWidth = 1280;
            controller.captureHeight = 720;
            controller.outputFolder = "TestResults/ParticleAB";
            controller.exitEditorWhenCaptureCompletes = exitWhenComplete;

            EditorUtility.SetDirty(shuriken);
            EditorUtility.SetDirty(shurikenRenderer);
            EditorUtility.SetDirty(controller);
            EditorSceneManager.MarkSceneDirty(validationScene);
            EditorSceneManager.SaveScene(validationScene);
            AssetDatabase.SaveAssets();
        }

        static Material GetOrCreateShurikenMaterial()
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(ShurikenMaterialPath);
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null)
            {
                throw new MissingReferenceException("URP Particles/Unlit shader is unavailable.");
            }

            if (material == null)
            {
                material = new Material(shader)
                {
                    name = "ParticleABShuriken"
                };
                AssetDatabase.CreateAsset(material, ShurikenMaterialPath);
            }
            else if (material.shader != shader)
            {
                material.shader = shader;
            }

            material.SetColor("_BaseColor", Color.white);
            material.SetTexture("_BaseMap", Texture2D.whiteTexture);
            material.SetFloat("_Surface", (float)BaseShaderGUI.SurfaceType.Transparent);
            material.SetFloat("_Blend", (float)BaseShaderGUI.BlendMode.Alpha);
            material.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);
            material.SetFloat("_AlphaClip", 0f);
            BaseShaderGUI.SetupMaterialBlendMode(material);
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
#endif
