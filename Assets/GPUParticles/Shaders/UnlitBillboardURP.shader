Shader "GPUParticles/UnlitBillboardURP"
{
    Properties
    {
        _BaseMap("Texture", 2D) = "white" {}
        _MinAlphaCull("MinAlphaCull", Range(0,1)) = 0.001
    }
    SubShader
    {
        Tags{ "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Name "GPUParticlesBillboard"
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // MRT state from simulation
            TEXTURE2D(_CurPosLife); SAMPLER(sampler_CurPosLife);
            TEXTURE2D(_CurVelSize); SAMPLER(sampler_CurVelSize);
            TEXTURE2D(_CurColor);   SAMPLER(sampler_CurColor);
            TEXTURE2D(_BaseMap);    SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                int   _GridSize;
                int   _MaxParticles;
                int   _SimulationSpace;

                // emitter transforms (for LS->WS)
                float4x4 _EmitterLocalToWorld;

                // camera
                float3 _CameraPosWS; float _pad0;
                float3 _CameraVelWS; float _pad1;
                float3 _CameraRightWS; float _pad2;
                float3 _CameraUpWS;    float _pad3;

                // renderer params
                int   _RenderMode;          // 0 Billbd, 1 Horiz, 2 Vert, 3 Stretch
                int   _RenderAlignment;     // 0 View,1 Facing,2 World,3 Local,4 Velocity
                int   _AllowRoll;
                float _NormalDirection;     // 0..1

                float2 _Pivot;
                float  _LenScale;
                float  _VelScale;
                float  _CamVelScale;

                int    _Freeform;
                int    _RotateWithStretch;  // placeholder; unlit不做UV旋转
                float  _MinAlphaCull;
                float  _padLast;
            CBUFFER_END

            // handy
            struct VOut{ float4 posHCS:SV_POSITION; float2 uv:TEXCOORD0; float4 col:COLOR; };

            float3 Ortho(float3 v){ return normalize( any(abs(v) > 0.0) ? (abs(v.z)<0.999?cross(v,float3(0,0,1)):cross(v,float3(0,1,0))) : float3(1,0,0) ); }

            float2 IndexToUV(uint idx, int grid)
            {
                int x = idx % grid;
                int y = idx / grid;
                return (float2(x,y) + 0.5) / grid;
            }

            void ToWS(float3 posSim, float3 velSim, out float3 posWS, out float3 velWS)
            {
                if (_SimulationSpace==1) { posWS=posSim; velWS=velSim; }
                else {
                    posWS = mul(_EmitterLocalToWorld, float4(posSim,1)).xyz;
                    velWS = mul(_EmitterLocalToWorld, float4(velSim,0)).xyz;
                }
            }

            void BuildCameraBasis(out float3 cr, out float3 cu, out float3 cf)
            {
                cf = normalize( cross(_CameraRightWS, _CameraUpWS) ); // ensure orthogonal
                if (_AllowRoll!=0) { cr = normalize(_CameraRightWS); cu = normalize(_CameraUpWS); return; }
                // lock roll: rebuild with world up
                float3 up = float3(0,1,0);
                cr = normalize(cross(up, cf));
                cu = normalize(cross(cf, cr));
            }

            void Basis_Billboard_View(out float3 Right, out float3 Up, out float3 Normal)
            {
                float3 cr,cu,cf; BuildCameraBasis(cr,cu,cf);
                Right=cr; Up=cu; Normal=cf;
            }

            void Basis_Billboard_Facing(float3 P, out float3 Right, out float3 Up, out float3 Normal)
            {
                float3 toCam = normalize(_CameraPosWS - P);
                if (any(isnan(toCam)) || length(toCam)<1e-6)
                { Basis_Billboard_View(Right,Up,Normal); return; }
                float3 upW = float3(0,1,0);
                Right = normalize(cross(upW, toCam));
                if (length(Right)<1e-6) { Basis_Billboard_View(Right,Up,Normal); return; }
                Up    = normalize(cross(toCam, Right));
                Normal= toCam;
            }

            void Basis_Billboard_World(out float3 Right, out float3 Up, out float3 Normal)
            { Right=float3(1,0,0); Up=float3(0,1,0); Normal=normalize(cross(Right,Up)); }

            void Basis_Billboard_Local(out float3 Right, out float3 Up, out float3 Normal)
            {
                float3 Rx = normalize(_EmitterLocalToWorld._m00_m10_m20);
                float3 Uy = normalize(_EmitterLocalToWorld._m01_m11_m21);
                Right=Rx; Up=Uy; Normal=normalize(cross(Right,Up));
            }

            void Basis_Billboard_Velocity(float3 V, out float3 Right, out float3 Up, out float3 Normal)
            {
                Normal = normalize(V);
                if (length(Normal)<1e-6) { Basis_Billboard_View(Right,Up,Normal); return; }
                Right = Ortho(Normal);
                Up    = normalize(cross(Normal, Right));
            }

            // stretched bases
            void Basis_Stretched_Classic(float3 V, out float3 Right, out float3 Up, out float3 Normal)
            {
                float3 cr,cu,cf; BuildCameraBasis(cr,cu,cf);
                Normal = cf;
                // project V onto camera plane
                float3 T = V - dot(V, Normal) * Normal;
                if (length(T)<1e-6) T = cu; // fallback
                T = normalize(T);
                Right = normalize(cross(Normal, T));
                Up    = T;
            }

            void Basis_Stretched_Freeform(float3 P, float3 V, out float3 Right, out float3 Up, out float3 Normal)
            {
                float3 view = normalize(_CameraPosWS - P);
                float3 T = normalize(V);
                if (length(T)<1e-6) { Basis_Stretched_Classic(V, Right, Up, Normal); return; }
                Right = normalize(cross(view, T));
                if (length(Right)<1e-6) Right = Ortho(T);
                Normal= normalize(cross(Right, T));
                Up    = T;
            }

            // quad corner from vertex id (0..5)
            int CornerFromTriVertex(uint triV)
            {
                // 0: (0,1,2) -> 0,1,2; 1: (0,2,3) -> 0,2,3
                const int map[6] = {0,1,2, 0,2,3};
                return map[triV];
            }

            VOut Vert(uint vid:SV_VertexID)
            {
                VOut o;

                uint quadId = vid / 6;
                uint tv     = vid % 6;
                int corner  = CornerFromTriVertex(tv);

                if (quadId >= (uint)_MaxParticles)
                {
                    o.posHCS = float4(0,0,0,0);
                    o.uv = 0; o.col = 0;
                    return o;
                }

                // sample particle
                float2 tuv = IndexToUV(quadId, _GridSize);
                float4 posLife = SAMPLE_TEXTURE2D_LOD(_CurPosLife, sampler_CurPosLife, tuv, 0);
                float4 velSize = SAMPLE_TEXTURE2D_LOD(_CurVelSize, sampler_CurVelSize, tuv, 0);
                float4 pcol    = SAMPLE_TEXTURE2D_LOD(_CurColor,   sampler_CurColor,   tuv, 0);

                float3 posWS, velWS; ToWS(posLife.xyz, velSize.xyz, posWS, velWS);

                // kill if dead
                if (posLife.w <= 0.0 || pcol.a <= _MinAlphaCull)
                {
                    o.posHCS = float4(0,0,0,0);
                    o.uv = 0; o.col = 0; return o;
                }

                // choose basis
                float3 Right, Up, Normal;
                if (_RenderMode == 3) // Stretched
                {
                    if (_Freeform!=0)  Basis_Stretched_Freeform(posWS, velWS, Right, Up, Normal);
                    else                Basis_Stretched_Classic (velWS, Right, Up, Normal);
                }
                else if (_RenderMode == 1) // Horizontal
                {
                    float3 up = float3(0,1,0);
                    float3 cf = normalize(cross(_CameraRightWS, _CameraUpWS));
                    Right = normalize(cross(up, cf));
                    Up    = up;
                    Normal= cross(Right, Up);
                }
                else if (_RenderMode == 2) // Vertical
                {
                    float3 up = float3(0,1,0);
                    float3 toCam = normalize(_CameraPosWS - posWS);
                    toCam.y = 0;
                    float3 f = normalize(toCam);
                    Right = normalize(cross(up, f));
                    Up    = up;
                    Normal= cross(Right, Up);
                }
                else // Billboard
                {
                    if (_RenderAlignment==0)      Basis_Billboard_View(Right,Up,Normal);
                    else if (_RenderAlignment==1) Basis_Billboard_Facing(posWS, Right,Up,Normal);
                    else if (_RenderAlignment==2) Basis_Billboard_World(Right,Up,Normal);
                    else if (_RenderAlignment==3) Basis_Billboard_Local(Right,Up,Normal);
                    else                          Basis_Billboard_Velocity(velWS, Right,Up,Normal);
                }

                // size (W/L) & pivot
                float size = velSize.w;
                float W = size;
                float L = (_RenderMode==3) ? max(1e-4, size * (_LenScale + length(velWS)*_VelScale + length(_CameraVelWS)*_CamVelScale)) : size;

                float2 quadUV[4] = { float2(0,0), float2(1,0), float2(1,1), float2(0,1) };
                float2 localXY[4]= { float2(-0.5,-0.5), float2(0.5,-0.5), float2(0.5,0.5), float2(-0.5,0.5) };
                float2 local = localXY[corner] * float2(W, (_RenderMode==3)?L:W);
                float2 pivotOff = float2(_Pivot.x*W, (_RenderMode==3)?(_Pivot.y*L):(_Pivot.y*W));
                local -= pivotOff;

                float3 wpos = posWS + Right*local.x + Up*local.y;

                o.posHCS = TransformWorldToHClip(wpos);
                o.uv = quadUV[corner];
                o.col = pcol;
                return o;
            }

            half4 Frag(VOut i) : SV_Target
            {
                float4 baseCol = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv);
                return baseCol * i.col;
            }
            ENDHLSL
        }
    }
}
