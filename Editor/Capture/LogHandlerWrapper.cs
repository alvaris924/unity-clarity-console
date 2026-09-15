using System;
using ClarityConsole.Core;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ClarityConsole.Capture
{
    /// <summary>
    /// Wraps whichever <see cref="ILogHandler"/> was installed before us, records the context object of each
    /// message in <see cref="PendingContext"/>, and forwards the call unchanged. Chains cleanly with other
    /// wrappers because it never swallows a call and always clears the slot on the way out.
    /// </summary>
    internal sealed class LogHandlerWrapper : ILogHandler
    {
        public LogHandlerWrapper(ILogHandler inner)
        {
            Inner = inner ?? throw new ArgumentNullException(nameof(inner));
        }

        public ILogHandler Inner { get; }

        public void LogFormat(LogType logType, Object context, string format, params object[] args)
        {
            PendingContext.Set(ObjectRefFor(context));
            try
            {
                Inner.LogFormat(logType, context, format, args);
            }
            finally
            {
                PendingContext.Clear();
            }
        }

        public void LogException(Exception exception, Object context)
        {
            PendingContext.Set(ObjectRefFor(context));
            try
            {
                Inner.LogException(exception, context);
            }
            finally
            {
                PendingContext.Clear();
            }
        }

        private static ObjectRef ObjectRefFor(Object context)
        {
            // ReferenceEquals on purpose: Unity's overloaded == reports destroyed objects as null,
            // and the id of a destroyed object is still worth showing.
            return ReferenceEquals(context, null) ? ObjectRef.None : new ObjectRef(context.GetInstanceID());
        }
    }
}
