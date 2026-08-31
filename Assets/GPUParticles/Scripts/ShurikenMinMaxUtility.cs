using UnityEngine;

namespace GPUParticles
{
    public static class ShurikenMinMaxUtility
    {
        public static bool TryGetConstantRange(
            ParticleSystem.MinMaxCurve curve,
            out float minimum,
            out float maximum)
        {
            switch (curve.mode)
            {
                case ParticleSystemCurveMode.Constant:
                    minimum = curve.constant;
                    maximum = curve.constant;
                    return true;

                case ParticleSystemCurveMode.TwoConstants:
                    minimum = curve.constantMin;
                    maximum = curve.constantMax;
                    return true;

                default:
                    maximum = curve.Evaluate(0f, 1f);
                    minimum = maximum;
                    return false;
            }
        }

        public static void GetRangeAtTime(
            ParticleSystem.MinMaxCurve curve,
            float time,
            out float minimum,
            out float maximum)
        {
            switch (curve.mode)
            {
                case ParticleSystemCurveMode.Constant:
                    minimum = curve.constant;
                    maximum = curve.constant;
                    return;

                case ParticleSystemCurveMode.TwoConstants:
                    minimum = curve.constantMin;
                    maximum = curve.constantMax;
                    return;

                case ParticleSystemCurveMode.TwoCurves:
                    minimum = curve.Evaluate(time, 0f);
                    maximum = curve.Evaluate(time, 1f);
                    return;

                default:
                    maximum = curve.Evaluate(time, 1f);
                    minimum = maximum;
                    return;
            }
        }

        public static float SampleSystemRandomValue(uint randomSeed)
        {
            // Shuriken resolves a randomized Main/Start Delay once per system
            // from the first scripting Random sample for the system seed.
            // Preserve the caller's global Random sequence while reproducing it.
            Random.State savedState = Random.state;
            try
            {
                Random.InitState(unchecked((int)randomSeed));
                return Random.value;
            }
            finally
            {
                Random.state = savedState;
            }
        }

        public static bool TryGetColorRange(
            ParticleSystem.MinMaxGradient gradient,
            out Color minimum,
            out Color maximum)
        {
            switch (gradient.mode)
            {
                case ParticleSystemGradientMode.Color:
                    minimum = gradient.color;
                    maximum = gradient.color;
                    return true;

                case ParticleSystemGradientMode.TwoColors:
                    minimum = gradient.colorMin;
                    maximum = gradient.colorMax;
                    return true;

                default:
                    Gradient fallback = gradient.gradientMax ?? gradient.gradient;
                    maximum = fallback != null ? fallback.Evaluate(0f) : Color.white;
                    minimum = maximum;
                    return false;
            }
        }
    }
}
