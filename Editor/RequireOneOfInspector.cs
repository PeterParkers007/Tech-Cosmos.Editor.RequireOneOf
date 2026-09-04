using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace TechCosmos.RequireOneOf.Editor
{
    /// <summary>Inspector 点选切换。自定义 Editor 在 OnInspectorGUI 开头调 <see cref="Draw"/>。</summary>
    [InitializeOnLoad]
    public static class RequireOneOfInspector
    {
        static RequireOneOfInspector()
        {
            UnityEditor.Editor.finishedDefaultHeaderGUI += DrawEditor;
            TryHookCustomInspector();
        }

        public static void Draw(UnityEngine.Object obj)
        {
            var component = obj as Component;
            if (component == null)
                return;

            var groups = RequireOneOfEnforcer.GroupsFor(component);
            if (groups.Count == 0)
            {
                DrawHost(component);
                return;
            }

            for (int g = 0; g < groups.Count; g++)
                DrawGroup(component, groups[g]);
        }

        static void DrawEditor(UnityEditor.Editor editor)
        {
            if (editor != null)
                Draw(editor.target);
        }

        static void DrawHost(Component component)
        {
            var attrs = (RequireOneOfAttribute[])component.GetType()
                .GetCustomAttributes(typeof(RequireOneOfAttribute), true);
            for (int i = 0; i < attrs.Length; i++)
                DrawGroup(component, attrs[i].Types);
        }

        static void DrawGroup(Component component, Type[] types)
        {
            if (types == null || types.Length < 2)
                return;

            int current = RequireOneOfEnforcer.IndexOf(types, component);
            if (current < 0)
            {
                for (int i = 0; i < types.Length; i++)
                {
                    if (types[i] == null)
                        continue;
                    if (component.gameObject.GetComponent(types[i]) != null)
                    {
                        current = i;
                        break;
                    }
                }
            }

            if (current < 0)
                return;

            string[] labels = new string[types.Length];
            for (int i = 0; i < types.Length; i++)
                labels[i] = types[i] == null ? "?" : ObjectNames.NicifyVariableName(types[i].Name);

            EditorGUILayout.Space(2f);
            EditorGUI.BeginChangeCheck();
            int next = GUILayout.Toolbar(current, labels);
            if (!EditorGUI.EndChangeCheck() || next == current || types[next] == null)
                return;

            var go = component.gameObject;
            Type to = types[next];
            Type[] group = types;
            EditorApplication.delayCall += () =>
            {
                if (go != null)
                    RequireOneOfEnforcer.SwitchTo(go, group, to);
            };
        }

        static void TryHookCustomInspector()
        {
            const string typeName = "TechCosmos.Spatial2D.Unity.Editor.InspectorAboveDraw";
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                Type hookType;
                try
                {
                    hookType = assemblies[i].GetType(typeName);
                }
                catch
                {
                    continue;
                }

                if (hookType == null)
                    continue;

                var field = hookType.GetField("Draw", BindingFlags.Public | BindingFlags.Static);
                if (field == null || field.FieldType != typeof(Action<UnityEngine.Object>))
                    return;

                var current = (Action<UnityEngine.Object>)field.GetValue(null);
                field.SetValue(null, current + new Action<UnityEngine.Object>(Draw));
                return;
            }
        }
    }
}
