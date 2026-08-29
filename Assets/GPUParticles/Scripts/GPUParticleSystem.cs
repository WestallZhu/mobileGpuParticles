using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System.Collections.Generic;

namespace GPUParticles
{
    public enum SimulationSpace { Local = 0, World = 1 }

    public enum ShapeTypeGPU { Sphere = 0, Hemisphere = 1, Cone = 2, Donut = 3, Box = 4, Circle = 5, Edge = 6, Rectangle = 7 }
    public enum ShapeEmitFromGPU { Volume = 0, Surface = 1, Base = 2 } // Base for Cone

    public enum GPURenderMode { Billboard = 0, HorizontalBillboard = 1, VerticalBillboard = 2, StretchedBillboard = 3 }
    public enum GPUAlignment  { View = 0, Facing = 1, World = 2, Local = 3, Velocity = 4 }

    [ExecuteAlways]
    public class GPUParticleSystem : MonoBehaviour
    {
        // --------- Capacity & Emission ---------
        [Header("Capacity")]
        [Min(1)] public int maxParticles = 65536;

        [Header("Emission")]
        [Min(0)] public float emissionRateOverTime = 2000f;

        // --------- Main ---------
        [Header("Main (Shuriken Mapping)")]
        [Min(0.001f)] public float startLifetime = 2.0f;
        public float startSpeed = 5.0f;
        [Min(0.0f)] public float startSize = 0.1f;
        public Color startColor = Color.white;
        public float gravityModifier = 0.0f;
        [Min(0.0f)] public float simulationSpeed = 1.0f;
        public float startRotation = 0.0f;
        public float rotationOverLifetime = 0.0f;
        public SimulationSpace simulationSpace = SimulationSpace.Local;

        [Header("Over Lifetime (LUTs)")]
        public Texture2D colorOverLifetimeLUT;
        public Texture2D sizeOverLifetimeLUT;

        [Header("Rendering")]
        public Texture2D baseMap;
        [Range(0,1)] public float minAlphaCull = 0.001f;
        public bool renderEnabled = true;

        [Header("Emitter Direction (fallback)")]
        public Vector3 initialDirectionWS = Vector3.forward;

        // --------- Shape ---------
        [Header("Shape (Sphere/Hemisphere/Cone/Donut/Box/Circle/Edge/Rectangle)")]
        public ShapeTypeGPU shapeType = ShapeTypeGPU.Cone;
        public ShapeEmitFromGPU shapeEmitFrom = ShapeEmitFromGPU.Volume;
        public bool alignToDirection = false; // default false (match Shuriken)

        // Sphere/Hemisphere
        public float shapeSphereRadius = 0.5f;

        // Cone
        [Range(0,90)] public float shapeConeAngle = 25f;
        public float shapeConeRadius = 0.25f;
        public float shapeConeLength = 1.0f;
        [Range(0,1)] public float shapeRadiusThickness = 1.0f;
        [Range(0,360)] public float shapeConeArcDeg = 360f;

        // Donut
        public float shapeDonutRadius = 1.0f; // 主圆环半径
        public float shapeDonutThickness = 0.2f; // 环的厚度

        // Box
        public Vector3 shapeBoxSize = new Vector3(1,1,1);

        // Circle
        public float shapeCircleRadius = 0.5f;

        // Edge
        public float shapeEdgeLength = 1.0f;

        // Rectangle
        public Vector2 shapeRectangleSize = new Vector2(1, 1);

        // Shape TRS (relative to emitter local)
        public Vector3 shapeLocalPosition = Vector3.zero;
        public Vector3 shapeLocalRotationEuler = Vector3.zero;
        public Vector3 shapeLocalScale = Vector3.one;

        // --------- Renderer (Shuriken Renderer Module mapping) ---------
        [Header("Renderer (Shuriken Mapping)")]
        public GPURenderMode renderMode = GPURenderMode.Billboard;
        public GPUAlignment  renderAlignment = GPUAlignment.View; // ignored in Stretched per Unity
        public bool allowRoll = true;
        [Range(0,1)] public float normalDirection = 1.0f; // billboard only
        public Vector2 pivot = Vector2.zero;
        [Min(0)] public float minParticleSize = 0f; // TODO: screen-space clamp (optional)
        [Min(0)] public float maxParticleSize = 0f;

        [Header("Stretched Billboard")]
        public float stretchedLengthScale = 1.0f;   // 1 = neutral
        public float stretchedVelocityScale = 0.0f;
        public float stretchedCameraVelocityScale = 0.0f;
        public bool  freeformStretching = false;
        public bool  rotateWithStretchDirection = true; // for lit/UV rotation pipelines

        // --------- Runtime ---------
        public static readonly List<GPUParticleSystem> Active = new List<GPUParticleSystem>();

        Material simulateMaterial;
        Material renderMaterial;

        RenderTexture[] posLife = new RenderTexture[2];
        RenderTexture[] velSize = new RenderTexture[2];
        RenderTexture[] colorRT = new RenderTexture[2];

        int ping;
        int gridSize;
        int capacity;

        // emission cursor
        int emitCursor = 0;
        float emitCarry = 0f;
        int lastSimulatedFrame = -1;

        // camera velocity cache
        Vector3 prevCamPos;
        bool prevCamPosValid = false;

        // ---- Shader property IDs ----
        static readonly int _CurPosLife = Shader.PropertyToID("_CurPosLife");
        static readonly int _CurVelSize = Shader.PropertyToID("_CurVelSize");
        static readonly int _CurColor   = Shader.PropertyToID("_CurColor");
        static readonly int _GridSize   = Shader.PropertyToID("_GridSize");
        static readonly int _MaxParticles = Shader.PropertyToID("_MaxParticles");
        static readonly int _DeltaTime  = Shader.PropertyToID("_DeltaTime");
        static readonly int _StartLifetime = Shader.PropertyToID("_StartLifetime");
        static readonly int _StartSpeed = Shader.PropertyToID("_StartSpeed");
        static readonly int _StartSize  = Shader.PropertyToID("_StartSize");
        static readonly int _StartColor = Shader.PropertyToID("_StartColor");
        static readonly int _StartRotation = Shader.PropertyToID("_StartRotation");
        static readonly int _RotationOverLifetime = Shader.PropertyToID("_RotationOverLifetime");
        static readonly int _GravityWS  = Shader.PropertyToID("_GravityWS");
        static readonly int _SimulationSpace = Shader.PropertyToID("_SimulationSpace");
        static readonly int _EmitStart  = Shader.PropertyToID("_EmitStart");
        static readonly int _EmitCount  = Shader.PropertyToID("_EmitCount");
        static readonly int _EmitCarryPrev = Shader.PropertyToID("_EmitCarryPrev");
        static readonly int _EmissionRate = Shader.PropertyToID("_EmissionRate");
        static readonly int _InitialDir = Shader.PropertyToID("_InitialDir");

        // shape params
        static readonly int _ShapeType = Shader.PropertyToID("_ShapeType");
        static readonly int _ShapeEmitFrom = Shader.PropertyToID("_ShapeEmitFrom");
        static readonly int _AlignToDirection = Shader.PropertyToID("_AlignToDirection");
        static readonly int _ShapeConeAngleRad = Shader.PropertyToID("_ShapeConeAngleRad");
        static readonly int _ShapeConeRadius = Shader.PropertyToID("_ShapeConeRadius");
        static readonly int _ShapeConeLength = Shader.PropertyToID("_ShapeConeLength");
        static readonly int _ShapeRadiusThickness = Shader.PropertyToID("_ShapeRadiusThickness");
        static readonly int _ShapeConeArcRad = Shader.PropertyToID("_ShapeConeArcRad");
        static readonly int _ShapeBoxSize = Shader.PropertyToID("_ShapeBoxSize");
        static readonly int _ShapeSphereRadius = Shader.PropertyToID("_ShapeSphereRadius");
        static readonly int _ShapeDonutRadius = Shader.PropertyToID("_ShapeDonutRadius");
        static readonly int _ShapeDonutThickness = Shader.PropertyToID("_ShapeDonutThickness");
        static readonly int _ShapeCircleRadius = Shader.PropertyToID("_ShapeCircleRadius");
        static readonly int _ShapeEdgeLength = Shader.PropertyToID("_ShapeEdgeLength");
        static readonly int _ShapeRectangleSize = Shader.PropertyToID("_ShapeRectangleSize");
        static readonly int _ShapePosL = Shader.PropertyToID("_ShapePosL");
        static readonly int _ShapeRightL = Shader.PropertyToID("_ShapeRightL");
        static readonly int _ShapeUpL = Shader.PropertyToID("_ShapeUpL");
        static readonly int _ShapeFwdL = Shader.PropertyToID("_ShapeFwdL");

        static readonly int _EmitterLocalToWorld = Shader.PropertyToID("_EmitterLocalToWorld");
        static readonly int _EmitterWorldToLocal = Shader.PropertyToID("_EmitterWorldToLocal");

        // render shader ids
        static readonly int _EmitterLocalToWorld_Render = Shader.PropertyToID("_EmitterLocalToWorld");
        static readonly int _CameraRightWS = Shader.PropertyToID("_CameraRightWS");
        static readonly int _CameraUpWS = Shader.PropertyToID("_CameraUpWS");
        static readonly int _CameraPosWS = Shader.PropertyToID("_CameraPosWS");
        static readonly int _CameraVelWS = Shader.PropertyToID("_CameraVelWS");
        static readonly int _MinAlphaCull = Shader.PropertyToID("_MinAlphaCull");

        static readonly int _RenderMode = Shader.PropertyToID("_RenderMode");
        static readonly int _RenderAlignment = Shader.PropertyToID("_RenderAlignment");
        static readonly int _AllowRoll = Shader.PropertyToID("_AllowRoll");
        static readonly int _NormalDirection = Shader.PropertyToID("_NormalDirection");
        static readonly int _Pivot = Shader.PropertyToID("_Pivot");
        static readonly int _LenScale = Shader.PropertyToID("_LenScale");
        static readonly int _VelScale = Shader.PropertyToID("_VelScale");
        static readonly int _CamVelScale = Shader.PropertyToID("_CamVelScale");
        static readonly int _Freeform = Shader.PropertyToID("_Freeform");
        static readonly int _RotateWithStretch = Shader.PropertyToID("_RotateWithStretch");

        internal RenderTexture CurrentPositionLifetimeTexture => posLife[ping];
        internal RenderTexture CurrentVelocitySizeTexture => velSize[ping];
        internal RenderTexture CurrentColorTexture => colorRT[ping];

        void OnEnable()
        {
            if (!Active.Contains(this)) Active.Add(this);
            EnsureMaterials();
            RecreateTargetsIfNeeded(true);
        }

        void OnDisable()
        {
            Active.Remove(this);
            ReleaseTargets();
            lastSimulatedFrame = -1;
        }

        void OnValidate()
        {
            maxParticles = Mathf.Max(1, maxParticles);
            startLifetime = Mathf.Max(1e-3f, startLifetime);
            startSize = Mathf.Max(0f, startSize);
            EnsureMaterials();
            RecreateTargetsIfNeeded(false);
        }

        void EnsureMaterials()
        {
            if (simulateMaterial == null)
            {
                var sim = Shader.Find("Hidden/GPUParticles/SimulateMRT");
                if (sim != null) simulateMaterial = CoreUtils.CreateEngineMaterial(sim);
            }
            if (renderMaterial == null)
            {
                var r = Shader.Find("GPUParticles/UnlitBillboardURP");
                if (r != null) renderMaterial = CoreUtils.CreateEngineMaterial(r);
            }
        }

        int CeilSqrt(int n) => Mathf.CeilToInt(Mathf.Sqrt(n));

        void RecreateTargetsIfNeeded(bool force)
        {
            int newGrid = CeilSqrt(maxParticles);
            if (!force && newGrid == gridSize && posLife[0] != null) return;

            ReleaseTargets();
            gridSize = newGrid;
            capacity = gridSize * gridSize;

            CreateRT(ref posLife[0], RenderTextureFormat.ARGBFloat);
            CreateRT(ref posLife[1], RenderTextureFormat.ARGBFloat);
            CreateRT(ref velSize[0], RenderTextureFormat.ARGBFloat);
            CreateRT(ref velSize[1], RenderTextureFormat.ARGBFloat);
            CreateRT(ref colorRT[0], RenderTextureFormat.ARGBHalf);
            CreateRT(ref colorRT[1], RenderTextureFormat.ARGBHalf);

            ClearRT(posLife[0]); ClearRT(posLife[1]);
            ClearRT(velSize[0]); ClearRT(velSize[1]);
            ClearRT(colorRT[0]); ClearRT(colorRT[1]);

            ping = 0;
            emitCursor = 0;
            emitCarry = 0f;
            lastSimulatedFrame = -1;
        }

        void CreateRT(ref RenderTexture rt, RenderTextureFormat fmt)
        {
            rt = new RenderTexture(gridSize, gridSize, 0, fmt, RenderTextureReadWrite.Linear);
            rt.name = $"GPUParticles_{fmt}_{gridSize}";
            rt.enableRandomWrite = false;
            rt.useMipMap = false;
            rt.wrapMode = TextureWrapMode.Clamp;
            rt.filterMode = FilterMode.Point;
            rt.Create();
        }

        void ClearRT(RenderTexture rt)
        {
            var active = RenderTexture.active;
            RenderTexture.active = rt;
            GL.Clear(false, true, Color.clear);
            RenderTexture.active = active;
        }

        void ReleaseTargets()
        {
            for (int i = 0; i < 2; i++)
            {
                if (posLife[i] != null) { posLife[i].Release(); Object.DestroyImmediate(posLife[i]); posLife[i] = null; }
                if (velSize[i] != null) { velSize[i].Release(); Object.DestroyImmediate(velSize[i]); velSize[i] = null; }
                if (colorRT[i] != null) { colorRT[i].Release(); Object.DestroyImmediate(colorRT[i]); colorRT[i] = null; }
            }
        }

        internal void ResetSimulation()
        {
            EnsureMaterials();
            RecreateTargetsIfNeeded(true);
        }

        internal void Simulate(CommandBuffer cmd, Camera camera)
        {
            if (simulateMaterial == null) return;

            // A renderer feature executes once per camera. Shuriken advances once per
            // player-loop frame, so advancing here for Scene/Game/overlay cameras would
            // make the GPU system run faster whenever more than one camera renders.
            if (Application.isPlaying)
            {
                int frame = Time.frameCount;
                if (lastSimulatedFrame == frame) return;
                lastSimulatedFrame = frame;
            }

            float dt = Application.isPlaying ? Time.deltaTime : (1f / 60f);
            dt *= simulationSpeed;
            SimulateStep(cmd, dt);
        }

        internal void SimulateStep(CommandBuffer cmd, float dt)
        {
            float emitCarryPrev = emitCarry;
            float toEmit = (emissionRateOverTime * dt) + emitCarryPrev;
            int emitCount = Mathf.Clamp(Mathf.FloorToInt(toEmit), 0, maxParticles);
            emitCarry = toEmit - emitCount;
            int emitStart = emitCursor;
            emitCursor = (emitCursor + emitCount) % maxParticles;

            int src = ping, dst = 1 - ping;

            simulateMaterial.SetTexture(_CurPosLife, posLife[src]);
            simulateMaterial.SetTexture(_CurVelSize, velSize[src]);
            simulateMaterial.SetTexture(_CurColor,   colorRT[src]);
            simulateMaterial.SetTexture("_GradLUT", colorOverLifetimeLUT != null ? colorOverLifetimeLUT : GradientLUTBuilder.GetDefaultWhiteLUT());
            simulateMaterial.SetTexture("_SizeLUT", sizeOverLifetimeLUT != null ? sizeOverLifetimeLUT : CurveLUTBuilder.GetDefaultUnitLUT());

            simulateMaterial.SetInt(_GridSize, gridSize);
            simulateMaterial.SetInt(_MaxParticles, maxParticles);
            simulateMaterial.SetFloat(_DeltaTime, dt);
            simulateMaterial.SetFloat(_StartLifetime, startLifetime);
            simulateMaterial.SetFloat(_StartSpeed, startSpeed);
            simulateMaterial.SetFloat(_StartSize, startSize);
            simulateMaterial.SetColor(_StartColor, startColor);

            Vector3 gWorld = Physics.gravity * gravityModifier;
            Vector3 gSim = (simulationSpace == SimulationSpace.World) ? gWorld : transform.InverseTransformDirection(gWorld);
            simulateMaterial.SetVector(_GravityWS, new Vector4(gSim.x, gSim.y, gSim.z, 0));

            simulateMaterial.SetInt(_SimulationSpace, (int)simulationSpace);
            simulateMaterial.SetInt(_EmitStart, emitStart);
            simulateMaterial.SetInt(_EmitCount, emitCount);
            simulateMaterial.SetFloat(_EmitCarryPrev, emitCarryPrev);
            simulateMaterial.SetFloat(_EmissionRate, emissionRateOverTime);

            Vector3 dirInitW = initialDirectionWS.sqrMagnitude > 1e-6f ? initialDirectionWS.normalized : transform.forward;
            Vector3 dirInitSim = (simulationSpace == SimulationSpace.World) ? dirInitW : transform.InverseTransformDirection(dirInitW);
            simulateMaterial.SetVector(_InitialDir, new Vector4(dirInitSim.x, dirInitSim.y, dirInitSim.z, 0));

            simulateMaterial.SetInt(_ShapeType, (int)shapeType);
            simulateMaterial.SetInt(_ShapeEmitFrom, (int)shapeEmitFrom);
            simulateMaterial.SetInt(_AlignToDirection, alignToDirection ? 1 : 0);
            simulateMaterial.SetFloat(_ShapeRadiusThickness, Mathf.Clamp01(shapeRadiusThickness));

            float avgScale = (shapeLocalScale.x + shapeLocalScale.y + shapeLocalScale.z) / 3f;

            // 根据shapeType设置对应的参数
            switch (shapeType)
            {
                case ShapeTypeGPU.Sphere:
                case ShapeTypeGPU.Hemisphere:
                    simulateMaterial.SetFloat(_ShapeSphereRadius, Mathf.Max(0f, shapeSphereRadius * avgScale));
                    break;

                case ShapeTypeGPU.Cone:
                    simulateMaterial.SetFloat(_ShapeConeAngleRad, shapeConeAngle * Mathf.Deg2Rad);
                    float coneRadiusScaled = shapeConeRadius * 0.5f * (shapeLocalScale.x + shapeLocalScale.y);
                    float coneLengthScaled = shapeConeLength * shapeLocalScale.z;
                    simulateMaterial.SetFloat(_ShapeConeRadius, coneRadiusScaled);
                    simulateMaterial.SetFloat(_ShapeConeLength, coneLengthScaled);
                    simulateMaterial.SetFloat(_ShapeConeArcRad, Mathf.Clamp(shapeConeArcDeg, 0f, 360f) * Mathf.Deg2Rad);
                    break;

                case ShapeTypeGPU.Donut:
                    simulateMaterial.SetFloat(_ShapeDonutRadius, Mathf.Max(0f, shapeDonutRadius * avgScale));
                    simulateMaterial.SetFloat(_ShapeDonutThickness, Mathf.Max(0f, shapeDonutThickness * avgScale));
                    break;

                case ShapeTypeGPU.Box:
                    Vector3 boxSizeScaled = Vector3.Scale(shapeBoxSize, shapeLocalScale);
                    simulateMaterial.SetVector(_ShapeBoxSize, new Vector4(boxSizeScaled.x, boxSizeScaled.y, boxSizeScaled.z, 0));
                    break;

                case ShapeTypeGPU.Circle:
                    simulateMaterial.SetFloat(_ShapeCircleRadius, Mathf.Max(0f, shapeCircleRadius * avgScale));
                    break;

                case ShapeTypeGPU.Edge:
                    simulateMaterial.SetFloat(_ShapeEdgeLength, Mathf.Max(0f, shapeEdgeLength * avgScale));
                    break;

                case ShapeTypeGPU.Rectangle:
                    Vector2 rectSizeScaled = new Vector2(
                        shapeRectangleSize.x * shapeLocalScale.x,
                        shapeRectangleSize.y * shapeLocalScale.y
                    );
                    simulateMaterial.SetVector(_ShapeRectangleSize, new Vector4(rectSizeScaled.x, rectSizeScaled.y, 0, 0));
                    break;
            }

            Quaternion q = Quaternion.Euler(shapeLocalRotationEuler);
            Vector3 rightL = q * Vector3.right;
            Vector3 upL = q * Vector3.up;
            Vector3 fwdL = q * Vector3.forward;
            Vector3 posL = shapeLocalPosition;
            simulateMaterial.SetVector(_ShapePosL, new Vector4(posL.x, posL.y, posL.z, 0));
            simulateMaterial.SetVector(_ShapeRightL, new Vector4(rightL.x, rightL.y, rightL.z, 0));
            simulateMaterial.SetVector(_ShapeUpL, new Vector4(upL.x, upL.y, upL.z, 0));
            simulateMaterial.SetVector(_ShapeFwdL, new Vector4(fwdL.x, fwdL.y, fwdL.z, 0));

            simulateMaterial.SetMatrix(_EmitterLocalToWorld, transform.localToWorldMatrix);
            simulateMaterial.SetMatrix(_EmitterWorldToLocal, transform.worldToLocalMatrix);

            var mrt = new RenderTargetIdentifier[] {
                new RenderTargetIdentifier(posLife[dst]),
                new RenderTargetIdentifier(velSize[dst]),
                new RenderTargetIdentifier(colorRT[dst]),
            };
            cmd.SetRenderTarget(mrt, posLife[dst]);
            cmd.SetViewport(new Rect(0, 0, gridSize, gridSize));
            CoreUtils.DrawFullScreen(cmd, simulateMaterial, null, 0);

            ping = dst;
        }

        internal void Render(CommandBuffer cmd, Camera camera)
        {
            if (!renderEnabled || renderMaterial == null) return;

            renderMaterial.SetTexture(_CurPosLife, posLife[ping]);
            renderMaterial.SetTexture(_CurVelSize, velSize[ping]);
            renderMaterial.SetTexture(_CurColor,   colorRT[ping]);
            renderMaterial.SetTexture("_BaseMap", baseMap != null ? baseMap : Texture2D.whiteTexture);

            renderMaterial.SetInt(_GridSize, gridSize);
            renderMaterial.SetInt(_MaxParticles, maxParticles);
            renderMaterial.SetInt(_SimulationSpace, (int)simulationSpace);
            renderMaterial.SetFloat(_StartLifetime, startLifetime);
            renderMaterial.SetFloat(_StartRotation, startRotation);
            renderMaterial.SetFloat(_RotationOverLifetime, rotationOverLifetime);
            renderMaterial.SetMatrix(_EmitterLocalToWorld_Render, transform.localToWorldMatrix);
            renderMaterial.SetVector(_CameraRightWS, camera.transform.right);
            renderMaterial.SetVector(_CameraUpWS, camera.transform.up);

            // camera position & velocity
            Vector3 camPos = camera.transform.position;
            Vector3 camVel = Vector3.zero;
            float dt = Application.isPlaying ? Time.deltaTime : (1f/60f);
            if (prevCamPosValid && dt > 1e-6f) camVel = (camPos - prevCamPos) / dt;
            prevCamPos = camPos; prevCamPosValid = true;
            renderMaterial.SetVector(_CameraPosWS, camPos);
            renderMaterial.SetVector(_CameraVelWS, camVel);

            // renderer params
            renderMaterial.SetInt(_RenderMode, (int)renderMode);
            renderMaterial.SetInt(_RenderAlignment, (int)renderAlignment);
            renderMaterial.SetInt(_AllowRoll, allowRoll ? 1 : 0);
            renderMaterial.SetFloat(_NormalDirection, normalDirection);
            renderMaterial.SetVector(_Pivot, pivot);
            renderMaterial.SetFloat(_LenScale, stretchedLengthScale);
            renderMaterial.SetFloat(_VelScale, stretchedVelocityScale);
            renderMaterial.SetFloat(_CamVelScale, stretchedCameraVelocityScale);
            renderMaterial.SetInt(_Freeform, freeformStretching ? 1 : 0);
            renderMaterial.SetInt(_RotateWithStretch, rotateWithStretchDirection ? 1 : 0);
            renderMaterial.SetFloat(_MinAlphaCull, minAlphaCull);

            // draw quads: 6 verts per particle
            int vertexCount = maxParticles * 6;
            cmd.DrawProcedural(Matrix4x4.identity, renderMaterial, 0, MeshTopology.Triangles, vertexCount, 1);
        }
    }
}
