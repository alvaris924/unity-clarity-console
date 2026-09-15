using System;
using System.IO;

namespace ClarityConsole.Core
{
    /// <summary>Serializes one <see cref="LogEntry"/> to and from the journal's record payload.</summary>
    internal static class JournalRecord
    {
        public const byte Version = 1;

        public static void Write(BinaryWriter writer, LogEntry entry)
        {
            writer.Write(Version);
            writer.Write((byte)entry.Kind);
            writer.Write((byte)entry.Severity);
            writer.Write(entry.TimestampUtc.Ticks);
            writer.Write(entry.Frame);
            writer.Write(entry.ThreadId);
            writer.Write(entry.IsMainThread);
            writer.Write(entry.Context.InstanceId);
            writer.Write(entry.Session);
            writer.Write(entry.Id);
            writer.Write(entry.Message);
            writer.Write(entry.StackTrace);
        }

        /// <param name="dropContext">
        /// True when the record comes from an earlier Editor session, whose object instance ids no longer
        /// mean anything.
        /// </param>
        public static LogEntry Read(BinaryReader reader, bool dropContext)
        {
            byte version = reader.ReadByte();
            if (version != Version)
            {
                throw new InvalidDataException("Unsupported journal record version " + version + ".");
            }

            var kind = (LogEntryKind)reader.ReadByte();
            var severity = (LogSeverity)reader.ReadByte();
            long ticks = reader.ReadInt64();
            int frame = reader.ReadInt32();
            int threadId = reader.ReadInt32();
            bool isMainThread = reader.ReadBoolean();
            int contextId = reader.ReadInt32();
            int session = reader.ReadInt32();
            long id = reader.ReadInt64();
            string message = reader.ReadString();
            string stackTrace = reader.ReadString();

            var entry = new LogEntry(
                kind,
                severity,
                message,
                stackTrace,
                new DateTime(ticks, DateTimeKind.Utc),
                frame,
                threadId,
                isMainThread,
                dropContext ? ObjectRef.None : new ObjectRef(contextId));
            entry.AssignSequence(id, session);
            return entry;
        }
    }
}
