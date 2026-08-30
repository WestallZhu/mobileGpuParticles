using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;

namespace GPUParticles
{
    public enum ParticleABDisplayMode
    {
        Both,
        ShurikenOnly,
        GPUOnly
    }

    public enum ParticleABValidationProfile
    {
        BaselineCone,
        ForceOverLifetimePoint,
        RandomizedMainPoint,
        EmissionBurstPoint,
        EmissionRateCurvePoint,
        EmissionRateDistancePoint,
        VelocityOverLifetimePoint,
        RotationOverLifetimeCurvePoint,
        RotationBySpeedCurvePoint,
        ColorSizeOverLifetimeRandomizedPoint,
        ColorSizeBySpeedRandomizedPoint
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class ParticleABValidationController : MonoBehaviour
    {
        [Header("A/B Systems")]
        public ParticleSystem shuriken;
        public GPUParticleSystem gpuParticles;
        public ParticleABDisplayMode displayMode = ParticleABDisplayMode.Both;
        public ParticleABValidationProfile validationProfile = ParticleABValidationProfile.BaselineCone;
        public uint randomSeed = 12345;

        [Header("Deterministic Playback")]
        [Min(1)] public int fixedFrameRate = 60;

        [Header("RT Capture")]
        public bool captureOnPlay = true;
        [Min(0.1f)] public float captureFrequency = 5f;
        [Min(0.1f)] public float captureDuration = 10f;
        [Min(64)] public int captureWidth = 1280;
        [Min(64)] public int captureHeight = 720;
        public string outputFolder = "TestResults/ParticleAB";
        public bool exitEditorWhenCaptureCompletes;

        Camera captureCamera;
        ParticleSystemRenderer shurikenRenderer;
        ParticleSystem.Particle[] shurikenParticles;
        RenderTexture cameraCaptureRT;
        int playbackFrame;
        int nextCaptureFrame;
        int finalCaptureFrame;
        int captureIndex;
        bool captureActive;
        string sessionFolder;
        string metricsPath;
        int maximumCountDelta;
        float maximumMeanAgeError;
        float maximumMeanSpeedError;
        float maximumMeanVelocityError;
        float maximumMeanPositionError;
        float maximumShurikenConeError;
        float maximumGPUConeError;
        float maximumForceKinematicsError;
        float maximumShurikenColorBoundsError;
        float maximumGPUColorBoundsError;
        float maximumShurikenSizeBoundsError;
        float maximumGPUSizeBoundsError;
        float maximumShurikenRotationError;
        float maximumGPURotationError;
        int maximumShurikenParticleCount;
        int maximumGPUParticleCount;
        Texture2D profileForceLUT;
        Texture2D profileVelocityLUT;
        Texture2D profileColorLUT;
        Texture2D profileSizeLUT;
        Texture2D profileRotationLUT;
        Texture2D profileRotationBySpeedLUT;
        Gradient profileColorMinimumGradient;
        Gradient profileColorMaximumGradient;
        AnimationCurve profileSizeMinimumCurve;
        AnimationCurve profileSizeMaximumCurve;
        static readonly Vector3 ValidationForce = new Vector3(2f, -1f, 0.5f);
        static readonly Vector3 RotationBySpeedAcceleration = Vector3.right * 2f;
        const float RotationProfileStartRotation = 0.25f;
        Vector3 shurikenBasePositionWS;
        Vector3 gpuBasePositionWS;
        ObservedRange shurikenLifetimeRange;
        ObservedRange gpuLifetimeRange;
        ObservedRange shurikenSpeedRange;
        ObservedRange gpuSpeedRange;
        ObservedRange shurikenSizeRange;
        ObservedRange gpuSizeRange;
        ObservedRange shurikenColorRedRange;
        ObservedRange gpuColorRedRange;
        ObservedRange shurikenDistancePositionRange;
        ObservedRange gpuDistancePositionRange;
        ObservedRange shurikenLifetimeColorBlendRange;
        ObservedRange gpuLifetimeColorBlendRange;
        ObservedRange shurikenLifetimeSizeBlendRange;
        ObservedRange gpuLifetimeSizeBlendRange;
        ObservedRange shurikenSpeedColorBlendRange;
        ObservedRange gpuSpeedColorBlendRange;
        ObservedRange shurikenSpeedSizeBlendRange;
        ObservedRange gpuSpeedSizeBlendRange;

        struct ObservedRange
        {
            public float Minimum;
            public float Maximum;
            public bool HasSamples;

            public void Reset()
            {
                Minimum = float.PositiveInfinity;
                Maximum = float.NegativeInfinity;
                HasSamples = false;
            }

            public void Observe(float value)
            {
                Minimum = Mathf.Min(Minimum, value);
                Maximum = Mathf.Max(Maximum, value);
                HasSamples = true;
            }

            public bool Covers(float expectedMinimum, float expectedMaximum)
            {
                const float boundsTolerance = 0.01f;
                float requiredSpread = (expectedMaximum - expectedMinimum) * 0.4f;
                return HasSamples &&
                       Minimum >= expectedMinimum - boundsTolerance &&
                       Maximum <= expectedMaximum + boundsTolerance &&
                       Maximum - Minimum >= requiredSpread;
            }
        }

        void Awake()
        {
            captureCamera = GetComponent<Camera>();
            shurikenRenderer = shuriken != null ? shuriken.GetComponent<ParticleSystemRenderer>() : null;
        }

        void Start()
        {
            fixedFrameRate = Mathf.Max(1, fixedFrameRate);
            Time.captureFramerate = fixedFrameRate;
            Application.targetFrameRate = fixedFrameRate;

            shurikenBasePositionWS = shuriken != null
                ? shuriken.transform.position
                : Vector3.zero;
            gpuBasePositionWS = gpuParticles != null
                ? gpuParticles.transform.position
                : Vector3.zero;

            ConfigureValidationProfile();
            RestartPlayback();
            ApplyDisplayMode();

            if (captureOnPlay)
            {
                BeginCapture();
            }
        }

        void OnDestroy()
        {
            Time.captureFramerate = 0;
            if (profileForceLUT != null) Destroy(profileForceLUT);
            if (profileVelocityLUT != null) Destroy(profileVelocityLUT);
            if (profileColorLUT != null) Destroy(profileColorLUT);
            if (profileSizeLUT != null) Destroy(profileSizeLUT);
            if (profileRotationLUT != null) Destroy(profileRotationLUT);
            if (profileRotationBySpeedLUT != null) Destroy(profileRotationBySpeedLUT);
            ReleaseCameraCaptureTarget();
        }

        void Update()
        {
            if (captureActive &&
                validationProfile == ParticleABValidationProfile.EmissionRateDistancePoint)
            {
                float nextSimulationTime = (playbackFrame + 1f) / fixedFrameRate;
                MoveValidationEmitters(nextSimulationTime);
            }

            if (Input.GetKeyDown(KeyCode.B)) SetDisplayMode(ParticleABDisplayMode.Both);
            if (Input.GetKeyDown(KeyCode.S)) SetDisplayMode(ParticleABDisplayMode.ShurikenOnly);
            if (Input.GetKeyDown(KeyCode.G)) SetDisplayMode(ParticleABDisplayMode.GPUOnly);
            if (Input.GetKeyDown(KeyCode.R)) RestartPlayback();
            if (Input.GetKeyDown(KeyCode.Space)) Time.timeScale = Time.timeScale > 0f ? 0f : 1f;
            if (Input.GetKeyDown(KeyCode.C) && !captureActive) BeginCapture();
        }

        void LateUpdate()
        {
            if (!captureActive) return;

            // Batch mode has no regular GameView render, so a render-pass-driven
            // simulation would otherwise advance only on capture frames. Submit the
            // normal guarded simulation once per validation frame; Camera.Render then
            // only reads that state because the per-frame guard rejects a second step.
            if (gpuParticles != null)
            {
                CommandBuffer command = CommandBufferPool.Get("ParticleAB.SimulateFrame");
                gpuParticles.Simulate(command, captureCamera);
                Graphics.ExecuteCommandBuffer(command);
                CommandBufferPool.Release(command);
            }

            playbackFrame++;
            if (playbackFrame >= nextCaptureFrame)
            {
                float elapsed = (float)playbackFrame / fixedFrameRate;
                CaptureSnapshot(elapsed);
                captureIndex++;
                nextCaptureFrame = CaptureFrameForIndex(captureIndex);
            }

            if (playbackFrame >= finalCaptureFrame)
            {
                CompleteCapture();
            }
        }

        void ConfigureValidationProfile()
        {
            if (validationProfile == ParticleABValidationProfile.BaselineCone ||
                shuriken == null || gpuParticles == null)
            {
                return;
            }

            ResetBySpeedModules();

            if (validationProfile == ParticleABValidationProfile.RandomizedMainPoint)
            {
                ConfigureRandomizedMainProfile();
                return;
            }

            if (validationProfile == ParticleABValidationProfile.EmissionBurstPoint)
            {
                ConfigureEmissionBurstProfile();
                return;
            }

            if (validationProfile == ParticleABValidationProfile.EmissionRateCurvePoint)
            {
                ConfigureEmissionRateCurveProfile();
                return;
            }

            if (validationProfile == ParticleABValidationProfile.EmissionRateDistancePoint)
            {
                ConfigureEmissionRateDistanceProfile();
                return;
            }

            if (validationProfile == ParticleABValidationProfile.VelocityOverLifetimePoint)
            {
                ConfigureVelocityOverLifetimeProfile();
                return;
            }

            if (validationProfile == ParticleABValidationProfile.RotationOverLifetimeCurvePoint)
            {
                ConfigureRotationOverLifetimeProfile();
                return;
            }

            if (validationProfile == ParticleABValidationProfile.RotationBySpeedCurvePoint)
            {
                ConfigureRotationBySpeedProfile();
                return;
            }

            if (validationProfile ==
                ParticleABValidationProfile.ColorSizeOverLifetimeRandomizedPoint)
            {
                ConfigureColorSizeOverLifetimeProfile();
                return;
            }

            if (validationProfile ==
                ParticleABValidationProfile.ColorSizeBySpeedRandomizedPoint)
            {
                ConfigureColorSizeBySpeedProfile();
                return;
            }

            var main = shuriken.main;
            main.maxParticles = 1000;
            main.startLifetime = 5f;
            main.startSpeed = 0f;
            main.startSize3D = false;
            main.startSize = 1f;
            main.startColor = Color.white;
            main.startRotation3D = false;
            main.startRotation = 0f;
            main.gravityModifier = 0f;
            main.simulationSpeed = 1f;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;

            var emission = shuriken.emission;
            emission.enabled = true;
            emission.rateOverTime = 10f;
            emission.rateOverDistance = 0f;
            emission.SetBursts(new ParticleSystem.Burst[0]);

            var shape = shuriken.shape;
            shape.enabled = false;

            var colorOverLifetime = shuriken.colorOverLifetime;
            colorOverLifetime.enabled = false;
            var sizeOverLifetime = shuriken.sizeOverLifetime;
            sizeOverLifetime.enabled = false;
            var rotationOverLifetime = shuriken.rotationOverLifetime;
            rotationOverLifetime.enabled = false;

            var force = shuriken.forceOverLifetime;
            force.enabled = true;
            force.space = ParticleSystemSimulationSpace.Local;
            force.randomized = false;
            force.x = ValidationForce.x;
            force.y = ValidationForce.y;
            force.z = ValidationForce.z;
            var velocityOverLifetime = shuriken.velocityOverLifetime;
            velocityOverLifetime.enabled = false;

            gpuParticles.maxParticles = main.maxParticles;
            gpuParticles.emissionEnabled = true;
            gpuParticles.SetEmissionRateOverTime(emission.rateOverTime);
            gpuParticles.SetEmissionRateOverDistance(emission.rateOverDistance);
            gpuParticles.emissionDuration = main.duration;
            gpuParticles.emissionLooping = main.loop;
            gpuParticles.SetEmissionStartDelayRange(0f, 0f);
            gpuParticles.SetEmissionBursts(System.Array.Empty<ParticleSystem.Burst>());
            gpuParticles.startLifetime = 5f;
            gpuParticles.startSpeed = 0f;
            gpuParticles.startSize = 1f;
            gpuParticles.startColor = Color.white;
            gpuParticles.startRotation = 0f;
            gpuParticles.rotationOverLifetime = 0f;
            gpuParticles.rotationOverLifetimeIntegralLUT =
                CurveLUTBuilder.GetDefaultZeroLUT();
            gpuParticles.gravityModifier = 0f;
            gpuParticles.simulationSpeed = 1f;
            gpuParticles.simulationSpace = SimulationSpace.Local;
            gpuParticles.colorOverLifetimeMode = ParticleSystemGradientMode.Gradient;
            gpuParticles.colorOverLifetimeLUT = GradientLUTBuilder.GetDefaultWhiteLUT();
            gpuParticles.sizeOverLifetimeLUT = CurveLUTBuilder.GetDefaultUnitLUT();
            gpuParticles.shapeType = ShapeTypeGPU.Point;
            gpuParticles.shapeEmitFrom = ShapeEmitFromGPU.Base;
            gpuParticles.alignToDirection = false;
            gpuParticles.shapeLocalPosition = Vector3.zero;
            gpuParticles.shapeLocalRotationEuler = Vector3.zero;
            gpuParticles.shapeLocalScale = Vector3.one;
            gpuParticles.forceOverLifetimeEnabled = true;
            gpuParticles.forceOverLifetimeSpace = SimulationSpace.Local;
            gpuParticles.forceOverLifetimeRandomized = false;

            if (profileForceLUT != null) Destroy(profileForceLUT);
            profileForceLUT = MinMaxCurveVector3LUTBuilder.Build(force.x, force.y, force.z);
            gpuParticles.forceOverLifetimeLUT = profileForceLUT;
            gpuParticles.velocityOverLifetimeEnabled = false;
            gpuParticles.velocityOverLifetimeLUT =
                MinMaxCurveVector3LUTBuilder.GetDefaultZeroLUT();
        }

        void ConfigureRandomizedMainProfile()
        {
            var main = shuriken.main;
            main.maxParticles = 1000;
            main.startLifetime = new ParticleSystem.MinMaxCurve(3f, 5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1f, 3f);
            main.startSize3D = false;
            main.startSize = new ParticleSystem.MinMaxCurve(0.5f, 1.5f);
            main.startColor = new ParticleSystem.MinMaxGradient(Color.red, Color.blue);
            main.startRotation3D = false;
            main.startRotation = new ParticleSystem.MinMaxCurve(-Mathf.PI, Mathf.PI);
            main.gravityModifier = 0f;
            main.simulationSpeed = 1f;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;

            var emission = shuriken.emission;
            emission.enabled = true;
            emission.rateOverTime = 30f;
            emission.rateOverDistance = 0f;
            emission.SetBursts(new ParticleSystem.Burst[0]);

            var shape = shuriken.shape;
            shape.enabled = false;

            var colorOverLifetime = shuriken.colorOverLifetime;
            colorOverLifetime.enabled = false;
            var sizeOverLifetime = shuriken.sizeOverLifetime;
            sizeOverLifetime.enabled = false;

            var rotationOverLifetime = shuriken.rotationOverLifetime;
            rotationOverLifetime.enabled = true;
            rotationOverLifetime.separateAxes = false;
            rotationOverLifetime.z = new ParticleSystem.MinMaxCurve(-1f, 1f);

            var force = shuriken.forceOverLifetime;
            force.enabled = false;
            var velocityOverLifetime = shuriken.velocityOverLifetime;
            velocityOverLifetime.enabled = false;

            gpuParticles.maxParticles = main.maxParticles;
            gpuParticles.emissionEnabled = true;
            gpuParticles.SetEmissionRateOverTime(emission.rateOverTime);
            gpuParticles.SetEmissionRateOverDistance(emission.rateOverDistance);
            gpuParticles.emissionDuration = main.duration;
            gpuParticles.emissionLooping = main.loop;
            gpuParticles.SetEmissionStartDelayRange(0f, 0f);
            gpuParticles.SetEmissionBursts(System.Array.Empty<ParticleSystem.Burst>());
            gpuParticles.SetStartLifetimeRange(3f, 5f);
            gpuParticles.SetStartSpeedRange(1f, 3f);
            gpuParticles.SetStartSizeRange(0.5f, 1.5f);
            gpuParticles.SetStartColorRange(Color.red, Color.blue, true);
            gpuParticles.SetGravityModifierRange(0f, 0f);
            gpuParticles.SetStartRotationRange(-Mathf.PI, Mathf.PI);
            gpuParticles.SetRotationOverLifetimeRange(-1f, 1f);
            if (profileRotationLUT != null) Destroy(profileRotationLUT);
            profileRotationLUT = CurveLUTBuilder.BuildIntegral(rotationOverLifetime.z);
            gpuParticles.rotationOverLifetimeIntegralLUT = profileRotationLUT;
            gpuParticles.simulationSpeed = 1f;
            gpuParticles.simulationSpace = SimulationSpace.Local;
            gpuParticles.colorOverLifetimeMode = ParticleSystemGradientMode.Gradient;
            gpuParticles.colorOverLifetimeLUT = GradientLUTBuilder.GetDefaultWhiteLUT();
            gpuParticles.sizeOverLifetimeLUT = CurveLUTBuilder.GetDefaultUnitLUT();
            gpuParticles.shapeType = ShapeTypeGPU.Point;
            gpuParticles.shapeEmitFrom = ShapeEmitFromGPU.Base;
            gpuParticles.alignToDirection = false;
            gpuParticles.shapeLocalPosition = Vector3.zero;
            gpuParticles.shapeLocalRotationEuler = Vector3.zero;
            gpuParticles.shapeLocalScale = Vector3.one;
            gpuParticles.forceOverLifetimeEnabled = false;
            gpuParticles.forceOverLifetimeLUT = MinMaxCurveVector3LUTBuilder.GetDefaultZeroLUT();
            gpuParticles.velocityOverLifetimeEnabled = false;
            gpuParticles.velocityOverLifetimeLUT =
                MinMaxCurveVector3LUTBuilder.GetDefaultZeroLUT();
        }

        void ConfigureEmissionBurstProfile()
        {
            ConfigureEmissionPointBase(2f, true);

            var main = shuriken.main;
            main.startDelay = 0.1f;
            gpuParticles.SetEmissionStartDelayRange(0.1f, 0.1f);

            var emission = shuriken.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[]
            {
                new ParticleSystem.Burst(0f, 5),
                new ParticleSystem.Burst(0.25f, 7, 7, 3, 0.5f),
                new ParticleSystem.Burst(1.75f, 4)
            });

            var bursts = new ParticleSystem.Burst[emission.burstCount];
            emission.GetBursts(bursts);
            gpuParticles.SetEmissionRateOverTime(emission.rateOverTime);
            gpuParticles.SetEmissionBursts(bursts);
        }

        void ConfigureEmissionRateCurveProfile()
        {
            ConfigureEmissionPointBase(2f, false);

            var emission = shuriken.emission;
            AnimationCurve rateCurve = AnimationCurve.Linear(0f, 10f, 1f, 30f);
            emission.rateOverTime = new ParticleSystem.MinMaxCurve(
                1f, rateCurve, rateCurve);
            emission.SetBursts(System.Array.Empty<ParticleSystem.Burst>());

            gpuParticles.SetEmissionRateOverTime(emission.rateOverTime);
            gpuParticles.SetEmissionBursts(System.Array.Empty<ParticleSystem.Burst>());
        }

        void ConfigureEmissionRateDistanceProfile()
        {
            ConfigureEmissionPointBase(5f, true);

            var main = shuriken.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            gpuParticles.simulationSpace = SimulationSpace.World;

            var emission = shuriken.emission;
            emission.rateOverTime = 0f;
            emission.rateOverDistance = 10f;
            emission.SetBursts(System.Array.Empty<ParticleSystem.Burst>());

            gpuParticles.SetEmissionRateOverTime(emission.rateOverTime);
            gpuParticles.SetEmissionRateOverDistance(emission.rateOverDistance);
            gpuParticles.SetEmissionBursts(System.Array.Empty<ParticleSystem.Burst>());
        }

        void ConfigureVelocityOverLifetimeProfile()
        {
            ConfigureEmissionPointBase(5f, true);

            var main = shuriken.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            gpuParticles.simulationSpace = SimulationSpace.World;

            var emission = shuriken.emission;
            emission.rateOverTime = 12f;
            emission.rateOverDistance = 0f;
            emission.SetBursts(System.Array.Empty<ParticleSystem.Burst>());
            gpuParticles.SetEmissionRateOverTime(emission.rateOverTime);
            gpuParticles.SetEmissionRateOverDistance(emission.rateOverDistance);
            gpuParticles.SetEmissionBursts(System.Array.Empty<ParticleSystem.Burst>());

            var velocity = shuriken.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(
                2f, AnimationCurve.Linear(0f, 0f, 1f, 1f));
            velocity.y = new ParticleSystem.MinMaxCurve(
                1f, AnimationCurve.Linear(0f, 0.5f, 1f, 0.5f));
            velocity.z = new ParticleSystem.MinMaxCurve(
                1f, AnimationCurve.Linear(0f, -0.25f, 1f, -0.25f));
            velocity.orbitalX = 0f;
            velocity.orbitalY = 0f;
            velocity.orbitalZ = 0f;
            velocity.orbitalOffsetX = 0f;
            velocity.orbitalOffsetY = 0f;
            velocity.orbitalOffsetZ = 0f;
            velocity.radial = 0f;
            velocity.speedModifier = 1f;

            if (profileVelocityLUT != null) Destroy(profileVelocityLUT);
            profileVelocityLUT = MinMaxCurveVector3LUTBuilder.Build(
                velocity.x, velocity.y, velocity.z);
            gpuParticles.velocityOverLifetimeEnabled = true;
            gpuParticles.velocityOverLifetimeSpace = SimulationSpace.World;
            gpuParticles.velocityOverLifetimeLUT = profileVelocityLUT;
        }

        void ConfigureRotationOverLifetimeProfile()
        {
            ConfigureEmissionPointBase(5f, true);

            var main = shuriken.main;
            main.startLifetime = 4f;
            main.startSize = 1.25f;
            main.startRotation = RotationProfileStartRotation;
            gpuParticles.SetStartLifetimeRange(4f, 4f);
            gpuParticles.SetStartSizeRange(1.25f, 1.25f);
            gpuParticles.SetStartRotationRange(
                RotationProfileStartRotation,
                RotationProfileStartRotation);

            var emission = shuriken.emission;
            emission.rateOverTime = 18f;
            emission.rateOverDistance = 0f;
            emission.SetBursts(Array.Empty<ParticleSystem.Burst>());
            gpuParticles.SetEmissionRateOverTime(emission.rateOverTime);
            gpuParticles.SetEmissionRateOverDistance(emission.rateOverDistance);
            gpuParticles.SetEmissionBursts(Array.Empty<ParticleSystem.Burst>());

            var rotation = shuriken.rotationOverLifetime;
            rotation.enabled = true;
            rotation.separateAxes = false;
            rotation.z = new ParticleSystem.MinMaxCurve(
                1f,
                AnimationCurve.Linear(0f, 0f, 1f, 2f));

            gpuParticles.SetRotationOverLifetimeRange(0f, 0f);
            if (profileRotationLUT != null) Destroy(profileRotationLUT);
            profileRotationLUT = CurveLUTBuilder.BuildIntegral(rotation.z);
            gpuParticles.rotationOverLifetimeIntegralLUT = profileRotationLUT;

            if (shurikenRenderer != null)
            {
                shurikenRenderer.pivot = new Vector3(0.35f, 0.15f, 0f);
            }
            gpuParticles.pivot = new Vector2(0.35f, 0.15f);
        }

        void ConfigureRotationBySpeedProfile()
        {
            ConfigureEmissionPointBase(5f, true);

            var main = shuriken.main;
            main.startLifetime = 4f;
            main.startSize = 1.25f;
            main.startRotation = RotationProfileStartRotation;
            gpuParticles.SetStartLifetimeRange(4f, 4f);
            gpuParticles.SetStartSizeRange(1.25f, 1.25f);
            gpuParticles.SetStartRotationRange(
                RotationProfileStartRotation,
                RotationProfileStartRotation);

            var emission = shuriken.emission;
            emission.rateOverTime = 18f;
            emission.rateOverDistance = 0f;
            emission.SetBursts(Array.Empty<ParticleSystem.Burst>());
            gpuParticles.SetEmissionRateOverTime(emission.rateOverTime);
            gpuParticles.SetEmissionRateOverDistance(emission.rateOverDistance);
            gpuParticles.SetEmissionBursts(Array.Empty<ParticleSystem.Burst>());

            var force = shuriken.forceOverLifetime;
            force.enabled = true;
            force.space = ParticleSystemSimulationSpace.Local;
            force.randomized = false;
            force.x = RotationBySpeedAcceleration.x;
            force.y = RotationBySpeedAcceleration.y;
            force.z = RotationBySpeedAcceleration.z;
            if (profileForceLUT != null) Destroy(profileForceLUT);
            profileForceLUT = MinMaxCurveVector3LUTBuilder.Build(
                force.x, force.y, force.z);
            gpuParticles.forceOverLifetimeEnabled = true;
            gpuParticles.forceOverLifetimeSpace = SimulationSpace.Local;
            gpuParticles.forceOverLifetimeRandomized = false;
            gpuParticles.forceOverLifetimeLUT = profileForceLUT;

            var rotationOverLifetime = shuriken.rotationOverLifetime;
            rotationOverLifetime.enabled = true;
            rotationOverLifetime.separateAxes = false;
            rotationOverLifetime.z = 0.5f;
            gpuParticles.SetRotationOverLifetimeRange(0.5f, 0.5f);
            if (profileRotationLUT != null) Destroy(profileRotationLUT);
            profileRotationLUT = CurveLUTBuilder.BuildIntegral(
                rotationOverLifetime.z);
            gpuParticles.rotationOverLifetimeIntegralLUT = profileRotationLUT;

            var rotationBySpeed = shuriken.rotationBySpeed;
            rotationBySpeed.enabled = true;
            rotationBySpeed.separateAxes = false;
            rotationBySpeed.range = new Vector2(0f, 4f);
            rotationBySpeed.z = new ParticleSystem.MinMaxCurve(
                4f,
                AnimationCurve.Linear(0f, 0f, 1f, 1f));
            gpuParticles.rotationBySpeedEnabled = true;
            gpuParticles.SetRotationBySpeedRange(rotationBySpeed.range);
            if (profileRotationBySpeedLUT != null)
            {
                Destroy(profileRotationBySpeedLUT);
            }
            profileRotationBySpeedLUT = CurveLUTBuilder.BuildSigned(
                rotationBySpeed.z,
                assetName: "RotationBySpeed_Profile_LUT");
            gpuParticles.rotationBySpeedLUT = profileRotationBySpeedLUT;

            if (shurikenRenderer != null)
            {
                shurikenRenderer.pivot = new Vector3(0.35f, 0.15f, 0f);
            }
            gpuParticles.pivot = new Vector2(0.35f, 0.15f);
        }

        void ConfigureColorSizeOverLifetimeProfile()
        {
            ConfigureEmissionPointBase(5f, true);

            var main = shuriken.main;
            main.startLifetime = 4f;
            gpuParticles.SetStartLifetimeRange(4f, 4f);

            var emission = shuriken.emission;
            emission.rateOverTime = 30f;
            emission.rateOverDistance = 0f;
            emission.SetBursts(Array.Empty<ParticleSystem.Burst>());
            gpuParticles.SetEmissionRateOverTime(emission.rateOverTime);
            gpuParticles.SetEmissionRateOverDistance(emission.rateOverDistance);
            gpuParticles.SetEmissionBursts(Array.Empty<ParticleSystem.Burst>());

            profileColorMinimumGradient = CreateGradient(
                new Color(0.2f, 0.1f, 0.3f, 0.8f),
                new Color(0.4f, 0.6f, 0.2f, 0.2f));
            profileColorMaximumGradient = CreateGradient(
                new Color(0.9f, 0.4f, 0.8f, 1f),
                new Color(0.8f, 1f, 0.5f, 0.6f));
            var colorOverLifetime = shuriken.colorOverLifetime;
            colorOverLifetime.enabled = true;
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(
                profileColorMinimumGradient,
                profileColorMaximumGradient);

            profileSizeMinimumCurve = AnimationCurve.Linear(0f, 0.5f, 1f, 1f);
            profileSizeMaximumCurve = AnimationCurve.Linear(0f, 1.5f, 1f, 2f);
            var sizeOverLifetime = shuriken.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.separateAxes = false;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(
                1f,
                profileSizeMinimumCurve,
                profileSizeMaximumCurve);

            if (profileColorLUT != null) Destroy(profileColorLUT);
            if (profileSizeLUT != null) Destroy(profileSizeLUT);
            profileColorLUT = GradientLUTBuilder.Build(colorOverLifetime.color);
            profileSizeLUT = CurveLUTBuilder.Build(sizeOverLifetime.size);
            gpuParticles.colorOverLifetimeMode = colorOverLifetime.color.mode;
            gpuParticles.colorOverLifetimeLUT = profileColorLUT;
            gpuParticles.sizeOverLifetimeLUT = profileSizeLUT;
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

        void ConfigureColorSizeBySpeedProfile()
        {
            ConfigureEmissionPointBase(5f, true);

            var main = shuriken.main;
            main.startLifetime = 4f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            gpuParticles.SetStartLifetimeRange(4f, 4f);
            gpuParticles.simulationSpace = SimulationSpace.World;

            var emission = shuriken.emission;
            emission.rateOverTime = 30f;
            emission.rateOverDistance = 0f;
            emission.SetBursts(Array.Empty<ParticleSystem.Burst>());
            gpuParticles.SetEmissionRateOverTime(emission.rateOverTime);
            gpuParticles.SetEmissionRateOverDistance(emission.rateOverDistance);
            gpuParticles.SetEmissionBursts(Array.Empty<ParticleSystem.Burst>());

            var force = shuriken.forceOverLifetime;
            force.enabled = true;
            force.space = ParticleSystemSimulationSpace.World;
            force.randomized = false;
            force.x = ValidationForce.x;
            force.y = ValidationForce.y;
            force.z = ValidationForce.z;
            if (profileForceLUT != null) Destroy(profileForceLUT);
            profileForceLUT = MinMaxCurveVector3LUTBuilder.Build(
                force.x, force.y, force.z);
            gpuParticles.forceOverLifetimeEnabled = true;
            gpuParticles.forceOverLifetimeSpace = SimulationSpace.World;
            gpuParticles.forceOverLifetimeRandomized = false;
            gpuParticles.forceOverLifetimeLUT = profileForceLUT;

            profileColorMinimumGradient = CreateGradient(
                new Color(0.2f, 0f, 0.3f, 0.8f),
                new Color(0.4f, 1f, 0.2f, 0.2f));
            profileColorMaximumGradient = CreateGradient(
                new Color(0.8f, 0f, 0.7f, 0.8f),
                new Color(1f, 1f, 0.6f, 0.2f));
            Vector2 speedRange = new Vector2(0f, ValidationForce.magnitude * 2f);
            var colorBySpeed = shuriken.colorBySpeed;
            colorBySpeed.enabled = true;
            colorBySpeed.range = speedRange;
            colorBySpeed.color = new ParticleSystem.MinMaxGradient(
                profileColorMinimumGradient,
                profileColorMaximumGradient);

            profileSizeMinimumCurve = AnimationCurve.Linear(0f, 0.5f, 1f, 1f);
            profileSizeMaximumCurve = AnimationCurve.Linear(0f, 1.5f, 1f, 2f);
            var sizeBySpeed = shuriken.sizeBySpeed;
            sizeBySpeed.enabled = true;
            sizeBySpeed.separateAxes = false;
            sizeBySpeed.range = speedRange;
            sizeBySpeed.size = new ParticleSystem.MinMaxCurve(
                1f,
                profileSizeMinimumCurve,
                profileSizeMaximumCurve);

            if (profileColorLUT != null) Destroy(profileColorLUT);
            if (profileSizeLUT != null) Destroy(profileSizeLUT);
            profileColorLUT = GradientLUTBuilder.Build(
                colorBySpeed.color,
                assetName: "ColorBySpeed_Profile_LUT");
            profileSizeLUT = CurveLUTBuilder.Build(
                sizeBySpeed.size,
                assetName: "SizeBySpeed_Profile_LUT");
            gpuParticles.colorBySpeedEnabled = true;
            gpuParticles.colorBySpeedMode = colorBySpeed.color.mode;
            gpuParticles.SetColorBySpeedRange(speedRange);
            gpuParticles.colorBySpeedLUT = profileColorLUT;
            gpuParticles.sizeBySpeedEnabled = true;
            gpuParticles.SetSizeBySpeedRange(speedRange);
            gpuParticles.sizeBySpeedLUT = profileSizeLUT;
        }

        void ResetBySpeedModules()
        {
            var colorBySpeed = shuriken.colorBySpeed;
            colorBySpeed.enabled = false;
            var sizeBySpeed = shuriken.sizeBySpeed;
            sizeBySpeed.enabled = false;

            gpuParticles.colorBySpeedEnabled = false;
            gpuParticles.colorBySpeedMode = ParticleSystemGradientMode.Gradient;
            gpuParticles.SetColorBySpeedRange(new Vector2(0f, 1f));
            gpuParticles.colorBySpeedLUT = GradientLUTBuilder.GetDefaultWhiteLUT();
            gpuParticles.sizeBySpeedEnabled = false;
            gpuParticles.SetSizeBySpeedRange(new Vector2(0f, 1f));
            gpuParticles.sizeBySpeedLUT = CurveLUTBuilder.GetDefaultUnitLUT();
            var rotationBySpeed = shuriken.rotationBySpeed;
            rotationBySpeed.enabled = false;
            gpuParticles.rotationBySpeedEnabled = false;
            gpuParticles.SetRotationBySpeedRange(new Vector2(0f, 1f));
            gpuParticles.rotationBySpeedLUT = CurveLUTBuilder.GetDefaultZeroLUT();
        }

        void ConfigureEmissionPointBase(float duration, bool looping)
        {
            shuriken.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = shuriken.main;
            main.maxParticles = 1000;
            main.duration = duration;
            main.loop = looping;
            main.startDelay = 0f;
            main.startLifetime = 5f;
            main.startSpeed = 0f;
            main.startSize3D = false;
            main.startSize = 1f;
            main.startColor = Color.white;
            main.startRotation3D = false;
            main.startRotation = 0f;
            main.gravityModifier = 0f;
            main.simulationSpeed = 1f;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;

            var emission = shuriken.emission;
            emission.enabled = true;
            emission.rateOverDistance = 0f;
            gpuParticles.SetEmissionRateOverDistance(emission.rateOverDistance);

            var shape = shuriken.shape;
            shape.enabled = false;
            var colorOverLifetime = shuriken.colorOverLifetime;
            colorOverLifetime.enabled = false;
            var sizeOverLifetime = shuriken.sizeOverLifetime;
            sizeOverLifetime.enabled = false;
            var rotationOverLifetime = shuriken.rotationOverLifetime;
            rotationOverLifetime.enabled = false;
            var force = shuriken.forceOverLifetime;
            force.enabled = false;
            var velocityOverLifetime = shuriken.velocityOverLifetime;
            velocityOverLifetime.enabled = false;

            gpuParticles.maxParticles = main.maxParticles;
            gpuParticles.emissionEnabled = true;
            gpuParticles.emissionDuration = main.duration;
            gpuParticles.emissionLooping = main.loop;
            gpuParticles.emissionRandomSeed = randomSeed == 0 ? 1u : randomSeed;
            gpuParticles.SetEmissionStartDelayRange(0f, 0f);
            gpuParticles.SetStartLifetimeRange(5f, 5f);
            gpuParticles.SetStartSpeedRange(0f, 0f);
            gpuParticles.SetStartSizeRange(1f, 1f);
            gpuParticles.SetStartColorRange(Color.white, Color.white, false);
            gpuParticles.SetGravityModifierRange(0f, 0f);
            gpuParticles.SetStartRotationRange(0f, 0f);
            gpuParticles.SetRotationOverLifetimeRange(0f, 0f);
            gpuParticles.rotationOverLifetimeIntegralLUT =
                CurveLUTBuilder.GetDefaultZeroLUT();
            gpuParticles.simulationSpeed = 1f;
            gpuParticles.simulationSpace = SimulationSpace.Local;
            gpuParticles.colorOverLifetimeMode = ParticleSystemGradientMode.Gradient;
            gpuParticles.colorOverLifetimeLUT = GradientLUTBuilder.GetDefaultWhiteLUT();
            gpuParticles.sizeOverLifetimeLUT = CurveLUTBuilder.GetDefaultUnitLUT();
            gpuParticles.shapeType = ShapeTypeGPU.Point;
            gpuParticles.shapeEmitFrom = ShapeEmitFromGPU.Base;
            gpuParticles.alignToDirection = false;
            gpuParticles.shapeLocalPosition = Vector3.zero;
            gpuParticles.shapeLocalRotationEuler = Vector3.zero;
            gpuParticles.shapeLocalScale = Vector3.one;
            gpuParticles.forceOverLifetimeEnabled = false;
            gpuParticles.forceOverLifetimeLUT = MinMaxCurveVector3LUTBuilder.GetDefaultZeroLUT();
            gpuParticles.velocityOverLifetimeEnabled = false;
            gpuParticles.velocityOverLifetimeLUT =
                MinMaxCurveVector3LUTBuilder.GetDefaultZeroLUT();
        }

        public void RestartPlayback()
        {
            Time.timeScale = 1f;
            MoveValidationEmitters(0f);

            if (shuriken != null)
            {
                shuriken.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                shuriken.Clear(true);
                shuriken.useAutoRandomSeed = false;
                shuriken.randomSeed = randomSeed == 0 ? 1u : randomSeed;
                var main = shuriken.main;
                main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
                shuriken.Play(true);
            }

            if (gpuParticles != null)
            {
                gpuParticles.ResetSimulation();
            }

            if (captureActive)
            {
                playbackFrame = 0;
                captureIndex = 1;
                nextCaptureFrame = CaptureFrameForIndex(captureIndex);
            }
        }

        void MoveValidationEmitters(float elapsed)
        {
            Vector3 offset = Vector3.right * Mathf.Max(0f, elapsed);
            if (shuriken != null)
            {
                shuriken.transform.position = shurikenBasePositionWS + offset;
            }
            if (gpuParticles != null)
            {
                gpuParticles.transform.position = gpuBasePositionWS + offset;
            }
        }

        public void SetDisplayMode(ParticleABDisplayMode mode)
        {
            displayMode = mode;
            ApplyDisplayMode();
        }

        public void BeginCapture()
        {
            if (captureActive) return;

            string root = Path.IsPathRooted(outputFolder)
                ? outputFolder
                : Path.GetFullPath(Path.Combine(Application.dataPath, "..", outputFolder));
            string sessionName = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
            sessionFolder = Path.Combine(root, sessionName);
            Directory.CreateDirectory(sessionFolder);

            metricsPath = Path.Combine(sessionFolder, "metrics.csv");
            File.WriteAllText(metricsPath,
                "capture,time,frame,shuriken_count,gpu_count,count_delta," +
                "shuriken_mean_x,shuriken_mean_y,shuriken_mean_z," +
                "gpu_mean_x,gpu_mean_y,gpu_mean_z," +
                "shuriken_mean_vel_x,shuriken_mean_vel_y,shuriken_mean_vel_z," +
                "gpu_mean_vel_x,gpu_mean_vel_y,gpu_mean_vel_z," +
                "shuriken_mean_speed,gpu_mean_speed," +
                "shuriken_mean_age,gpu_mean_age\n");

            EnsureCameraCaptureTarget();
            playbackFrame = 0;
            captureIndex = 1;
            nextCaptureFrame = CaptureFrameForIndex(captureIndex);
            finalCaptureFrame = Mathf.Max(1, Mathf.RoundToInt(captureDuration * fixedFrameRate));
            maximumCountDelta = 0;
            maximumMeanAgeError = 0f;
            maximumMeanSpeedError = 0f;
            maximumMeanVelocityError = 0f;
            maximumMeanPositionError = 0f;
            maximumShurikenConeError = 0f;
            maximumGPUConeError = 0f;
            maximumForceKinematicsError = 0f;
            maximumShurikenColorBoundsError = 0f;
            maximumGPUColorBoundsError = 0f;
            maximumShurikenSizeBoundsError = 0f;
            maximumGPUSizeBoundsError = 0f;
            maximumShurikenRotationError = 0f;
            maximumGPURotationError = 0f;
            maximumShurikenParticleCount = 0;
            maximumGPUParticleCount = 0;
            shurikenLifetimeRange.Reset();
            gpuLifetimeRange.Reset();
            shurikenSpeedRange.Reset();
            gpuSpeedRange.Reset();
            shurikenSizeRange.Reset();
            gpuSizeRange.Reset();
            shurikenColorRedRange.Reset();
            gpuColorRedRange.Reset();
            shurikenDistancePositionRange.Reset();
            gpuDistancePositionRange.Reset();
            shurikenLifetimeColorBlendRange.Reset();
            gpuLifetimeColorBlendRange.Reset();
            shurikenLifetimeSizeBlendRange.Reset();
            gpuLifetimeSizeBlendRange.Reset();
            shurikenSpeedColorBlendRange.Reset();
            gpuSpeedColorBlendRange.Reset();
            shurikenSpeedSizeBlendRange.Reset();
            gpuSpeedSizeBlendRange.Reset();
            captureActive = true;

            Debug.Log($"Particle A/B RT capture started: {sessionFolder}", this);
        }

        int CaptureFrameForIndex(int index)
        {
            return Mathf.Max(1, Mathf.RoundToInt(index * fixedFrameRate / Mathf.Max(0.1f, captureFrequency)));
        }

        void CaptureSnapshot(float elapsed)
        {
            if (captureCamera == null || gpuParticles == null) return;

            EnsureCameraCaptureTarget();
            string prefix = $"{captureIndex:D4}-t{elapsed:F3}";
            ParticleABDisplayMode previousMode = displayMode;
            CaptureCameraImage(prefix + "-both.png", ParticleABDisplayMode.Both);
            CaptureCameraImage(prefix + "-shuriken.png", ParticleABDisplayMode.ShurikenOnly);
            CaptureCameraImage(prefix + "-gpu.png", ParticleABDisplayMode.GPUOnly);
            SetDisplayMode(previousMode);

            Texture2D posLife = ReadRenderTexture(gpuParticles.CurrentPositionLifetimeTexture, TextureFormat.RGBAFloat, true);
            Texture2D velSize = ReadRenderTexture(gpuParticles.CurrentVelocitySizeTexture, TextureFormat.RGBAFloat, true);
            Texture2D colors = ReadRenderTexture(gpuParticles.CurrentColorTexture, TextureFormat.RGBAFloat, true);
            Texture2D rotationPhases = ReadRenderTexture(
                gpuParticles.CurrentRotationPhaseTexture,
                TextureFormat.RGBAFloat,
                true);
            File.WriteAllBytes(Path.Combine(sessionFolder, prefix + "-gpu-poslife.exr"),
                posLife.EncodeToEXR(Texture2D.EXRFlags.OutputAsFloat));
            File.WriteAllBytes(Path.Combine(sessionFolder, prefix + "-gpu-velsize.exr"),
                velSize.EncodeToEXR(Texture2D.EXRFlags.OutputAsFloat));
            File.WriteAllBytes(Path.Combine(sessionFolder, prefix + "-gpu-color.exr"),
                colors.EncodeToEXR(Texture2D.EXRFlags.OutputAsFloat));
            File.WriteAllBytes(Path.Combine(sessionFolder, prefix + "-gpu-rotation.exr"),
                rotationPhases.EncodeToEXR(Texture2D.EXRFlags.OutputAsFloat));

            AppendMetrics(
                elapsed,
                posLife.GetPixels(),
                velSize.GetPixels(),
                colors.GetPixels(),
                rotationPhases.GetPixels());
            WriteParticleState(prefix, posLife.GetPixels(), velSize.GetPixels());
            Destroy(posLife);
            Destroy(velSize);
            Destroy(colors);
            Destroy(rotationPhases);
        }

        void CaptureCameraImage(string fileName, ParticleABDisplayMode mode)
        {
            SetDisplayMode(mode);
            RenderTexture previousTarget = captureCamera.targetTexture;
            captureCamera.targetTexture = cameraCaptureRT;
            captureCamera.Render();
            captureCamera.targetTexture = previousTarget;

            Texture2D cameraImage = ReadRenderTexture(cameraCaptureRT, TextureFormat.RGBA32, false);
            File.WriteAllBytes(Path.Combine(sessionFolder, fileName), cameraImage.EncodeToPNG());
            Destroy(cameraImage);
        }

        void WriteParticleState(string prefix, Color[] gpuPosLife, Color[] gpuVelSize)
        {
            var state = new StringBuilder(4096);
            state.Append("system,index,pos_x,pos_y,pos_z,vel_x,vel_y,vel_z,age,lifetime\n");

            if (shuriken != null)
            {
                int required = Mathf.Max(1, shuriken.main.maxParticles);
                if (shurikenParticles == null || shurikenParticles.Length < required)
                {
                    shurikenParticles = new ParticleSystem.Particle[required];
                }

                int count = shuriken.GetParticles(shurikenParticles);
                for (int i = 0; i < count; i++)
                {
                    ParticleSystem.Particle particle = shurikenParticles[i];
                    AppendStateRow(state, "shuriken", i, particle.position, particle.totalVelocity,
                        particle.startLifetime - particle.remainingLifetime, particle.remainingLifetime);
                }
            }

            int pixelCount = Mathf.Min(gpuPosLife.Length, gpuVelSize.Length);
            for (int i = 0; i < pixelCount; i++)
            {
                Color positionLife = gpuPosLife[i];
                if (positionLife.a <= 0f) continue;

                Color velocitySize = gpuVelSize[i];
                AppendStateRow(state, "gpu", i,
                    new Vector3(positionLife.r, positionLife.g, positionLife.b),
                    new Vector3(velocitySize.r, velocitySize.g, velocitySize.b),
                    Mathf.Max(0f, gpuParticles.ResolveStartLifetime(i) - positionLife.a), positionLife.a);
            }

            File.WriteAllText(Path.Combine(sessionFolder, prefix + "-state.csv"), state.ToString());
        }

        static void AppendStateRow(StringBuilder state, string systemName, int index, Vector3 position,
            Vector3 velocity, float age, float remainingLifetime)
        {
            state.Append(systemName).Append(',');
            Append(state, index);
            Append(state, position.x);
            Append(state, position.y);
            Append(state, position.z);
            Append(state, velocity.x);
            Append(state, velocity.y);
            Append(state, velocity.z);
            Append(state, age);
            state.Append(remainingLifetime.ToString("R", CultureInfo.InvariantCulture)).Append('\n');
        }

        void AppendMetrics(
            float elapsed,
            Color[] gpuPosLife,
            Color[] gpuVelSize,
            Color[] gpuColors,
            Color[] gpuRotationPhases)
        {
            int shurikenCount = 0;
            Vector3 shurikenPositionSum = Vector3.zero;
            Vector3 shurikenVelocitySum = Vector3.zero;
            float shurikenSpeedSum = 0f;
            float shurikenAgeSum = 0f;

            if (shuriken != null)
            {
                int required = Mathf.Max(1, shuriken.main.maxParticles);
                if (shurikenParticles == null || shurikenParticles.Length < required)
                {
                    shurikenParticles = new ParticleSystem.Particle[required];
                }

                shurikenCount = shuriken.GetParticles(shurikenParticles);
                for (int i = 0; i < shurikenCount; i++)
                {
                    ParticleSystem.Particle particle = shurikenParticles[i];
                    Vector3 shurikenVelocity = particle.totalVelocity;
                    shurikenPositionSum += particle.position;
                    shurikenVelocitySum += shurikenVelocity;
                    shurikenSpeedSum += shurikenVelocity.magnitude;
                    float age = particle.startLifetime - particle.remainingLifetime;
                    shurikenAgeSum += age;
                    if (validationProfile == ParticleABValidationProfile.RandomizedMainPoint)
                    {
                        Color startColor = particle.startColor;
                        shurikenLifetimeRange.Observe(particle.startLifetime);
                        shurikenSpeedRange.Observe(shurikenVelocity.magnitude);
                        shurikenSizeRange.Observe(particle.startSize);
                        shurikenColorRedRange.Observe(startColor.r);
                    }
                    if (validationProfile == ParticleABValidationProfile.BaselineCone)
                    {
                        maximumShurikenConeError = Mathf.Max(maximumShurikenConeError,
                            ConeRelationError(particle.position, shurikenVelocity, age));
                    }
                    else if (validationProfile == ParticleABValidationProfile.ForceOverLifetimePoint)
                    {
                        maximumForceKinematicsError = Mathf.Max(maximumForceKinematicsError,
                            (shurikenVelocity - ValidationForce * age).magnitude);
                    }
                    else if (validationProfile ==
                             ParticleABValidationProfile.RotationOverLifetimeCurvePoint)
                    {
                        float expectedRotation = RotationProfileExpectedRadians(
                            particle.startLifetime, age);
                        float rotationError = Mathf.Abs(Mathf.DeltaAngle(
                            expectedRotation * Mathf.Rad2Deg,
                            particle.rotation)) * Mathf.Deg2Rad;
                        maximumShurikenRotationError = Mathf.Max(
                            maximumShurikenRotationError, rotationError);
                    }
                    else if (validationProfile ==
                             ParticleABValidationProfile.RotationBySpeedCurvePoint)
                    {
                        float expectedRotation = RotationBySpeedProfileExpectedRadians(age);
                        float rotationError = Mathf.Abs(Mathf.DeltaAngle(
                            expectedRotation * Mathf.Rad2Deg,
                            particle.rotation)) * Mathf.Deg2Rad;
                        maximumShurikenRotationError = Mathf.Max(
                            maximumShurikenRotationError, rotationError);
                        maximumForceKinematicsError = Mathf.Max(
                            maximumForceKinematicsError,
                            (shurikenVelocity - RotationBySpeedAcceleration * age).magnitude);
                    }
                    else if (validationProfile ==
                             ParticleABValidationProfile.EmissionRateDistancePoint)
                    {
                        shurikenDistancePositionRange.Observe(
                            particle.position.x - shurikenBasePositionWS.x);
                    }
                    else if (validationProfile ==
                             ParticleABValidationProfile.ColorSizeOverLifetimeRandomizedPoint)
                    {
                        float normalizedAge = Mathf.Clamp01(age / particle.startLifetime);
                        Color minimumColor = profileColorMinimumGradient.Evaluate(normalizedAge);
                        Color maximumColor = profileColorMaximumGradient.Evaluate(normalizedAge);
                        Color currentColor = particle.GetCurrentColor(shuriken);
                        maximumShurikenColorBoundsError = Mathf.Max(
                            maximumShurikenColorBoundsError,
                            ColorBoundsError(currentColor, minimumColor, maximumColor));
                        ObserveBlendFactor(
                            currentColor.r,
                            minimumColor.r,
                            maximumColor.r,
                            ref shurikenLifetimeColorBlendRange);

                        float minimumSize = profileSizeMinimumCurve.Evaluate(normalizedAge);
                        float maximumSize = profileSizeMaximumCurve.Evaluate(normalizedAge);
                        float currentSize = particle.GetCurrentSize(shuriken);
                        maximumShurikenSizeBoundsError = Mathf.Max(
                            maximumShurikenSizeBoundsError,
                            RangeViolation(currentSize, minimumSize, maximumSize));
                        ObserveBlendFactor(
                            currentSize,
                            minimumSize,
                            maximumSize,
                            ref shurikenLifetimeSizeBlendRange);
                    }
                    else if (validationProfile ==
                             ParticleABValidationProfile.ColorSizeBySpeedRandomizedPoint)
                    {
                        float speed = shurikenVelocity.magnitude;
                        float speedPosition = Mathf.InverseLerp(
                            gpuParticles.colorBySpeedRange.x,
                            gpuParticles.colorBySpeedRange.y,
                            speed);
                        Color minimumColor = profileColorMinimumGradient.Evaluate(speedPosition);
                        Color maximumColor = profileColorMaximumGradient.Evaluate(speedPosition);
                        Color currentColor = particle.GetCurrentColor(shuriken);
                        maximumShurikenColorBoundsError = Mathf.Max(
                            maximumShurikenColorBoundsError,
                            ColorBoundsError(currentColor, minimumColor, maximumColor));
                        ObserveBlendFactor(
                            currentColor.r,
                            minimumColor.r,
                            maximumColor.r,
                            ref shurikenSpeedColorBlendRange);

                        float minimumSize = profileSizeMinimumCurve.Evaluate(speedPosition);
                        float maximumSize = profileSizeMaximumCurve.Evaluate(speedPosition);
                        float currentSize = particle.GetCurrentSize(shuriken);
                        maximumShurikenSizeBoundsError = Mathf.Max(
                            maximumShurikenSizeBoundsError,
                            RangeViolation(currentSize, minimumSize, maximumSize));
                        ObserveBlendFactor(
                            currentSize,
                            minimumSize,
                            maximumSize,
                            ref shurikenSpeedSizeBlendRange);
                        maximumForceKinematicsError = Mathf.Max(
                            maximumForceKinematicsError,
                            (shurikenVelocity - ValidationForce * age).magnitude);
                    }
                }
            }

            int gpuCount = 0;
            Vector3 gpuPositionSum = Vector3.zero;
            Vector3 gpuVelocitySum = Vector3.zero;
            float gpuSpeedSum = 0f;
            float gpuAgeSum = 0f;
            int pixelCount = Mathf.Min(
                Mathf.Min(
                    Mathf.Min(gpuPosLife.Length, gpuVelSize.Length),
                    gpuColors.Length),
                gpuRotationPhases.Length);
            for (int i = 0; i < pixelCount; i++)
            {
                Color positionLife = gpuPosLife[i];
                if (positionLife.a <= 0f) continue;

                Color velocitySize = gpuVelSize[i];
                gpuCount++;
                gpuPositionSum += new Vector3(positionLife.r, positionLife.g, positionLife.b);
                Vector3 gpuVelocity = new Vector3(velocitySize.r, velocitySize.g, velocitySize.b);
                gpuVelocitySum += gpuVelocity;
                gpuSpeedSum += gpuVelocity.magnitude;
                float particleStartLifetime = gpuParticles.ResolveStartLifetime(i);
                float age = Mathf.Max(0f, particleStartLifetime - positionLife.a);
                gpuAgeSum += age;
                if (validationProfile == ParticleABValidationProfile.RandomizedMainPoint)
                {
                    gpuLifetimeRange.Observe(particleStartLifetime);
                    gpuSpeedRange.Observe(gpuVelocity.magnitude);
                    gpuSizeRange.Observe(velocitySize.a);
                    gpuColorRedRange.Observe(gpuColors[i].r);
                }
                if (validationProfile == ParticleABValidationProfile.BaselineCone)
                {
                    maximumGPUConeError = Mathf.Max(maximumGPUConeError,
                        ConeRelationError(
                            new Vector3(positionLife.r, positionLife.g, positionLife.b),
                            gpuVelocity, age));
                }
                else if (validationProfile == ParticleABValidationProfile.ForceOverLifetimePoint)
                {
                    maximumForceKinematicsError = Mathf.Max(maximumForceKinematicsError,
                        (gpuVelocity - ValidationForce * age).magnitude);
                }
                else if (validationProfile ==
                         ParticleABValidationProfile.RotationOverLifetimeCurvePoint)
                {
                    float expectedRotation = RotationProfileExpectedRadians(
                        particleStartLifetime, age);
                    float actualRotation = gpuParticles.ResolveParticleRotationRadians(
                        i, positionLife.a, gpuRotationPhases[i].r);
                    float rotationError = Mathf.Abs(Mathf.DeltaAngle(
                        expectedRotation * Mathf.Rad2Deg,
                        actualRotation * Mathf.Rad2Deg)) * Mathf.Deg2Rad;
                    maximumGPURotationError = Mathf.Max(
                        maximumGPURotationError, rotationError);
                }
                else if (validationProfile ==
                         ParticleABValidationProfile.RotationBySpeedCurvePoint)
                {
                    float expectedRotation = RotationBySpeedProfileExpectedRadians(age);
                    float actualRotation = gpuParticles.ResolveParticleRotationRadians(
                        i, positionLife.a, gpuRotationPhases[i].r);
                    float rotationError = Mathf.Abs(Mathf.DeltaAngle(
                        expectedRotation * Mathf.Rad2Deg,
                        actualRotation * Mathf.Rad2Deg)) * Mathf.Deg2Rad;
                    maximumGPURotationError = Mathf.Max(
                        maximumGPURotationError, rotationError);
                    maximumForceKinematicsError = Mathf.Max(
                        maximumForceKinematicsError,
                        (gpuVelocity - RotationBySpeedAcceleration * age).magnitude);
                }
                else if (validationProfile ==
                         ParticleABValidationProfile.EmissionRateDistancePoint)
                {
                    gpuDistancePositionRange.Observe(
                        positionLife.r - gpuBasePositionWS.x);
                }
                else if (validationProfile ==
                         ParticleABValidationProfile.ColorSizeOverLifetimeRandomizedPoint)
                {
                    float normalizedAge = particleStartLifetime > 0f
                        ? Mathf.Clamp01(age / particleStartLifetime)
                        : 0f;
                    Color minimumColor = profileColorMinimumGradient.Evaluate(normalizedAge);
                    Color maximumColor = profileColorMaximumGradient.Evaluate(normalizedAge);
                    Color currentColor = gpuColors[i];
                    maximumGPUColorBoundsError = Mathf.Max(
                        maximumGPUColorBoundsError,
                        ColorBoundsError(currentColor, minimumColor, maximumColor));
                    ObserveBlendFactor(
                        currentColor.r,
                        minimumColor.r,
                        maximumColor.r,
                        ref gpuLifetimeColorBlendRange);

                    float minimumSize = profileSizeMinimumCurve.Evaluate(normalizedAge);
                    float maximumSize = profileSizeMaximumCurve.Evaluate(normalizedAge);
                    maximumGPUSizeBoundsError = Mathf.Max(
                        maximumGPUSizeBoundsError,
                        RangeViolation(velocitySize.a, minimumSize, maximumSize));
                    ObserveBlendFactor(
                        velocitySize.a,
                        minimumSize,
                        maximumSize,
                        ref gpuLifetimeSizeBlendRange);
                }
                else if (validationProfile ==
                         ParticleABValidationProfile.ColorSizeBySpeedRandomizedPoint)
                {
                    float speed = gpuVelocity.magnitude;
                    float speedPosition = Mathf.InverseLerp(
                        gpuParticles.colorBySpeedRange.x,
                        gpuParticles.colorBySpeedRange.y,
                        speed);
                    Color minimumColor = profileColorMinimumGradient.Evaluate(speedPosition);
                    Color maximumColor = profileColorMaximumGradient.Evaluate(speedPosition);
                    Color currentColor = gpuColors[i];
                    maximumGPUColorBoundsError = Mathf.Max(
                        maximumGPUColorBoundsError,
                        ColorBoundsError(currentColor, minimumColor, maximumColor));
                    ObserveBlendFactor(
                        currentColor.r,
                        minimumColor.r,
                        maximumColor.r,
                        ref gpuSpeedColorBlendRange);

                    float minimumSize = profileSizeMinimumCurve.Evaluate(speedPosition);
                    float maximumSize = profileSizeMaximumCurve.Evaluate(speedPosition);
                    maximumGPUSizeBoundsError = Mathf.Max(
                        maximumGPUSizeBoundsError,
                        RangeViolation(velocitySize.a, minimumSize, maximumSize));
                    ObserveBlendFactor(
                        velocitySize.a,
                        minimumSize,
                        maximumSize,
                        ref gpuSpeedSizeBlendRange);
                    maximumForceKinematicsError = Mathf.Max(
                        maximumForceKinematicsError,
                        (gpuVelocity - ValidationForce * age).magnitude);
                }
            }

            Vector3 shurikenMean = shurikenCount > 0 ? shurikenPositionSum / shurikenCount : Vector3.zero;
            Vector3 gpuMean = gpuCount > 0 ? gpuPositionSum / gpuCount : Vector3.zero;
            Vector3 shurikenMeanVelocity = shurikenCount > 0
                ? shurikenVelocitySum / shurikenCount
                : Vector3.zero;
            Vector3 gpuMeanVelocity = gpuCount > 0 ? gpuVelocitySum / gpuCount : Vector3.zero;
            float shurikenMeanSpeed = shurikenCount > 0 ? shurikenSpeedSum / shurikenCount : 0f;
            float gpuMeanSpeed = gpuCount > 0 ? gpuSpeedSum / gpuCount : 0f;
            float shurikenMeanAge = shurikenCount > 0 ? shurikenAgeSum / shurikenCount : 0f;
            float gpuMeanAge = gpuCount > 0 ? gpuAgeSum / gpuCount : 0f;

            maximumCountDelta = Mathf.Max(maximumCountDelta, Mathf.Abs(gpuCount - shurikenCount));
            maximumShurikenParticleCount = Mathf.Max(maximumShurikenParticleCount, shurikenCount);
            maximumGPUParticleCount = Mathf.Max(maximumGPUParticleCount, gpuCount);
            maximumMeanAgeError = Mathf.Max(maximumMeanAgeError, Mathf.Abs(gpuMeanAge - shurikenMeanAge));
            maximumMeanSpeedError = Mathf.Max(maximumMeanSpeedError,
                Mathf.Abs(gpuMeanSpeed - shurikenMeanSpeed));
            maximumMeanVelocityError = Mathf.Max(maximumMeanVelocityError,
                (gpuMeanVelocity - shurikenMeanVelocity).magnitude);
            if ((validationProfile == ParticleABValidationProfile.EmissionRateDistancePoint ||
                 validationProfile == ParticleABValidationProfile.VelocityOverLifetimePoint) &&
                shurikenCount > 0 && gpuCount > 0)
            {
                Vector3 shurikenMeanDisplacement =
                    shurikenMean - shurikenBasePositionWS;
                Vector3 gpuMeanDisplacement = gpuMean - gpuBasePositionWS;
                maximumMeanPositionError = Mathf.Max(
                    maximumMeanPositionError,
                    (gpuMeanDisplacement - shurikenMeanDisplacement).magnitude);
            }

            var line = new StringBuilder(256);
            Append(line, captureIndex);
            Append(line, elapsed);
            Append(line, Time.frameCount);
            Append(line, shurikenCount);
            Append(line, gpuCount);
            Append(line, gpuCount - shurikenCount);
            Append(line, shurikenMean.x);
            Append(line, shurikenMean.y);
            Append(line, shurikenMean.z);
            Append(line, gpuMean.x);
            Append(line, gpuMean.y);
            Append(line, gpuMean.z);
            Append(line, shurikenMeanVelocity.x);
            Append(line, shurikenMeanVelocity.y);
            Append(line, shurikenMeanVelocity.z);
            Append(line, gpuMeanVelocity.x);
            Append(line, gpuMeanVelocity.y);
            Append(line, gpuMeanVelocity.z);
            Append(line, shurikenMeanSpeed);
            Append(line, gpuMeanSpeed);
            Append(line, shurikenMeanAge);
            line.Append(gpuMeanAge.ToString("R", CultureInfo.InvariantCulture));
            line.Append('\n');
            File.AppendAllText(metricsPath, line.ToString());
        }

        static float ColorBoundsError(Color value, Color minimum, Color maximum)
        {
            return Mathf.Max(
                Mathf.Max(
                    RangeViolation(value.r, minimum.r, maximum.r),
                    RangeViolation(value.g, minimum.g, maximum.g)),
                Mathf.Max(
                    RangeViolation(value.b, minimum.b, maximum.b),
                    RangeViolation(value.a, minimum.a, maximum.a)));
        }

        static float RangeViolation(float value, float minimum, float maximum)
        {
            float lower = Mathf.Min(minimum, maximum);
            float upper = Mathf.Max(minimum, maximum);
            if (value < lower) return lower - value;
            if (value > upper) return value - upper;
            return 0f;
        }

        static float RotationProfileExpectedRadians(float startLifetime, float age)
        {
            float normalizedAge = startLifetime > 1e-6f
                ? Mathf.Clamp01(age / startLifetime)
                : 0f;
            // The validation angular velocity curve is omega(u) = 2u radians/second.
            return RotationProfileStartRotation +
                   startLifetime * normalizedAge * normalizedAge;
        }

        static float RotationBySpeedProfileExpectedRadians(float age)
        {
            age = Mathf.Max(0f, age);
            float rotationBySpeedPhase = age <= 2f
                ? age * age
                : 4f * age - 4f;
            return RotationProfileStartRotation + 0.5f * age +
                   rotationBySpeedPhase;
        }

        static void ObserveBlendFactor(
            float value,
            float minimum,
            float maximum,
            ref ObservedRange range)
        {
            float denominator = maximum - minimum;
            if (Mathf.Abs(denominator) <= 1e-6f) return;
            range.Observe((value - minimum) / denominator);
        }

        float ConeRelationError(Vector3 position, Vector3 velocity, float age)
        {
            float radius = Mathf.Max(1e-6f, gpuParticles.shapeConeRadius);
            float tanAngle = Mathf.Tan(gpuParticles.shapeConeAngle * Mathf.Deg2Rad);
            if (tanAngle <= 1e-6f || Mathf.Abs(velocity.z) <= 1e-6f) return 0f;

            Vector3 spawnPosition = position - velocity * age;
            float positionRatio = new Vector2(spawnPosition.x, spawnPosition.y).magnitude / radius;
            float velocityRatio = new Vector2(velocity.x, velocity.y).magnitude /
                                  Mathf.Abs(velocity.z) / tanAngle;
            return Mathf.Abs(positionRatio - velocityRatio);
        }

        static void Append(StringBuilder line, int value)
        {
            line.Append(value.ToString(CultureInfo.InvariantCulture));
            line.Append(',');
        }

        static void Append(StringBuilder line, float value)
        {
            line.Append(value.ToString("R", CultureInfo.InvariantCulture));
            line.Append(',');
        }

        static Texture2D ReadRenderTexture(RenderTexture source, TextureFormat format, bool linear)
        {
            if (source == null)
            {
                throw new InvalidOperationException("Particle A/B capture source RT is not initialised.");
            }

            var texture = new Texture2D(source.width, source.height, format, false, linear);
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = source;
            texture.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0, false);
            texture.Apply(false, false);
            RenderTexture.active = previous;
            return texture;
        }

        void EnsureCameraCaptureTarget()
        {
            if (cameraCaptureRT != null &&
                cameraCaptureRT.width == captureWidth &&
                cameraCaptureRT.height == captureHeight)
            {
                return;
            }

            ReleaseCameraCaptureTarget();
            cameraCaptureRT = new RenderTexture(captureWidth, captureHeight, 24, RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.sRGB)
            {
                name = "ParticleAB_CameraCapture",
                antiAliasing = 1,
                useMipMap = false
            };
            cameraCaptureRT.Create();
        }

        void ReleaseCameraCaptureTarget()
        {
            if (cameraCaptureRT == null) return;
            cameraCaptureRT.Release();
            Destroy(cameraCaptureRT);
            cameraCaptureRT = null;
        }

        void CompleteCapture()
        {
            captureActive = false;
            bool profileSpecificPassed;
            switch (validationProfile)
            {
                case ParticleABValidationProfile.BaselineCone:
                    profileSpecificPassed = maximumMeanSpeedError <= 0.001f &&
                                            maximumShurikenConeError <= 0.001f &&
                                            maximumGPUConeError <= 0.001f;
                    break;

                case ParticleABValidationProfile.ForceOverLifetimePoint:
                    profileSpecificPassed = maximumMeanSpeedError <= 0.001f &&
                                            maximumMeanVelocityError <= 0.001f &&
                                            maximumForceKinematicsError <= 0.005f;
                    break;

                case ParticleABValidationProfile.RandomizedMainPoint:
                    profileSpecificPassed =
                        shurikenLifetimeRange.Covers(3f, 5f) &&
                        gpuLifetimeRange.Covers(3f, 5f) &&
                        shurikenSpeedRange.Covers(1f, 3f) &&
                        gpuSpeedRange.Covers(1f, 3f) &&
                        shurikenSizeRange.Covers(0.5f, 1.5f) &&
                        gpuSizeRange.Covers(0.5f, 1.5f) &&
                        shurikenColorRedRange.Covers(0f, 1f) &&
                        gpuColorRedRange.Covers(0f, 1f);
                    break;

                case ParticleABValidationProfile.EmissionBurstPoint:
                    profileSpecificPassed = maximumMeanSpeedError <= 0.001f &&
                                            maximumShurikenParticleCount == 42 &&
                                            maximumGPUParticleCount == 42;
                    break;

                case ParticleABValidationProfile.EmissionRateCurvePoint:
                    profileSpecificPassed = maximumMeanSpeedError <= 0.001f &&
                                            maximumShurikenParticleCount == 39 &&
                                            maximumGPUParticleCount == 39;
                    break;

                case ParticleABValidationProfile.EmissionRateDistancePoint:
                    profileSpecificPassed = maximumMeanSpeedError <= 0.001f &&
                                            maximumMeanPositionError <= 0.03f &&
                                            maximumShurikenParticleCount >= 15 &&
                                            maximumGPUParticleCount >= 15 &&
                                            shurikenDistancePositionRange.Covers(
                                                0f, captureDuration + 0.1f) &&
                                            gpuDistancePositionRange.Covers(
                                                0f, captureDuration + 0.1f);
                    break;

                case ParticleABValidationProfile.VelocityOverLifetimePoint:
                    profileSpecificPassed = maximumMeanVelocityError <= 0.02f &&
                                            maximumMeanPositionError <= 0.03f &&
                                            maximumShurikenParticleCount > 0 &&
                                            maximumGPUParticleCount > 0;
                    break;

                case ParticleABValidationProfile.RotationOverLifetimeCurvePoint:
                    profileSpecificPassed = maximumMeanSpeedError <= 0.001f &&
                                            maximumShurikenParticleCount > 0 &&
                                            maximumGPUParticleCount > 0 &&
                                            maximumShurikenRotationError <= 0.05f &&
                                            maximumGPURotationError <= 0.01f;
                    break;

                case ParticleABValidationProfile.RotationBySpeedCurvePoint:
                    profileSpecificPassed = maximumMeanSpeedError <= 0.001f &&
                                            maximumMeanVelocityError <= 0.001f &&
                                            maximumForceKinematicsError <= 0.005f &&
                                            maximumShurikenParticleCount > 0 &&
                                            maximumGPUParticleCount > 0 &&
                                            maximumShurikenRotationError <= 0.08f &&
                                            maximumGPURotationError <= 0.02f;
                    break;

                case ParticleABValidationProfile.ColorSizeOverLifetimeRandomizedPoint:
                    profileSpecificPassed = maximumMeanSpeedError <= 0.001f &&
                                            maximumShurikenParticleCount > 0 &&
                                            maximumGPUParticleCount > 0 &&
                                            maximumShurikenColorBoundsError <= 0.015f &&
                                            maximumGPUColorBoundsError <= 0.015f &&
                                            maximumShurikenSizeBoundsError <= 0.01f &&
                                            maximumGPUSizeBoundsError <= 0.01f &&
                                            shurikenLifetimeColorBlendRange.Covers(0f, 1f) &&
                                            gpuLifetimeColorBlendRange.Covers(0f, 1f) &&
                                            shurikenLifetimeSizeBlendRange.Covers(0f, 1f) &&
                                            gpuLifetimeSizeBlendRange.Covers(0f, 1f);
                    break;

                case ParticleABValidationProfile.ColorSizeBySpeedRandomizedPoint:
                    profileSpecificPassed = maximumMeanSpeedError <= 0.001f &&
                                            maximumMeanVelocityError <= 0.001f &&
                                            maximumForceKinematicsError <= 0.005f &&
                                            maximumShurikenParticleCount > 0 &&
                                            maximumGPUParticleCount > 0 &&
                                            maximumShurikenColorBoundsError <= 0.005f &&
                                            maximumGPUColorBoundsError <= 0.001f &&
                                            maximumShurikenSizeBoundsError <= 0.01f &&
                                            maximumGPUSizeBoundsError <= 0.01f &&
                                            shurikenSpeedColorBlendRange.Covers(0f, 1f) &&
                                            gpuSpeedColorBlendRange.Covers(0f, 1f) &&
                                            shurikenSpeedSizeBlendRange.Covers(0f, 1f) &&
                                            gpuSpeedSizeBlendRange.Covers(0f, 1f);
                    break;

                default:
                    profileSpecificPassed = false;
                    break;
            }

            bool passed = maximumCountDelta == 0 &&
                          maximumMeanAgeError <= 0.001f &&
                          profileSpecificPassed;
            string result = passed ? "PASS" : "FAIL";
            Debug.Log(
                $"PARTICLE_AB_CAPTURE_RESULT:{result}; " +
                $"profile={validationProfile}; " +
                $"maxCountDelta={maximumCountDelta}; " +
                $"maxMeanAgeError={maximumMeanAgeError:R}; " +
                $"maxMeanSpeedError={maximumMeanSpeedError:R}; " +
                $"maxMeanVelocityError={maximumMeanVelocityError:R}; " +
                $"maxMeanPositionError={maximumMeanPositionError:R}; " +
                $"maxShurikenConeError={maximumShurikenConeError:R}; " +
                $"maxGPUConeError={maximumGPUConeError:R}; " +
                $"maxForceKinematicsError={maximumForceKinematicsError:R}; " +
                $"maxShurikenColorBoundsError={maximumShurikenColorBoundsError:R}; " +
                $"maxGPUColorBoundsError={maximumGPUColorBoundsError:R}; " +
                $"maxShurikenSizeBoundsError={maximumShurikenSizeBoundsError:R}; " +
                $"maxGPUSizeBoundsError={maximumGPUSizeBoundsError:R}; " +
                $"maxShurikenRotationError={maximumShurikenRotationError:R}; " +
                $"maxGPURotationError={maximumGPURotationError:R}; " +
                $"maxShurikenCount={maximumShurikenParticleCount}; " +
                $"maxGPUCount={maximumGPUParticleCount}; " +
                $"shurikenLifetimeRange={FormatRange(shurikenLifetimeRange)}; " +
                $"gpuLifetimeRange={FormatRange(gpuLifetimeRange)}; " +
                $"shurikenSpeedRange={FormatRange(shurikenSpeedRange)}; " +
                $"gpuSpeedRange={FormatRange(gpuSpeedRange)}; " +
                $"shurikenSizeRange={FormatRange(shurikenSizeRange)}; " +
                $"gpuSizeRange={FormatRange(gpuSizeRange)}; " +
                $"shurikenColorRedRange={FormatRange(shurikenColorRedRange)}; " +
                $"gpuColorRedRange={FormatRange(gpuColorRedRange)}; " +
                $"shurikenDistancePositionRange={FormatRange(shurikenDistancePositionRange)}; " +
                $"gpuDistancePositionRange={FormatRange(gpuDistancePositionRange)}; " +
                $"shurikenLifetimeColorBlendRange={FormatRange(shurikenLifetimeColorBlendRange)}; " +
                $"gpuLifetimeColorBlendRange={FormatRange(gpuLifetimeColorBlendRange)}; " +
                $"shurikenLifetimeSizeBlendRange={FormatRange(shurikenLifetimeSizeBlendRange)}; " +
                $"gpuLifetimeSizeBlendRange={FormatRange(gpuLifetimeSizeBlendRange)}; " +
                $"shurikenSpeedColorBlendRange={FormatRange(shurikenSpeedColorBlendRange)}; " +
                $"gpuSpeedColorBlendRange={FormatRange(gpuSpeedColorBlendRange)}; " +
                $"shurikenSpeedSizeBlendRange={FormatRange(shurikenSpeedSizeBlendRange)}; " +
                $"gpuSpeedSizeBlendRange={FormatRange(gpuSpeedSizeBlendRange)}", this);
            Debug.Log($"PARTICLE_AB_CAPTURE_COMPLETE:{sessionFolder}", this);

#if UNITY_EDITOR
            if (exitEditorWhenCaptureCompletes && Application.isBatchMode)
            {
                UnityEditor.EditorApplication.Exit(passed ? 0 : 1);
            }
#endif
        }

        static string FormatRange(ObservedRange range)
        {
            return range.HasSamples
                ? $"[{range.Minimum:R},{range.Maximum:R}]"
                : "[]";
        }

        void ApplyDisplayMode()
        {
            if (shurikenRenderer != null)
            {
                shurikenRenderer.enabled = displayMode != ParticleABDisplayMode.GPUOnly;
            }

            if (gpuParticles != null)
            {
                gpuParticles.renderEnabled = displayMode != ParticleABDisplayMode.ShurikenOnly;
            }
        }

        void OnGUI()
        {
            const float width = 190f;
            GUILayout.BeginArea(new Rect(12f, 12f, width, 210f), GUI.skin.box);
            GUILayout.Label("Particle A/B Validation");
            GUILayout.Label($"Profile: {validationProfile}");
            GUILayout.Label("B: Both  S: Shuriken  G: GPU");
            GUILayout.Label("R: Restart  Space: Pause  C: Capture");
            GUILayout.Label($"Mode: {displayMode}");
            GUILayout.Label(captureActive ? $"Capture #{captureIndex}" : "Capture idle");

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Both")) SetDisplayMode(ParticleABDisplayMode.Both);
            if (GUILayout.Button("CPU")) SetDisplayMode(ParticleABDisplayMode.ShurikenOnly);
            if (GUILayout.Button("GPU")) SetDisplayMode(ParticleABDisplayMode.GPUOnly);
            GUILayout.EndHorizontal();
            if (GUILayout.Button("Restart")) RestartPlayback();
            GUILayout.EndArea();
        }
    }
}
