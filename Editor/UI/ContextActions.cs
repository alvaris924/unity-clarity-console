using System;
using System.Collections.Generic;
using System.Reflection;
using ClarityConsole.Core;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ClarityConsole.UI
{
    /// <summary>
    /// One <c>[ContextMenu]</c> method on a script, ready to run from a console row: the debug cheats a
    /// project already writes for the Inspector ("Kill", "Give 100 gold", "Skip wave") become one click
    /// away from the message the object logged.
    /// </summary>
    internal sealed class ContextAction
    {
        private readonly MethodInfo _method;
        private readonly MethodInfo _validate;

        public ContextAction(Object target, string menuItem, int priority, MethodInfo method, MethodInfo validate)
        {
            Target = target;
            MenuItem = menuItem;
            Priority = priority;
            _method = method;
            _validate = validate;
        }

        /// <summary>The script instance the method runs on.</summary>
        public Object Target { get; }

        /// <summary>The name the Inspector shows, which may itself hold '/' for a submenu.</summary>
        public string MenuItem { get; }

        public int Priority { get; }

        /// <summary>"Enemy/Kill": the script's type, then its item, for the row's context menu.</summary>
        public string Path => Target.GetType().Name + "/" + MenuItem;

        /// <summary>
        /// False when the target is gone, or when the script's own validate function for this item says
        /// no, which is how the Inspector greys an item out.
        /// </summary>
        public bool IsEnabled()
        {
            if (Target == null)
            {
                return false;
            }

            if (_validate == null)
            {
                return true;
            }

            try
            {
                return _validate.Invoke(Target, null) is bool enabled && enabled;
            }
            catch (TargetInvocationException)
            {
                return false;
            }
        }

        /// <summary>
        /// Runs the method with an undo step, like the Inspector does. An exception it throws is logged
        /// against the target rather than escaping into the menu, so it lands in the console as a row.
        /// </summary>
        public bool Run()
        {
            if (!IsEnabled())
            {
                return false;
            }

            Undo.RecordObject(Target, MenuItem);
            try
            {
                _method.Invoke(Target, null);
            }
            catch (TargetInvocationException ex)
            {
                Debug.LogException(ex.InnerException ?? ex, Target);
                return false;
            }

            if (!EditorApplication.isPlaying)
            {
                EditorUtility.SetDirty(Target);
            }

            return true;
        }
    }

    /// <summary>
    /// Finds the <c>[ContextMenu]</c> methods reachable from a log entry's context object: the object's
    /// own when it is a script, and those of every script on the same GameObject, the logging script
    /// first. Reads the project's own types through reflection on a public attribute; no Editor
    /// internals are involved.
    /// </summary>
    internal static class ContextActions
    {
        private const BindingFlags Declared = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        /// <summary>The object an entry was logged with, if it still exists in this Editor session.</summary>
        public static Object Resolve(ObjectRef context)
        {
            return context.HasValue ? EditorUtility.InstanceIDToObject(context.InstanceId) : null;
        }

        /// <summary>The actions for an object, in the order the menu lists them. Empty for null or a built-in type.</summary>
        public static List<ContextAction> For(Object context)
        {
            var actions = new List<ContextAction>();
            if (context == null)
            {
                return actions;
            }

            foreach (Object target in TargetsOf(context))
            {
                AddActions(target, actions);
            }

            return actions;
        }

        /// <summary>The name the menu gives the object: its GameObject's name for a component.</summary>
        public static string DisplayName(Object context)
        {
            if (context == null)
            {
                return string.Empty;
            }

            string name = context is Component component ? component.gameObject.name : context.name;
            return string.IsNullOrEmpty(name) ? context.GetType().Name : name;
        }

        private static IEnumerable<Object> TargetsOf(Object context)
        {
            GameObject owner = context is Component component ? component.gameObject : context as GameObject;
            if (owner == null)
            {
                // A ScriptableObject or any other asset: only its own methods.
                yield return context;
                yield break;
            }

            if (context is MonoBehaviour first)
            {
                yield return first;
            }

            foreach (MonoBehaviour script in owner.GetComponents<MonoBehaviour>())
            {
                // A missing script shows up as a null entry.
                if (script != null && !ReferenceEquals(script, context))
                {
                    yield return script;
                }
            }
        }

        private static void AddActions(Object target, List<ContextAction> actions)
        {
            var items = new List<KeyValuePair<ContextMenu, MethodInfo>>();
            var validators = new Dictionary<string, MethodInfo>();
            var seen = new HashSet<string>();

            // Walk from the most derived type down, so an override is found once and wins by name.
            for (Type type = target.GetType(); type != null && type != typeof(MonoBehaviour) && type != typeof(ScriptableObject) && type != typeof(Object); type = type.BaseType)
            {
                foreach (MethodInfo method in type.GetMethods(Declared))
                {
                    if (method.GetParameters().Length != 0)
                    {
                        continue;
                    }

                    foreach (ContextMenu attribute in method.GetCustomAttributes<ContextMenu>(false))
                    {
                        if (string.IsNullOrEmpty(attribute.menuItem))
                        {
                            continue;
                        }

                        if (attribute.validate)
                        {
                            if (method.ReturnType == typeof(bool) && !validators.ContainsKey(attribute.menuItem))
                            {
                                validators[attribute.menuItem] = method;
                            }
                        }
                        else if (seen.Add(attribute.menuItem))
                        {
                            items.Add(new KeyValuePair<ContextMenu, MethodInfo>(attribute, method));
                        }
                    }
                }
            }

            // Ordered by priority like the Inspector; equal priorities keep the order they were found in.
            var order = new List<int>(items.Count);
            for (int i = 0; i < items.Count; i++)
            {
                order.Add(i);
            }

            order.Sort((a, b) =>
            {
                int byPriority = items[a].Key.priority.CompareTo(items[b].Key.priority);
                return byPriority != 0 ? byPriority : a.CompareTo(b);
            });

            foreach (int index in order)
            {
                ContextMenu attribute = items[index].Key;
                validators.TryGetValue(attribute.menuItem, out MethodInfo validate);
                actions.Add(new ContextAction(target, attribute.menuItem, attribute.priority, items[index].Value, validate));
            }
        }
    }
}
