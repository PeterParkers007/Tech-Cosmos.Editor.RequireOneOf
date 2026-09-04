using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace TechCosmos.RequireOneOf.Editor
{
    [InitializeOnLoad]
    static class RequireOneOfEnforcer
    {
        static readonly Dictionary<Type, RequireOneOfAttribute[]> AttrCache = new Dictionary<Type, RequireOneOfAttribute[]>();
        static readonly HashSet<int> Scheduled = new HashSet<int>();
        static bool _busy;

        static RequireOneOfEnforcer()
        {
            ObjectFactory.componentWasAdded += OnComponentAdded;
            ObjectChangeEvents.changesPublished += OnChanges;
        }

        static void OnComponentAdded(Component component)
        {
            if (component == null)
                return;
            Enforce(component.gameObject, component);
        }

        static void OnChanges(ref ObjectChangeEventStream stream)
        {
            for (int i = 0; i < stream.length; i++)
            {
                if (stream.GetEventType(i) != ObjectChangeKind.ChangeGameObjectStructure)
                    continue;
                stream.GetChangeGameObjectStructureEvent(i, out var evt);
                Schedule(evt.instanceId);
            }
        }

        static void Schedule(int instanceId)
        {
            if (!_busy && Scheduled.Add(instanceId))
                EditorApplication.delayCall += Flush;
        }

        static void Flush()
        {
            if (Scheduled.Count == 0)
                return;

            int[] ids = new int[Scheduled.Count];
            Scheduled.CopyTo(ids);
            Scheduled.Clear();

            for (int i = 0; i < ids.Length; i++)
            {
                var go = EditorUtility.InstanceIDToObject(ids[i]) as GameObject;
                if (go != null)
                    Enforce(go, null);
            }
        }

        static void Enforce(GameObject go, Component preferredKeep)
        {
            if (go == null || _busy)
                return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
                return;
            if (!HasHost(go))
                return;

            _busy = true;
            try
            {
                EnforceExclusive(go, preferredKeep);
                EnforceMissing(go);
            }
            finally
            {
                _busy = false;
            }
        }

        static void EnforceExclusive(GameObject go, Component preferredKeep)
        {
            var hosts = go.GetComponents<MonoBehaviour>();
            for (int h = 0; h < hosts.Length; h++)
            {
                var host = hosts[h];
                if (host == null)
                    continue;

                var attrs = Attrs(host.GetType());
                for (int a = 0; a < attrs.Length; a++)
                {
                    var present = Collect(go, Resolve(attrs[a]));
                    if (present.Count <= 1)
                        continue;

                    Component keep = preferredKeep != null && present.Contains(preferredKeep)
                        ? preferredKeep
                        : present[present.Count - 1];

                    for (int i = 0; i < present.Count; i++)
                    {
                        var extra = present[i];
                        if (extra == null || extra == keep)
                            continue;
                        Undo.DestroyObjectImmediate(extra);
                    }
                }
            }
        }

        internal static List<Type[]> GroupsFor(Component member)
        {
            var list = new List<Type[]>();
            if (member == null)
                return list;

            var hosts = member.gameObject.GetComponents<MonoBehaviour>();
            for (int h = 0; h < hosts.Length; h++)
            {
                var host = hosts[h];
                if (host == null)
                    continue;

                var attrs = Attrs(host.GetType());
                for (int a = 0; a < attrs.Length; a++)
                {
                    var types = Resolve(attrs[a]);
                    if (IndexOf(types, member) >= 0)
                        list.Add(types);
                }
            }

            return list;
        }

        internal static Type[] Resolve(RequireOneOfAttribute attr)
        {
            if (attr == null || attr.Types == null || attr.Types.Length == 0)
                return Array.Empty<Type>();
            if (attr.FromBase)
                return Expand(attr.Types[0]);
            return attr.Types;
        }

        static readonly Dictionary<Type, Type[]> ExpandCache = new Dictionary<Type, Type[]>();

        static Type[] Expand(Type baseType)
        {
            if (baseType == null)
                return Array.Empty<Type>();
            if (ExpandCache.TryGetValue(baseType, out var cached))
                return cached;

            var list = new List<Type>();
            var derived = TypeCache.GetTypesDerivedFrom(baseType);
            for (int i = 0; i < derived.Count; i++)
            {
                Type t = derived[i];
                if (t == null || t.IsAbstract || t.IsInterface)
                    continue;
                if (!typeof(Component).IsAssignableFrom(t))
                    continue;
                list.Add(t);
            }

            list.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
            cached = list.ToArray();
            ExpandCache[baseType] = cached;
            return cached;
        }

        internal static int IndexOf(Type[] types, Component component)
        {
            if (types == null || component == null)
                return -1;
            for (int i = 0; i < types.Length; i++)
            {
                if (types[i] != null && types[i].IsInstanceOfType(component))
                    return i;
            }

            return -1;
        }

        internal static void SwitchTo(GameObject go, Type[] group, Type to)
        {
            if (go == null || group == null || to == null || to.IsAbstract || _busy)
                return;
            if (!typeof(Component).IsAssignableFrom(to))
                return;

            var present = Collect(go, group);
            Component current = present.Count > 0 ? present[0] : null;
            if (current != null && to.IsInstanceOfType(current))
                return;

            _busy = true;
            try
            {
                Undo.IncrementCurrentGroup();
                int undo = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName("切换互斥组件");

                var added = Undo.AddComponent(go, to);
                if (added != null && current != null)
                    Transfer(current, added);

                for (int i = 0; i < present.Count; i++)
                {
                    var extra = present[i];
                    if (extra == null || extra == added)
                        continue;
                    Undo.DestroyObjectImmediate(extra);
                }

                Undo.CollapseUndoOperations(undo);
            }
            finally
            {
                _busy = false;
            }
        }

        static void Transfer(Component from, Component to)
        {
            var fromSo = new SerializedObject(from);
            var toSo = new SerializedObject(to);
            var it = fromSo.GetIterator();
            bool enterChildren = true;
            while (it.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (it.name == "m_Script")
                    continue;
                var dst = toSo.FindProperty(it.propertyPath);
                if (dst == null || dst.propertyType != it.propertyType)
                    continue;
                toSo.CopyFromSerializedProperty(it);
            }

            toSo.ApplyModifiedProperties();
        }

        static void EnforceMissing(GameObject go)
        {
            var hosts = go.GetComponents<MonoBehaviour>();
            for (int h = 0; h < hosts.Length; h++)
            {
                var host = hosts[h];
                if (host == null)
                    continue;

                var attrs = Attrs(host.GetType());
                for (int a = 0; a < attrs.Length; a++)
                {
                    var types = Resolve(attrs[a]);
                    if (Collect(go, types).Count > 0)
                        continue;

                    Type add = FirstAddable(types);
                    if (add == null)
                        continue;
                    if (Undo.AddComponent(go, add) == null)
                        Debug.LogWarning("[RequireOneOf] 无法添加 " + add.Name + "，已跳过。", go);
                }
            }
        }

        static List<Component> Collect(GameObject go, Type[] types)
        {
            var list = new List<Component>(4);
            if (types == null)
                return list;

            for (int i = 0; i < types.Length; i++)
            {
                Type t = types[i];
                if (t == null || !typeof(Component).IsAssignableFrom(t))
                    continue;

                var found = go.GetComponents(t);
                for (int c = 0; c < found.Length; c++)
                {
                    var comp = found[c];
                    if (comp != null && !list.Contains(comp))
                        list.Add(comp);
                }
            }

            return list;
        }

        static bool _hostTypesReady;
        static HashSet<Type> _hostTypes;

        static bool HasHost(GameObject go)
        {
            EnsureHostTypes();
            if (_hostTypes.Count == 0)
                return false;

            var hosts = go.GetComponents<MonoBehaviour>();
            for (int i = 0; i < hosts.Length; i++)
            {
                var host = hosts[i];
                if (host != null && IsHostType(host.GetType()))
                    return true;
            }

            return false;
        }

        static bool IsHostType(Type type)
        {
            while (type != null && type != typeof(MonoBehaviour) && type != typeof(object))
            {
                if (_hostTypes.Contains(type))
                    return true;
                type = type.BaseType;
            }

            return false;
        }

        static void EnsureHostTypes()
        {
            if (_hostTypesReady)
                return;
            _hostTypesReady = true;
            _hostTypes = new HashSet<Type>();
            var found = TypeCache.GetTypesWithAttribute<RequireOneOfAttribute>();
            for (int i = 0; i < found.Count; i++)
                _hostTypes.Add(found[i]);
        }

        static Type FirstAddable(Type[] types)
        {
            if (types == null)
                return null;

            for (int i = 0; i < types.Length; i++)
            {
                Type t = types[i];
                if (t == null || t.IsAbstract || t.IsInterface)
                    continue;
                if (typeof(Component).IsAssignableFrom(t))
                    return t;
            }

            return null;
        }

        static RequireOneOfAttribute[] Attrs(Type type)
        {
            if (type == null)
                return Array.Empty<RequireOneOfAttribute>();
            if (AttrCache.TryGetValue(type, out var cached))
                return cached;

            var found = (RequireOneOfAttribute[])type.GetCustomAttributes(typeof(RequireOneOfAttribute), true);
            AttrCache[type] = found;
            return found;
        }
    }
}
