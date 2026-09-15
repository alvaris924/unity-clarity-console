using System;
using System.Collections.Generic;

namespace ClarityConsole.Core
{
    /// <summary>
    /// Turns the file paths found in stack traces into asset paths the Editor can open:
    /// project-relative paths pass through, absolute paths under the project are made relative,
    /// and package-cache or resolved-package paths become <c>Packages/&lt;name&gt;/...</c>.
    /// </summary>
    internal sealed class ProjectPathMapper
    {
        private const string PackageCachePrefix = "Library/PackageCache/";

        private readonly string _projectRoot;
        private readonly List<KeyValuePair<string, string>> _packageRoots;

        /// <param name="projectRoot">Absolute project folder, the parent of <c>Assets</c>.</param>
        /// <param name="packageRoots">Resolved absolute folder of each package mapped to its name.</param>
        public ProjectPathMapper(string projectRoot, IEnumerable<KeyValuePair<string, string>> packageRoots)
        {
            _projectRoot = StackTraceParser.NormalizePath(projectRoot).TrimEnd('/');
            _packageRoots = new List<KeyValuePair<string, string>>();
            foreach (KeyValuePair<string, string> pair in packageRoots)
            {
                string root = StackTraceParser.NormalizePath(pair.Key).TrimEnd('/');
                if (root.Length > 0)
                {
                    _packageRoots.Add(new KeyValuePair<string, string>(root, pair.Value));
                }
            }

            // Longest root first so a package nested inside another maps to the inner one.
            _packageRoots.Sort((a, b) => b.Key.Length.CompareTo(a.Key.Length));
        }

        public bool TryMapToAssetPath(string path, out string assetPath)
        {
            string normalized = StackTraceParser.NormalizePath(path ?? string.Empty);

            if (IsAssetPath(normalized))
            {
                assetPath = normalized;
                return true;
            }

            if (TryMapPackageCache(normalized, out assetPath))
            {
                return true;
            }

            if (TryStripRoot(normalized, _projectRoot, out string relative))
            {
                if (IsAssetPath(relative))
                {
                    assetPath = relative;
                    return true;
                }

                if (TryMapPackageCache(relative, out assetPath))
                {
                    return true;
                }
            }

            foreach (KeyValuePair<string, string> package in _packageRoots)
            {
                if (TryStripRoot(normalized, package.Key, out string inPackage))
                {
                    assetPath = "Packages/" + package.Value + "/" + inPackage;
                    return true;
                }
            }

            assetPath = null;
            return false;
        }

        private static bool IsAssetPath(string path)
        {
            return path.StartsWith("Assets/", StringComparison.Ordinal) || path.StartsWith("Packages/", StringComparison.Ordinal);
        }

        private static bool TryMapPackageCache(string relative, out string assetPath)
        {
            if (relative.StartsWith(PackageCachePrefix, StringComparison.Ordinal))
            {
                string rest = relative.Substring(PackageCachePrefix.Length);
                int slash = rest.IndexOf('/');
                if (slash > 0)
                {
                    string folder = rest.Substring(0, slash);
                    int at = folder.IndexOf('@');
                    string name = at > 0 ? folder.Substring(0, at) : folder;
                    assetPath = "Packages/" + name + rest.Substring(slash);
                    return true;
                }
            }

            assetPath = null;
            return false;
        }

        private static bool TryStripRoot(string path, string root, out string relative)
        {
            if (root.Length > 0
                && path.Length > root.Length + 1
                && path[root.Length] == '/'
                && path.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            {
                relative = path.Substring(root.Length + 1);
                return true;
            }

            relative = null;
            return false;
        }
    }
}
