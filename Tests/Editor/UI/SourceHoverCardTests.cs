using System.Linq;
using ClarityConsole.Core;
using ClarityConsole.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace ClarityConsole.Tests.UI
{
    internal sealed class SourceHoverCardTests
    {
        [Test]
        public void Show_AttachesToTheHost_NamesTheFile_AndDrawsTheLinesWithTheHit()
        {
            var host = new VisualElement();
            var card = new SourceHoverCard();
            var snippet = new SourceSnippet("D:/proj/Assets/Game/Foo.cs", 10, new[] { "    a();", "    b();", "        c();" }, 1);

            card.Show(host, snippet, "Assets/Game/Foo.cs", new Vector2(40, 60));

            Assert.That(card.parent, Is.SameAs(host));
            Assert.That(card.IsShown, Is.True);
            Assert.That(card.HeaderText, Is.EqualTo("Assets/Game/Foo.cs:11"));
            Label[] lines = card.Query<Label>(className: SourcePreview.LineClass).ToList().ToArray();
            Assert.That(lines.Select(l => l.text), Is.EqualTo(new[] { "10  a();", "11  b();", "12      c();" }), "same numbering and dedent as the blocks");
            Assert.That(lines[1].ClassListContains(SourcePreview.HighlightClass), Is.True);
            Assert.That(card.style.left.value.value, Is.EqualTo(40 + SourceHoverCard.Offset), "starts at the pointer until measured");
        }

        [Test]
        public void Show_Null_Hides_AndShowAgainReusesTheCard()
        {
            var host = new VisualElement();
            var card = new SourceHoverCard();
            card.Show(host, new SourceSnippet("a.cs", 1, new[] { "x" }, 0), "a.cs", Vector2.zero);

            card.Show(host, null, null, Vector2.zero);
            Assert.That(card.IsShown, Is.False);

            card.Show(host, new SourceSnippet("b.cs", 5, new[] { "y", "z" }, 1), "b.cs", Vector2.zero);
            Assert.That(card.IsShown, Is.True);
            Assert.That(card.LineCount, Is.EqualTo(2));
            Assert.That(host.childCount, Is.EqualTo(1), "one card, re-shown, not a second one");
        }

        [TestCase(100, 100, 200, 80, 1000, 600, 114, 114, TestName = "Fit_BelowRight_WhenThereIsRoom")]
        [TestCase(900, 100, 200, 80, 1000, 600, 686, 114, TestName = "Fit_FlipsLeft_NearTheRightEdge")]
        [TestCase(100, 560, 200, 80, 1000, 600, 114, 466, TestName = "Fit_FlipsAbove_NearTheBottom")]
        [TestCase(20, 20, 200, 80, 100, 50, 0, 0, TestName = "Fit_ClampsToTheHost_WhenItFitsNeitherWay")]
        [TestCase(990, 590, 200, 80, 1000, 600, 776, 496, TestName = "Fit_FlipsBothWays_InTheCorner")]
        public void Fit(float ax, float ay, float w, float h, float hostW, float hostH, float expectedX, float expectedY)
        {
            Vector2 position = SourceHoverCard.Fit(new Vector2(ax, ay), new Vector2(w, h), new Vector2(hostW, hostH), 14);

            Assert.That(position.x, Is.EqualTo(expectedX).Within(0.01f));
            Assert.That(position.y, Is.EqualTo(expectedY).Within(0.01f));
        }
    }
}
