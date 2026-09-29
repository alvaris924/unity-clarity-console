using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using ClarityConsole.Core;
using ClarityConsole.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ClarityConsole.Tests.UI
{
    internal sealed class ContextActionsTests
    {
        private ContextActionsFixture _fixture;

        [SetUp]
        public void SetUp()
        {
            _fixture = ScriptableObject.CreateInstance<ContextActionsFixture>();
            _fixture.name = "Shop";
        }

        [TearDown]
        public void TearDown()
        {
            if (_fixture != null)
            {
                Object.DestroyImmediate(_fixture);
            }
        }

        [Test]
        public void For_ListsParameterlessContextMenuItems_ByPriority_AndSkipsValidators()
        {
            List<ContextAction> actions = ContextActions.For(_fixture);

            // Priorities -10 and 50 come first; the two left at Unity's default priority follow.
            Assert.That(actions.Take(2).Select(a => a.MenuItem), Is.EqualTo(new[] { "Reset first", "Waves/Skip wave" }));
            Assert.That(actions.Skip(2).Select(a => a.MenuItem), Is.EquivalentTo(new[] { "Give 100 gold", "Break" }));
            Assert.That(actions.Any(a => a.MenuItem == "With argument"), Is.False, "the Inspector cannot call it either");
            Assert.That(actions.All(a => ReferenceEquals(a.Target, _fixture)), Is.True);
            Assert.That(actions.Single(a => a.MenuItem == "Give 100 gold").Path, Is.EqualTo("ContextActionsFixture/Give 100 gold"));
        }

        [Test]
        public void Run_CallsTheMethod_EvenAPrivateOne()
        {
            ContextAction give = ContextActions.For(_fixture).Single(a => a.MenuItem == "Give 100 gold");

            Assert.That(give.Run(), Is.True);
            Assert.That(give.Run(), Is.True);
            Assert.That(_fixture.Gold, Is.EqualTo(200));
        }

        [Test]
        public void IsEnabled_FollowsTheScriptsOwnValidateFunction()
        {
            ContextAction skip = ContextActions.For(_fixture).Single(a => a.MenuItem == "Waves/Skip wave");

            _fixture.CanSkip = false;
            Assert.That(skip.IsEnabled(), Is.False);
            Assert.That(skip.Run(), Is.False, "a greyed-out item does not run");

            _fixture.CanSkip = true;
            Assert.That(skip.IsEnabled(), Is.True);
        }

        [Test]
        public void Run_LogsWhatTheMethodThrows_InsteadOfLettingItEscape()
        {
            ContextAction broken = ContextActions.For(_fixture).Single(a => a.MenuItem == "Break");

            LogAssert.Expect(LogType.Exception, new Regex("broken on purpose"));
            Assert.That(broken.Run(), Is.False);
        }

        [Test]
        public void An_ActionWhoseTargetIsGone_IsDisabled()
        {
            ContextAction give = ContextActions.For(_fixture).First();
            Object.DestroyImmediate(_fixture);

            Assert.That(give.IsEnabled(), Is.False);
            Assert.That(give.Run(), Is.False);
        }

        [Test]
        public void For_NothingOrABuiltInObject_HasNoActions()
        {
            Assert.That(ContextActions.For(null), Is.Empty);

            var go = new GameObject("Plain");
            try
            {
                Assert.That(ContextActions.For(go), Is.Empty, "a GameObject with only a Transform has no scripts to ask");
                Assert.That(ContextActions.For(go.transform), Is.Empty);
                Assert.That(ContextActions.DisplayName(go.transform), Is.EqualTo("Plain"), "a component is named after its GameObject");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void Resolve_FindsTheLoggedObject_ByItsId()
        {
            Assert.That(ContextActions.Resolve(new ObjectRef(_fixture.GetInstanceID())), Is.SameAs(_fixture));
            Assert.That(ContextActions.Resolve(ObjectRef.None), Is.Null);
            Assert.That(ContextActions.DisplayName(_fixture), Is.EqualTo("Shop"));
        }

        [Test]
        public void MenuSafe_KeepsSlashesInNamesFromOpeningSubmenus()
        {
            Assert.That(ClarityConsoleWindow.MenuSafe("Enemy/Boss"), Does.Not.Contain("/"));
            Assert.That(ClarityConsoleWindow.MenuSafe(null), Is.Empty);
        }
    }
}
