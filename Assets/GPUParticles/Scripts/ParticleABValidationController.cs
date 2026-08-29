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

    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class ParticleABValidationController : MonoBehaviour
    {
        [Header("A/B Systems")]
        public ParticleSystem shuriken;
        public GPUParticleSystem gpuParticles;
        public ParticleABDisplayMode displayMode = ParticleABDisplayMode.Both;
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
        float maximumShurikenConeError;
        float maximumGPUConeError;

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
            maximumShurikenConeError = 0f;
            maximumGPUConeError = 0f;
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
            File.WriteAllBytes(Path.Combine(sessionFolder, prefix + "-gpu-poslife.exr"),
                posLife.EncodeToEXR(Texture2D.EXRFlags.OutputAsFloat));
            File.WriteAllBytes(Path.Combine(sessionFolder, prefix + "-gpu-velsize.exr"),
                velSize.EncodeToEXR(Texture2D.EXRFlags.OutputAsFloat));

            AppendMetrics(elapsed, posLife.GetPixels(), velSize.GetPixels());
            WriteParticleState(prefix, posLife.GetPixels(), velSize.GetPixels());
            Destroy(posLife);
            Destroy(velSize);
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
                    Mathf.Max(0f, gpuParticles.startLifetime - positionLife.a), positionLife.a);
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

        void AppendMetrics(float elapsed, Color[] gpuPosLife, Color[] gpuVelSize)
        {
            int shurikenCount = 0;
            Vector3 shurikenPositionSum = Vector3.zero;
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
                    shurikenSpeedSum += particle.velocity.magnitude;
                    float age = particle.startLifetime - particle.remainingLifetime;
                    shurikenAgeSum += age;
                    maximumShurikenConeError = Mathf.Max(maximumShurikenConeError,
                        ConeRelationError(particle.position, particle.velocity, age));
                }
            }

            int gpuCount = 0;
            Vector3 gpuPositionSum = Vector3.zero;
            float gpuSpeedSum = 0f;
            float gpuAgeSum = 0f;
            int pixelCount = Mathf.Min(gpuPosLife.Length, gpuVelSize.Length);
            for (int i = 0; i < pixelCount; i++)
            {
                Color positionLife = gpuPosLife[i];
                if (positionLife.a <= 0f) continue;

                Color velocitySize = gpuVelSize[i];
                gpuCount++;
                gpuPositionSum += new Vector3(positionLife.r, positionLife.g, positionLife.b);
                gpuSpeedSum += new Vector3(velocitySize.r, velocitySize.g, velocitySize.b).magnitude;
                float age = Mathf.Max(0f, gpuParticles.startLifetime - positionLife.a);
                gpuAgeSum += age;
                maximumGPUConeError = Mathf.Max(maximumGPUConeError,
                    ConeRelationError(
                        new Vector3(positionLife.r, positionLife.g, positionLife.b),
                        new Vector3(velocitySize.r, velocitySize.g, velocitySize.b), age));
            }

            Vector3 shurikenMean = shurikenCount > 0 ? shurikenPositionSum / shurikenCount : Vector3.zero;
            Vector3 gpuMean = gpuCount > 0 ? gpuPositionSum / gpuCount : Vector3.zero;
            float shurikenMeanSpeed = shurikenCount > 0 ? shurikenSpeedSum / shurikenCount : 0f;
            float gpuMeanSpeed = gpuCount > 0 ? gpuSpeedSum / gpuCount : 0f;
            float shurikenMeanAge = shurikenCount > 0 ? shurikenAgeSum / shurikenCount : 0f;
            float gpuMeanAge = gpuCount > 0 ? gpuAgeSum / gpuCount : 0f;

            maximumCountDelta = Mathf.Max(maximumCountDelta, Mathf.Abs(gpuCount - shurikenCount));
            maximumMeanAgeError = Mathf.Max(maximumMeanAgeError, Mathf.Abs(gpuMeanAge - shurikenMeanAge));
            maximumMeanSpeedError = Mathf.Max(maximumMeanSpeedError,
                Mathf.Abs(gpuMeanSpeed - shurikenMeanSpeed));

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
            bool passed = maximumCountDelta == 0 &&
                          maximumMeanAgeError <= 0.001f &&
                          maximumMeanSpeedError <= 0.001f &&
                          maximumShurikenConeError <= 0.001f &&
                          maximumGPUConeError <= 0.001f;
            string result = passed ? "PASS" : "FAIL";
            Debug.Log(
                $"PARTICLE_AB_CAPTURE_RESULT:{result}; " +
                $"maxCountDelta={maximumCountDelta}; " +
                $"maxMeanAgeError={maximumMeanAgeError:R}; " +
                $"maxMeanSpeedError={maximumMeanSpeedError:R}; " +
                $"maxShurikenConeError={maximumShurikenConeError:R}; " +
                $"maxGPUConeError={maximumGPUConeError:R}", this);
            Debug.Log($"PARTICLE_AB_CAPTURE_COMPLETE:{sessionFolder}", this);

#if UNITY_EDITOR
            if (exitEditorWhenCaptureCompletes && Application.isBatchMode)
            {
                UnityEditor.EditorApplication.Exit(passed ? 0 : 1);
            }
#endif
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
            GUILayout.BeginArea(new Rect(12f, 12f, width, 190f), GUI.skin.box);
            GUILayout.Label("Particle A/B Validation");
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
