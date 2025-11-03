#if UNITY_EDITOR
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace GPUParticles.Editor
{
    [CustomEditor(typeof(GPUParticleSystem))]
    public class GPUParticleSystemEditor : UnityEditor.Editor
    {
        private static readonly FieldInfo PosLifeField =
            typeof(GPUParticleSystem).GetField("posLife", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo VelSizeField =
            typeof(GPUParticleSystem).GetField("velSize", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo ColorField =
            typeof(GPUParticleSystem).GetField("colorRT", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo PingField =
            typeof(GPUParticleSystem).GetField("ping", BindingFlags.NonPublic | BindingFlags.Instance);

        private bool showDebugTargets;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                showDebugTargets = EditorGUILayout.Foldout(showDebugTargets, "Debug Render Targets", true);
                if (!showDebugTargets) return;

                var system = (GPUParticleSystem)target;
                if (system == null)
                {
                    EditorGUILayout.HelpBox("No GPU particle system selected.", MessageType.Info);
                    return;
                }

                if (!TryFetchTextures(system, out var posLife, out var velSize, out var color))
                {
                    EditorGUILayout.HelpBox("Render textures not initialised yet. Enter Play Mode or trigger the system to allocate buffers.", MessageType.Info);
                }
                else
                {
                    DrawTexturePreview("Position / Lifetime", posLife);
                    DrawTexturePreview("Velocity / Size", velSize);
                    DrawTexturePreview("Color", color);
                }
            }

            if (showDebugTargets) Repaint();
        }

        private static bool TryFetchTextures(GPUParticleSystem system, out RenderTexture posLife, out RenderTexture velSize, out RenderTexture color)
        {
            posLife = velSize = color = null;

            var posLifeArr = PosLifeField?.GetValue(system) as RenderTexture[];
            var velSizeArr = VelSizeField?.GetValue(system) as RenderTexture[];
            var colorArr = ColorField?.GetValue(system) as RenderTexture[];
            var pingObj = PingField?.GetValue(system);

            if (posLifeArr == null || velSizeArr == null || colorArr == null || pingObj == null) return false;
            if (posLifeArr.Length == 0 || velSizeArr.Length == 0 || colorArr.Length == 0) return false;

            var ping = Mathf.Clamp((int)pingObj, 0, posLifeArr.Length - 1);

            posLife = posLifeArr[ping];
            velSize = velSizeArr[Mathf.Clamp(ping, 0, velSizeArr.Length - 1)];
            color = colorArr[Mathf.Clamp(ping, 0, colorArr.Length - 1)];

            return posLife != null && velSize != null && color != null;
        }

        private static void DrawTexturePreview(string label, Texture texture)
        {
            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
            if (texture == null)
            {
                EditorGUILayout.HelpBox("Texture not available.", MessageType.Warning);
                return;
            }

            const float previewHeight = 128f;
            var rect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.Height(previewHeight), GUILayout.ExpandWidth(true));
            EditorGUI.DrawPreviewTexture(rect, texture, null, ScaleMode.ScaleToFit);
        }
    }
}
#endif
