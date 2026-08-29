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
