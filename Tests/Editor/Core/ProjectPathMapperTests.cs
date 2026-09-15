using System.Collections.Generic;
using ClarityConsole.Core;
using NUnit.Framework;

namespace ClarityConsole.Tests.Core
{
    internal sealed class ProjectPathMapperTests
    {
        private static ProjectPathMapper Mapper()
        {
            return new ProjectPathMapper(@"D:\Games\Zombieland", new[]
            {
                new KeyValuePair<string, string>(@"D:\Github\clarity\unity-clarity-console", "com.alvaris.clarity-console"),
                new KeyValuePair<string, string>("D:/Games/Zombieland/Library/PackageCache/com.unity.ugui@2.0.0", "com.unity.ugui"),
            });
        }

        [TestCase("Assets/Game/Foo.cs", "Assets/Game/Foo.cs")]
        [TestCase(@"Assets\Game\Foo.cs", "Assets/Game/Foo.cs")]
        [TestCase("./Packages/com.x/Runtime/X.cs", "Packages/com.x/Runtime/X.cs")]
        public void ProjectRelativePaths_PassThroughNormalized(string input, string expected)
        {
            Assert.That(Mapper().TryMapToAssetPath(input, out string assetPath), Is.True);
            Assert.That(assetPath, Is.EqualTo(expected));
        }

        [Test]
        public void AbsolutePathUnderProject_BecomesRelative_CaseInsensitively()
        {
            Assert.That(Mapper().TryMapToAssetPath(@"d:\games\ZOMBIELAND\Assets\Game\Foo.cs", out string assetPath), Is.True);
            Assert.That(assetPath, Is.EqualTo("Assets/Game/Foo.cs"));
        }

        [TestCase("Library/PackageCache/com.cysharp.unitask@2.5.0/Runtime/UniTask.cs")]
        [TestCase(@"D:\Games\Zombieland\Library\PackageCache\com.cysharp.unitask@2.5.0\Runtime\UniTask.cs")]
        public void PackageCachePaths_MapToPackageName_WithoutVersion(string input)
        {
            Assert.That(Mapper().TryMapToAssetPath(input, out string assetPath), Is.True);
            Assert.That(assetPath, Is.EqualTo("Packages/com.cysharp.unitask/Runtime/UniTask.cs"));
        }

        [Test]
        public void PackageCachePath_WithHashInsteadOfVersion_StillMaps()
        {
            Assert.That(Mapper().TryMapToAssetPath("Library/PackageCache/com.unity.ugui@a1b2c3d4e5/Runtime/UI.cs", out string assetPath), Is.True);
            Assert.That(assetPath, Is.EqualTo("Packages/com.unity.ugui/Runtime/UI.cs"));
        }

        [Test]
        public void AbsolutePathUnderRegisteredPackageRoot_MapsToPackagesFolder()
        {
            Assert.That(Mapper().TryMapToAssetPath(@"D:\Github\clarity\unity-clarity-console\Editor\Capture\LogCapture.cs", out string assetPath), Is.True);
            Assert.That(assetPath, Is.EqualTo("Packages/com.alvaris.clarity-console/Editor/Capture/LogCapture.cs"));
        }

        [Test]
        public void UnrelatedAbsolutePath_IsNotMapped()
        {
            Assert.That(Mapper().TryMapToAssetPath(@"C:\Program Files\Unity\Editor\Data\Foo.cs", out string assetPath), Is.False);
            Assert.That(assetPath, Is.Null);
        }

        [Test]
        public void ProjectRootPrefix_MustEndAtASeparator()
        {
            Assert.That(Mapper().TryMapToAssetPath("D:/Games/Zombieland2/Assets/Foo.cs", out _), Is.False);
        }
    }
}
