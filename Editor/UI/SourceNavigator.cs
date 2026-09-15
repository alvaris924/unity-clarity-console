using System.Collections.Generic;
using System.IO;
using ClarityConsole.Core;
using Unity.CodeEditor;
using UnityEditor;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ClarityConsole.UI
{
    /// <summary>
    /// Opens stack frames in the user's code editor and pings context objects. Asset paths go through
    /// <see cref="AssetDatabase.OpenAsset(Object, int)"/> so the configured external editor and its
    /// line handling apply; files outside the asset database go to the code editor integration directly.
    /// </summary>
    internal sealed class SourceNavigator
    {
        private ProjectPathMapper _mapper;
        private string _projectRoot;

        public bool TryOpenEntryFrame(LogEntry entry)
        {
            TraceFrame frame = entry?.Trace.EntryFrame;
            return frame != null && TryOpen(frame);
        }

        public bool TryOpen(TraceFrame frame)
        {
            if (frame == null || !frame.HasLocation)
            {
                return false;
            }

            EnsureMapper();

            if (_mapper.TryMapToAssetPath(frame.FilePath, out string assetPath))
            {
                var asset = AssetDatabase.LoadAssetAtPath<Object>(assetPath);
                if (asset != null && AssetDatabase.OpenAsset(asset, frame.Line))
                {
                    return true;
                }
            }

            string fullPath = frame.FilePath;
            if (!Path.IsPathRooted(fullPath))
            {
                fullPath = Path.GetFullPath(Path.Combine(_projectRoot, fullPath));
            }

            if (!File.Exists(fullPath))
            {
                return false;
            }

            return CodeEditor.Editor.CurrentCodeEditor.OpenProject(fullPath, frame.Line, 0);
        }

        /// <summary>Highlights the entry's context object in the Hierarchy or Project window, if it still exists.</summary>
        public static void Ping(ObjectRef context)
        {
            if (context.HasValue)
            {
                EditorGUIUtility.PingObject(context.InstanceId);
            }
        }

        private void EnsureMapper()
        {
            if (_mapper != null)
            {
                return;
            }

            _projectRoot = Path.GetDirectoryName(Application.dataPath);
            var packageRoots = new List<KeyValuePair<string, string>>();
            foreach (PackageInfo package in PackageInfo.GetAllRegisteredPackages())
            {
                if (!string.IsNullOrEmpty(package.resolvedPath))
                {
                    packageRoots.Add(new KeyValuePair<string, string>(package.resolvedPath, package.name));
                }
            }

            _mapper = new ProjectPathMapper(_projectRoot, packageRoots);
        }
    }
}
