using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace ProtoHandlerGen
{
    struct MissingPartialInfo : IEquatable<MissingPartialInfo>
    {
        public string ClassName;
        public Location Location;

        public bool Equals(MissingPartialInfo other) => ClassName == other.ClassName;
        public override bool Equals(object obj) => obj is MissingPartialInfo other && Equals(other);
        public override int GetHashCode() => ClassName?.GetHashCode() ?? 0;
    }

    struct PresenterModel : IEquatable<PresenterModel>
    {
        public string ClassName;
        public string Namespace;
        public string InfrastructureNamespace;
        public bool IsSealed;
        // Excluded from Equals/GetHashCode to avoid cache invalidation
        public Location ClassLocation;
        public string CommandTypeFullName;
        public string EventTypeFullName;
        public string CommandOneofEnumFullName;
        public string CommandOneofPropertyName;
        public ImmutableArray<CaseModel> CommandCases;
        public ImmutableArray<CaseModel> EventCases;
        public ImmutableArray<HandlerModel> Handlers;
        public ImmutableArray<CaseModel> UnhandledCases;
        public ImmutableArray<UnmatchedMethodModel> UnmatchedHandleMethods;

        public bool Equals(PresenterModel other) =>
            ClassName == other.ClassName
            && Namespace == other.Namespace
            && InfrastructureNamespace == other.InfrastructureNamespace
            && IsSealed == other.IsSealed
            && CommandTypeFullName == other.CommandTypeFullName
            && EventTypeFullName == other.EventTypeFullName
            && CommandOneofEnumFullName == other.CommandOneofEnumFullName
            && CommandOneofPropertyName == other.CommandOneofPropertyName
            && CommandCases.SequenceEqual(other.CommandCases)
            && EventCases.SequenceEqual(other.EventCases)
            && Handlers.SequenceEqual(other.Handlers)
            && UnhandledCases.SequenceEqual(other.UnhandledCases)
            && UnmatchedHandleMethods.SequenceEqual(other.UnmatchedHandleMethods);

        public override bool Equals(object obj) => obj is PresenterModel other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = 17;
                hash = hash * 31 + (ClassName?.GetHashCode() ?? 0);
                hash = hash * 31 + (Namespace?.GetHashCode() ?? 0);
                hash = hash * 31 + (InfrastructureNamespace?.GetHashCode() ?? 0);
                hash = hash * 31 + IsSealed.GetHashCode();
                hash = hash * 31 + (CommandTypeFullName?.GetHashCode() ?? 0);
                hash = hash * 31 + (EventTypeFullName?.GetHashCode() ?? 0);
                hash = hash * 31 + (CommandOneofEnumFullName?.GetHashCode() ?? 0);
                hash = hash * 31 + (CommandOneofPropertyName?.GetHashCode() ?? 0);
                return hash;
            }
        }
    }

    struct CaseModel : IEquatable<CaseModel>
    {
        public string CaseName;
        public string CaseTypeFullName;

        public bool Equals(CaseModel other) =>
            CaseName == other.CaseName && CaseTypeFullName == other.CaseTypeFullName;

        public override bool Equals(object obj) => obj is CaseModel other && Equals(other);
        public override int GetHashCode() => (CaseName?.GetHashCode() ?? 0) ^ (CaseTypeFullName?.GetHashCode() ?? 0);
    }

    struct UnmatchedMethodModel : IEquatable<UnmatchedMethodModel>
    {
        public string MethodName;
        public string ParameterTypeFullName;

        public bool Equals(UnmatchedMethodModel other) =>
            MethodName == other.MethodName && ParameterTypeFullName == other.ParameterTypeFullName;

        public override bool Equals(object obj) => obj is UnmatchedMethodModel other && Equals(other);
        public override int GetHashCode() => (MethodName?.GetHashCode() ?? 0) ^ (ParameterTypeFullName?.GetHashCode() ?? 0);
    }

    struct HandlerModel : IEquatable<HandlerModel>
    {
        public string MethodName;
        public string CaseName;
        public string CaseTypeFullName;
        public bool IsAsync;

        public bool Equals(HandlerModel other) =>
            MethodName == other.MethodName
            && CaseName == other.CaseName
            && CaseTypeFullName == other.CaseTypeFullName
            && IsAsync == other.IsAsync;

        public override bool Equals(object obj) => obj is HandlerModel other && Equals(other);
        public override int GetHashCode() => (MethodName?.GetHashCode() ?? 0) ^ (CaseName?.GetHashCode() ?? 0);
    }
}
