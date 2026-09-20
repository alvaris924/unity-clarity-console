using System.Collections.Generic;
using System.Reflection;
using ClarityConsole.Settings;

namespace ClarityConsole.Tests.Settings
{
    /// <summary>
    /// Preferences live in EditorPrefs, which every Unity project on the machine shares, so a test that
    /// resets or changes one is editing the developer's real console setup in whatever project they have
    /// open. Captures every settable preference by reflection, so a preference added later is covered
    /// without touching this class, and puts them all back on restore.
    /// </summary>
    internal sealed class PreferenceSnapshot
    {
        private readonly List<KeyValuePair<PropertyInfo, object>> _values = new List<KeyValuePair<PropertyInfo, object>>();

        private PreferenceSnapshot()
        {
        }

        /// <summary>The names of the preferences this snapshot holds.</summary>
        public IEnumerable<string> Names
        {
            get
            {
                foreach (KeyValuePair<PropertyInfo, object> value in _values)
                {
                    yield return value.Key.Name;
                }
            }
        }

        public static PreferenceSnapshot Capture()
        {
            var snapshot = new PreferenceSnapshot();
            foreach (PropertyInfo property in Properties())
            {
                snapshot._values.Add(new KeyValuePair<PropertyInfo, object>(property, property.GetValue(null)));
            }

            return snapshot;
        }

        public void Restore()
        {
            foreach (KeyValuePair<PropertyInfo, object> value in _values)
            {
                value.Key.SetValue(null, value.Value);
            }
        }

        /// <summary>Every public static property of the preferences that can be read and written.</summary>
        public static IEnumerable<PropertyInfo> Properties()
        {
            foreach (PropertyInfo property in typeof(ConsolePreferences).GetProperties(BindingFlags.Public | BindingFlags.Static))
            {
                if (property.CanRead && property.CanWrite)
                {
                    yield return property;
                }
            }
        }
    }
}
