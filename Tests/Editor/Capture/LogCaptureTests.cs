using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ClarityConsole.Capture;
using ClarityConsole.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace ClarityConsole.Tests.Capture
{
    /// <summary>
    /// Exercises the real Editor logging path. A second capture runs beside the bootstrap one; both see
    /// every message, each into its own store.
    /// </summary>
    public sealed class LogCaptureTests
    {
        private const string Tag = "[ClarityConsoleTest]";

        private LogStore _store;
        private LogCapture _capture;

        [SetUp]
        public void SetUp()
        {
            _store = new LogStore(capacity: 64);
            _capture = new LogCapture(_store);
            _capture.Start();
        }

        [TearDown]
        public void TearDown()
        {
            _capture.Stop();
        }

        [Test]
        public void Start_InstallsWrapper_AndStop_RestoresPreviousHandler()
        {
            ILogHandler during = Debug.unityLogger.logHandler;
            Assert.That(during, Is.TypeOf<LogHandlerWrapper>());
            ILogHandler inner = ((LogHandlerWrapper)during).Inner;

            _capture.Stop();

            Assert.That(Debug.unityLogger.logHandler, Is.SameAs(inner));
            Assert.That(_capture.HandlerChainBroken, Is.False);
        }

        [Test]
        public void Start_AppendsDomainLoadedMarkerFirst()
        {
            Assert.That(_store[0].Kind, Is.EqualTo(LogEntryKind.Marker));
            Assert.That(_store[0].Message, Is.EqualTo("Domain loaded"));
        }

        [Test]
        public void Log_IsCapturedWithSeverityThreadAndSequence()
        {
            Debug.Log($"{Tag} plain message");
            _capture.DrainAll();

            LogEntry entry = LastLog();
            Assert.That(entry.Message, Is.EqualTo($"{Tag} plain message"));
            Assert.That(entry.Severity, Is.EqualTo(LogSeverity.Log));
            Assert.That(entry.IsMainThread, Is.True);
            Assert.That(entry.Context.HasValue, Is.False);
            Assert.That(entry.Id, Is.GreaterThan(0));
            Assert.That(_capture.Pending, Is.EqualTo(0));
        }

        [Test]
        public void Log_WithContextObject_RecordsItsInstanceId()
        {
            var context = ScriptableObject.CreateInstance<ScriptableObject>();
            try
            {
                Debug.Log($"{Tag} with context", context);
                _capture.DrainAll();

                Assert.That(LastLog().Context.InstanceId, Is.EqualTo(context.GetInstanceID()));
            }
            finally
            {
                Object.DestroyImmediate(context);
            }
        }

        [Test]
        public void Log_WithoutContext_AfterOneWithContext_HasNoStaleContext()
        {
            var context = ScriptableObject.CreateInstance<ScriptableObject>();
            try
            {
                Debug.Log($"{Tag} with context", context);
                Debug.Log($"{Tag} without context");
                _capture.DrainAll();

                Assert.That(LastLog().Context.HasValue, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(context);
            }
        }

        [Test]
        public void WarningErrorAndException_MapToTheirSeverities()
        {
            LogAssert.Expect(LogType.Error, new Regex(Regex.Escape($"{Tag} error")));
            LogAssert.Expect(LogType.Exception, new Regex(Regex.Escape($"{Tag} boom")));

            Debug.LogWarning($"{Tag} warn");
            Debug.LogError($"{Tag} error");
            Debug.LogException(new InvalidOperationException($"{Tag} boom"));
            _capture.DrainAll();

            LogSeverity[] severities = _store.Entries
                .Where(e => e.Kind == LogEntryKind.Log && e.Message.Contains(Tag))
                .Select(e => e.Severity)
                .ToArray();
            Assert.That(severities, Is.EqualTo(new[] { LogSeverity.Warning, LogSeverity.Error, LogSeverity.Exception }));
        }

        [Test]
        public void Log_FromWorkerThread_IsQueuedAndFlaggedAsNotMainThread()
        {
            Task.Run(() => Debug.Log($"{Tag} from worker")).Wait();
            _capture.DrainAll();

            LogEntry entry = LastLog();
            Assert.That(entry.Message, Is.EqualTo($"{Tag} from worker"));
            Assert.That(entry.IsMainThread, Is.False);
        }

        [Test]
        public void Drain_StopsAfterBudget_AndFinishesOnTheNextCall()
        {
            for (int i = 0; i < 50; i++)
            {
                Debug.Log($"{Tag} burst {i}");
            }

            int first = _capture.Drain(TimeSpan.Zero);
            int rest = _capture.DrainAll();

            Assert.That(first, Is.EqualTo(1), "a zero budget still moves one entry per call");
            Assert.That(first + rest, Is.EqualTo(50));
            Assert.That(_capture.Pending, Is.EqualTo(0));
        }

        private LogEntry LastLog()
        {
            return _store.Entries.Last(e => e.Kind == LogEntryKind.Log);
        }
    }
}
