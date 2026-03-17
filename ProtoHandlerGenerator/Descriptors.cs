using Microsoft.CodeAnalysis;

namespace ProtoHandlerGen
{
    static class Descriptors
    {
        public static readonly DiagnosticDescriptor UnhandledOneofCase = new(
            id: "PROTO001",
            title: "Unhandled oneof case",
            messageFormat: "Oneof case '{0}' in '{1}' has no handler method in '{2}'. Add: void Handle{0}({3} command) {{ }}",
            category: "ProtoHandler",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor UnmatchedHandleMethod = new(
            id: "PROTO002",
            title: "Unmatched Handle method",
            messageFormat: "Method '{0}' in '{1}' starts with 'Handle' but parameter type '{2}' does not match any oneof case in '{3}'",
            category: "ProtoHandler",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor HandleMethodInEventOnly = new(
            id: "PROTO003",
            title: "Handle method in event-only presenter",
            messageFormat: "Method '{0}' in '{1}' starts with 'Handle' but the class has no command type",
            category: "ProtoHandler",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor MissingPartial = new(
            id: "PROTO004",
            title: "Missing partial keyword",
            messageFormat: "Class '{0}' with [ProtoHandler] must be declared as partial",
            category: "ProtoHandler",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor AmbiguousRoute = new(
            id: "PROTO005",
            title: "Ambiguous routing path",
            messageFormat: "Type '{0}' is contained in multiple parent oneof messages: {1}. Use [ProtoRoute(typeof({2}))] or [ProtoRoute(typeof({3}))] to specify the route.",
            category: "ProtoHandler",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor InvalidRouteHint = new(
            id: "PROTO006",
            title: "Invalid route hint",
            messageFormat: "Type '{0}' in [ProtoRoute] does not match any parent in the routing path of '{1}'. Valid parents: {2}",
            category: "ProtoHandler",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);
    }
}
