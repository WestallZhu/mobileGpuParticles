#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

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

        [MenuItem("Tools/GPU Particles/Convert All Particle Systems")]
        public static void ConvertAllParticleSystems()
        {
            Scene activeScene = SceneManager.GetActiveScene();
            if (activeScene == null || !activeScene.IsValid())
            {
                Debug.LogError("当前没有有效的活动场景！");
                return;
            }

            ParticleSystem[] allParticleSystems = Object.FindObjectsOfType<ParticleSystem>();
            
            if (allParticleSystems.Length == 0)
            {
                Debug.LogWarning("场景中没有找到ParticleSystem组件！");
                return;
            }

            int convertedCount = 0;
            foreach (var ps in allParticleSystems)
            {
                if (ps == null) continue;
                
                // 检查是否已经转换过（通过检查父节点下是否已有GPU版本）
                Transform parent = ps.transform;
                bool alreadyConverted = false;
                foreach (Transform child in parent)
                {
                    if (child.name.Contains("_GPU") && child.GetComponent<GPUParticleSystem>() != null)
                    {
                        alreadyConverted = true;
                        break;
                    }
                }

                if (!alreadyConverted)
                {
                    ShurikenConverter.ConvertToNewChild(ps);
                    convertedCount++;
                }
            }

            Debug.Log($"转换完成！共转换了 {convertedCount} 个粒子系统。");
            
            if (convertedCount > 0)
            {
                EditorSceneManager.MarkSceneDirty(activeScene);
            }
        }
    }
}
#endif
