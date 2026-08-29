#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Rendering.Universal.ShaderGUI;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

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

            CreateOrRefreshValidationScene(false, ParticleABValidationProfile.BaselineCone);
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
            StartCapture(false, ParticleABValidationProfile.BaselineCone);
        }

        [MenuItem("Tools/GPU Particles/Run Common Features A-B RT Capture")]
        public static void RunCommonFeatureCaptureMenu()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("Stop Play Mode before starting a deterministic A/B capture.");
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            StartCapture(false, ParticleABValidationProfile.ForceOverLifetimePoint);
        }

        [MenuItem("Tools/GPU Particles/Run Randomized Main A-B RT Capture")]
        public static void RunRandomizedMainCaptureMenu()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("Stop Play Mode before starting a deterministic A/B capture.");
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            StartCapture(false, ParticleABValidationProfile.RandomizedMainPoint);
        }

        [MenuItem("Tools/GPU Particles/Validate Common Feature Mapping")]
        public static void ValidateCommonFeatureMappingMenu()
        {
            ValidateCommonFeatureMapping();
        }

        // Command line entry point:
        // Unity -batchmode -projectPath . -executeMethod GPUParticles.Editor.ParticleABValidationTool.RunBatchCapture
        public static void RunBatchCapture()
        {
            ValidateCommonFeatureMapping();
            StartCapture(true, ParticleABValidationProfile.BaselineCone);
        }

        // Command line entry point for the Point Shape + Force over Lifetime profile.
        public static void RunBatchCommonFeatureCapture()
        {
            ValidateCommonFeatureMapping();
            StartCapture(true, ParticleABValidationProfile.ForceOverLifetimePoint);
        }

        public static void RunBatchRandomizedMainCapture()
        {
            ValidateCommonFeatureMapping();
            StartCapture(true, ParticleABValidationProfile.RandomizedMainPoint);
        }

        static void StartCapture(bool exitWhenComplete, ParticleABValidationProfile profile)
        {
            CreateOrRefreshValidationScene(exitWhenComplete, profile);
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

        static void CreateOrRefreshValidationScene(
            bool exitWhenComplete,
            ParticleABValidationProfile profile)
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
            controller.validationProfile = profile;
            controller.randomSeed = 12345;
            controller.fixedFrameRate = 60;
            controller.captureOnPlay = true;
            controller.captureFrequency = 5f;
            controller.captureDuration = CaptureDuration(profile);
            controller.captureWidth = 1280;
            controller.captureHeight = 720;
            controller.outputFolder = CaptureOutputFolder(profile);
            controller.exitEditorWhenCaptureCompletes = exitWhenComplete;

            EditorUtility.SetDirty(shuriken);
            EditorUtility.SetDirty(shurikenRenderer);
            EditorUtility.SetDirty(controller);
            EditorSceneManager.MarkSceneDirty(validationScene);
            EditorSceneManager.SaveScene(validationScene);
            AssetDatabase.SaveAssets();
        }

        static float CaptureDuration(ParticleABValidationProfile profile)
        {
            switch (profile)
            {
                case ParticleABValidationProfile.BaselineCone: return 10f;
                case ParticleABValidationProfile.ForceOverLifetimePoint: return 3f;
                case ParticleABValidationProfile.RandomizedMainPoint: return 2f;
                default: return 3f;
            }
        }

        static string CaptureOutputFolder(ParticleABValidationProfile profile)
        {
            switch (profile)
            {
                case ParticleABValidationProfile.BaselineCone:
                    return "TestResults/ParticleAB";
                case ParticleABValidationProfile.ForceOverLifetimePoint:
                    return "TestResults/ParticleCommonFeatures";
                case ParticleABValidationProfile.RandomizedMainPoint:
                    return "TestResults/ParticleRandomizedMain";
                default:
                    return "TestResults/ParticleAB";
            }
        }

        static void ValidateCommonFeatureMapping()
        {
            var owner = new GameObject("ParticleCommonFeatureMappingValidation");
            owner.SetActive(false);
            Texture2D firstForceLUT = null;

            try
            {
                var shuriken = owner.AddComponent<ParticleSystem>();
                var main = shuriken.main;
                main.startLifetime = new ParticleSystem.MinMaxCurve(2f, 4f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(1f, 3f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.5f, 1.5f);
                main.startColor = new ParticleSystem.MinMaxGradient(Color.red, Color.blue);
                main.gravityModifier = new ParticleSystem.MinMaxCurve(0.25f, 0.75f);
                main.startRotation = new ParticleSystem.MinMaxCurve(-0.5f, 0.5f);

                var rotationOverLifetime = shuriken.rotationOverLifetime;
                rotationOverLifetime.enabled = true;
                rotationOverLifetime.separateAxes = false;
                rotationOverLifetime.z = new ParticleSystem.MinMaxCurve(-1f, 1f);

                var emission = shuriken.emission;
                emission.enabled = false;
                emission.rateOverTime = 12f;

                var shape = shuriken.shape;
                shape.enabled = false;

                var force = shuriken.forceOverLifetime;
                force.enabled = true;
                force.space = ParticleSystemSimulationSpace.World;
                force.randomized = true;
                force.x = new ParticleSystem.MinMaxCurve(-2f, 2f);
                force.y = new ParticleSystem.MinMaxCurve(3f);
                force.z = new ParticleSystem.MinMaxCurve(-1f, 4f);

                ShurikenConverter.Convert(owner);
                var gpu = owner.GetComponent<GPUParticleSystem>();
                Require(gpu != null, "Converter did not create GPUParticleSystem.");
                Require(gpu.randomizeStartLifetime, "Start Lifetime Two Constants was not mapped.");
                RequireApproximately(gpu.startLifetimeMin, 2f, "Start Lifetime minimum");
                RequireApproximately(gpu.startLifetime, 4f, "Start Lifetime maximum");
                Require(gpu.randomizeStartSpeed, "Start Speed Two Constants was not mapped.");
                RequireApproximately(gpu.startSpeedMin, 1f, "Start Speed minimum");
                RequireApproximately(gpu.startSpeed, 3f, "Start Speed maximum");
                Require(gpu.randomizeStartSize, "Start Size Two Constants was not mapped.");
                RequireApproximately(gpu.startSizeMin, 0.5f, "Start Size minimum");
                RequireApproximately(gpu.startSize, 1.5f, "Start Size maximum");
                Require(gpu.randomizeStartColor, "Start Color Two Colors was not mapped.");
                Require(gpu.startColorMin == Color.red && gpu.startColor == Color.blue,
                    "Start Color endpoints were not preserved.");
                Require(gpu.randomizeGravityModifier,
                    "Gravity Modifier Two Constants was not mapped.");
                RequireApproximately(gpu.gravityModifierMin, 0.25f, "Gravity minimum");
                RequireApproximately(gpu.gravityModifier, 0.75f, "Gravity maximum");
                Require(gpu.randomizeStartRotation,
                    "Start Rotation Two Constants was not mapped.");
                Require(gpu.randomizeRotationOverLifetime,
                    "Rotation over Lifetime Two Constants was not mapped.");
                Require(!gpu.emissionEnabled, "Emission.enabled was not mapped.");
                Require(gpu.shapeType == ShapeTypeGPU.Point, "Disabled Shape was not mapped to Point.");
                Require(gpu.forceOverLifetimeEnabled, "Force over Lifetime enabled state was not mapped.");
                Require(gpu.forceOverLifetimeSpace == SimulationSpace.World,
                    "Force over Lifetime space was not mapped.");
                Require(gpu.forceOverLifetimeRandomized,
                    "Force over Lifetime randomized state was not mapped.");
                Require(gpu.forceOverLifetimeLUT != null,
                    "Force over Lifetime MinMaxCurve LUT was not generated.");

                firstForceLUT = gpu.forceOverLifetimeLUT;
                Color minimum = firstForceLUT.GetPixel(0, 0);
                Color maximum = firstForceLUT.GetPixel(0, 1);
                RequireApproximately(minimum.r, -2f, "Force minimum X");
                RequireApproximately(minimum.g, 3f, "Force minimum Y");
                RequireApproximately(minimum.b, -1f, "Force minimum Z");
                RequireApproximately(maximum.r, 2f, "Force maximum X");
                RequireApproximately(maximum.g, 3f, "Force maximum Y");
                RequireApproximately(maximum.b, 4f, "Force maximum Z");

                Object.DestroyImmediate(firstForceLUT);
                firstForceLUT = null;
                gpu.forceOverLifetimeLUT = null;

                shape.enabled = true;
                shape.shapeType = ParticleSystemShapeType.Circle;
                shape.radius = 3f;
                shape.scale = new Vector3(2f, 3f, 1f);
                shape.arc = 90f;
                ShurikenConverter.Convert(owner);

                Require(gpu.shapeType == ShapeTypeGPU.Circle, "Circle Shape was not mapped.");
                RequireApproximately(gpu.shapeCircleRadius, 3f,
                    "Circle radius must remain unscaled before GPU Shape TRS");
                Require(gpu.shapeLocalScale == shape.scale, "Shape scale was not preserved.");
                RequireApproximately(gpu.shapeConeArcDeg, 90f, "Shape Arc");

                if (gpu.forceOverLifetimeLUT != null)
                {
                    Object.DestroyImmediate(gpu.forceOverLifetimeLUT);
                    gpu.forceOverLifetimeLUT = null;
                }

                Debug.Log("PARTICLE_COMMON_FEATURE_MAPPING_RESULT:PASS");
            }
            finally
            {
                if (firstForceLUT != null) Object.DestroyImmediate(firstForceLUT);
                Object.DestroyImmediate(owner);
            }
        }

        static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        static void RequireApproximately(float actual, float expected, string label)
        {
            if (Mathf.Abs(actual - expected) > 0.01f)
            {
                throw new InvalidOperationException(
                    $"{label} mismatch. Expected {expected:R}, got {actual:R}.");
            }
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
