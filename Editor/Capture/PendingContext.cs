using System;
using ClarityConsole.Core;

namespace ClarityConsole.Capture
{
    /// <summary>
    /// Thread-static hand-off between <see cref="LogHandlerWrapper"/> and the threaded log callback.
    /// The wrapper stores the context object of the message it is about to forward. Unity raises
    /// <c>Application.logMessageReceivedThreaded</c> synchronously on the same thread while forwarding,
    /// so the callback reads the slot without guessing which message it belongs to.
    /// </summary>
    internal static class PendingContext
    {
        [ThreadStatic]
        private static ObjectRef _pending;

        public static void Set(ObjectRef context) => _pending = context;

        public static ObjectRef Peek() => _pending;

        public static void Clear() => _pending = ObjectRef.None;
    }
}
