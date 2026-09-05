using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace TechCosmos.RequireOneOf.Editor
{
    /// <summary>
    /// 点选切换。往每个 Inspector 块里插一排按钮，自定义 Editor 不用写一行。
    /// </summary>
    [InitializeOnLoad]
    public static class RequireOneOfInspector
    {
        const string ToolbarName = "TechCosmosRequireOneOfToolbar";
        const int RetryFrames = 30;

        static readonly Type InspectorWindowType =
            typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.InspectorWindow");
        static readonly Type PropertyEditorType =
            typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.PropertyEditor");

        static readonly PropertyInfo InspectorEditorProperty =
            typeof(InspectorElement).GetProperty("editor", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        static readonly FieldInfo InspectorEditorField =
            typeof(InspectorElement).GetField("m_Editor", BindingFlags.Instance | BindingFlags.NonPublic);

        static int _lastEditorStamp = int.MinValue;
        static int _retries;

        static RequireOneOfInspector()
        {
            Selection.selectionChanged += ScheduleInject;
            ObjectFactory.componentWasAdded += _ => ScheduleInject();
            EditorApplication.hierarchyChanged += ScheduleInject;
            EditorApplication.update += Tick;
            ScheduleInject();
        }

        static void ScheduleInject()
        {
            _retries = RetryFrames;
            EditorApplication.delayCall += InjectOpenInspectors;
        }

        static void Tick()
        {
            int stamp = EditorStamp();
            if (stamp != _lastEditorStamp)
            {
                _lastEditorStamp = stamp;
                _retries = RetryFrames;
            }

            if (_retries <= 0)
                return;
            _retries--;
            InjectOpenInspectors();
        }

        static int EditorStamp()
        {
            var tracker = ActiveEditorTracker.sharedTracker;
            if (tracker == null)
                return 0;

            var editors = tracker.activeEditors;
            if (editors == null)
                return 0;

            int stamp = editors.Length;
            for (int i = 0; i < editors.Length; i++)
            {
                var e = editors[i];
                stamp ^= e == null ? 0 : e.GetInstanceID();
                if (e != null && e.target != null)
                    stamp ^= e.target.GetInstanceID();
            }

            return stamp;
        }

        static void InjectOpenInspectors()
        {
            var windows = Resources.FindObjectsOfTypeAll<EditorWindow>();
            for (int i = 0; i < windows.Length; i++)
            {
                var window = windows[i];
                if (window == null)
                    continue;
                var type = window.GetType();
                if (type != InspectorWindowType && type != PropertyEditorType)
                    continue;
                InjectWindow(window);
            }
        }

        static void InjectWindow(EditorWindow window)
        {
            var root = window.rootVisualElement;
            if (root == null)
                return;

            var list = root.Query<InspectorElement>().ToList();
            for (int i = 0; i < list.Count; i++)
                EnsureToolbar(list[i]);
        }

        static void EnsureToolbar(InspectorElement element)
        {
            if (element == null)
                return;

            var editor = GetEditor(element);
            if (editor == null || editor.target == null || !(editor.target is Component))
                return;

            if (element.Q(ToolbarName) != null)
                return;

            var bar = new IMGUIContainer(() =>
            {
                var current = GetEditor(element);
                if (current == null || current.target == null)
                    return;
                Draw(current.target);
            })
            {
                name = ToolbarName
            };
            element.Insert(0, bar);
        }

        static UnityEditor.Editor GetEditor(InspectorElement element)
        {
            if (element == null)
                return null;

            if (InspectorEditorProperty != null)
            {
                var value = InspectorEditorProperty.GetValue(element);
                if (value is UnityEditor.Editor fromProp)
                    return fromProp;
            }

            if (InspectorEditorField != null)
                return InspectorEditorField.GetValue(element) as UnityEditor.Editor;
            return null;
        }

        /// <summary>一般不用调。Inspector 里已经会画。</summary>
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

        static void DrawHost(Component component)
        {
            var attrs = (RequireOneOfAttribute[])component.GetType()
                .GetCustomAttributes(typeof(RequireOneOfAttribute), true);
            for (int i = 0; i < attrs.Length; i++)
                DrawGroup(component, RequireOneOfEnforcer.Resolve(attrs[i]));
        }

        static void DrawGroup(Component component, Type[] types)
        {
            if (component == null || types == null || types.Length < 2)
                return;

            int current = RequireOneOfEnforcer.IndexOf(types, component);
            if (current < 0)
            {
                var go = component.gameObject;
                if (go == null)
                    return;

                for (int i = 0; i < types.Length; i++)
                {
                    if (types[i] == null)
                        continue;
                    if (go.GetComponent(types[i]) != null)
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

            var gameObject = component.gameObject;
            Type to = types[next];
            Type[] group = types;
            EditorApplication.delayCall += () =>
            {
                if (gameObject != null)
                    RequireOneOfEnforcer.SwitchTo(gameObject, group, to);
            };
        }
    }
}
