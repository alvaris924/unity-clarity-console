using System;

namespace ClarityConsole.Core
{
    /// <summary>
    /// Identity of the Unity object a log entry was attached to, expressed without an engine reference.
    /// Wraps the instance id so that the eventual EntityId migration touches this one type (see ADR-0002).
    /// </summary>
    internal readonly struct ObjectRef : IEquatable<ObjectRef>
    {
        public static readonly ObjectRef None = default;

        public ObjectRef(int instanceId)
        {
            InstanceId = instanceId;
        }

        public int InstanceId { get; }

        public bool HasValue => InstanceId != 0;

        public bool Equals(ObjectRef other) => InstanceId == other.InstanceId;

        public override bool Equals(object obj) => obj is ObjectRef other && Equals(other);

        public override int GetHashCode() => InstanceId;

        public override string ToString() => HasValue ? $"ObjectRef({InstanceId})" : "ObjectRef(none)";
    }
}
