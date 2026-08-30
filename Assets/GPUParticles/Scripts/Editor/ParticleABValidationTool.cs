#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Rendering.Universal.ShaderGUI;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
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

        [MenuItem("Tools/GPU Particles/Run Start Color Gradient A-B RT Capture")]
        public static void RunStartColorGradientCaptureMenu()
        {
            StartStartColorCapture(
                ParticleABValidationProfile.StartColorGradientPoint);
        }

        [MenuItem("Tools/GPU Particles/Run Start Color Two Gradients A-B RT Capture")]
        public static void RunStartColorTwoGradientsCaptureMenu()
        {
            StartStartColorCapture(
                ParticleABValidationProfile.StartColorTwoGradientsPoint);
        }

        [MenuItem("Tools/GPU Particles/Run Start Color Random Color A-B RT Capture")]
        public static void RunStartColorRandomColorCaptureMenu()
        {
            StartStartColorCapture(
                ParticleABValidationProfile.StartColorRandomColorPoint);
        }

        static void StartStartColorCapture(
            ParticleABValidationProfile profile)
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning(
                    "Stop Play Mode before starting a deterministic A/B capture.");
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }
            StartCapture(false, profile);
        }

        [MenuItem("Tools/GPU Particles/Run Start Lifetime Curve A-B RT Capture")]
        public static void RunStartLifetimeCurveCaptureMenu()
        {
            StartStartLifetimeCapture(
                ParticleABValidationProfile.StartLifetimeCurvePoint);
        }

        [MenuItem("Tools/GPU Particles/Run Start Lifetime Two Curves A-B RT Capture")]
        public static void RunStartLifetimeTwoCurvesCaptureMenu()
        {
            StartStartLifetimeCapture(
                ParticleABValidationProfile.StartLifetimeTwoCurvesPoint);
        }

        static void StartStartLifetimeCapture(
            ParticleABValidationProfile profile)
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning(
                    "Stop Play Mode before starting a deterministic A/B capture.");
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }
            StartCapture(false, profile);
        }

        [MenuItem("Tools/GPU Particles/Run Start Rotation Curve A-B RT Capture")]
        public static void RunStartRotationCurveCaptureMenu()
        {
            StartStartRotationCapture(
                ParticleABValidationProfile.StartRotationCurvePoint);
        }

        [MenuItem("Tools/GPU Particles/Run Start Rotation Two Curves A-B RT Capture")]
        public static void RunStartRotationTwoCurvesCaptureMenu()
        {
            StartStartRotationCapture(
                ParticleABValidationProfile.StartRotationTwoCurvesPoint);
        }

        static void StartStartRotationCapture(
            ParticleABValidationProfile profile)
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning(
                    "Stop Play Mode before starting a deterministic A/B capture.");
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }
            StartCapture(false, profile);
        }

        [MenuItem("Tools/GPU Particles/Run Start Speed Curve A-B RT Capture")]
        public static void RunStartSpeedCurveCaptureMenu()
        {
            StartStartSpeedCapture(
                ParticleABValidationProfile.StartSpeedCurvePoint);
        }

        [MenuItem("Tools/GPU Particles/Run Start Speed Two Curves A-B RT Capture")]
        public static void RunStartSpeedTwoCurvesCaptureMenu()
        {
            StartStartSpeedCapture(
                ParticleABValidationProfile.StartSpeedTwoCurvesPoint);
        }

        static void StartStartSpeedCapture(
            ParticleABValidationProfile profile)
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning(
                    "Stop Play Mode before starting a deterministic A/B capture.");
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }
            StartCapture(false, profile);
        }

        [MenuItem("Tools/GPU Particles/Run Start Size Curve A-B RT Capture")]
        public static void RunStartSizeCurveCaptureMenu()
        {
            StartStartSizeCapture(
                ParticleABValidationProfile.StartSizeCurvePoint);
        }

        [MenuItem("Tools/GPU Particles/Run Start Size Two Curves A-B RT Capture")]
        public static void RunStartSizeTwoCurvesCaptureMenu()
        {
            StartStartSizeCapture(
                ParticleABValidationProfile.StartSizeTwoCurvesPoint);
        }

        [MenuItem("Tools/GPU Particles/Run Separate Axes Size A-B RT Capture")]
        public static void RunSizeSeparateAxesCaptureMenu()
        {
            StartStartSizeCapture(
                ParticleABValidationProfile.SizeSeparateAxesPoint);
        }

        [MenuItem("Tools/GPU Particles/Run Renderer Screen Size Clamp A-B RT Capture")]
        public static void RunRendererScreenSizeClampCaptureMenu()
        {
            StartStartSizeCapture(
                ParticleABValidationProfile.RendererScreenSizeClampPoint);
        }

        [MenuItem("Tools/GPU Particles/Run Unscaled Time A-B RT Capture")]
        public static void RunUnscaledTimeCaptureMenu()
        {
            StartStartSizeCapture(
                ParticleABValidationProfile.UnscaledTimePoint);
        }

        [MenuItem("Tools/GPU Particles/Run Scaling Hierarchy A-B RT Capture")]
        public static void RunScalingHierarchyCaptureMenu()
        {
            StartStartSizeCapture(
                ParticleABValidationProfile.ScalingHierarchyPoint);
        }

        [MenuItem("Tools/GPU Particles/Run Scaling Local A-B RT Capture")]
        public static void RunScalingLocalCaptureMenu()
        {
            StartStartSizeCapture(
                ParticleABValidationProfile.ScalingLocalPoint);
        }

        [MenuItem("Tools/GPU Particles/Run Scaling Shape A-B RT Capture")]
        public static void RunScalingShapeCaptureMenu()
        {
            StartStartSizeCapture(
                ParticleABValidationProfile.ScalingShapePoint);
        }

        [MenuItem("Tools/GPU Particles/Run Playback Lifecycle A-B RT Capture")]
        public static void RunPlaybackLifecycleCaptureMenu()
        {
            StartStartSizeCapture(
                ParticleABValidationProfile.PlaybackLifecyclePoint);
        }

        [MenuItem("Tools/GPU Particles/Run Prewarm A-B RT Capture")]
        public static void RunPrewarmCaptureMenu()
        {
            StartStartSizeCapture(ParticleABValidationProfile.PrewarmPoint);
        }

        [MenuItem("Tools/GPU Particles/Run Flip Rotation A-B RT Capture")]
        public static void RunFlipRotationCaptureMenu()
        {
            StartStartSizeCapture(
                ParticleABValidationProfile.FlipRotationPoint);
        }

        [MenuItem("Tools/GPU Particles/Run Gravity Source 2D A-B RT Capture")]
        public static void RunGravitySource2DCaptureMenu()
        {
            StartStartSizeCapture(
                ParticleABValidationProfile.GravitySource2DPoint);
        }

        [MenuItem("Tools/GPU Particles/Run Custom Simulation Space A-B RT Capture")]
        public static void RunCustomSimulationSpaceCaptureMenu()
        {
            StartStartSizeCapture(
                ParticleABValidationProfile.CustomSimulationSpacePoint);
        }

        static void StartStartSizeCapture(
            ParticleABValidationProfile profile)
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning(
                    "Stop Play Mode before starting a deterministic A/B capture.");
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }
            StartCapture(false, profile);
        }

        [MenuItem("Tools/GPU Particles/Run Gravity Modifier Curve A-B RT Capture")]
        public static void RunGravityModifierCurveCaptureMenu()
        {
            StartGravityModifierCapture(
                ParticleABValidationProfile.GravityModifierCurvePoint);
        }

        [MenuItem("Tools/GPU Particles/Run Gravity Modifier Two Curves A-B RT Capture")]
        public static void RunGravityModifierTwoCurvesCaptureMenu()
        {
            StartGravityModifierCapture(
                ParticleABValidationProfile.GravityModifierTwoCurvesPoint);
        }

        static void StartGravityModifierCapture(
            ParticleABValidationProfile profile)
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning(
                    "Stop Play Mode before starting a deterministic A/B capture.");
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }
            StartCapture(false, profile);
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

        [MenuItem("Tools/GPU Particles/Run Velocity Orbital Radial A-B RT Capture")]
        public static void RunVelocityOrbitalRadialCaptureMenu()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("Stop Play Mode before starting a deterministic A/B capture.");
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            StartCapture(
                false,
                ParticleABValidationProfile.VelocityOrbitalRadialPoint);
        }

        [MenuItem("Tools/GPU Particles/Run Velocity Speed Modifier A-B RT Capture")]
        public static void RunVelocitySpeedModifierCaptureMenu()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("Stop Play Mode before starting a deterministic A/B capture.");
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            StartCapture(
                false,
                ParticleABValidationProfile.VelocitySpeedModifierPoint);
        }

        [MenuItem("Tools/GPU Particles/Run Limit Velocity over Lifetime A-B RT Capture")]
        public static void RunLimitVelocityOverLifetimeCaptureMenu()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("Stop Play Mode before starting a deterministic A/B capture.");
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            StartCapture(
                false,
                ParticleABValidationProfile.LimitVelocityOverLifetimePoint);
        }

        [MenuItem("Tools/GPU Particles/Run Limit Velocity Axes A-B RT Capture")]
        public static void RunLimitVelocityOverLifetimeAxesCaptureMenu()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("Stop Play Mode before starting a deterministic A/B capture.");
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            StartCapture(
                false,
                ParticleABValidationProfile.LimitVelocityOverLifetimeAxesPoint);
        }

        [MenuItem("Tools/GPU Particles/Run Inherit Velocity Initial A-B RT Capture")]
        public static void RunInheritVelocityInitialCaptureMenu()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("Stop Play Mode before starting a deterministic A/B capture.");
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            StartCapture(
                false,
                ParticleABValidationProfile.InheritVelocityInitialPoint);
        }

        [MenuItem("Tools/GPU Particles/Run Inherit Velocity Current A-B RT Capture")]
        public static void RunInheritVelocityCurrentCaptureMenu()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("Stop Play Mode before starting a deterministic A/B capture.");
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            StartCapture(
                false,
                ParticleABValidationProfile.InheritVelocityCurrentPoint);
        }

        [MenuItem("Tools/GPU Particles/Run Lifetime by Emitter Speed A-B RT Capture")]
        public static void RunLifetimeByEmitterSpeedCaptureMenu()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning(
                    "Stop Play Mode before starting a deterministic A/B capture.");
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }
            StartCapture(
                false,
                ParticleABValidationProfile.LifetimeByEmitterSpeedPoint);
        }

        [MenuItem("Tools/GPU Particles/Run Texture Sheet Lifetime A-B RT Capture")]
        public static void RunTextureSheetLifetimeCaptureMenu()
        {
            StartTextureSheetCaptureMenu(
                ParticleABValidationProfile.TextureSheetLifetimePoint);
        }

        [MenuItem("Tools/GPU Particles/Run Texture Sheet Speed A-B RT Capture")]
        public static void RunTextureSheetSpeedCaptureMenu()
        {
            StartTextureSheetCaptureMenu(
                ParticleABValidationProfile.TextureSheetSpeedPoint);
        }

        [MenuItem("Tools/GPU Particles/Run Texture Sheet FPS A-B RT Capture")]
        public static void RunTextureSheetFPSCaptureMenu()
        {
            StartTextureSheetCaptureMenu(
                ParticleABValidationProfile.TextureSheetFPSPoint);
        }

        [MenuItem("Tools/GPU Particles/Run Texture Sheet Single Row A-B RT Capture")]
        public static void RunTextureSheetSingleRowCaptureMenu()
        {
            StartTextureSheetCaptureMenu(
                ParticleABValidationProfile.TextureSheetSingleRowPoint);
        }

        [MenuItem("Tools/GPU Particles/Run Shape Sphere A-B RT Capture")]
        public static void RunShapeSphereCaptureMenu()
        {
            StartShapeCaptureMenu(ParticleABValidationProfile.ShapeSpherePoint);
        }

        [MenuItem("Tools/GPU Particles/Run Shape Circle A-B RT Capture")]
        public static void RunShapeCircleCaptureMenu()
        {
            StartShapeCaptureMenu(ParticleABValidationProfile.ShapeCirclePoint);
        }

        [MenuItem("Tools/GPU Particles/Run Shape Donut A-B RT Capture")]
        public static void RunShapeDonutCaptureMenu()
        {
            StartShapeCaptureMenu(ParticleABValidationProfile.ShapeDonutPoint);
        }

        [MenuItem("Tools/GPU Particles/Run Shape Edge A-B RT Capture")]
        public static void RunShapeEdgeCaptureMenu()
        {
            StartShapeCaptureMenu(ParticleABValidationProfile.ShapeEdgePoint);
        }

        [MenuItem("Tools/GPU Particles/Run Shape Rectangle A-B RT Capture")]
        public static void RunShapeRectangleCaptureMenu()
        {
            StartShapeCaptureMenu(
                ParticleABValidationProfile.ShapeRectanglePoint);
        }

        [MenuItem("Tools/GPU Particles/Run Shape Box Edge A-B RT Capture")]
        public static void RunShapeBoxEdgeCaptureMenu()
        {
            StartShapeCaptureMenu(ParticleABValidationProfile.ShapeBoxEdgePoint);
        }

        static void StartShapeCaptureMenu(ParticleABValidationProfile profile)
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning(
                    "Stop Play Mode before starting a deterministic A/B capture.");
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }
            StartCapture(false, profile);
        }

        static void StartTextureSheetCaptureMenu(
            ParticleABValidationProfile profile)
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning(
                    "Stop Play Mode before starting a deterministic A/B capture.");
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }
            StartCapture(false, profile);
        }

        [MenuItem("Tools/GPU Particles/Run Rotation over Lifetime A-B RT Capture")]
        public static void RunRotationOverLifetimeCaptureMenu()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("Stop Play Mode before starting a deterministic A/B capture.");
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            StartCapture(false, ParticleABValidationProfile.RotationOverLifetimeCurvePoint);
        }

        [MenuItem("Tools/GPU Particles/Run Rotation by Speed A-B RT Capture")]
        public static void RunRotationBySpeedCaptureMenu()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("Stop Play Mode before starting a deterministic A/B capture.");
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            StartCapture(false, ParticleABValidationProfile.RotationBySpeedCurvePoint);
        }

        [MenuItem("Tools/GPU Particles/Run Color Size over Lifetime A-B RT Capture")]
        public static void RunColorSizeOverLifetimeCaptureMenu()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("Stop Play Mode before starting a deterministic A/B capture.");
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            StartCapture(
                false,
                ParticleABValidationProfile.ColorSizeOverLifetimeRandomizedPoint);
        }

        [MenuItem("Tools/GPU Particles/Run Color Size by Speed A-B RT Capture")]
        public static void RunColorSizeBySpeedCaptureMenu()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("Stop Play Mode before starting a deterministic A/B capture.");
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            StartCapture(
                false,
                ParticleABValidationProfile.ColorSizeBySpeedRandomizedPoint);
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

        public static void RunBatchStartColorGradientCapture()
        {
            ValidateCommonFeatureMapping();
            StartCapture(
                true,
                ParticleABValidationProfile.StartColorGradientPoint);
        }

        public static void RunBatchStartColorTwoGradientsCapture()
        {
            ValidateCommonFeatureMapping();
            StartCapture(
                true,
                ParticleABValidationProfile.StartColorTwoGradientsPoint);
        }

        public static void RunBatchStartColorRandomColorCapture()
        {
            ValidateCommonFeatureMapping();
            StartCapture(
                true,
                ParticleABValidationProfile.StartColorRandomColorPoint);
        }

        public static void RunBatchStartLifetimeCurveCapture()
        {
            ValidateCommonFeatureMapping();
            StartCapture(
                true,
                ParticleABValidationProfile.StartLifetimeCurvePoint);
        }

        public static void RunBatchStartLifetimeTwoCurvesCapture()
        {
            ValidateCommonFeatureMapping();
            StartCapture(
                true,
                ParticleABValidationProfile.StartLifetimeTwoCurvesPoint);
        }

        public static void RunBatchStartRotationCurveCapture()
        {
            ValidateCommonFeatureMapping();
            StartCapture(
                true,
                ParticleABValidationProfile.StartRotationCurvePoint);
        }

        public static void RunBatchStartRotationTwoCurvesCapture()
        {
            ValidateCommonFeatureMapping();
            StartCapture(
                true,
                ParticleABValidationProfile.StartRotationTwoCurvesPoint);
        }

        public static void RunBatchStartSpeedCurveCapture()
        {
            ValidateCommonFeatureMapping();
            StartCapture(
                true,
                ParticleABValidationProfile.StartSpeedCurvePoint);
        }

        public static void RunBatchStartSpeedTwoCurvesCapture()
        {
            ValidateCommonFeatureMapping();
            StartCapture(
                true,
                ParticleABValidationProfile.StartSpeedTwoCurvesPoint);
        }

        public static void RunBatchStartSizeCurveCapture()
        {
            ValidateCommonFeatureMapping();
            StartCapture(
                true,
                ParticleABValidationProfile.StartSizeCurvePoint);
        }

        public static void RunBatchStartSizeTwoCurvesCapture()
        {
            ValidateCommonFeatureMapping();
            StartCapture(
                true,
                ParticleABValidationProfile.StartSizeTwoCurvesPoint);
        }

        public static void RunBatchSizeSeparateAxesCapture()
        {
            ValidateCommonFeatureMapping();
            StartCapture(
                true,
                ParticleABValidationProfile.SizeSeparateAxesPoint);
        }

        public static void RunBatchRendererScreenSizeClampCapture()
        {
            ValidateCommonFeatureMapping();
            StartCapture(
                true,
                ParticleABValidationProfile.RendererScreenSizeClampPoint);
        }

        public static void RunBatchUnscaledTimeCapture()
        {
            ValidateCommonFeatureMapping();
            StartCapture(
                true,
                ParticleABValidationProfile.UnscaledTimePoint);
        }

        public static void RunBatchScalingHierarchyCapture()
        {
            ValidateCommonFeatureMapping();
            StartCapture(
                true,
                ParticleABValidationProfile.ScalingHierarchyPoint);
        }

        public static void RunBatchScalingLocalCapture()
        {
            ValidateCommonFeatureMapping();
            StartCapture(
                true,
                ParticleABValidationProfile.ScalingLocalPoint);
        }

        public static void RunBatchScalingShapeCapture()
        {
            ValidateCommonFeatureMapping();
            StartCapture(
                true,
                ParticleABValidationProfile.ScalingShapePoint);
        }

        public static void RunBatchPlaybackLifecycleCapture()
        {
            ValidateCommonFeatureMapping();
            StartCapture(
                true,
                ParticleABValidationProfile.PlaybackLifecyclePoint);
        }

        public static void RunBatchPrewarmCapture()
        {
            ValidateCommonFeatureMapping();
            StartCapture(true, ParticleABValidationProfile.PrewarmPoint);
        }

        public static void RunBatchFlipRotationCapture()
        {
            ValidateCommonFeatureMapping();
            StartCapture(true, ParticleABValidationProfile.FlipRotationPoint);
        }

        public static void RunBatchGravitySource2DCapture()
        {
            ValidateCommonFeatureMapping();
            StartCapture(true, ParticleABValidationProfile.GravitySource2DPoint);
        }

        public static void RunBatchCustomSimulationSpaceCapture()
        {
            ValidateCommonFeatureMapping();
            StartCapture(
                true,
                ParticleABValidationProfile.CustomSimulationSpacePoint);
        }

        public static void RunBatchGravityModifierCurveCapture()
        {
            ValidateCommonFeatureMapping();
            StartCapture(
                true,
                ParticleABValidationProfile.GravityModifierCurvePoint);
        }

        public static void RunBatchGravityModifierTwoCurvesCapture()
        {
            ValidateCommonFeatureMapping();
            StartCapture(
                true,
                ParticleABValidationProfile.GravityModifierTwoCurvesPoint);
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

        public static void RunBatchVelocityOrbitalRadialCapture()
        {
            ValidateCommonFeatureMapping();
            StartCapture(
                true,
                ParticleABValidationProfile.VelocityOrbitalRadialPoint);
        }

        public static void RunBatchVelocitySpeedModifierCapture()
        {
            ValidateCommonFeatureMapping();
            StartCapture(
                true,
                ParticleABValidationProfile.VelocitySpeedModifierPoint);
        }

        public static void RunBatchLimitVelocityOverLifetimeCapture()
        {
            ValidateCommonFeatureMapping();
            StartCapture(
                true,
                ParticleABValidationProfile.LimitVelocityOverLifetimePoint);
        }

        public static void RunBatchLimitVelocityOverLifetimeAxesCapture()
        {
            ValidateCommonFeatureMapping();
            StartCapture(
                true,
                ParticleABValidationProfile.LimitVelocityOverLifetimeAxesPoint);
        }

        public static void RunBatchInheritVelocityInitialCapture()
        {
            ValidateCommonFeatureMapping();
            StartCapture(
                true,
                ParticleABValidationProfile.InheritVelocityInitialPoint);
        }

        public static void RunBatchInheritVelocityCurrentCapture()
        {
            ValidateCommonFeatureMapping();
            StartCapture(
                true,
                ParticleABValidationProfile.InheritVelocityCurrentPoint);
        }

        public static void RunBatchLifetimeByEmitterSpeedCapture()
        {
            ValidateCommonFeatureMapping();
            StartCapture(
                true,
                ParticleABValidationProfile.LifetimeByEmitterSpeedPoint);
        }

        public static void RunBatchShapeSphereCapture()
        {
            ValidateCommonFeatureMapping();
            StartCapture(true, ParticleABValidationProfile.ShapeSpherePoint);
        }

        public static void RunBatchShapeCircleCapture()
        {
            ValidateCommonFeatureMapping();
            StartCapture(true, ParticleABValidationProfile.ShapeCirclePoint);
        }

        public static void RunBatchShapeDonutCapture()
        {
            ValidateCommonFeatureMapping();
            StartCapture(true, ParticleABValidationProfile.ShapeDonutPoint);
        }

        public static void RunBatchShapeEdgeCapture()
        {
            ValidateCommonFeatureMapping();
            StartCapture(true, ParticleABValidationProfile.ShapeEdgePoint);
        }

        public static void RunBatchShapeRectangleCapture()
        {
            ValidateCommonFeatureMapping();
            StartCapture(true, ParticleABValidationProfile.ShapeRectanglePoint);
        }

        public static void RunBatchShapeBoxEdgeCapture()
        {
            ValidateCommonFeatureMapping();
            StartCapture(true, ParticleABValidationProfile.ShapeBoxEdgePoint);
        }

        public static void RunBatchTextureSheetLifetimeCapture()
        {
            ValidateCommonFeatureMapping();
            StartCapture(
                true,
                ParticleABValidationProfile.TextureSheetLifetimePoint);
        }

        public static void RunBatchTextureSheetSpeedCapture()
        {
            ValidateCommonFeatureMapping();
            StartCapture(
                true,
                ParticleABValidationProfile.TextureSheetSpeedPoint);
        }

        public static void RunBatchTextureSheetFPSCapture()
        {
            ValidateCommonFeatureMapping();
            StartCapture(
                true,
                ParticleABValidationProfile.TextureSheetFPSPoint);
        }

        public static void RunBatchTextureSheetSingleRowCapture()
        {
            ValidateCommonFeatureMapping();
            StartCapture(
                true,
                ParticleABValidationProfile.TextureSheetSingleRowPoint);
        }

        public static void RunBatchRotationOverLifetimeCapture()
        {
            ValidateCommonFeatureMapping();
            StartCapture(true, ParticleABValidationProfile.RotationOverLifetimeCurvePoint);
        }

        public static void RunBatchRotationBySpeedCapture()
        {
            ValidateCommonFeatureMapping();
            StartCapture(true, ParticleABValidationProfile.RotationBySpeedCurvePoint);
        }

        public static void RunBatchColorSizeOverLifetimeCapture()
        {
            ValidateCommonFeatureMapping();
            StartCapture(
                true,
                ParticleABValidationProfile.ColorSizeOverLifetimeRandomizedPoint);
        }

        public static void RunBatchColorSizeBySpeedCapture()
        {
            ValidateCommonFeatureMapping();
            StartCapture(
                true,
                ParticleABValidationProfile.ColorSizeBySpeedRandomizedPoint);
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
            bool validatePlaybackLifecycle = profile ==
                ParticleABValidationProfile.PlaybackLifecyclePoint;
            bool validatePrewarm = profile ==
                ParticleABValidationProfile.PrewarmPoint;
            main.playOnAwake = !validatePlaybackLifecycle;
            gpu.playOnAwake = !validatePlaybackLifecycle;
            main.prewarm = validatePrewarm;
            gpu.prewarm = validatePrewarm;

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
            EditorUtility.SetDirty(gpu);
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
                case ParticleABValidationProfile.StartColorGradientPoint:
                case ParticleABValidationProfile.StartColorTwoGradientsPoint:
                case ParticleABValidationProfile.StartColorRandomColorPoint:
                case ParticleABValidationProfile.StartSpeedCurvePoint:
                case ParticleABValidationProfile.StartSpeedTwoCurvesPoint:
                case ParticleABValidationProfile.StartSizeCurvePoint:
                case ParticleABValidationProfile.StartSizeTwoCurvesPoint:
                case ParticleABValidationProfile.GravityModifierCurvePoint:
                case ParticleABValidationProfile.GravityModifierTwoCurvesPoint:
                case ParticleABValidationProfile.StartRotationCurvePoint:
                case ParticleABValidationProfile.StartRotationTwoCurvesPoint:
                    return 2.5f;
                case ParticleABValidationProfile.SizeSeparateAxesPoint:
                    return 3f;
                case ParticleABValidationProfile.RendererScreenSizeClampPoint:
                    return 4.2f;
                case ParticleABValidationProfile.UnscaledTimePoint:
                    return 3.2f;
                case ParticleABValidationProfile.ScalingHierarchyPoint:
                case ParticleABValidationProfile.ScalingLocalPoint:
                case ParticleABValidationProfile.ScalingShapePoint:
                    return 1.5f;
                case ParticleABValidationProfile.PlaybackLifecyclePoint:
                    return 4.7f;
                case ParticleABValidationProfile.PrewarmPoint:
                    return 1f;
                case ParticleABValidationProfile.FlipRotationPoint:
                    return 1.5f;
                case ParticleABValidationProfile.GravitySource2DPoint:
                    return 2.2f;
                case ParticleABValidationProfile.CustomSimulationSpacePoint:
                    return 3.4f;
                case ParticleABValidationProfile.StartLifetimeCurvePoint:
                case ParticleABValidationProfile.StartLifetimeTwoCurvesPoint:
                    return 4.5f;
                case ParticleABValidationProfile.EmissionBurstPoint: return 2.7f;
                case ParticleABValidationProfile.EmissionRateCurvePoint: return 2.2f;
                case ParticleABValidationProfile.EmissionRateDistancePoint: return 2.2f;
                case ParticleABValidationProfile.VelocityOverLifetimePoint: return 3f;
                case ParticleABValidationProfile.VelocityOrbitalRadialPoint: return 3f;
                case ParticleABValidationProfile.VelocitySpeedModifierPoint: return 3f;
                case ParticleABValidationProfile.LimitVelocityOverLifetimePoint: return 3.5f;
                case ParticleABValidationProfile.LimitVelocityOverLifetimeAxesPoint: return 2.5f;
                case ParticleABValidationProfile.InheritVelocityInitialPoint: return 3.2f;
                case ParticleABValidationProfile.InheritVelocityCurrentPoint: return 3.2f;
                case ParticleABValidationProfile.LifetimeByEmitterSpeedPoint: return 4.2f;
                case ParticleABValidationProfile.ShapeSpherePoint:
                case ParticleABValidationProfile.ShapeCirclePoint:
                case ParticleABValidationProfile.ShapeDonutPoint:
                case ParticleABValidationProfile.ShapeEdgePoint:
                case ParticleABValidationProfile.ShapeRectanglePoint:
                case ParticleABValidationProfile.ShapeBoxEdgePoint:
                    return 2.5f;
                case ParticleABValidationProfile.TextureSheetLifetimePoint:
                case ParticleABValidationProfile.TextureSheetSpeedPoint:
                case ParticleABValidationProfile.TextureSheetFPSPoint:
                case ParticleABValidationProfile.TextureSheetSingleRowPoint:
                    return 3.8f;
                case ParticleABValidationProfile.RotationOverLifetimeCurvePoint: return 2.5f;
                case ParticleABValidationProfile.RotationBySpeedCurvePoint: return 2.5f;
                case ParticleABValidationProfile.ColorSizeOverLifetimeRandomizedPoint: return 2f;
                case ParticleABValidationProfile.ColorSizeBySpeedRandomizedPoint: return 2f;
                default: return 3f;
            }
        }

        static float CaptureFrequency(ParticleABValidationProfile profile)
        {
            if (profile == ParticleABValidationProfile.PlaybackLifecyclePoint)
            {
                return 10f;
            }
            if (profile == ParticleABValidationProfile.PrewarmPoint)
            {
                return 10f;
            }
            if (profile == ParticleABValidationProfile.FlipRotationPoint)
            {
                return 10f;
            }
            if (profile == ParticleABValidationProfile.GravitySource2DPoint)
            {
                return 10f;
            }
            if (profile ==
                ParticleABValidationProfile.CustomSimulationSpacePoint)
            {
                return 10f;
            }

            return profile == ParticleABValidationProfile.EmissionBurstPoint ||
                   profile == ParticleABValidationProfile.StartColorGradientPoint ||
                   profile == ParticleABValidationProfile.StartColorTwoGradientsPoint ||
                   profile == ParticleABValidationProfile.StartColorRandomColorPoint ||
                   profile == ParticleABValidationProfile.StartLifetimeCurvePoint ||
                   profile == ParticleABValidationProfile.StartLifetimeTwoCurvesPoint ||
                   profile == ParticleABValidationProfile.StartRotationCurvePoint ||
                   profile == ParticleABValidationProfile.StartRotationTwoCurvesPoint ||
                   profile == ParticleABValidationProfile.StartSpeedCurvePoint ||
                   profile == ParticleABValidationProfile.StartSpeedTwoCurvesPoint ||
                    profile == ParticleABValidationProfile.StartSizeCurvePoint ||
                    profile == ParticleABValidationProfile.StartSizeTwoCurvesPoint ||
                    profile == ParticleABValidationProfile.SizeSeparateAxesPoint ||
                    profile == ParticleABValidationProfile.RendererScreenSizeClampPoint ||
                    profile == ParticleABValidationProfile.UnscaledTimePoint ||
                    profile == ParticleABValidationProfile.ScalingHierarchyPoint ||
                    profile == ParticleABValidationProfile.ScalingLocalPoint ||
                    profile == ParticleABValidationProfile.ScalingShapePoint ||
                   profile == ParticleABValidationProfile.GravityModifierCurvePoint ||
                   profile == ParticleABValidationProfile.GravityModifierTwoCurvesPoint ||
                   profile == ParticleABValidationProfile.EmissionRateCurvePoint ||
                    profile == ParticleABValidationProfile.EmissionRateDistancePoint ||
                    profile == ParticleABValidationProfile.VelocityOrbitalRadialPoint ||
                    profile == ParticleABValidationProfile.VelocitySpeedModifierPoint ||
                   profile == ParticleABValidationProfile.InheritVelocityInitialPoint ||
                   profile == ParticleABValidationProfile.InheritVelocityCurrentPoint ||
                   profile == ParticleABValidationProfile.LifetimeByEmitterSpeedPoint ||
                   profile == ParticleABValidationProfile.TextureSheetLifetimePoint ||
                   profile == ParticleABValidationProfile.TextureSheetSpeedPoint ||
                   profile == ParticleABValidationProfile.TextureSheetFPSPoint ||
                   profile == ParticleABValidationProfile.TextureSheetSingleRowPoint
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
                case ParticleABValidationProfile.StartColorGradientPoint:
                    return "TestResults/ParticleStartColorGradient";
                case ParticleABValidationProfile.StartColorTwoGradientsPoint:
                    return "TestResults/ParticleStartColorTwoGradients";
                case ParticleABValidationProfile.StartColorRandomColorPoint:
                    return "TestResults/ParticleStartColorRandomColor";
                case ParticleABValidationProfile.StartLifetimeCurvePoint:
                    return "TestResults/ParticleStartLifetimeCurve";
                case ParticleABValidationProfile.StartLifetimeTwoCurvesPoint:
                    return "TestResults/ParticleStartLifetimeTwoCurves";
                case ParticleABValidationProfile.StartRotationCurvePoint:
                    return "TestResults/ParticleStartRotationCurve";
                case ParticleABValidationProfile.StartRotationTwoCurvesPoint:
                    return "TestResults/ParticleStartRotationTwoCurves";
                case ParticleABValidationProfile.StartSpeedCurvePoint:
                    return "TestResults/ParticleStartSpeedCurve";
                case ParticleABValidationProfile.StartSpeedTwoCurvesPoint:
                    return "TestResults/ParticleStartSpeedTwoCurves";
                case ParticleABValidationProfile.StartSizeCurvePoint:
                    return "TestResults/ParticleStartSizeCurve";
                case ParticleABValidationProfile.StartSizeTwoCurvesPoint:
                    return "TestResults/ParticleStartSizeTwoCurves";
                case ParticleABValidationProfile.SizeSeparateAxesPoint:
                    return "TestResults/ParticleSizeSeparateAxes";
                case ParticleABValidationProfile.RendererScreenSizeClampPoint:
                    return "TestResults/ParticleRendererScreenSizeClamp";
                case ParticleABValidationProfile.UnscaledTimePoint:
                    return "TestResults/ParticleUnscaledTime";
                case ParticleABValidationProfile.ScalingHierarchyPoint:
                    return "TestResults/ParticleScalingHierarchy";
                case ParticleABValidationProfile.ScalingLocalPoint:
                    return "TestResults/ParticleScalingLocal";
                case ParticleABValidationProfile.ScalingShapePoint:
                    return "TestResults/ParticleScalingShape";
                case ParticleABValidationProfile.PlaybackLifecyclePoint:
                    return "TestResults/ParticlePlaybackLifecycle";
                case ParticleABValidationProfile.PrewarmPoint:
                    return "TestResults/ParticlePrewarm";
                case ParticleABValidationProfile.FlipRotationPoint:
                    return "TestResults/ParticleFlipRotation";
                case ParticleABValidationProfile.GravitySource2DPoint:
                    return "TestResults/ParticleGravitySource2D";
                case ParticleABValidationProfile.CustomSimulationSpacePoint:
                    return "TestResults/ParticleCustomSimulationSpace";
                case ParticleABValidationProfile.GravityModifierCurvePoint:
                    return "TestResults/ParticleGravityModifierCurve";
                case ParticleABValidationProfile.GravityModifierTwoCurvesPoint:
                    return "TestResults/ParticleGravityModifierTwoCurves";
                case ParticleABValidationProfile.EmissionBurstPoint:
                    return "TestResults/ParticleEmissionBurst";
                case ParticleABValidationProfile.EmissionRateCurvePoint:
                    return "TestResults/ParticleEmissionRateCurve";
                case ParticleABValidationProfile.EmissionRateDistancePoint:
                    return "TestResults/ParticleEmissionRateDistance";
                case ParticleABValidationProfile.VelocityOverLifetimePoint:
                    return "TestResults/ParticleVelocityOverLifetime";
                case ParticleABValidationProfile.VelocityOrbitalRadialPoint:
                    return "TestResults/ParticleVelocityOrbitalRadial";
                case ParticleABValidationProfile.VelocitySpeedModifierPoint:
                    return "TestResults/ParticleVelocitySpeedModifier";
                case ParticleABValidationProfile.LimitVelocityOverLifetimePoint:
                    return "TestResults/ParticleLimitVelocityOverLifetime";
                case ParticleABValidationProfile.LimitVelocityOverLifetimeAxesPoint:
                    return "TestResults/ParticleLimitVelocityAxes";
                case ParticleABValidationProfile.InheritVelocityInitialPoint:
                    return "TestResults/ParticleInheritVelocityInitial";
                case ParticleABValidationProfile.InheritVelocityCurrentPoint:
                    return "TestResults/ParticleInheritVelocityCurrent";
                case ParticleABValidationProfile.LifetimeByEmitterSpeedPoint:
                    return "TestResults/ParticleLifetimeByEmitterSpeed";
                case ParticleABValidationProfile.ShapeSpherePoint:
                    return "TestResults/ParticleShapeSphere";
                case ParticleABValidationProfile.ShapeCirclePoint:
                    return "TestResults/ParticleShapeCircle";
                case ParticleABValidationProfile.ShapeDonutPoint:
                    return "TestResults/ParticleShapeDonut";
                case ParticleABValidationProfile.ShapeEdgePoint:
                    return "TestResults/ParticleShapeEdge";
                case ParticleABValidationProfile.ShapeRectanglePoint:
                    return "TestResults/ParticleShapeRectangle";
                case ParticleABValidationProfile.ShapeBoxEdgePoint:
                    return "TestResults/ParticleShapeBoxEdge";
                case ParticleABValidationProfile.TextureSheetLifetimePoint:
                    return "TestResults/ParticleTextureSheetLifetime";
                case ParticleABValidationProfile.TextureSheetSpeedPoint:
                    return "TestResults/ParticleTextureSheetSpeed";
                case ParticleABValidationProfile.TextureSheetFPSPoint:
                    return "TestResults/ParticleTextureSheetFPS";
                case ParticleABValidationProfile.TextureSheetSingleRowPoint:
                    return "TestResults/ParticleTextureSheetSingleRow";
                case ParticleABValidationProfile.RotationOverLifetimeCurvePoint:
                    return "TestResults/ParticleRotationOverLifetime";
                case ParticleABValidationProfile.RotationBySpeedCurvePoint:
                    return "TestResults/ParticleRotationBySpeed";
                case ParticleABValidationProfile.ColorSizeOverLifetimeRandomizedPoint:
                    return "TestResults/ParticleColorSizeOverLifetime";
                case ParticleABValidationProfile.ColorSizeBySpeedRandomizedPoint:
                    return "TestResults/ParticleColorSizeBySpeed";
                default:
                    return "TestResults/ParticleAB";
            }
        }

        static void ValidateCommonFeatureMapping()
        {
            var owner = new GameObject("ParticleCommonFeatureMappingValidation");
            owner.SetActive(false);
            var customSpaceObject = new GameObject(
                "ParticleCommonFeatureCustomSimulationSpace");
            customSpaceObject.transform.SetParent(owner.transform, false);
            customSpaceObject.transform.localPosition =
                new Vector3(1f, 2f, 3f);
            customSpaceObject.transform.localRotation =
                Quaternion.Euler(10f, 20f, 30f);
            Texture2D firstForceLUT = null;
            Texture2D firstVelocityLUT = null;
            Texture2D firstVelocityOrbitalLUT = null;
            Texture2D firstVelocityOrbitalOffsetLUT = null;
            Texture2D firstLimitVelocityLUT = null;
            Texture2D firstInheritVelocityLUT = null;
            Texture2D firstLifetimeByEmitterSpeedLUT = null;
            Texture2D firstTextureSheetFrameLUT = null;
            Texture2D firstTextureSheetStartLUT = null;
            Texture2D firstRotationLUT = null;
            Texture2D firstRotationBySpeedLUT = null;
            Texture2D firstStartLifetimeLUT = null;
            Texture2D firstStartRotationLUT = null;
            Texture2D firstStartSpeedLUT = null;
            Texture2D firstStartSizeLUT = null;
            Texture2D firstGravityModifierLUT = null;
            Texture2D firstStartColorLUT = null;
            Texture2D firstColorLUT = null;
            Texture2D firstSizeLUT = null;
            Texture2D firstColorBySpeedLUT = null;
            Texture2D firstSizeBySpeedLUT = null;
            string firstForceAssetPath = null;
            string secondForceAssetPath = null;
            string firstVelocityAssetPath = null;
            string secondVelocityAssetPath = null;
            string firstVelocityOrbitalAssetPath = null;
            string secondVelocityOrbitalAssetPath = null;
            string firstVelocityOrbitalOffsetAssetPath = null;
            string secondVelocityOrbitalOffsetAssetPath = null;
            string firstLimitVelocityAssetPath = null;
            string secondLimitVelocityAssetPath = null;
            string firstInheritVelocityAssetPath = null;
            string secondInheritVelocityAssetPath = null;
            string firstLifetimeByEmitterSpeedAssetPath = null;
            string secondLifetimeByEmitterSpeedAssetPath = null;
            string firstTextureSheetFrameAssetPath = null;
            string secondTextureSheetFrameAssetPath = null;
            string firstTextureSheetStartAssetPath = null;
            string secondTextureSheetStartAssetPath = null;
            string firstRotationAssetPath = null;
            string secondRotationAssetPath = null;
            string firstRotationBySpeedAssetPath = null;
            string secondRotationBySpeedAssetPath = null;
            string firstStartLifetimeAssetPath = null;
            string firstStartRotationAssetPath = null;
            string firstStartSpeedAssetPath = null;
            string firstStartSizeAssetPath = null;
            string firstGravityModifierAssetPath = null;
            string firstStartColorAssetPath = null;
            string secondStartColorAssetPath = null;
            string firstColorAssetPath = null;
            string secondColorAssetPath = null;
            string firstSizeAssetPath = null;
            string secondSizeAssetPath = null;
            string firstColorBySpeedAssetPath = null;
            string secondColorBySpeedAssetPath = null;
            string firstSizeBySpeedAssetPath = null;
            string secondSizeBySpeedAssetPath = null;

            try
            {
                var shuriken = owner.AddComponent<ParticleSystem>();
                var main = shuriken.main;
                main.useUnscaledTime = true;
                main.playOnAwake = false;
                main.prewarm = true;
                main.flipRotation = 0.65f;
                main.gravitySource = ParticleSystemGravitySource.Physics2D;
                main.scalingMode = ParticleSystemScalingMode.Local;
                var shurikenRenderer =
                    owner.GetComponent<ParticleSystemRenderer>();
                shurikenRenderer.minParticleSize = 0.075f;
                shurikenRenderer.maxParticleSize = 0.325f;
                AnimationCurve startLifetimeMinimumCurve =
                    AnimationCurve.Linear(0f, 0.75f, 1f, 1.25f);
                AnimationCurve startLifetimeMaximumCurve =
                    AnimationCurve.Linear(0f, 2f, 1f, 3.5f);
                main.startLifetime = new ParticleSystem.MinMaxCurve(
                    1f,
                    startLifetimeMinimumCurve,
                    startLifetimeMaximumCurve);
                AnimationCurve startSpeedMinimumCurve =
                    AnimationCurve.Linear(0f, 1f, 1f, 2f);
                AnimationCurve startSpeedMaximumCurve =
                    AnimationCurve.Linear(0f, 3f, 1f, 6f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(
                    1f,
                    startSpeedMinimumCurve,
                    startSpeedMaximumCurve);
                AnimationCurve startSizeMinimumCurve =
                    AnimationCurve.Linear(0f, 0.5f, 1f, 1f);
                AnimationCurve startSizeMaximumCurve =
                    AnimationCurve.Linear(0f, 1.5f, 1f, 2.5f);
                main.startSize = new ParticleSystem.MinMaxCurve(
                    1f,
                    startSizeMinimumCurve,
                    startSizeMaximumCurve);
                Gradient startColorMinimumGradient = CreateGradient(
                    new Color(0.1f, 0.2f, 0.3f, 0.4f),
                    new Color(0.4f, 0.5f, 0.6f, 0.7f));
                Gradient startColorMaximumGradient = CreateGradient(
                    new Color(0.6f, 0.7f, 0.8f, 0.9f),
                    new Color(0.9f, 0.8f, 0.7f, 1f));
                main.startColor = new ParticleSystem.MinMaxGradient(
                    startColorMinimumGradient,
                    startColorMaximumGradient);
                AnimationCurve gravityMinimumCurve =
                    AnimationCurve.Linear(0f, -0.5f, 1f, 0.25f);
                AnimationCurve gravityMaximumCurve =
                    AnimationCurve.Linear(0f, 0.75f, 1f, 1.5f);
                main.gravityModifier = new ParticleSystem.MinMaxCurve(
                    1f,
                    gravityMinimumCurve,
                    gravityMaximumCurve);
                AnimationCurve startRotationMinimumCurve =
                    AnimationCurve.Linear(0f, -0.75f, 1f, 0.25f);
                AnimationCurve startRotationMaximumCurve =
                    AnimationCurve.Linear(0f, 0.5f, 1f, 1.5f);
                main.startRotation = new ParticleSystem.MinMaxCurve(
                    1f,
                    startRotationMinimumCurve,
                    startRotationMaximumCurve);
                main.duration = 2f;
                main.loop = true;
                main.startDelay = new ParticleSystem.MinMaxCurve(0.1f, 0.2f);
                main.simulationSpace = ParticleSystemSimulationSpace.Custom;
                main.customSimulationSpace = customSpaceObject.transform;
                main.emitterVelocityMode =
                    ParticleSystemEmitterVelocityMode.Transform;

                var rotationOverLifetime = shuriken.rotationOverLifetime;
                rotationOverLifetime.enabled = true;
                rotationOverLifetime.separateAxes = false;
                rotationOverLifetime.z = new ParticleSystem.MinMaxCurve(
                    1f,
                    AnimationCurve.Linear(0f, 0f, 1f, 2f),
                    AnimationCurve.Linear(0f, 1f, 1f, 3f));

                var rotationBySpeed = shuriken.rotationBySpeed;
                rotationBySpeed.enabled = true;
                rotationBySpeed.separateAxes = false;
                rotationBySpeed.range = new Vector2(2f, 8f);
                rotationBySpeed.z = new ParticleSystem.MinMaxCurve(
                    1f,
                    AnimationCurve.Linear(0f, -2f, 1f, 0f),
                    AnimationCurve.Linear(0f, 1f, 1f, 3f));

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
                velocity.speedModifier = new ParticleSystem.MinMaxCurve(
                    1f,
                    AnimationCurve.Linear(0f, -0.5f, 1f, 0.25f),
                    AnimationCurve.Linear(0f, 0.5f, 1f, 1.5f));
                velocity.orbitalX = new ParticleSystem.MinMaxCurve(-0.5f, 0.5f);
                velocity.orbitalY = 2f;
                velocity.orbitalZ = new ParticleSystem.MinMaxCurve(
                    1f,
                    AnimationCurve.Linear(0f, -1f, 1f, -0.25f),
                    AnimationCurve.Linear(0f, 1f, 1f, 2f));
                velocity.radial = new ParticleSystem.MinMaxCurve(
                    1f,
                    AnimationCurve.Linear(0f, 0.1f, 1f, 0.3f),
                    AnimationCurve.Linear(0f, 0.4f, 1f, 0.8f));
                velocity.orbitalOffsetX =
                    new ParticleSystem.MinMaxCurve(-3f, 3f);
                velocity.orbitalOffsetY = 2f;
                velocity.orbitalOffsetZ = new ParticleSystem.MinMaxCurve(
                    1f,
                    AnimationCurve.Linear(0f, 0.5f, 1f, 1.5f));

                var limitVelocity = shuriken.limitVelocityOverLifetime;
                limitVelocity.enabled = true;
                limitVelocity.separateAxes = true;
                limitVelocity.space = ParticleSystemSimulationSpace.World;
                limitVelocity.limitX = new ParticleSystem.MinMaxCurve(
                    1f,
                    AnimationCurve.Linear(0f, 2f, 1f, 4f),
                    AnimationCurve.Linear(0f, 6f, 1f, 8f));
                limitVelocity.limitY = 3f;
                limitVelocity.limitZ = new ParticleSystem.MinMaxCurve(1f, 5f);
                limitVelocity.dampen = 0.4f;
                limitVelocity.drag = new ParticleSystem.MinMaxCurve(
                    1f,
                    AnimationCurve.Linear(0f, 0.1f, 1f, 0.3f),
                    AnimationCurve.Linear(0f, 0.5f, 1f, 0.7f));
                limitVelocity.multiplyDragByParticleSize = true;
                limitVelocity.multiplyDragByParticleVelocity = true;

                var inheritVelocity = shuriken.inheritVelocity;
                inheritVelocity.enabled = true;
                inheritVelocity.mode = ParticleSystemInheritVelocityMode.Current;
                inheritVelocity.curve = new ParticleSystem.MinMaxCurve(
                    1f,
                    AnimationCurve.Linear(0f, -0.5f, 1f, 0.25f),
                    AnimationCurve.Linear(0f, 1.5f, 1f, 0.75f));

                var lifetimeByEmitterSpeed =
                    shuriken.lifetimeByEmitterSpeed;
                lifetimeByEmitterSpeed.enabled = true;
                lifetimeByEmitterSpeed.range = new Vector2(1.5f, 4.5f);
                lifetimeByEmitterSpeed.curve =
                    new ParticleSystem.MinMaxCurve(
                        1f,
                        AnimationCurve.Linear(0f, 0.25f, 1f, 0.75f),
                        AnimationCurve.Linear(0f, 1.25f, 1f, 2.25f));

                var textureSheet = shuriken.textureSheetAnimation;
                textureSheet.enabled = true;
                textureSheet.mode = ParticleSystemAnimationMode.Grid;
                textureSheet.numTilesX = 4;
                textureSheet.numTilesY = 3;
                textureSheet.animation = ParticleSystemAnimationType.SingleRow;
                textureSheet.timeMode = ParticleSystemAnimationTimeMode.Speed;
                textureSheet.rowMode = ParticleSystemAnimationRowMode.Custom;
                textureSheet.rowIndex = 2;
                textureSheet.cycleCount = 3;
                textureSheet.speedRange = new Vector2(2f, 10f);
                textureSheet.fps = 12f;
                textureSheet.uvChannelMask =
                    UVChannelFlags.UV0 | UVChannelFlags.UV2;
                textureSheet.frameOverTime = new ParticleSystem.MinMaxCurve(
                    1f,
                    AnimationCurve.Linear(0f, 0f, 1f, 0.5f),
                    AnimationCurve.Linear(0f, 0.25f, 1f, 1f));
                textureSheet.startFrame =
                    new ParticleSystem.MinMaxCurve(0.125f, 0.375f);

                Gradient minimumGradient = CreateGradient(
                    new Color(0.2f, 0.1f, 0.3f, 0.8f),
                    new Color(0.4f, 0.6f, 0.2f, 0.2f));
                Gradient maximumGradient = CreateGradient(
                    new Color(0.9f, 0.4f, 0.8f, 1f),
                    new Color(0.8f, 1f, 0.5f, 0.6f));
                var colorOverLifetime = shuriken.colorOverLifetime;
                colorOverLifetime.enabled = true;
                colorOverLifetime.color = new ParticleSystem.MinMaxGradient(
                    minimumGradient,
                    maximumGradient);

                var sizeOverLifetime = shuriken.sizeOverLifetime;
                sizeOverLifetime.enabled = true;
                sizeOverLifetime.separateAxes = false;
                sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(
                    1f,
                    AnimationCurve.Linear(0f, 0.5f, 1f, 1f),
                    AnimationCurve.Linear(0f, 1.5f, 1f, 2f));

                Gradient minimumSpeedGradient = CreateGradient(
                    new Color(0.1f, 0.2f, 0.3f, 0.7f),
                    new Color(0.3f, 0.4f, 0.5f, 0.5f));
                Gradient maximumSpeedGradient = CreateGradient(
                    new Color(0.7f, 0.8f, 0.9f, 1f),
                    new Color(0.9f, 1f, 0.8f, 0.8f));
                var colorBySpeed = shuriken.colorBySpeed;
                colorBySpeed.enabled = true;
                colorBySpeed.range = new Vector2(1f, 5f);
                colorBySpeed.color = new ParticleSystem.MinMaxGradient(
                    minimumSpeedGradient,
                    maximumSpeedGradient);

                var sizeBySpeed = shuriken.sizeBySpeed;
                sizeBySpeed.enabled = true;
                sizeBySpeed.separateAxes = false;
                sizeBySpeed.range = new Vector2(2f, 6f);
                sizeBySpeed.size = new ParticleSystem.MinMaxCurve(
                    1f,
                    AnimationCurve.Linear(0f, 0.25f, 1f, 0.75f),
                    AnimationCurve.Linear(0f, 1.25f, 1f, 2.25f));

                ShurikenConverter.Convert(owner);
                var gpu = owner.GetComponent<GPUParticleSystem>();
                Require(gpu != null, "Converter did not create GPUParticleSystem.");
                Require(gpu.useUnscaledTime,
                    "Main Use Unscaled Time was not mapped.");
                Require(!gpu.playOnAwake,
                    "Main Play On Awake was not mapped.");
                Require(gpu.prewarm,
                    "Main Prewarm was not mapped.");
                RequireApproximately(
                    gpu.flipRotation,
                    0.65f,
                    "Main Flip Rotation");
                Require(
                    gpu.gravitySource == ParticleSystemGravitySource.Physics2D,
                    "Main Gravity Source was not mapped.");
                Require(
                    gpu.simulationSpace == SimulationSpace.Custom,
                    "Main Custom Simulation Space mode was not mapped.");
                Require(
                    gpu.customSimulationSpace == customSpaceObject.transform,
                    "Main Custom Simulation Space Transform was not mapped.");
                Require(
                    gpu.scalingMode == ParticleSystemScalingMode.Local,
                    "Main Scaling Mode Local was not mapped.");
                Require(gpu.screenSpaceSizeClampEnabled,
                    "Renderer screen-space size clamp was not enabled.");
                RequireApproximately(
                    gpu.minParticleSize,
                    0.075f,
                    "Renderer Min Particle Size");
                RequireApproximately(
                    gpu.maxParticleSize,
                    0.325f,
                    "Renderer Max Particle Size");
                Require(gpu.startLifetimeMode ==
                        ParticleSystemCurveMode.TwoCurves,
                    "Start Lifetime Two Curves mode was not mapped.");
                Require(gpu.startLifetimeLUT != null &&
                        gpu.startLifetimeLUT.height == 2,
                    "Start Lifetime minimum/maximum Curve LUT rows were not generated.");
                firstStartLifetimeLUT = gpu.startLifetimeLUT;
                firstStartLifetimeAssetPath =
                    AssetDatabase.GetAssetPath(firstStartLifetimeLUT);
                RequireApproximately(
                    firstStartLifetimeLUT.GetPixel(0, 0).r,
                    0.75f,
                    "Start Lifetime minimum Curve start");
                RequireApproximately(
                    firstStartLifetimeLUT.GetPixel(
                        firstStartLifetimeLUT.width - 1, 0).r,
                    1.25f,
                    "Start Lifetime minimum Curve end");
                RequireApproximately(
                    firstStartLifetimeLUT.GetPixel(0, 1).r,
                    2f,
                    "Start Lifetime maximum Curve start");
                RequireApproximately(
                    firstStartLifetimeLUT.GetPixel(
                        firstStartLifetimeLUT.width - 1, 1).r,
                    3.5f,
                    "Start Lifetime maximum Curve end");
                Require(gpu.startSpeedMode == ParticleSystemCurveMode.TwoCurves,
                    "Start Speed Two Curves mode was not mapped.");
                Require(gpu.startSpeedLUT != null &&
                        gpu.startSpeedLUT.height == 2,
                    "Start Speed minimum/maximum Curve LUT rows were not generated.");
                firstStartSpeedLUT = gpu.startSpeedLUT;
                firstStartSpeedAssetPath =
                    AssetDatabase.GetAssetPath(firstStartSpeedLUT);
                RequireApproximately(
                    firstStartSpeedLUT.GetPixel(0, 0).r,
                    1f,
                    "Start Speed minimum Curve start");
                RequireApproximately(
                    firstStartSpeedLUT.GetPixel(
                        firstStartSpeedLUT.width - 1, 0).r,
                    2f,
                    "Start Speed minimum Curve end");
                RequireApproximately(
                    firstStartSpeedLUT.GetPixel(0, 1).r,
                    3f,
                    "Start Speed maximum Curve start");
                RequireApproximately(
                    firstStartSpeedLUT.GetPixel(
                        firstStartSpeedLUT.width - 1, 1).r,
                    6f,
                    "Start Speed maximum Curve end");
                Require(gpu.startSizeMode == ParticleSystemCurveMode.TwoCurves,
                    "Start Size Two Curves mode was not mapped.");
                Require(gpu.startSizeLUT != null &&
                        gpu.startSizeLUT.height == 2,
                    "Start Size minimum/maximum Curve LUT rows were not generated.");
                firstStartSizeLUT = gpu.startSizeLUT;
                firstStartSizeAssetPath =
                    AssetDatabase.GetAssetPath(firstStartSizeLUT);
                RequireApproximately(
                    firstStartSizeLUT.GetPixel(0, 0).r,
                    0.5f,
                    "Start Size minimum Curve start");
                RequireApproximately(
                    firstStartSizeLUT.GetPixel(
                        firstStartSizeLUT.width - 1, 0).r,
                    1f,
                    "Start Size minimum Curve end");
                RequireApproximately(
                    firstStartSizeLUT.GetPixel(0, 1).r,
                    1.5f,
                    "Start Size maximum Curve start");
                RequireApproximately(
                    firstStartSizeLUT.GetPixel(
                        firstStartSizeLUT.width - 1, 1).r,
                    2.5f,
                    "Start Size maximum Curve end");
                Require(gpu.startColorMode ==
                        ParticleSystemGradientMode.TwoGradients,
                    "Start Color Two Gradients mode was not mapped.");
                Require(gpu.startColorLUT != null &&
                        gpu.startColorLUT.height == 2,
                    "Start Color minimum/maximum Gradient LUT rows were not generated.");
                firstStartColorLUT = gpu.startColorLUT;
                firstStartColorAssetPath =
                    AssetDatabase.GetAssetPath(firstStartColorLUT);
                RequireColorApproximately(
                    firstStartColorLUT.GetPixel(0, 0),
                    new Color(0.1f, 0.2f, 0.3f, 0.4f),
                    "Start Color minimum Gradient start");
                RequireColorApproximately(
                    firstStartColorLUT.GetPixel(
                        firstStartColorLUT.width - 1, 0),
                    new Color(0.4f, 0.5f, 0.6f, 0.7f),
                    "Start Color minimum Gradient end");
                RequireColorApproximately(
                    firstStartColorLUT.GetPixel(0, 1),
                    new Color(0.6f, 0.7f, 0.8f, 0.9f),
                    "Start Color maximum Gradient start");
                RequireColorApproximately(
                    firstStartColorLUT.GetPixel(
                        firstStartColorLUT.width - 1, 1),
                    new Color(0.9f, 0.8f, 0.7f, 1f),
                    "Start Color maximum Gradient end");
                Require(gpu.gravityModifierMode ==
                        ParticleSystemCurveMode.TwoCurves,
                    "Gravity Modifier Two Curves mode was not mapped.");
                Require(gpu.gravityModifierLUT != null &&
                        gpu.gravityModifierLUT.height == 2,
                    "Gravity Modifier minimum/maximum Curve LUT rows were not generated.");
                firstGravityModifierLUT = gpu.gravityModifierLUT;
                firstGravityModifierAssetPath =
                    AssetDatabase.GetAssetPath(firstGravityModifierLUT);
                RequireApproximately(
                    firstGravityModifierLUT.GetPixel(0, 0).r,
                    -0.5f,
                    "Gravity Modifier minimum Curve start");
                RequireApproximately(
                    firstGravityModifierLUT.GetPixel(
                        firstGravityModifierLUT.width - 1, 0).r,
                    0.25f,
                    "Gravity Modifier minimum Curve end");
                RequireApproximately(
                    firstGravityModifierLUT.GetPixel(0, 1).r,
                    0.75f,
                    "Gravity Modifier maximum Curve start");
                RequireApproximately(
                    firstGravityModifierLUT.GetPixel(
                        firstGravityModifierLUT.width - 1, 1).r,
                    1.5f,
                    "Gravity Modifier maximum Curve end");
                Require(gpu.startRotationMode ==
                        ParticleSystemCurveMode.TwoCurves,
                    "Start Rotation Two Curves mode was not mapped.");
                Require(gpu.startRotationLUT != null &&
                        gpu.startRotationLUT.height == 2,
                    "Start Rotation minimum/maximum Curve LUT rows were not generated.");
                firstStartRotationLUT = gpu.startRotationLUT;
                firstStartRotationAssetPath =
                    AssetDatabase.GetAssetPath(firstStartRotationLUT);
                RequireApproximately(
                    firstStartRotationLUT.GetPixel(0, 0).r,
                    -0.75f,
                    "Start Rotation minimum Curve start");
                RequireApproximately(
                    firstStartRotationLUT.GetPixel(
                        firstStartRotationLUT.width - 1, 0).r,
                    0.25f,
                    "Start Rotation minimum Curve end");
                RequireApproximately(
                    firstStartRotationLUT.GetPixel(0, 1).r,
                    0.5f,
                    "Start Rotation maximum Curve start");
                RequireApproximately(
                    firstStartRotationLUT.GetPixel(
                        firstStartRotationLUT.width - 1, 1).r,
                    1.5f,
                    "Start Rotation maximum Curve end");
                Require(gpu.randomizeRotationOverLifetime,
                    "Rotation over Lifetime Two Curves endpoints were not mapped.");
                Require(gpu.rotationOverLifetimeIntegralLUT != null &&
                        gpu.rotationOverLifetimeIntegralLUT.height == 2,
                    "Rotation over Lifetime cumulative minimum/maximum LUT rows were not generated.");
                Require(gpu.rotationBySpeedEnabled,
                    "Rotation by Speed enabled state was not mapped.");
                RequireApproximately(gpu.rotationBySpeedRange.x, 2f,
                    "Rotation by Speed range minimum");
                RequireApproximately(gpu.rotationBySpeedRange.y, 8f,
                    "Rotation by Speed range maximum");
                Require(gpu.rotationBySpeedLUT != null &&
                        gpu.rotationBySpeedLUT.height == 2,
                    "Rotation by Speed signed minimum/maximum LUT rows were not generated.");
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
                Require(gpu.velocityOverLifetimeSpeedModifierEnabled,
                    "Velocity over Lifetime Speed Modifier state was not mapped.");
                Require(gpu.velocityOverLifetimeOrbitalEnabled,
                    "Velocity over Lifetime Orbital/Radius state was not mapped.");
                Require(gpu.velocityOverLifetimeOrbitalLUT != null &&
                        gpu.velocityOverLifetimeOrbitalLUT.height == 2,
                    "Velocity over Lifetime Orbital XYZ and Radial LUT was not generated.");
                Require(gpu.velocityOverLifetimeOrbitalOffsetLUT != null &&
                        gpu.velocityOverLifetimeOrbitalOffsetLUT.height == 2,
                    "Velocity over Lifetime Orbital Offset LUT was not generated.");
                Require(gpu.limitVelocityOverLifetimeEnabled,
                    "Limit Velocity over Lifetime enabled state was not mapped.");
                Require(gpu.limitVelocityOverLifetimeSeparateAxes,
                    "Limit Velocity over Lifetime Separate Axes was not mapped.");
                Require(gpu.limitVelocityOverLifetimeSpace == SimulationSpace.World,
                    "Limit Velocity over Lifetime space was not mapped.");
                RequireApproximately(
                    gpu.limitVelocityOverLifetimeDampen,
                    0.4f,
                    "Limit Velocity over Lifetime Dampen");
                Require(gpu.limitVelocityMultiplyDragBySize,
                    "Limit Velocity Multiply by Size was not mapped.");
                Require(gpu.limitVelocityMultiplyDragByVelocity,
                    "Limit Velocity Multiply by Velocity was not mapped.");
                Require(gpu.limitVelocityOverLifetimeLUT != null &&
                        gpu.limitVelocityOverLifetimeLUT.height == 2,
                    "Limit Velocity and Drag minimum/maximum LUT rows were not generated.");
                Require(gpu.inheritVelocityEnabled,
                    "Inherit Velocity enabled state was not mapped.");
                Require(gpu.inheritVelocityMode ==
                        ParticleSystemInheritVelocityMode.Current,
                    "Inherit Velocity mode was not mapped.");
                Require(gpu.inheritVelocityLUT != null &&
                        gpu.inheritVelocityLUT.height == 2,
                    "Inherit Velocity signed minimum/maximum LUT rows were not generated.");
                Require(gpu.lifetimeByEmitterSpeedEnabled,
                    "Lifetime by Emitter Speed enabled state was not mapped.");
                RequireApproximately(
                    gpu.lifetimeByEmitterSpeedRange.x,
                    1.5f,
                    "Lifetime by Emitter Speed range minimum");
                RequireApproximately(
                    gpu.lifetimeByEmitterSpeedRange.y,
                    4.5f,
                    "Lifetime by Emitter Speed range maximum");
                Require(gpu.lifetimeByEmitterSpeedLUT != null &&
                        gpu.lifetimeByEmitterSpeedLUT.height == 2,
                    "Lifetime by Emitter Speed minimum/maximum LUT rows were not generated.");
                Require(gpu.textureSheetAnimationEnabled,
                    "Texture Sheet Animation UV0 Grid state was not mapped.");
                Require(gpu.textureSheetMode == ParticleSystemAnimationMode.Grid,
                    "Texture Sheet Animation Grid mode was not mapped.");
                Require(gpu.textureSheetAnimation ==
                        ParticleSystemAnimationType.SingleRow,
                    "Texture Sheet Animation Single Row mode was not mapped.");
                Require(gpu.textureSheetTimeMode ==
                        ParticleSystemAnimationTimeMode.Speed,
                    "Texture Sheet Animation Speed time mode was not mapped.");
                Require(gpu.textureSheetRowMode ==
                        ParticleSystemAnimationRowMode.Custom &&
                        gpu.textureSheetRowIndex == 2,
                    "Texture Sheet Animation Custom row was not mapped.");
                Require(gpu.textureSheetTilesX == 4 &&
                        gpu.textureSheetTilesY == 3,
                    "Texture Sheet Animation tile grid was not mapped.");
                Require(gpu.textureSheetCycleCount == 3,
                    "Texture Sheet Animation Cycle Count was not mapped.");
                RequireApproximately(gpu.textureSheetFps, 12f,
                    "Texture Sheet Animation FPS");
                RequireApproximately(gpu.textureSheetSpeedRange.x, 2f,
                    "Texture Sheet Animation speed minimum");
                RequireApproximately(gpu.textureSheetSpeedRange.y, 10f,
                    "Texture Sheet Animation speed maximum");
                Require(gpu.textureSheetUVChannelMask ==
                        (UVChannelFlags.UV0 | UVChannelFlags.UV2),
                    "Texture Sheet Animation UV channel mask was not preserved.");
                Require(gpu.textureSheetFrameOverTimeLUT != null &&
                        gpu.textureSheetFrameOverTimeLUT.height == 2,
                    "Texture Sheet Animation Frame over Time LUT was not generated.");
                Require(gpu.textureSheetStartFrameLUT != null &&
                        gpu.textureSheetStartFrameLUT.height == 2,
                    "Texture Sheet Animation Start Frame LUT was not generated.");
                Require(gpu.colorOverLifetimeMode == ParticleSystemGradientMode.TwoGradients,
                    "Color over Lifetime Two Gradients mode was not mapped.");
                Require(gpu.colorOverLifetimeLUT != null &&
                        gpu.colorOverLifetimeLUT.height == 2,
                    "Color over Lifetime minimum/maximum LUT rows were not generated.");
                Require(gpu.sizeOverLifetimeLUT != null &&
                        gpu.sizeOverLifetimeLUT.height == 2,
                    "Size over Lifetime minimum/maximum LUT rows were not generated.");

                firstRotationLUT = gpu.rotationOverLifetimeIntegralLUT;
                firstRotationAssetPath = AssetDatabase.GetAssetPath(firstRotationLUT);
                int rotationMidpoint = (firstRotationLUT.width - 1) / 2;
                RequireApproximately(
                    firstRotationLUT.GetPixel(rotationMidpoint, 0).r,
                    0.25f,
                    "Rotation over Lifetime minimum integral midpoint");
                RequireApproximately(
                    firstRotationLUT.GetPixel(rotationMidpoint, 1).r,
                    0.75f,
                    "Rotation over Lifetime maximum integral midpoint");
                RequireApproximately(
                    firstRotationLUT.GetPixel(firstRotationLUT.width - 1, 0).r,
                    1f,
                    "Rotation over Lifetime minimum integral end");
                RequireApproximately(
                    firstRotationLUT.GetPixel(firstRotationLUT.width - 1, 1).r,
                    2f,
                    "Rotation over Lifetime maximum integral end");

                firstRotationBySpeedLUT = gpu.rotationBySpeedLUT;
                firstRotationBySpeedAssetPath =
                    AssetDatabase.GetAssetPath(firstRotationBySpeedLUT);
                RequireApproximately(
                    firstRotationBySpeedLUT.GetPixel(0, 0).r,
                    -2f,
                    "Rotation by Speed minimum start");
                RequireApproximately(
                    firstRotationBySpeedLUT.GetPixel(
                        firstRotationBySpeedLUT.width - 1, 0).r,
                    0f,
                    "Rotation by Speed minimum end");
                RequireApproximately(
                    firstRotationBySpeedLUT.GetPixel(0, 1).r,
                    1f,
                    "Rotation by Speed maximum start");
                RequireApproximately(
                    firstRotationBySpeedLUT.GetPixel(
                        firstRotationBySpeedLUT.width - 1, 1).r,
                    3f,
                    "Rotation by Speed maximum end");

                firstColorLUT = gpu.colorOverLifetimeLUT;
                firstColorAssetPath = AssetDatabase.GetAssetPath(firstColorLUT);
                Color minimumColor = firstColorLUT.GetPixel(0, 0);
                Color maximumColor = firstColorLUT.GetPixel(0, 1);
                RequireColorApproximately(
                    minimumColor,
                    new Color(0.2f, 0.1f, 0.3f, 0.8f),
                    "Color over Lifetime minimum start");
                RequireColorApproximately(
                    maximumColor,
                    new Color(0.9f, 0.4f, 0.8f, 1f),
                    "Color over Lifetime maximum start");

                firstSizeLUT = gpu.sizeOverLifetimeLUT;
                firstSizeAssetPath = AssetDatabase.GetAssetPath(firstSizeLUT);
                RequireApproximately(
                    firstSizeLUT.GetPixel(0, 0).r,
                    0.5f,
                    "Size over Lifetime minimum start");
                RequireApproximately(
                    firstSizeLUT.GetPixel(firstSizeLUT.width - 1, 0).r,
                    1f,
                    "Size over Lifetime minimum end");
                RequireApproximately(
                    firstSizeLUT.GetPixel(0, 1).r,
                    1.5f,
                    "Size over Lifetime maximum start");
                RequireApproximately(
                    firstSizeLUT.GetPixel(firstSizeLUT.width - 1, 1).r,
                    2f,
                    "Size over Lifetime maximum end above one");

                Require(gpu.colorBySpeedEnabled,
                    "Color by Speed enabled state was not mapped.");
                Require(gpu.colorBySpeedMode == ParticleSystemGradientMode.TwoGradients,
                    "Color by Speed Two Gradients mode was not mapped.");
                RequireApproximately(gpu.colorBySpeedRange.x, 1f,
                    "Color by Speed range minimum");
                RequireApproximately(gpu.colorBySpeedRange.y, 5f,
                    "Color by Speed range maximum");
                Require(gpu.colorBySpeedLUT != null && gpu.colorBySpeedLUT.height == 2,
                    "Color by Speed minimum/maximum LUT rows were not generated.");
                firstColorBySpeedLUT = gpu.colorBySpeedLUT;
                firstColorBySpeedAssetPath =
                    AssetDatabase.GetAssetPath(firstColorBySpeedLUT);
                RequireColorApproximately(
                    firstColorBySpeedLUT.GetPixel(0, 0),
                    new Color(0.1f, 0.2f, 0.3f, 0.7f),
                    "Color by Speed minimum start");
                RequireColorApproximately(
                    firstColorBySpeedLUT.GetPixel(0, 1),
                    new Color(0.7f, 0.8f, 0.9f, 1f),
                    "Color by Speed maximum start");

                Require(gpu.sizeBySpeedEnabled,
                    "Size by Speed enabled state was not mapped.");
                RequireApproximately(gpu.sizeBySpeedRange.x, 2f,
                    "Size by Speed range minimum");
                RequireApproximately(gpu.sizeBySpeedRange.y, 6f,
                    "Size by Speed range maximum");
                Require(gpu.sizeBySpeedLUT != null && gpu.sizeBySpeedLUT.height == 2,
                    "Size by Speed minimum/maximum LUT rows were not generated.");
                firstSizeBySpeedLUT = gpu.sizeBySpeedLUT;
                firstSizeBySpeedAssetPath =
                    AssetDatabase.GetAssetPath(firstSizeBySpeedLUT);
                RequireApproximately(
                    firstSizeBySpeedLUT.GetPixel(0, 0).r,
                    0.25f,
                    "Size by Speed minimum start");
                RequireApproximately(
                    firstSizeBySpeedLUT.GetPixel(firstSizeBySpeedLUT.width - 1, 0).r,
                    0.75f,
                    "Size by Speed minimum end");
                RequireApproximately(
                    firstSizeBySpeedLUT.GetPixel(0, 1).r,
                    1.25f,
                    "Size by Speed maximum start");
                RequireApproximately(
                    firstSizeBySpeedLUT.GetPixel(firstSizeBySpeedLUT.width - 1, 1).r,
                    2.25f,
                    "Size by Speed maximum end above one");

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
                RequireApproximately(minimum.a, -0.5f,
                    "Velocity Speed Modifier minimum start");
                RequireApproximately(maximum.a, 0.5f,
                    "Velocity Speed Modifier maximum start");
                minimum = firstVelocityLUT.GetPixel(
                    firstVelocityLUT.width - 1, 0);
                maximum = firstVelocityLUT.GetPixel(
                    firstVelocityLUT.width - 1, 1);
                RequireApproximately(minimum.a, 0.25f,
                    "Velocity Speed Modifier minimum end");
                RequireApproximately(maximum.a, 1.5f,
                    "Velocity Speed Modifier maximum end");

                firstVelocityOrbitalLUT =
                    gpu.velocityOverLifetimeOrbitalLUT;
                firstVelocityOrbitalAssetPath =
                    AssetDatabase.GetAssetPath(firstVelocityOrbitalLUT);
                minimum = firstVelocityOrbitalLUT.GetPixel(0, 0);
                maximum = firstVelocityOrbitalLUT.GetPixel(0, 1);
                RequireColorApproximately(
                    minimum,
                    new Color(-0.5f, 2f, -1f, 0.1f),
                    "Velocity Orbital/Radial minimum start");
                RequireColorApproximately(
                    maximum,
                    new Color(0.5f, 2f, 1f, 0.4f),
                    "Velocity Orbital/Radial maximum start");
                minimum = firstVelocityOrbitalLUT.GetPixel(
                    firstVelocityOrbitalLUT.width - 1, 0);
                maximum = firstVelocityOrbitalLUT.GetPixel(
                    firstVelocityOrbitalLUT.width - 1, 1);
                RequireColorApproximately(
                    minimum,
                    new Color(-0.5f, 2f, -0.25f, 0.3f),
                    "Velocity Orbital/Radial minimum end");
                RequireColorApproximately(
                    maximum,
                    new Color(0.5f, 2f, 2f, 0.8f),
                    "Velocity Orbital/Radial maximum end");

                firstVelocityOrbitalOffsetLUT =
                    gpu.velocityOverLifetimeOrbitalOffsetLUT;
                firstVelocityOrbitalOffsetAssetPath =
                    AssetDatabase.GetAssetPath(firstVelocityOrbitalOffsetLUT);
                minimum = firstVelocityOrbitalOffsetLUT.GetPixel(0, 0);
                maximum = firstVelocityOrbitalOffsetLUT.GetPixel(0, 1);
                RequireColorApproximately(
                    minimum,
                    new Color(-3f, 2f, 0.5f, 0f),
                    "Velocity Orbital Offset minimum start");
                RequireColorApproximately(
                    maximum,
                    new Color(3f, 2f, 0.5f, 0f),
                    "Velocity Orbital Offset maximum start");
                minimum = firstVelocityOrbitalOffsetLUT.GetPixel(
                    firstVelocityOrbitalOffsetLUT.width - 1, 0);
                maximum = firstVelocityOrbitalOffsetLUT.GetPixel(
                    firstVelocityOrbitalOffsetLUT.width - 1, 1);
                RequireColorApproximately(
                    minimum,
                    new Color(-3f, 2f, 1.5f, 0f),
                    "Velocity Orbital Offset minimum end");
                RequireColorApproximately(
                    maximum,
                    new Color(3f, 2f, 1.5f, 0f),
                    "Velocity Orbital Offset maximum end");

                firstLimitVelocityLUT = gpu.limitVelocityOverLifetimeLUT;
                firstLimitVelocityAssetPath =
                    AssetDatabase.GetAssetPath(firstLimitVelocityLUT);
                minimum = firstLimitVelocityLUT.GetPixel(0, 0);
                maximum = firstLimitVelocityLUT.GetPixel(0, 1);
                RequireColorApproximately(
                    minimum,
                    new Color(2f, 3f, 1f, 0.1f),
                    "Limit Velocity minimum start");
                RequireColorApproximately(
                    maximum,
                    new Color(6f, 3f, 5f, 0.5f),
                    "Limit Velocity maximum start");
                minimum = firstLimitVelocityLUT.GetPixel(
                    firstLimitVelocityLUT.width - 1, 0);
                maximum = firstLimitVelocityLUT.GetPixel(
                    firstLimitVelocityLUT.width - 1, 1);
                RequireColorApproximately(
                    minimum,
                    new Color(4f, 3f, 1f, 0.3f),
                    "Limit Velocity minimum end");
                RequireColorApproximately(
                    maximum,
                    new Color(8f, 3f, 5f, 0.7f),
                    "Limit Velocity maximum end");

                firstInheritVelocityLUT = gpu.inheritVelocityLUT;
                firstInheritVelocityAssetPath =
                    AssetDatabase.GetAssetPath(firstInheritVelocityLUT);
                RequireApproximately(
                    firstInheritVelocityLUT.GetPixel(0, 0).r,
                    -0.5f,
                    "Inherit Velocity minimum start");
                RequireApproximately(
                    firstInheritVelocityLUT.GetPixel(
                        firstInheritVelocityLUT.width - 1, 0).r,
                    0.25f,
                    "Inherit Velocity minimum end");
                RequireApproximately(
                    firstInheritVelocityLUT.GetPixel(0, 1).r,
                    1.5f,
                    "Inherit Velocity maximum start");
                RequireApproximately(
                    firstInheritVelocityLUT.GetPixel(
                        firstInheritVelocityLUT.width - 1, 1).r,
                    0.75f,
                    "Inherit Velocity maximum end");

                firstLifetimeByEmitterSpeedLUT =
                    gpu.lifetimeByEmitterSpeedLUT;
                firstLifetimeByEmitterSpeedAssetPath =
                    AssetDatabase.GetAssetPath(
                        firstLifetimeByEmitterSpeedLUT);
                RequireApproximately(
                    firstLifetimeByEmitterSpeedLUT.GetPixel(0, 0).r,
                    0.25f,
                    "Lifetime by Emitter Speed minimum start");
                RequireApproximately(
                    firstLifetimeByEmitterSpeedLUT.GetPixel(
                        firstLifetimeByEmitterSpeedLUT.width - 1,
                        0).r,
                    0.75f,
                    "Lifetime by Emitter Speed minimum end");
                RequireApproximately(
                    firstLifetimeByEmitterSpeedLUT.GetPixel(0, 1).r,
                    1.25f,
                    "Lifetime by Emitter Speed maximum start");
                RequireApproximately(
                    firstLifetimeByEmitterSpeedLUT.GetPixel(
                        firstLifetimeByEmitterSpeedLUT.width - 1,
                        1).r,
                    2.25f,
                    "Lifetime by Emitter Speed maximum end");

                firstTextureSheetFrameLUT =
                    gpu.textureSheetFrameOverTimeLUT;
                firstTextureSheetFrameAssetPath =
                    AssetDatabase.GetAssetPath(firstTextureSheetFrameLUT);
                RequireApproximately(
                    firstTextureSheetFrameLUT.GetPixel(0, 0).r,
                    0f,
                    "Texture Sheet Frame over Time minimum start");
                RequireApproximately(
                    firstTextureSheetFrameLUT.GetPixel(
                        firstTextureSheetFrameLUT.width - 1, 0).r,
                    0.5f,
                    "Texture Sheet Frame over Time minimum end");
                RequireApproximately(
                    firstTextureSheetFrameLUT.GetPixel(0, 1).r,
                    0.25f,
                    "Texture Sheet Frame over Time maximum start");
                RequireApproximately(
                    firstTextureSheetFrameLUT.GetPixel(
                        firstTextureSheetFrameLUT.width - 1, 1).r,
                    1f,
                    "Texture Sheet Frame over Time maximum end");

                firstTextureSheetStartLUT = gpu.textureSheetStartFrameLUT;
                firstTextureSheetStartAssetPath =
                    AssetDatabase.GetAssetPath(firstTextureSheetStartLUT);
                RequireApproximately(
                    firstTextureSheetStartLUT.GetPixel(0, 0).r,
                    0.125f,
                    "Texture Sheet Start Frame minimum at t=0");
                RequireApproximately(
                    firstTextureSheetStartLUT.GetPixel(0, 1).r,
                    0.375f,
                    "Texture Sheet Start Frame maximum at t=0");

                if (string.IsNullOrEmpty(firstStartLifetimeAssetPath))
                {
                    Object.DestroyImmediate(firstStartLifetimeLUT);
                }
                firstStartLifetimeLUT = null;
                gpu.startLifetimeLUT = null;

                if (string.IsNullOrEmpty(firstStartRotationAssetPath))
                {
                    Object.DestroyImmediate(firstStartRotationLUT);
                }
                firstStartRotationLUT = null;
                gpu.startRotationLUT = null;

                if (string.IsNullOrEmpty(firstStartSpeedAssetPath))
                {
                    Object.DestroyImmediate(firstStartSpeedLUT);
                }
                firstStartSpeedLUT = null;
                gpu.startSpeedLUT = null;

                if (string.IsNullOrEmpty(firstStartSizeAssetPath))
                {
                    Object.DestroyImmediate(firstStartSizeLUT);
                }
                firstStartSizeLUT = null;
                gpu.startSizeLUT = null;

                if (string.IsNullOrEmpty(firstGravityModifierAssetPath))
                {
                    Object.DestroyImmediate(firstGravityModifierLUT);
                }
                firstGravityModifierLUT = null;
                gpu.gravityModifierLUT = null;

                if (string.IsNullOrEmpty(firstStartColorAssetPath))
                {
                    Object.DestroyImmediate(firstStartColorLUT);
                }
                firstStartColorLUT = null;
                gpu.startColorLUT = null;

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

                if (string.IsNullOrEmpty(firstVelocityOrbitalAssetPath))
                {
                    Object.DestroyImmediate(firstVelocityOrbitalLUT);
                }
                firstVelocityOrbitalLUT = null;
                gpu.velocityOverLifetimeOrbitalLUT = null;

                if (string.IsNullOrEmpty(firstVelocityOrbitalOffsetAssetPath))
                {
                    Object.DestroyImmediate(firstVelocityOrbitalOffsetLUT);
                }
                firstVelocityOrbitalOffsetLUT = null;
                gpu.velocityOverLifetimeOrbitalOffsetLUT = null;

                if (string.IsNullOrEmpty(firstLimitVelocityAssetPath))
                {
                    Object.DestroyImmediate(firstLimitVelocityLUT);
                }
                firstLimitVelocityLUT = null;
                gpu.limitVelocityOverLifetimeLUT = null;

                if (string.IsNullOrEmpty(firstInheritVelocityAssetPath))
                {
                    Object.DestroyImmediate(firstInheritVelocityLUT);
                }
                firstInheritVelocityLUT = null;
                gpu.inheritVelocityLUT = null;

                if (string.IsNullOrEmpty(
                        firstLifetimeByEmitterSpeedAssetPath))
                {
                    Object.DestroyImmediate(
                        firstLifetimeByEmitterSpeedLUT);
                }
                firstLifetimeByEmitterSpeedLUT = null;
                gpu.lifetimeByEmitterSpeedLUT = null;

                if (string.IsNullOrEmpty(firstTextureSheetFrameAssetPath))
                {
                    Object.DestroyImmediate(firstTextureSheetFrameLUT);
                }
                firstTextureSheetFrameLUT = null;
                gpu.textureSheetFrameOverTimeLUT = null;

                if (string.IsNullOrEmpty(firstTextureSheetStartAssetPath))
                {
                    Object.DestroyImmediate(firstTextureSheetStartLUT);
                }
                firstTextureSheetStartLUT = null;
                gpu.textureSheetStartFrameLUT = null;

                if (string.IsNullOrEmpty(firstRotationAssetPath))
                {
                    Object.DestroyImmediate(firstRotationLUT);
                }
                firstRotationLUT = null;
                gpu.rotationOverLifetimeIntegralLUT = null;

                if (string.IsNullOrEmpty(firstRotationBySpeedAssetPath))
                {
                    Object.DestroyImmediate(firstRotationBySpeedLUT);
                }
                firstRotationBySpeedLUT = null;
                gpu.rotationBySpeedLUT = null;

                if (string.IsNullOrEmpty(firstColorAssetPath))
                {
                    Object.DestroyImmediate(firstColorLUT);
                }
                firstColorLUT = null;
                gpu.colorOverLifetimeLUT = null;

                if (string.IsNullOrEmpty(firstSizeAssetPath))
                {
                    Object.DestroyImmediate(firstSizeLUT);
                }
                firstSizeLUT = null;
                gpu.sizeOverLifetimeLUT = null;

                if (string.IsNullOrEmpty(firstColorBySpeedAssetPath))
                {
                    Object.DestroyImmediate(firstColorBySpeedLUT);
                }
                firstColorBySpeedLUT = null;
                gpu.colorBySpeedLUT = null;

                if (string.IsNullOrEmpty(firstSizeBySpeedAssetPath))
                {
                    Object.DestroyImmediate(firstSizeBySpeedLUT);
                }
                firstSizeBySpeedLUT = null;
                gpu.sizeBySpeedLUT = null;

                main.startLifetime = new ParticleSystem.MinMaxCurve(2f, 4f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(1f, 3f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.5f, 1.5f);
                main.gravityModifier =
                    new ParticleSystem.MinMaxCurve(0.25f, 0.75f);
                main.startRotation = new ParticleSystem.MinMaxCurve(-0.5f, 0.5f);
                emission.rateOverTime = new ParticleSystem.MinMaxCurve(4f, 8f);
                textureSheet.rowMode = ParticleSystemAnimationRowMode.Random;
                shape.enabled = true;
                shape.shapeType = ParticleSystemShapeType.Circle;
                shape.radius = 3f;
                shape.scale = new Vector3(2f, 3f, 1f);
                shape.arc = 90f;
                main.useUnscaledTime = false;
                main.scalingMode = ParticleSystemScalingMode.Shape;
                shurikenRenderer.minParticleSize = 0.125f;
                shurikenRenderer.maxParticleSize = 0.45f;
                ShurikenConverter.Convert(owner);

                Require(gpu.shapeType == ShapeTypeGPU.Circle, "Circle Shape was not mapped.");
                Require(!gpu.useUnscaledTime,
                    "Updated Main Use Unscaled Time was not mapped.");
                Require(
                    gpu.scalingMode == ParticleSystemScalingMode.Shape,
                    "Updated Main Scaling Mode Shape was not mapped.");
                RequireApproximately(gpu.shapeCircleRadius, 3f,
                    "Circle radius must remain unscaled before GPU Shape TRS");
                Require(gpu.shapeLocalScale == shape.scale, "Shape scale was not preserved.");
                RequireApproximately(gpu.shapeConeArcDeg, 90f, "Shape Arc");
                Require(gpu.screenSpaceSizeClampEnabled,
                    "Renderer screen-space size clamp was not preserved.");
                RequireApproximately(
                    gpu.minParticleSize,
                    0.125f,
                    "Updated Renderer Min Particle Size");
                RequireApproximately(
                    gpu.maxParticleSize,
                    0.45f,
                    "Updated Renderer Max Particle Size");
                Require(gpu.emissionRateOverTimeMode == ParticleSystemCurveMode.TwoConstants,
                    "Emission Rate over Time Two Constants mode was not mapped.");
                RequireApproximately(gpu.emissionRateOverTimeMin, 4f,
                    "Emission Rate over Time minimum");
                RequireApproximately(gpu.emissionRateOverTime, 8f,
                    "Emission Rate over Time maximum");
                Require(gpu.startLifetimeMode ==
                        ParticleSystemCurveMode.TwoConstants &&
                        gpu.randomizeStartLifetime,
                    "Start Lifetime Two Constants mode was not preserved.");
                RequireApproximately(gpu.startLifetimeMin, 2f,
                    "Start Lifetime Two Constants minimum");
                RequireApproximately(gpu.startLifetime, 4f,
                    "Start Lifetime Two Constants maximum");
                Require(gpu.startSpeedMode ==
                        ParticleSystemCurveMode.TwoConstants &&
                        gpu.randomizeStartSpeed,
                    "Start Speed Two Constants mode was not preserved.");
                RequireApproximately(gpu.startSpeedMin, 1f,
                    "Start Speed Two Constants minimum");
                RequireApproximately(gpu.startSpeed, 3f,
                    "Start Speed Two Constants maximum");
                Require(gpu.startSizeMode ==
                        ParticleSystemCurveMode.TwoConstants &&
                        gpu.randomizeStartSize,
                    "Start Size Two Constants mode was not preserved.");
                RequireApproximately(gpu.startSizeMin, 0.5f,
                    "Start Size Two Constants minimum");
                RequireApproximately(gpu.startSize, 1.5f,
                    "Start Size Two Constants maximum");
                Require(gpu.gravityModifierMode ==
                        ParticleSystemCurveMode.TwoConstants &&
                        gpu.randomizeGravityModifier,
                    "Gravity Modifier Two Constants mode was not preserved.");
                RequireApproximately(gpu.gravityModifierMin, 0.25f,
                    "Gravity Modifier Two Constants minimum");
                RequireApproximately(gpu.gravityModifier, 0.75f,
                    "Gravity Modifier Two Constants maximum");
                Require(gpu.startRotationMode ==
                        ParticleSystemCurveMode.TwoConstants &&
                        gpu.randomizeStartRotation,
                    "Start Rotation Two Constants mode was not preserved.");
                RequireApproximately(gpu.startRotationMin, -0.5f,
                    "Start Rotation Two Constants minimum");
                RequireApproximately(gpu.startRotation, 0.5f,
                    "Start Rotation Two Constants maximum");
                Require(gpu.textureSheetRowMode ==
                        ParticleSystemAnimationRowMode.Random,
                    "Texture Sheet Animation Random row mode was not mapped.");

                gpu.startLifetimeLUT = null;
                gpu.startSpeedLUT = null;
                gpu.startSizeLUT = null;
                gpu.gravityModifierLUT = null;
                gpu.startRotationLUT = null;

                if (gpu.startColorLUT != null)
                {
                    secondStartColorAssetPath =
                        AssetDatabase.GetAssetPath(gpu.startColorLUT);
                    if (string.IsNullOrEmpty(secondStartColorAssetPath))
                    {
                        Object.DestroyImmediate(gpu.startColorLUT);
                    }
                    gpu.startColorLUT = null;
                }

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

                if (gpu.velocityOverLifetimeOrbitalLUT != null)
                {
                    secondVelocityOrbitalAssetPath =
                        AssetDatabase.GetAssetPath(
                            gpu.velocityOverLifetimeOrbitalLUT);
                    if (string.IsNullOrEmpty(secondVelocityOrbitalAssetPath))
                    {
                        Object.DestroyImmediate(
                            gpu.velocityOverLifetimeOrbitalLUT);
                    }
                    gpu.velocityOverLifetimeOrbitalLUT = null;
                }

                if (gpu.velocityOverLifetimeOrbitalOffsetLUT != null)
                {
                    secondVelocityOrbitalOffsetAssetPath =
                        AssetDatabase.GetAssetPath(
                            gpu.velocityOverLifetimeOrbitalOffsetLUT);
                    if (string.IsNullOrEmpty(
                            secondVelocityOrbitalOffsetAssetPath))
                    {
                        Object.DestroyImmediate(
                            gpu.velocityOverLifetimeOrbitalOffsetLUT);
                    }
                    gpu.velocityOverLifetimeOrbitalOffsetLUT = null;
                }

                if (gpu.limitVelocityOverLifetimeLUT != null)
                {
                    secondLimitVelocityAssetPath =
                        AssetDatabase.GetAssetPath(
                            gpu.limitVelocityOverLifetimeLUT);
                    if (string.IsNullOrEmpty(secondLimitVelocityAssetPath))
                    {
                        Object.DestroyImmediate(
                            gpu.limitVelocityOverLifetimeLUT);
                    }
                    gpu.limitVelocityOverLifetimeLUT = null;
                }

                if (gpu.inheritVelocityLUT != null)
                {
                    secondInheritVelocityAssetPath =
                        AssetDatabase.GetAssetPath(gpu.inheritVelocityLUT);
                    if (string.IsNullOrEmpty(secondInheritVelocityAssetPath))
                    {
                        Object.DestroyImmediate(gpu.inheritVelocityLUT);
                    }
                    gpu.inheritVelocityLUT = null;
                }

                if (gpu.lifetimeByEmitterSpeedLUT != null)
                {
                    secondLifetimeByEmitterSpeedAssetPath =
                        AssetDatabase.GetAssetPath(
                            gpu.lifetimeByEmitterSpeedLUT);
                    if (string.IsNullOrEmpty(
                            secondLifetimeByEmitterSpeedAssetPath))
                    {
                        Object.DestroyImmediate(
                            gpu.lifetimeByEmitterSpeedLUT);
                    }
                    gpu.lifetimeByEmitterSpeedLUT = null;
                }

                if (gpu.textureSheetFrameOverTimeLUT != null)
                {
                    secondTextureSheetFrameAssetPath =
                        AssetDatabase.GetAssetPath(
                            gpu.textureSheetFrameOverTimeLUT);
                    if (string.IsNullOrEmpty(
                            secondTextureSheetFrameAssetPath))
                    {
                        Object.DestroyImmediate(
                            gpu.textureSheetFrameOverTimeLUT);
                    }
                    gpu.textureSheetFrameOverTimeLUT = null;
                }

                if (gpu.textureSheetStartFrameLUT != null)
                {
                    secondTextureSheetStartAssetPath =
                        AssetDatabase.GetAssetPath(
                            gpu.textureSheetStartFrameLUT);
                    if (string.IsNullOrEmpty(
                            secondTextureSheetStartAssetPath))
                    {
                        Object.DestroyImmediate(gpu.textureSheetStartFrameLUT);
                    }
                    gpu.textureSheetStartFrameLUT = null;
                }

                if (gpu.rotationOverLifetimeIntegralLUT != null)
                {
                    secondRotationAssetPath =
                        AssetDatabase.GetAssetPath(gpu.rotationOverLifetimeIntegralLUT);
                    if (string.IsNullOrEmpty(secondRotationAssetPath))
                    {
                        Object.DestroyImmediate(gpu.rotationOverLifetimeIntegralLUT);
                    }
                    gpu.rotationOverLifetimeIntegralLUT = null;
                }

                if (gpu.rotationBySpeedLUT != null)
                {
                    secondRotationBySpeedAssetPath =
                        AssetDatabase.GetAssetPath(gpu.rotationBySpeedLUT);
                    if (string.IsNullOrEmpty(secondRotationBySpeedAssetPath))
                    {
                        Object.DestroyImmediate(gpu.rotationBySpeedLUT);
                    }
                    gpu.rotationBySpeedLUT = null;
                }

                if (gpu.colorOverLifetimeLUT != null)
                {
                    secondColorAssetPath =
                        AssetDatabase.GetAssetPath(gpu.colorOverLifetimeLUT);
                    if (string.IsNullOrEmpty(secondColorAssetPath))
                    {
                        Object.DestroyImmediate(gpu.colorOverLifetimeLUT);
                    }
                    gpu.colorOverLifetimeLUT = null;
                }

                if (gpu.sizeOverLifetimeLUT != null)
                {
                    secondSizeAssetPath =
                        AssetDatabase.GetAssetPath(gpu.sizeOverLifetimeLUT);
                    if (string.IsNullOrEmpty(secondSizeAssetPath))
                    {
                        Object.DestroyImmediate(gpu.sizeOverLifetimeLUT);
                    }
                    gpu.sizeOverLifetimeLUT = null;
                }

                if (gpu.colorBySpeedLUT != null)
                {
                    secondColorBySpeedAssetPath =
                        AssetDatabase.GetAssetPath(gpu.colorBySpeedLUT);
                    if (string.IsNullOrEmpty(secondColorBySpeedAssetPath))
                    {
                        Object.DestroyImmediate(gpu.colorBySpeedLUT);
                    }
                    gpu.colorBySpeedLUT = null;
                }

                if (gpu.sizeBySpeedLUT != null)
                {
                    secondSizeBySpeedAssetPath =
                        AssetDatabase.GetAssetPath(gpu.sizeBySpeedLUT);
                    if (string.IsNullOrEmpty(secondSizeBySpeedAssetPath))
                    {
                        Object.DestroyImmediate(gpu.sizeBySpeedLUT);
                    }
                    gpu.sizeBySpeedLUT = null;
                }

                ValidateSeparateAxisSizeMapping();
                ValidateShapeMappings();
                Debug.Log("PARTICLE_COMMON_FEATURE_MAPPING_RESULT:PASS");
            }
            finally
            {
                if (firstStartLifetimeLUT != null &&
                    string.IsNullOrEmpty(firstStartLifetimeAssetPath))
                {
                    Object.DestroyImmediate(firstStartLifetimeLUT);
                }
                if (firstStartRotationLUT != null &&
                    string.IsNullOrEmpty(firstStartRotationAssetPath))
                {
                    Object.DestroyImmediate(firstStartRotationLUT);
                }
                if (firstStartSpeedLUT != null &&
                    string.IsNullOrEmpty(firstStartSpeedAssetPath))
                {
                    Object.DestroyImmediate(firstStartSpeedLUT);
                }
                if (firstStartSizeLUT != null &&
                    string.IsNullOrEmpty(firstStartSizeAssetPath))
                {
                    Object.DestroyImmediate(firstStartSizeLUT);
                }
                if (firstGravityModifierLUT != null &&
                    string.IsNullOrEmpty(firstGravityModifierAssetPath))
                {
                    Object.DestroyImmediate(firstGravityModifierLUT);
                }
                if (firstStartColorLUT != null &&
                    string.IsNullOrEmpty(firstStartColorAssetPath))
                {
                    Object.DestroyImmediate(firstStartColorLUT);
                }
                if (firstForceLUT != null && string.IsNullOrEmpty(firstForceAssetPath))
                {
                    Object.DestroyImmediate(firstForceLUT);
                }
                if (firstVelocityLUT != null &&
                    string.IsNullOrEmpty(firstVelocityAssetPath))
                {
                    Object.DestroyImmediate(firstVelocityLUT);
                }
                if (firstVelocityOrbitalLUT != null &&
                    string.IsNullOrEmpty(firstVelocityOrbitalAssetPath))
                {
                    Object.DestroyImmediate(firstVelocityOrbitalLUT);
                }
                if (firstVelocityOrbitalOffsetLUT != null &&
                    string.IsNullOrEmpty(
                        firstVelocityOrbitalOffsetAssetPath))
                {
                    Object.DestroyImmediate(firstVelocityOrbitalOffsetLUT);
                }
                if (firstLimitVelocityLUT != null &&
                    string.IsNullOrEmpty(firstLimitVelocityAssetPath))
                {
                    Object.DestroyImmediate(firstLimitVelocityLUT);
                }
                if (firstInheritVelocityLUT != null &&
                    string.IsNullOrEmpty(firstInheritVelocityAssetPath))
                {
                    Object.DestroyImmediate(firstInheritVelocityLUT);
                }
                if (firstLifetimeByEmitterSpeedLUT != null &&
                    string.IsNullOrEmpty(
                        firstLifetimeByEmitterSpeedAssetPath))
                {
                    Object.DestroyImmediate(
                        firstLifetimeByEmitterSpeedLUT);
                }
                if (firstTextureSheetFrameLUT != null &&
                    string.IsNullOrEmpty(firstTextureSheetFrameAssetPath))
                {
                    Object.DestroyImmediate(firstTextureSheetFrameLUT);
                }
                if (firstTextureSheetStartLUT != null &&
                    string.IsNullOrEmpty(firstTextureSheetStartAssetPath))
                {
                    Object.DestroyImmediate(firstTextureSheetStartLUT);
                }
                if (firstRotationLUT != null &&
                    string.IsNullOrEmpty(firstRotationAssetPath))
                {
                    Object.DestroyImmediate(firstRotationLUT);
                }
                if (firstRotationBySpeedLUT != null &&
                    string.IsNullOrEmpty(firstRotationBySpeedAssetPath))
                {
                    Object.DestroyImmediate(firstRotationBySpeedLUT);
                }
                if (firstColorLUT != null && string.IsNullOrEmpty(firstColorAssetPath))
                {
                    Object.DestroyImmediate(firstColorLUT);
                }
                if (firstSizeLUT != null && string.IsNullOrEmpty(firstSizeAssetPath))
                {
                    Object.DestroyImmediate(firstSizeLUT);
                }
                if (firstColorBySpeedLUT != null &&
                    string.IsNullOrEmpty(firstColorBySpeedAssetPath))
                {
                    Object.DestroyImmediate(firstColorBySpeedLUT);
                }
                if (firstSizeBySpeedLUT != null &&
                    string.IsNullOrEmpty(firstSizeBySpeedAssetPath))
                {
                    Object.DestroyImmediate(firstSizeBySpeedLUT);
                }
                Object.DestroyImmediate(owner);
                if (!string.IsNullOrEmpty(firstStartLifetimeAssetPath))
                {
                    AssetDatabase.DeleteAsset(firstStartLifetimeAssetPath);
                }
                if (!string.IsNullOrEmpty(firstStartRotationAssetPath))
                {
                    AssetDatabase.DeleteAsset(firstStartRotationAssetPath);
                }
                if (!string.IsNullOrEmpty(firstStartSpeedAssetPath))
                {
                    AssetDatabase.DeleteAsset(firstStartSpeedAssetPath);
                }
                if (!string.IsNullOrEmpty(firstStartSizeAssetPath))
                {
                    AssetDatabase.DeleteAsset(firstStartSizeAssetPath);
                }
                if (!string.IsNullOrEmpty(firstGravityModifierAssetPath))
                {
                    AssetDatabase.DeleteAsset(firstGravityModifierAssetPath);
                }
                if (!string.IsNullOrEmpty(firstStartColorAssetPath))
                {
                    AssetDatabase.DeleteAsset(firstStartColorAssetPath);
                }
                if (!string.IsNullOrEmpty(secondStartColorAssetPath) &&
                    secondStartColorAssetPath != firstStartColorAssetPath)
                {
                    AssetDatabase.DeleteAsset(secondStartColorAssetPath);
                }
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
                if (!string.IsNullOrEmpty(firstVelocityOrbitalAssetPath))
                {
                    AssetDatabase.DeleteAsset(firstVelocityOrbitalAssetPath);
                }
                if (!string.IsNullOrEmpty(secondVelocityOrbitalAssetPath) &&
                    secondVelocityOrbitalAssetPath !=
                        firstVelocityOrbitalAssetPath)
                {
                    AssetDatabase.DeleteAsset(secondVelocityOrbitalAssetPath);
                }
                if (!string.IsNullOrEmpty(
                        firstVelocityOrbitalOffsetAssetPath))
                {
                    AssetDatabase.DeleteAsset(
                        firstVelocityOrbitalOffsetAssetPath);
                }
                if (!string.IsNullOrEmpty(
                        secondVelocityOrbitalOffsetAssetPath) &&
                    secondVelocityOrbitalOffsetAssetPath !=
                        firstVelocityOrbitalOffsetAssetPath)
                {
                    AssetDatabase.DeleteAsset(
                        secondVelocityOrbitalOffsetAssetPath);
                }
                if (!string.IsNullOrEmpty(firstLimitVelocityAssetPath))
                {
                    AssetDatabase.DeleteAsset(firstLimitVelocityAssetPath);
                }
                if (!string.IsNullOrEmpty(secondLimitVelocityAssetPath) &&
                    secondLimitVelocityAssetPath != firstLimitVelocityAssetPath)
                {
                    AssetDatabase.DeleteAsset(secondLimitVelocityAssetPath);
                }
                if (!string.IsNullOrEmpty(firstInheritVelocityAssetPath))
                {
                    AssetDatabase.DeleteAsset(firstInheritVelocityAssetPath);
                }
                if (!string.IsNullOrEmpty(secondInheritVelocityAssetPath) &&
                    secondInheritVelocityAssetPath != firstInheritVelocityAssetPath)
                {
                    AssetDatabase.DeleteAsset(secondInheritVelocityAssetPath);
                }
                if (!string.IsNullOrEmpty(
                        firstLifetimeByEmitterSpeedAssetPath))
                {
                    AssetDatabase.DeleteAsset(
                        firstLifetimeByEmitterSpeedAssetPath);
                }
                if (!string.IsNullOrEmpty(
                        secondLifetimeByEmitterSpeedAssetPath) &&
                    secondLifetimeByEmitterSpeedAssetPath !=
                        firstLifetimeByEmitterSpeedAssetPath)
                {
                    AssetDatabase.DeleteAsset(
                        secondLifetimeByEmitterSpeedAssetPath);
                }
                if (!string.IsNullOrEmpty(firstTextureSheetFrameAssetPath))
                {
                    AssetDatabase.DeleteAsset(firstTextureSheetFrameAssetPath);
                }
                if (!string.IsNullOrEmpty(secondTextureSheetFrameAssetPath) &&
                    secondTextureSheetFrameAssetPath !=
                        firstTextureSheetFrameAssetPath)
                {
                    AssetDatabase.DeleteAsset(secondTextureSheetFrameAssetPath);
                }
                if (!string.IsNullOrEmpty(firstTextureSheetStartAssetPath))
                {
                    AssetDatabase.DeleteAsset(firstTextureSheetStartAssetPath);
                }
                if (!string.IsNullOrEmpty(secondTextureSheetStartAssetPath) &&
                    secondTextureSheetStartAssetPath !=
                        firstTextureSheetStartAssetPath)
                {
                    AssetDatabase.DeleteAsset(secondTextureSheetStartAssetPath);
                }
                if (!string.IsNullOrEmpty(firstRotationAssetPath))
                {
                    AssetDatabase.DeleteAsset(firstRotationAssetPath);
                }
                if (!string.IsNullOrEmpty(secondRotationAssetPath) &&
                    secondRotationAssetPath != firstRotationAssetPath)
                {
                    AssetDatabase.DeleteAsset(secondRotationAssetPath);
                }
                if (!string.IsNullOrEmpty(firstRotationBySpeedAssetPath))
                {
                    AssetDatabase.DeleteAsset(firstRotationBySpeedAssetPath);
                }
                if (!string.IsNullOrEmpty(secondRotationBySpeedAssetPath) &&
                    secondRotationBySpeedAssetPath != firstRotationBySpeedAssetPath)
                {
                    AssetDatabase.DeleteAsset(secondRotationBySpeedAssetPath);
                }
                if (!string.IsNullOrEmpty(firstColorAssetPath))
                {
                    AssetDatabase.DeleteAsset(firstColorAssetPath);
                }
                if (!string.IsNullOrEmpty(secondColorAssetPath) &&
                    secondColorAssetPath != firstColorAssetPath)
                {
                    AssetDatabase.DeleteAsset(secondColorAssetPath);
                }
                if (!string.IsNullOrEmpty(firstSizeAssetPath))
                {
                    AssetDatabase.DeleteAsset(firstSizeAssetPath);
                }
                if (!string.IsNullOrEmpty(secondSizeAssetPath) &&
                    secondSizeAssetPath != firstSizeAssetPath)
                {
                    AssetDatabase.DeleteAsset(secondSizeAssetPath);
                }
                if (!string.IsNullOrEmpty(firstColorBySpeedAssetPath))
                {
                    AssetDatabase.DeleteAsset(firstColorBySpeedAssetPath);
                }
                if (!string.IsNullOrEmpty(secondColorBySpeedAssetPath) &&
                    secondColorBySpeedAssetPath != firstColorBySpeedAssetPath)
                {
                    AssetDatabase.DeleteAsset(secondColorBySpeedAssetPath);
                }
                if (!string.IsNullOrEmpty(firstSizeBySpeedAssetPath))
                {
                    AssetDatabase.DeleteAsset(firstSizeBySpeedAssetPath);
                }
                if (!string.IsNullOrEmpty(secondSizeBySpeedAssetPath) &&
                    secondSizeBySpeedAssetPath != firstSizeBySpeedAssetPath)
                {
                    AssetDatabase.DeleteAsset(secondSizeBySpeedAssetPath);
                }
            }
        }

        static void ValidateSeparateAxisSizeMapping()
        {
            var owner = new GameObject("ParticleSeparateAxisSizeMappingValidation");
            owner.SetActive(false);
            var generatedTextures = new Texture2D[6];
            try
            {
                var shuriken = owner.AddComponent<ParticleSystem>();
                var main = shuriken.main;
                main.startSize3D = true;
                main.startSizeX = new ParticleSystem.MinMaxCurve(
                    1f,
                    AnimationCurve.Linear(0f, 0.5f, 1f, 1f),
                    AnimationCurve.Linear(0f, 1.5f, 1f, 2.5f));
                main.startSizeY = new ParticleSystem.MinMaxCurve(
                    1f,
                    AnimationCurve.Linear(0f, 0.25f, 1f, 0.75f),
                    AnimationCurve.Linear(0f, 2f, 1f, 3f));
                main.startSizeZ = 4f;

                var sizeOverLifetime = shuriken.sizeOverLifetime;
                sizeOverLifetime.enabled = true;
                sizeOverLifetime.separateAxes = true;
                sizeOverLifetime.x = new ParticleSystem.MinMaxCurve(
                    1f,
                    AnimationCurve.Linear(0f, 0.5f, 1f, 1f),
                    AnimationCurve.Linear(0f, 1.5f, 1f, 2f));
                sizeOverLifetime.y = new ParticleSystem.MinMaxCurve(
                    1f,
                    AnimationCurve.Linear(0f, 0.75f, 1f, 0.25f),
                    AnimationCurve.Linear(0f, 2.5f, 1f, 1.5f));
                sizeOverLifetime.z = 3f;

                var sizeBySpeed = shuriken.sizeBySpeed;
                sizeBySpeed.enabled = true;
                sizeBySpeed.separateAxes = true;
                sizeBySpeed.range = new Vector2(2f, 6f);
                sizeBySpeed.x = new ParticleSystem.MinMaxCurve(
                    1f,
                    AnimationCurve.Linear(0f, 0.25f, 1f, 0.75f),
                    AnimationCurve.Linear(0f, 1.25f, 1f, 2.25f));
                sizeBySpeed.y = new ParticleSystem.MinMaxCurve(
                    1f,
                    AnimationCurve.Linear(0f, 0.5f, 1f, 1f),
                    AnimationCurve.Linear(0f, 1.5f, 1f, 0.75f));
                sizeBySpeed.z = 2f;

                ShurikenConverter.Convert(owner);
                var gpu = owner.GetComponent<GPUParticleSystem>();
                Require(gpu != null,
                    "Separate-axis size conversion did not create GPUParticleSystem.");

                generatedTextures[0] = gpu.startSizeLUT;
                generatedTextures[1] = gpu.startSizeYLUT;
                generatedTextures[2] = gpu.sizeOverLifetimeLUT;
                generatedTextures[3] = gpu.sizeOverLifetimeYLUT;
                generatedTextures[4] = gpu.sizeBySpeedLUT;
                generatedTextures[5] = gpu.sizeBySpeedYLUT;

                Require(gpu.startSize3D,
                    "Start Size 3D enabled state was not mapped.");
                Require(gpu.startSizeMode == ParticleSystemCurveMode.TwoCurves &&
                        gpu.startSizeYMode == ParticleSystemCurveMode.TwoCurves,
                    "Start Size 3D X/Y curve modes were not mapped.");
                RequireTwoRowLUT(gpu.startSizeLUT, "Start Size 3D X");
                RequireTwoRowLUT(gpu.startSizeYLUT, "Start Size 3D Y");
                RequireLUTEndpoints(
                    gpu.startSizeLUT, 0.5f, 1f, 1.5f, 2.5f,
                    "Start Size 3D X");
                RequireLUTEndpoints(
                    gpu.startSizeYLUT, 0.25f, 0.75f, 2f, 3f,
                    "Start Size 3D Y");

                Require(gpu.sizeOverLifetimeSeparateAxes,
                    "Size over Lifetime Separate Axes state was not mapped.");
                RequireTwoRowLUT(
                    gpu.sizeOverLifetimeLUT, "Size over Lifetime X");
                RequireTwoRowLUT(
                    gpu.sizeOverLifetimeYLUT, "Size over Lifetime Y");
                RequireLUTEndpoints(
                    gpu.sizeOverLifetimeLUT, 0.5f, 1f, 1.5f, 2f,
                    "Size over Lifetime X");
                RequireLUTEndpoints(
                    gpu.sizeOverLifetimeYLUT, 0.75f, 0.25f, 2.5f, 1.5f,
                    "Size over Lifetime Y");

                Require(gpu.sizeBySpeedEnabled &&
                        gpu.sizeBySpeedSeparateAxes,
                    "Size by Speed Separate Axes state was not mapped.");
                RequireApproximately(gpu.sizeBySpeedRange.x, 2f,
                    "Size by Speed Separate Axes range minimum");
                RequireApproximately(gpu.sizeBySpeedRange.y, 6f,
                    "Size by Speed Separate Axes range maximum");
                RequireTwoRowLUT(gpu.sizeBySpeedLUT, "Size by Speed X");
                RequireTwoRowLUT(gpu.sizeBySpeedYLUT, "Size by Speed Y");
                RequireLUTEndpoints(
                    gpu.sizeBySpeedLUT, 0.25f, 0.75f, 1.25f, 2.25f,
                    "Size by Speed X");
                RequireLUTEndpoints(
                    gpu.sizeBySpeedYLUT, 0.5f, 1f, 1.5f, 0.75f,
                    "Size by Speed Y");
            }
            finally
            {
                Object.DestroyImmediate(owner);
                for (int i = 0; i < generatedTextures.Length; i++)
                {
                    CleanupGeneratedValidationTexture(generatedTextures[i]);
                }
            }
        }

        static void RequireTwoRowLUT(Texture2D texture, string label)
        {
            Require(texture != null && texture.height == 2,
                $"{label} minimum/maximum LUT rows were not generated.");
        }

        static void RequireLUTEndpoints(
            Texture2D texture,
            float minimumStart,
            float minimumEnd,
            float maximumStart,
            float maximumEnd,
            string label)
        {
            RequireApproximately(texture.GetPixel(0, 0).r,
                minimumStart, $"{label} minimum start");
            RequireApproximately(texture.GetPixel(texture.width - 1, 0).r,
                minimumEnd, $"{label} minimum end");
            RequireApproximately(texture.GetPixel(0, 1).r,
                maximumStart, $"{label} maximum start");
            RequireApproximately(texture.GetPixel(texture.width - 1, 1).r,
                maximumEnd, $"{label} maximum end");
        }

        static void CleanupGeneratedValidationTexture(Texture2D texture)
        {
            if (texture == null) return;

            string assetPath = AssetDatabase.GetAssetPath(texture);
            if (string.IsNullOrEmpty(assetPath))
            {
                Object.DestroyImmediate(texture);
            }
            else
            {
                AssetDatabase.DeleteAsset(assetPath);
            }
        }

        static void ValidateShapeMappings()
        {
            var owner = new GameObject("ParticleShapeMappingValidation");
            owner.SetActive(false);
            try
            {
                var particleSystem = owner.AddComponent<ParticleSystem>();
                var main = particleSystem.main;
                main.playOnAwake = false;
                main.startLifetime = 2f;
                main.startSpeed = 1f;

                var emission = particleSystem.emission;
                emission.enabled = false;

                var shape = particleSystem.shape;
                shape.enabled = true;
                shape.position = Vector3.zero;
                shape.rotation = Vector3.zero;
                shape.scale = Vector3.one;
                shape.radius = 2f;
                shape.radiusThickness = 1f;
                shape.arc = 360f;
                shape.alignToDirection = false;

                shape.shapeType = ParticleSystemShapeType.Donut;
                shape.donutRadius = 0.5f;
                shape.radiusThickness = 0f;
                ShurikenConverter.Convert(owner);
                var gpu = owner.GetComponent<GPUParticleSystem>();
                Require(gpu != null, "Shape mapping did not create a GPU system.");
                Require(gpu.shapeType == ShapeTypeGPU.Donut,
                    "Donut Shape was not mapped.");
                Require(gpu.shapeEmitFrom == ShapeEmitFromGPU.Surface,
                    "Donut shell was not mapped to Surface emission.");
                RequireApproximately(gpu.shapeDonutRadius, 2f,
                    "Donut major radius");
                RequireApproximately(gpu.shapeDonutThickness, 0.5f,
                    "Donut cross-section radius");

                shape.shapeType = ParticleSystemShapeType.SingleSidedEdge;
                shape.radius = 2f;
                shape.radiusThickness = 1f;
                shape.scale = new Vector3(3f, 1f, 1f);
                shape.alignToDirection = true;
                ShurikenConverter.Convert(owner);
                Require(gpu.shapeType == ShapeTypeGPU.Edge,
                    "SingleSidedEdge Shape was not mapped to Edge.");
                Require(gpu.shapeEmitFrom == ShapeEmitFromGPU.Edge,
                    "SingleSidedEdge emission mode was not mapped.");
                RequireApproximately(gpu.shapeEdgeLength, 4f,
                    "Unscaled Edge length");
                Require(gpu.shapeLocalScale == shape.scale,
                    "Edge Shape scale was not preserved.");
                Require(gpu.alignToDirection,
                    "Shape Align to Direction metadata was not preserved.");

                shape.shapeType = ParticleSystemShapeType.Rectangle;
                shape.scale = new Vector3(4f, 2f, 1f);
                shape.alignToDirection = false;
                ShurikenConverter.Convert(owner);
                Require(gpu.shapeType == ShapeTypeGPU.Rectangle,
                    "Rectangle Shape was not mapped.");
                Require(gpu.shapeEmitFrom == ShapeEmitFromGPU.Volume,
                    "Rectangle Shape was not mapped to area emission.");
                Require(gpu.shapeRectangleSize == Vector2.one,
                    "Rectangle base size must remain unscaled before Shape TRS.");
                Require(gpu.shapeLocalScale == shape.scale,
                    "Rectangle Shape scale was not preserved.");

                shape.shapeType = ParticleSystemShapeType.BoxEdge;
                ShurikenConverter.Convert(owner);
                Require(gpu.shapeType == ShapeTypeGPU.Box,
                    "BoxEdge Shape was not mapped to Box.");
                Require(gpu.shapeEmitFrom == ShapeEmitFromGPU.Edge,
                    "BoxEdge Shape was not mapped to edge emission.");
            }
            finally
            {
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

        static void RequireColorApproximately(Color actual, Color expected, string label)
        {
            if (Mathf.Abs(actual.r - expected.r) > 0.01f ||
                Mathf.Abs(actual.g - expected.g) > 0.01f ||
                Mathf.Abs(actual.b - expected.b) > 0.01f ||
                Mathf.Abs(actual.a - expected.a) > 0.01f)
            {
                throw new InvalidOperationException(
                    $"{label} mismatch. Expected {expected}, got {actual}.");
            }
        }

        static Gradient CreateGradient(Color start, Color end)
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(start, 0f),
                    new GradientColorKey(end, 1f)
                },
                new[]
                {
                    new GradientAlphaKey(start.a, 0f),
                    new GradientAlphaKey(end.a, 1f)
                });
            return gradient;
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
