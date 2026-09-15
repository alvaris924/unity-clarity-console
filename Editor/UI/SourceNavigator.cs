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
    /// Opens stack frames in the user's code editor, resolves them to files on disk for the preview, and
    /// pings context objects. Asset paths go through <see cref="AssetDatabase.OpenAsset(Object, int)"/> so
    /// the configured external editor and its line handling apply; files outside the asset database go to
    /// the code editor integration directly.
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

            return TryResolveAbsolutePath(frame, out string fullPath)
                && CodeEditor.Editor.CurrentCodeEditor.OpenProject(fullPath, frame.Line, 0);
        }

        /// <summary>
        /// Resolves a frame to a file that exists on disk: an absolute path as printed, a path relative to
        /// the project, or a package path resolved through the Package Manager.
        /// </summary>
        public bool TryResolveAbsolutePath(TraceFrame frame, out string absolutePath)
        {
            absolutePath = null;
            if (frame == null || frame.FilePath.Length == 0)
            {
                return false;
            }

            EnsureMapper();

            if (Path.IsPathRooted(frame.FilePath))
            {
                return Exists(frame.FilePath, out absolutePath);
            }

            if (Exists(Path.Combine(_projectRoot, frame.FilePath), out absolutePath))
            {
                return true;
            }

            if (_mapper.TryMapToAssetPath(frame.FilePath, out string assetPath))
            {
                if (Exists(Path.Combine(_projectRoot, assetPath), out absolutePath))
                {
                    return true;
                }

                PackageInfo package = PackageInfo.FindForAssetPath(assetPath);
                if (package != null && !string.IsNullOrEmpty(package.resolvedPath))
                {
                    // assetPath is "Packages/<name>/<rest>"; resolvedPath already points at "<name>".
                    int slash = assetPath.IndexOf('/', "Packages/".Length);
                    if (slash > 0 && Exists(Path.Combine(package.resolvedPath, assetPath.Substring(slash + 1)), out absolutePath))
                    {
                        return true;
                    }
                }
            }

            absolutePath = null;
            return false;
        }

        /// <summary>Highlights the entry's context object in the Hierarchy or Project window, if it still exists.</summary>
        public static void Ping(ObjectRef context)
        {
            if (context.HasValue)
            {
                EditorGUIUtility.PingObject(context.InstanceId);
            }
        }

        private static bool Exists(string candidate, out string absolutePath)
        {
            try
            {
                string full = Path.GetFullPath(candidate);
                if (File.Exists(full))
                {
                    absolutePath = full;
                    return true;
                }
            }
            catch (System.Exception ex) when (ex is System.ArgumentException || ex is IOException || ex is System.NotSupportedException)
            {
                // A path Unity printed but this platform cannot express: treat it as unresolvable.
            }

            absolutePath = null;
            return false;
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
