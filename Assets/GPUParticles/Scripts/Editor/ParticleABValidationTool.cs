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

        [MenuItem("Tools/GPU Particles/Run Emission Burst A-B RT Capture")]
        public static void RunEmissionBurstCaptureMenu()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("Stop Play Mode before starting a deterministic A/B capture.");
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            StartCapture(false, ParticleABValidationProfile.EmissionBurstPoint);
        }

        [MenuItem("Tools/GPU Particles/Run Emission Rate Curve A-B RT Capture")]
        public static void RunEmissionRateCurveCaptureMenu()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("Stop Play Mode before starting a deterministic A/B capture.");
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            StartCapture(false, ParticleABValidationProfile.EmissionRateCurvePoint);
        }

        [MenuItem("Tools/GPU Particles/Run Emission Rate Distance A-B RT Capture")]
        public static void RunEmissionRateDistanceCaptureMenu()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("Stop Play Mode before starting a deterministic A/B capture.");
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            StartCapture(false, ParticleABValidationProfile.EmissionRateDistancePoint);
        }

        [MenuItem("Tools/GPU Particles/Run Velocity over Lifetime A-B RT Capture")]
        public static void RunVelocityOverLifetimeCaptureMenu()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("Stop Play Mode before starting a deterministic A/B capture.");
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            StartCapture(false, ParticleABValidationProfile.VelocityOverLifetimePoint);
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

        public static void RunBatchEmissionBurstCapture()
        {
            ValidateCommonFeatureMapping();
            StartCapture(true, ParticleABValidationProfile.EmissionBurstPoint);
        }

        public static void RunBatchEmissionRateCurveCapture()
        {
            ValidateCommonFeatureMapping();
            StartCapture(true, ParticleABValidationProfile.EmissionRateCurvePoint);
        }

        public static void RunBatchEmissionRateDistanceCapture()
        {
            ValidateCommonFeatureMapping();
            StartCapture(true, ParticleABValidationProfile.EmissionRateDistancePoint);
        }

        public static void RunBatchVelocityOverLifetimeCapture()
        {
            ValidateCommonFeatureMapping();
            StartCapture(true, ParticleABValidationProfile.VelocityOverLifetimePoint);
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
            controller.captureFrequency = CaptureFrequency(profile);
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
                case ParticleABValidationProfile.EmissionBurstPoint: return 2.7f;
                case ParticleABValidationProfile.EmissionRateCurvePoint: return 2.2f;
                case ParticleABValidationProfile.EmissionRateDistancePoint: return 2.2f;
                case ParticleABValidationProfile.VelocityOverLifetimePoint: return 3f;
                default: return 3f;
            }
        }

        static float CaptureFrequency(ParticleABValidationProfile profile)
        {
            return profile == ParticleABValidationProfile.EmissionBurstPoint ||
                   profile == ParticleABValidationProfile.EmissionRateCurvePoint ||
                   profile == ParticleABValidationProfile.EmissionRateDistancePoint
                ? 20f
                : 5f;
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
                case ParticleABValidationProfile.EmissionBurstPoint:
                    return "TestResults/ParticleEmissionBurst";
                case ParticleABValidationProfile.EmissionRateCurvePoint:
                    return "TestResults/ParticleEmissionRateCurve";
                case ParticleABValidationProfile.EmissionRateDistancePoint:
                    return "TestResults/ParticleEmissionRateDistance";
                case ParticleABValidationProfile.VelocityOverLifetimePoint:
                    return "TestResults/ParticleVelocityOverLifetime";
                default:
                    return "TestResults/ParticleAB";
            }
        }

        static void ValidateCommonFeatureMapping()
        {
            var owner = new GameObject("ParticleCommonFeatureMappingValidation");
            owner.SetActive(false);
            Texture2D firstForceLUT = null;
            Texture2D firstVelocityLUT = null;
            string firstForceAssetPath = null;
            string secondForceAssetPath = null;
            string firstVelocityAssetPath = null;
            string secondVelocityAssetPath = null;

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
                main.duration = 2f;
                main.loop = true;
                main.startDelay = new ParticleSystem.MinMaxCurve(0.1f, 0.2f);

                var rotationOverLifetime = shuriken.rotationOverLifetime;
                rotationOverLifetime.enabled = true;
                rotationOverLifetime.separateAxes = false;
                rotationOverLifetime.z = new ParticleSystem.MinMaxCurve(-1f, 1f);

                var emission = shuriken.emission;
                emission.enabled = false;
                emission.rateOverTime = new ParticleSystem.MinMaxCurve(
                    12f, AnimationCurve.Linear(0f, 0.25f, 1f, 1f));
                emission.rateOverDistance = new ParticleSystem.MinMaxCurve(2f, 6f);
                var validationBurst = new ParticleSystem.Burst(
                    0.25f, new ParticleSystem.MinMaxCurve(5f, 9f), 3, 0.5f)
                {
                    probability = 0.75f
                };
                emission.SetBursts(new[] { validationBurst });

                var shape = shuriken.shape;
                shape.enabled = false;

                var force = shuriken.forceOverLifetime;
                force.enabled = true;
                force.space = ParticleSystemSimulationSpace.World;
                force.randomized = true;
                force.x = new ParticleSystem.MinMaxCurve(-2f, 2f);
                force.y = new ParticleSystem.MinMaxCurve(3f);
                force.z = new ParticleSystem.MinMaxCurve(-1f, 4f);

                var velocity = shuriken.velocityOverLifetime;
                velocity.enabled = true;
                velocity.space = ParticleSystemSimulationSpace.World;
                velocity.x = new ParticleSystem.MinMaxCurve(-3f, 3f);
                velocity.y = new ParticleSystem.MinMaxCurve(2f, 2f);
                velocity.z = new ParticleSystem.MinMaxCurve(-1f, 4f);
                velocity.speedModifier = 1f;

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
                Require(gpu.emissionRateOverTimeMode == ParticleSystemCurveMode.Curve,
                    "Emission Rate over Time Curve mode was not mapped.");
                RequireApproximately(gpu.emissionRateOverTimeCurveMultiplier, 12f,
                    "Emission Rate over Time curve multiplier");
                Require(gpu.emissionRateOverDistanceMode == ParticleSystemCurveMode.TwoConstants,
                    "Emission Rate over Distance Two Constants mode was not mapped.");
                RequireApproximately(gpu.emissionRateOverDistanceMin, 2f,
                    "Emission Rate over Distance minimum");
                RequireApproximately(gpu.emissionRateOverDistance, 6f,
                    "Emission Rate over Distance maximum");
                RequireApproximately(gpu.emissionDuration, 2f, "Emission duration");
                Require(gpu.emissionLooping, "Emission looping state was not mapped.");
                Require(gpu.randomizeEmissionStartDelay,
                    "Emission Start Delay Two Constants was not mapped.");
                RequireApproximately(gpu.emissionStartDelayMin, 0.1f,
                    "Emission Start Delay minimum");
                RequireApproximately(gpu.emissionStartDelay, 0.2f,
                    "Emission Start Delay maximum");
                Require(gpu.emissionBursts != null && gpu.emissionBursts.Length == 1,
                    "Emission Burst array was not mapped.");
                GPUEmissionBurst mappedBurst = gpu.emissionBursts[0];
                RequireApproximately(mappedBurst.time, 0.25f, "Burst time");
                Require(mappedBurst.countMode == ParticleSystemCurveMode.TwoConstants,
                    "Burst count mode was not mapped.");
                RequireApproximately(mappedBurst.countMin, 5f, "Burst count minimum");
                RequireApproximately(mappedBurst.countMax, 9f, "Burst count maximum");
                Require(mappedBurst.cycleCount == 3, "Burst cycle count was not mapped.");
                RequireApproximately(mappedBurst.repeatInterval, 0.5f, "Burst interval");
                RequireApproximately(mappedBurst.probability, 0.75f, "Burst probability");
                Require(gpu.shapeType == ShapeTypeGPU.Point, "Disabled Shape was not mapped to Point.");
                Require(gpu.forceOverLifetimeEnabled, "Force over Lifetime enabled state was not mapped.");
                Require(gpu.forceOverLifetimeSpace == SimulationSpace.World,
                    "Force over Lifetime space was not mapped.");
                Require(gpu.forceOverLifetimeRandomized,
                    "Force over Lifetime randomized state was not mapped.");
                Require(gpu.forceOverLifetimeLUT != null,
                    "Force over Lifetime MinMaxCurve LUT was not generated.");
                Require(gpu.velocityOverLifetimeEnabled,
                    "Velocity over Lifetime enabled state was not mapped.");
                Require(gpu.velocityOverLifetimeSpace == SimulationSpace.World,
                    "Velocity over Lifetime space was not mapped.");
                Require(gpu.velocityOverLifetimeLUT != null,
                    "Velocity over Lifetime Linear XYZ LUT was not generated.");

                firstForceLUT = gpu.forceOverLifetimeLUT;
                firstForceAssetPath = AssetDatabase.GetAssetPath(firstForceLUT);
                Color minimum = firstForceLUT.GetPixel(0, 0);
                Color maximum = firstForceLUT.GetPixel(0, 1);
                RequireApproximately(minimum.r, -2f, "Force minimum X");
                RequireApproximately(minimum.g, 3f, "Force minimum Y");
                RequireApproximately(minimum.b, -1f, "Force minimum Z");
                RequireApproximately(maximum.r, 2f, "Force maximum X");
                RequireApproximately(maximum.g, 3f, "Force maximum Y");
                RequireApproximately(maximum.b, 4f, "Force maximum Z");

                firstVelocityLUT = gpu.velocityOverLifetimeLUT;
                firstVelocityAssetPath = AssetDatabase.GetAssetPath(firstVelocityLUT);
                minimum = firstVelocityLUT.GetPixel(0, 0);
                maximum = firstVelocityLUT.GetPixel(0, 1);
                RequireApproximately(minimum.r, -3f, "Velocity minimum X");
                RequireApproximately(minimum.g, 2f, "Velocity minimum Y");
                RequireApproximately(minimum.b, -1f, "Velocity minimum Z");
                RequireApproximately(maximum.r, 3f, "Velocity maximum X");
                RequireApproximately(maximum.g, 2f, "Velocity maximum Y");
                RequireApproximately(maximum.b, 4f, "Velocity maximum Z");

                if (string.IsNullOrEmpty(firstForceAssetPath))
                {
                    Object.DestroyImmediate(firstForceLUT);
                }
                firstForceLUT = null;
                gpu.forceOverLifetimeLUT = null;

                if (string.IsNullOrEmpty(firstVelocityAssetPath))
                {
                    Object.DestroyImmediate(firstVelocityLUT);
                }
                firstVelocityLUT = null;
                gpu.velocityOverLifetimeLUT = null;

                emission.rateOverTime = new ParticleSystem.MinMaxCurve(4f, 8f);
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
                Require(gpu.emissionRateOverTimeMode == ParticleSystemCurveMode.TwoConstants,
                    "Emission Rate over Time Two Constants mode was not mapped.");
                RequireApproximately(gpu.emissionRateOverTimeMin, 4f,
                    "Emission Rate over Time minimum");
                RequireApproximately(gpu.emissionRateOverTime, 8f,
                    "Emission Rate over Time maximum");

                if (gpu.forceOverLifetimeLUT != null)
                {
                    secondForceAssetPath = AssetDatabase.GetAssetPath(gpu.forceOverLifetimeLUT);
                    if (string.IsNullOrEmpty(secondForceAssetPath))
                    {
                        Object.DestroyImmediate(gpu.forceOverLifetimeLUT);
                    }
                    gpu.forceOverLifetimeLUT = null;
                }

                if (gpu.velocityOverLifetimeLUT != null)
                {
                    secondVelocityAssetPath =
                        AssetDatabase.GetAssetPath(gpu.velocityOverLifetimeLUT);
                    if (string.IsNullOrEmpty(secondVelocityAssetPath))
                    {
                        Object.DestroyImmediate(gpu.velocityOverLifetimeLUT);
                    }
                    gpu.velocityOverLifetimeLUT = null;
                }

                Debug.Log("PARTICLE_COMMON_FEATURE_MAPPING_RESULT:PASS");
            }
            finally
            {
                if (firstForceLUT != null && string.IsNullOrEmpty(firstForceAssetPath))
                {
                    Object.DestroyImmediate(firstForceLUT);
                }
                if (firstVelocityLUT != null &&
                    string.IsNullOrEmpty(firstVelocityAssetPath))
                {
                    Object.DestroyImmediate(firstVelocityLUT);
                }
                Object.DestroyImmediate(owner);
                if (!string.IsNullOrEmpty(firstForceAssetPath))
                {
                    AssetDatabase.DeleteAsset(firstForceAssetPath);
                }
                if (!string.IsNullOrEmpty(secondForceAssetPath) &&
                    secondForceAssetPath != firstForceAssetPath)
                {
                    AssetDatabase.DeleteAsset(secondForceAssetPath);
                }
                if (!string.IsNullOrEmpty(firstVelocityAssetPath))
                {
                    AssetDatabase.DeleteAsset(firstVelocityAssetPath);
                }
                if (!string.IsNullOrEmpty(secondVelocityAssetPath) &&
                    secondVelocityAssetPath != firstVelocityAssetPath)
                {
                    AssetDatabase.DeleteAsset(secondVelocityAssetPath);
                }
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
