using System;

namespace Cortis
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class ProtoHandlerAttribute : Attribute
    {
        public Type CommandType { get; }
        public Type EventType { get; }

        public ProtoHandlerAttribute(Type commandType, Type eventType)
        {
            CommandType = commandType;
            EventType = eventType;
        }

        public ProtoHandlerAttribute(Type commandType)
        {
            CommandType = commandType;
            EventType = null;
        }
    }
}
