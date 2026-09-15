using System;
using ClarityConsole.Capture;
using ClarityConsole.Core;
using NUnit.Framework;
using UnityEngine;

namespace ClarityConsole.Tests.Capture
{
    internal sealed class SeverityMappingTests
    {
        [TestCase(LogType.Log, LogSeverity.Log)]
        [TestCase(LogType.Warning, LogSeverity.Warning)]
        [TestCase(LogType.Error, LogSeverity.Error)]
        [TestCase(LogType.Exception, LogSeverity.Exception)]
        [TestCase(LogType.Assert, LogSeverity.Assert)]
        public void FromLogType_MapsEachValue(LogType logType, LogSeverity expected)
        {
            Assert.That(SeverityMapping.FromLogType(logType), Is.EqualTo(expected));
        }

        [Test]
        public void FromLogType_CoversEveryLogType()
        {
            foreach (LogType logType in Enum.GetValues(typeof(LogType)))
            {
                Assert.DoesNotThrow(() => SeverityMapping.FromLogType(logType), logType.ToString());
            }
        }
    }
}
