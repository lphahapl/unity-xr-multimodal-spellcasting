using System.Reflection;
using NarutoVR.SceneArt;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace NarutoVR.Editor
{
    public abstract class TravelInspector : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();
            var property = serializedObject.GetIterator();
            bool enterChildren = true;
            while (property.NextVisible(enterChildren))
            {
                enterChildren = false;
                bool isScript = property.propertyPath == "m_Script";
                var info = target.GetType().GetField(property.name,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                string label = isScript ? "脚本" : info?.GetCustomAttribute<InspectorNameAttribute>()?.displayName ?? property.displayName;
                var field = new PropertyField(property.Copy(), label) { tooltip = property.tooltip };
                field.SetEnabled(!isScript);
                root.Add(field);
            }
            return root;
        }
    }

    [CustomEditor(typeof(StoryCameraSequence)), CanEditMultipleObjects]
    public sealed class StoryCameraSequenceInspector : TravelInspector { }

    [CustomEditor(typeof(SceneTravelGate)), CanEditMultipleObjects]
    public sealed class SceneTravelGateInspector : TravelInspector { }

    [CustomEditor(typeof(SceneLoadingOverlay)), CanEditMultipleObjects]
    public sealed class SceneLoadingOverlayInspector : TravelInspector { }

    [CustomEditor(typeof(SceneLoadingProgressBar)), CanEditMultipleObjects]
    public sealed class SceneLoadingProgressBarInspector : TravelInspector { }
}
