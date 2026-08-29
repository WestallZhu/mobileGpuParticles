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
        EmissionRateCurvePoint
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
        float maximumShurikenConeError;
        float maximumGPUConeError;
        float maximumForceKinematicsError;
        int maximumShurikenParticleCount;
        int maximumGPUParticleCount;
        Texture2D profileForceLUT;
        static readonly Vector3 ValidationForce = new Vector3(2f, -1f, 0.5f);
        ObservedRange shurikenLifetimeRange;
        ObservedRange gpuLifetimeRange;
        ObservedRange shurikenSpeedRange;
        ObservedRange gpuSpeedRange;
        ObservedRange shurikenSizeRange;
        ObservedRange gpuSizeRange;
        ObservedRange shurikenColorRedRange;
        ObservedRange gpuColorRedRange;

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
            ReleaseCameraCaptureTarget();
        }

        void Update()
        {
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

            gpuParticles.maxParticles = main.maxParticles;
            gpuParticles.emissionEnabled = true;
            gpuParticles.SetEmissionRateOverTime(emission.rateOverTime);
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
            gpuParticles.gravityModifier = 0f;
            gpuParticles.simulationSpeed = 1f;
            gpuParticles.simulationSpace = SimulationSpace.Local;
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

            gpuParticles.maxParticles = main.maxParticles;
            gpuParticles.emissionEnabled = true;
            gpuParticles.SetEmissionRateOverTime(emission.rateOverTime);
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
            gpuParticles.simulationSpeed = 1f;
            gpuParticles.simulationSpace = SimulationSpace.Local;
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
            gpuParticles.simulationSpeed = 1f;
            gpuParticles.simulationSpace = SimulationSpace.Local;
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
        }

        public void RestartPlayback()
        {
            Time.timeScale = 1f;

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
            maximumShurikenConeError = 0f;
            maximumGPUConeError = 0f;
            maximumForceKinematicsError = 0f;
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
            File.WriteAllBytes(Path.Combine(sessionFolder, prefix + "-gpu-poslife.exr"),
                posLife.EncodeToEXR(Texture2D.EXRFlags.OutputAsFloat));
            File.WriteAllBytes(Path.Combine(sessionFolder, prefix + "-gpu-velsize.exr"),
                velSize.EncodeToEXR(Texture2D.EXRFlags.OutputAsFloat));
            File.WriteAllBytes(Path.Combine(sessionFolder, prefix + "-gpu-color.exr"),
                colors.EncodeToEXR(Texture2D.EXRFlags.OutputAsFloat));

            AppendMetrics(elapsed, posLife.GetPixels(), velSize.GetPixels(), colors.GetPixels());
            WriteParticleState(prefix, posLife.GetPixels(), velSize.GetPixels());
            Destroy(posLife);
            Destroy(velSize);
            Destroy(colors);
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
                    AppendStateRow(state, "shuriken", i, particle.position, particle.velocity,
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
            Color[] gpuColors)
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
                    shurikenPositionSum += particle.position;
                    shurikenVelocitySum += particle.velocity;
                    shurikenSpeedSum += particle.velocity.magnitude;
                    float age = particle.startLifetime - particle.remainingLifetime;
                    shurikenAgeSum += age;
                    if (validationProfile == ParticleABValidationProfile.RandomizedMainPoint)
                    {
                        Color startColor = particle.startColor;
                        shurikenLifetimeRange.Observe(particle.startLifetime);
                        shurikenSpeedRange.Observe(particle.velocity.magnitude);
                        shurikenSizeRange.Observe(particle.startSize);
                        shurikenColorRedRange.Observe(startColor.r);
                    }
                    if (validationProfile == ParticleABValidationProfile.BaselineCone)
                    {
                        maximumShurikenConeError = Mathf.Max(maximumShurikenConeError,
                            ConeRelationError(particle.position, particle.velocity, age));
                    }
                    else if (validationProfile == ParticleABValidationProfile.ForceOverLifetimePoint)
                    {
                        maximumForceKinematicsError = Mathf.Max(maximumForceKinematicsError,
                            (particle.velocity - ValidationForce * age).magnitude);
                    }
                }
            }

            int gpuCount = 0;
            Vector3 gpuPositionSum = Vector3.zero;
            Vector3 gpuVelocitySum = Vector3.zero;
            float gpuSpeedSum = 0f;
            float gpuAgeSum = 0f;
            int pixelCount = Mathf.Min(
                Mathf.Min(gpuPosLife.Length, gpuVelSize.Length),
                gpuColors.Length);
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
                $"maxShurikenConeError={maximumShurikenConeError:R}; " +
                $"maxGPUConeError={maximumGPUConeError:R}; " +
                $"maxForceKinematicsError={maximumForceKinematicsError:R}; " +
                $"maxShurikenCount={maximumShurikenParticleCount}; " +
                $"maxGPUCount={maximumGPUParticleCount}; " +
                $"shurikenLifetimeRange={FormatRange(shurikenLifetimeRange)}; " +
                $"gpuLifetimeRange={FormatRange(gpuLifetimeRange)}; " +
                $"shurikenSpeedRange={FormatRange(shurikenSpeedRange)}; " +
                $"gpuSpeedRange={FormatRange(gpuSpeedRange)}; " +
                $"shurikenSizeRange={FormatRange(shurikenSizeRange)}; " +
                $"gpuSizeRange={FormatRange(gpuSizeRange)}; " +
                $"shurikenColorRedRange={FormatRange(shurikenColorRedRange)}; " +
                $"gpuColorRedRange={FormatRange(gpuColorRedRange)}", this);
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
