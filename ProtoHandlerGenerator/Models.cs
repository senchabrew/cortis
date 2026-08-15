using System;
using System.Collections.Generic;
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
        public ImmutableArray<RouteSegment> CommandRoute;
        public ImmutableArray<RouteSegment> EventRoute;
        public string CommandRouteAmbiguity;
        public string EventRouteAmbiguity;
        public ImmutableArray<string> RouteHints;
        public string InvalidRouteHint;

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
            && UnmatchedHandleMethods.SequenceEqual(other.UnmatchedHandleMethods)
            && CommandRoute.SequenceEqual(other.CommandRoute)
            && EventRoute.SequenceEqual(other.EventRoute)
            && CommandRouteAmbiguity == other.CommandRouteAmbiguity
            && EventRouteAmbiguity == other.EventRouteAmbiguity
            && RouteHints.SequenceEqual(other.RouteHints)
            && InvalidRouteHint == other.InvalidRouteHint;

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
                hash = hash * 31 + CommandRoute.Length;
                hash = hash * 31 + EventRoute.Length;
                hash = hash * 31 + (CommandRouteAmbiguity?.GetHashCode() ?? 0);
                hash = hash * 31 + (EventRouteAmbiguity?.GetHashCode() ?? 0);
                hash = hash * 31 + RouteHints.Length;
                hash = hash * 31 + (InvalidRouteHint?.GetHashCode() ?? 0);
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

    /// <summary>
    /// oneof メッセージ型1件分の情報。ISymbol を保持せず文字列のみで構成するため、
    /// Compilation をまたいで値として比較できる。
    /// </summary>
    struct OneofMessageInfo : IEquatable<OneofMessageInfo>
    {
        public string FullName;
        public string OneofEnumFullName;
        public string OneofCasePropertyName;

        /// oneof case 名 → そのプロパティの型の完全修飾名
        public ImmutableArray<OneofCaseLink> Cases;

        public bool Equals(OneofMessageInfo other) =>
            FullName == other.FullName
            && OneofEnumFullName == other.OneofEnumFullName
            && OneofCasePropertyName == other.OneofCasePropertyName
            && Cases.SequenceEqual(other.Cases);

        public override bool Equals(object obj) => obj is OneofMessageInfo other && Equals(other);
        public override int GetHashCode() => (FullName?.GetHashCode() ?? 0) ^ Cases.Length;
    }

    struct OneofCaseLink : IEquatable<OneofCaseLink>
    {
        public string CaseName;
        public string PropertyTypeFullName;

        public bool Equals(OneofCaseLink other) =>
            CaseName == other.CaseName && PropertyTypeFullName == other.PropertyTypeFullName;

        public override bool Equals(object obj) => obj is OneofCaseLink other && Equals(other);
        public override int GetHashCode() =>
            (CaseName?.GetHashCode() ?? 0) ^ (PropertyTypeFullName?.GetHashCode() ?? 0);
    }

    /// <summary>
    /// Compilation 内の全 oneof メッセージ型の索引。
    /// ルーティング経路の探索に必要な情報だけを文字列で保持するため、
    /// proto 定義に変化がなければ値として等価になり、下流ステージの再実行を防げる。
    /// </summary>
    sealed class OneofIndex : IEquatable<OneofIndex>
    {
        public static readonly OneofIndex Empty = new(ImmutableArray<OneofMessageInfo>.Empty);

        public ImmutableArray<OneofMessageInfo> Messages { get; }

        /// 子型の完全修飾名 → その型を oneof case として持つ親セグメント群
        readonly Dictionary<string, List<RouteSegment>> _parentsByChild;

        public OneofIndex(ImmutableArray<OneofMessageInfo> messages)
        {
            Messages = messages;
            _parentsByChild = BuildParentLookup(messages);
        }

        static Dictionary<string, List<RouteSegment>> BuildParentLookup(
            ImmutableArray<OneofMessageInfo> messages)
        {
            var lookup = new Dictionary<string, List<RouteSegment>>();

            foreach (var message in messages)
            {
                foreach (var link in message.Cases)
                {
                    // 自己参照はルーティング経路にならない
                    if (link.PropertyTypeFullName == message.FullName) continue;

                    if (!lookup.TryGetValue(link.PropertyTypeFullName, out var parents))
                    {
                        parents = new List<RouteSegment>();
                        lookup[link.PropertyTypeFullName] = parents;
                    }

                    parents.Add(new RouteSegment
                    {
                        ParentTypeFullName = message.FullName,
                        PropertyName = link.CaseName,
                        OneofEnumFullName = message.OneofEnumFullName,
                        OneofCasePropertyName = message.OneofCasePropertyName,
                    });
                }
            }

            return lookup;
        }

        /// <summary>
        /// childFullName を oneof case として持つ親セグメントを返す。
        /// </summary>
        public IReadOnlyList<RouteSegment> FindParents(string childFullName)
        {
            if (childFullName != null && _parentsByChild.TryGetValue(childFullName, out var parents))
                return parents;
            return System.Array.Empty<RouteSegment>();
        }

        // 索引の等価性は内容のみで決まる（_parentsByChild は Messages から導出される）
        public bool Equals(OneofIndex other) =>
            other != null && Messages.SequenceEqual(other.Messages);

        public override bool Equals(object obj) => obj is OneofIndex other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = 17;
                hash = hash * 31 + Messages.Length;
                foreach (var message in Messages)
                    hash = hash * 31 + (message.FullName?.GetHashCode() ?? 0);
                return hash;
            }
        }
    }

    /// <summary>
    /// Root メッセージから inner メッセージへのルーティング経路の1ステップ。
    /// 例: PAppAction --(PlayerAction)--> PAurisExperiencePlayerAction
    /// </summary>
    struct RouteSegment : IEquatable<RouteSegment>
    {
        /// 親メッセージ型の完全修飾名 (e.g. "Jp.Co.Gatari.Protos.PAppAction")
        public string ParentTypeFullName;

        /// 親メッセージ上のプロパティ名 (e.g. "PlayerAction")
        public string PropertyName;

        /// OneofCase enum の完全修飾名 (e.g. "Jp.Co.Gatari.Protos.PAppAction.ActionOneofCase")
        public string OneofEnumFullName;

        /// OneofCase プロパティ名 (e.g. "ActionCase")
        public string OneofCasePropertyName;

        public bool Equals(RouteSegment other) =>
            ParentTypeFullName == other.ParentTypeFullName
            && PropertyName == other.PropertyName
            && OneofEnumFullName == other.OneofEnumFullName
            && OneofCasePropertyName == other.OneofCasePropertyName;

        public override bool Equals(object obj) => obj is RouteSegment other && Equals(other);

        public override int GetHashCode() =>
            (ParentTypeFullName?.GetHashCode() ?? 0) ^ (PropertyName?.GetHashCode() ?? 0);
    }
}
