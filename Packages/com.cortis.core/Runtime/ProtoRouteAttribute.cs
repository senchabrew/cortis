using System;

namespace Cortis
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class ProtoRouteAttribute : Attribute
    {
        public Type[] Path { get; }

        public ProtoRouteAttribute(params Type[] path)
        {
            Path = path;
        }
    }
}
