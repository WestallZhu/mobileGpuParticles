using System;
using UnityEngine;

namespace GPUParticles
{
    [Serializable]
    public sealed class GPUEmissionBurst
    {
        [Min(0f)] public float time;
        public ParticleSystemCurveMode countMode = ParticleSystemCurveMode.Constant;
        [Min(0f)] public float countMin = 30f;
        [Min(0f)] public float countMax = 30f;
        [Min(0f)] public float countCurveMultiplier = 1f;
        public AnimationCurve countCurveMin = AnimationCurve.Constant(0f, 1f, 30f);
        public AnimationCurve countCurveMax = AnimationCurve.Constant(0f, 1f, 30f);
        [Min(0)] public int cycleCount = 1;
        [Min(0f)] public float repeatInterval = 0.01f;
        [Range(0f, 1f)] public float probability = 1f;

        public static GPUEmissionBurst FromShuriken(ParticleSystem.Burst source)
        {
            ParticleSystem.MinMaxCurve count = source.count;
            return new GPUEmissionBurst
            {
                time = Mathf.Max(0f, source.time),
                countMode = count.mode,
                countMin = Mathf.Max(0f, count.mode == ParticleSystemCurveMode.Constant
                    ? count.constant
                    : count.constantMin),
                countMax = Mathf.Max(0f, count.mode == ParticleSystemCurveMode.Constant
                    ? count.constant
                    : count.constantMax),
                countCurveMultiplier = Mathf.Max(0f, count.curveMultiplier),
                countCurveMin = count.curveMin ?? count.curve,
                countCurveMax = count.curveMax ?? count.curve,
                cycleCount = Mathf.Max(0, source.cycleCount),
                repeatInterval = Mathf.Max(0f, source.repeatInterval),
                probability = Mathf.Clamp01(source.probability)
            };
        }

        internal float EvaluateCount(float normalizedSystemTime, float randomValue)
        {
            normalizedSystemTime = Mathf.Clamp01(normalizedSystemTime);
            randomValue = Mathf.Clamp01(randomValue);

            switch (countMode)
            {
                case ParticleSystemCurveMode.TwoConstants:
                    return Mathf.LerpUnclamped(countMin, countMax, randomValue);

                case ParticleSystemCurveMode.Curve:
                    return Mathf.Max(0f,
                        EvaluateCurve(countCurveMax, normalizedSystemTime, countMax) *
                        countCurveMultiplier);

                case ParticleSystemCurveMode.TwoCurves:
                    float minimum = EvaluateCurve(countCurveMin, normalizedSystemTime, countMin);
                    float maximum = EvaluateCurve(countCurveMax, normalizedSystemTime, countMax);
                    return Mathf.Max(0f,
                        Mathf.LerpUnclamped(minimum, maximum, randomValue) * countCurveMultiplier);

                default:
                    return Mathf.Max(0f, countMax);
            }
        }

        static float EvaluateCurve(AnimationCurve curve, float time, float fallback)
        {
            return curve != null ? curve.Evaluate(time) : fallback;
        }
    }
}
