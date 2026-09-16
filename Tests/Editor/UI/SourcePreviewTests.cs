using System.Linq;
using ClarityConsole.Core;
using ClarityConsole.UI;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace ClarityConsole.Tests.UI
{
    internal sealed class SourcePreviewTests
    {
        [Test]
        public void Show_DrawsNumberedLines_AndHighlightsTheHit()
        {
            var preview = new SourcePreview();
            var snippet = new SourceSnippet("D:/proj/Assets/Foo.cs", 9, new[] { "if (a)", "{", "    Boom();", "}" }, 2);

            preview.Show(snippet, "Assets/Foo.cs");

            Label[] lines = preview.Query<Label>(className: SourcePreview.LineClass).ToList().ToArray();
            Assert.That(lines.Length, Is.EqualTo(4));
            Assert.That(lines[0].text, Is.EqualTo(" 9  if (a)"));
            Assert.That(lines[2].text, Is.EqualTo("11      Boom();"));
            Assert.That(lines[2].ClassListContains(SourcePreview.HighlightClass), Is.True);
            Assert.That(lines[0].ClassListContains(SourcePreview.HighlightClass), Is.False);
            Assert.That(preview.Q<Label>(className: SourcePreview.HeaderClass).text, Is.EqualTo("Assets/Foo.cs:11"));
            Assert.That(preview.style.display.value, Is.EqualTo(DisplayStyle.Flex));
        }

        [Test]
        public void Show_ExpandsTabs_AndDropsTheIndentationEveryLineShares()
        {
            var preview = new SourcePreview();

            preview.Show(new SourceSnippet("a.cs", 1, new[] { "\tif (a)", "\t\t    Boom();", "", "\t}" }, 1), "a.cs");

            string[] lines = preview.Query<Label>(className: SourcePreview.LineClass).ToList().Select(l => l.text).ToArray();
            Assert.That(lines[0], Is.EqualTo("1  if (a)"), "the shared tab is gone");
            Assert.That(lines[1], Is.EqualTo("2          Boom();"), "the extra tab and spaces are kept, so nesting still reads");
            Assert.That(lines[2], Is.EqualTo("3  "), "a blank line stays blank and does not count as indentation");
            Assert.That(lines[3], Is.EqualTo("4  }"));
        }

        [TestCase(new[] { "    a", "      b" }, 4)]
        [TestCase(new[] { "a", "    b" }, 0)]
        [TestCase(new[] { "        ", "    x" }, 4)]
        [TestCase(new[] { "", "   " }, 0)]
        [TestCase(new string[0], 0)]
        public void CommonIndent_IsTheSmallestIndentAmongLinesWithText(string[] lines, int expected)
        {
            Assert.That(SourcePreview.CommonIndent(lines), Is.EqualTo(expected));
        }

        [Test]
        public void Hide_RemovesEveryLineAndCollapses()
        {
            var preview = new SourcePreview();
            preview.Show(new SourceSnippet("a.cs", 1, new[] { "one" }, 0), "a.cs");

            preview.Hide();

            Assert.That(preview.LineCount, Is.EqualTo(0));
            Assert.That(preview.style.display.value, Is.EqualTo(DisplayStyle.None));
        }
    }
}
