using System;
using System.Collections.Generic;
using ClarityConsole.Capture;
using ClarityConsole.Core;
using NUnit.Framework;

namespace ClarityConsole.Tests.Capture
{
    internal sealed class ClearPolicyTests
    {
        private LogStore _store;
        private HashSet<ClearTrigger> _enabled;
        private bool _errorPause;
        private int _pauseRequests;
        private ClearPolicy _policy;

        [SetUp]
        public void SetUp()
        {
            _store = new LogStore(capacity: 16);
            _enabled = new HashSet<ClearTrigger>();
            _errorPause = false;
            _pauseRequests = 0;
            _policy = new ClearPolicy(_store, trigger => _enabled.Contains(trigger), () => _errorPause);
            _policy.PauseRequested += () => _pauseRequests++;
        }

        [TestCase(ClearTrigger.EnteringPlayMode)]
        [TestCase(ClearTrigger.CompilationStarted)]
        [TestCase(ClearTrigger.BuildStarted)]
        public void Handle_ClearsOnlyWhenTheTriggerIsEnabled(ClearTrigger trigger)
        {
            _store.Append(Entry(LogSeverity.Log));

            Assert.That(_policy.Handle(trigger), Is.False);
            Assert.That(_store.Count, Is.EqualTo(1));

            _enabled.Add(trigger);
            Assert.That(_policy.Handle(trigger), Is.True);
            Assert.That(_store.Count, Is.EqualTo(0));
        }

        [Test]
        public void Handle_OneTriggerDoesNotImplyAnother()
        {
            _enabled.Add(ClearTrigger.EnteringPlayMode);
            _store.Append(Entry(LogSeverity.Log));

            Assert.That(_policy.Handle(ClearTrigger.CompilationStarted), Is.False);
            Assert.That(_store.Count, Is.EqualTo(1));
        }

        [TestCase(LogSeverity.Error)]
        [TestCase(LogSeverity.Exception)]
        [TestCase(LogSeverity.Assert)]
        public void Inspect_RequestsAPauseForEachFailingSeverity(LogSeverity severity)
        {
            _errorPause = true;

            Assert.That(_policy.Inspect(Entry(severity), isPlaying: true), Is.True);
            Assert.That(_pauseRequests, Is.EqualTo(1));
        }

        [TestCase(LogSeverity.Log)]
        [TestCase(LogSeverity.Warning)]
        public void Inspect_IgnoresEverythingBelowAnError(LogSeverity severity)
        {
            _errorPause = true;

            Assert.That(_policy.Inspect(Entry(severity), isPlaying: true), Is.False);
            Assert.That(_pauseRequests, Is.EqualTo(0));
        }

        [Test]
        public void Inspect_DoesNothingWhenDisabled_OrOutsidePlayMode_OrForMarkers()
        {
            _errorPause = false;
            Assert.That(_policy.Inspect(Entry(LogSeverity.Error), isPlaying: true), Is.False, "disabled");

            _errorPause = true;
            Assert.That(_policy.Inspect(Entry(LogSeverity.Error), isPlaying: false), Is.False, "nothing to pause in Edit mode");
            Assert.That(_policy.Inspect(LogEntry.Marker("Entered Play mode", DateTime.UtcNow, 0), isPlaying: true), Is.False, "markers");
            Assert.That(_policy.Inspect(null, isPlaying: true), Is.False);
            Assert.That(_pauseRequests, Is.EqualTo(0));
        }

        [Test]
        public void Inspect_AsksEveryTime_SoPausingAgainAfterResumingWorks()
        {
            _errorPause = true;

            _policy.Inspect(Entry(LogSeverity.Error), isPlaying: true);
            _policy.Inspect(Entry(LogSeverity.Error), isPlaying: true);

            Assert.That(_pauseRequests, Is.EqualTo(2));
        }

        [Test]
        public void Constructor_RejectsMissingCollaborators()
        {
            Assert.Throws<ArgumentNullException>(() => new ClearPolicy(null, _ => true, () => true));
            Assert.Throws<ArgumentNullException>(() => new ClearPolicy(_store, null, () => true));
            Assert.Throws<ArgumentNullException>(() => new ClearPolicy(_store, _ => true, null));
        }

        private static LogEntry Entry(LogSeverity severity)
        {
            return new LogEntry(LogEntryKind.Log, severity, "message", string.Empty, DateTime.UtcNow, 0, 1, true, ObjectRef.None);
        }
    }
}
