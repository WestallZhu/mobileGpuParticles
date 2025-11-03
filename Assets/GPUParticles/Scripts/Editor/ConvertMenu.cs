#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace GPUParticles.Editor
{
    public static class ConvertMenu
    {
        [MenuItem("Tools/GPU Particles/Convert Selected Shuriken")]
        public static void ConvertSelected()
        {
            foreach (var obj in Selection.gameObjects)
            {
                var ps = obj.GetComponent<ParticleSystem>();
                if (ps == null) continue;
                ShurikenConverter.Convert(ps);
            }
        }
    }
}
#endif
