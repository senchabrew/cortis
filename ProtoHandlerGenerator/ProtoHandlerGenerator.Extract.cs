using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ProtoHandlerGen
{
    public sealed partial class ProtoHandlerGenerator
    {
        /// <summary>
        /// [ProtoHandler] クラスから、ルーティング経路を除く全情報を抽出する。
        /// Compilation 全体には触れず、対象クラスと属性引数の型のみを見る。
        /// </summary>
        static PresenterModel? ExtractPresenter(GeneratorSyntaxContext ctx, CancellationToken ct)
        {
            var classDecl = (ClassDeclarationSyntax)ctx.Node;
            var classSymbol = ctx.SemanticModel.GetDeclaredSymbol(classDecl, ct) as INamedTypeSymbol;
            if (classSymbol == null) return null;

            var attr = classSymbol.GetAttributes().FirstOrDefault(a =>
                a.AttributeClass?.Name == "ProtoHandlerAttribute");
            if (attr == null) return null;

            if (attr.ConstructorArguments.Length < 1) return null;

            var commandType = attr.ConstructorArguments[0].Value as INamedTypeSymbol;
            var eventType = attr.ConstructorArguments.Length >= 2
                ? attr.ConstructorArguments[1].Value as INamedTypeSymbol
                : null;

            // At least one of commandType or eventType must be specified
            if (commandType == null && eventType == null) return null;

            var commandCases = commandType != null
                ? DiscoverOneofCases(commandType)
                : ImmutableArray<CaseModel>.Empty;
            var eventCases = eventType != null ? DiscoverOneofCases(eventType) : ImmutableArray<CaseModel>.Empty;

            string commandOneofEnumFullName = null;
            string commandOneofPropertyName = null;
            if (commandType != null)
            {
                var commandOneofEnum = commandType.GetTypeMembers()
                    .FirstOrDefault(t => t.TypeKind == TypeKind.Enum
                                      && t.Name.EndsWith("OneofCase"));
                commandOneofEnumFullName = commandOneofEnum?.ToDisplayString();
                var oneofName = commandOneofEnum?.Name;
                commandOneofPropertyName = oneofName != null
                    ? oneofName.Substring(0, oneofName.Length - "OneofCase".Length) + "Case"
                    : null;
            }

            var handlers = commandType != null
                ? MatchHandlers(classSymbol, commandCases)
                : ImmutableArray<HandlerModel>.Empty;

            var handledNames = new HashSet<string>(handlers.Select(h => h.CaseName));
            var unhandledCases = commandCases.Where(c => !handledNames.Contains(c.CaseName)).ToImmutableArray();

            var unmatchedHandleMethods = FindUnmatchedHandleMethods(classSymbol, commandCases);

            // Extract [ProtoRoute] hints
            var routeHints = ImmutableArray<string>.Empty;
            var routeAttr = classSymbol.GetAttributes().FirstOrDefault(a =>
                a.AttributeClass?.Name == "ProtoRouteAttribute");
            if (routeAttr != null)
            {
                var builder2 = ImmutableArray.CreateBuilder<string>();
                foreach (var arg in routeAttr.ConstructorArguments)
                {
                    if (arg.Kind == TypedConstantKind.Array)
                    {
                        foreach (var elem in arg.Values)
                        {
                            if (elem.Value is INamedTypeSymbol t)
                                builder2.Add(t.ToDisplayString());
                        }
                    }
                    else if (arg.Value is INamedTypeSymbol t)
                    {
                        builder2.Add(t.ToDisplayString());
                    }
                }
                routeHints = builder2.ToImmutable();
            }

            var infraNamespace = attr.AttributeClass.ContainingNamespace is { IsGlobalNamespace: false } ns
                ? ns.ToDisplayString()
                : null;

            return new PresenterModel
            {
                ClassName = classSymbol.Name,
                Namespace = classSymbol.ContainingNamespace.IsGlobalNamespace
                    ? null
                    : classSymbol.ContainingNamespace.ToDisplayString(),
                InfrastructureNamespace = infraNamespace,
                IsSealed = classSymbol.IsSealed,
                ClassLocation = classDecl.GetLocation(),
                CommandTypeFullName = commandType?.ToDisplayString(),
                EventTypeFullName = eventType?.ToDisplayString(),
                CommandOneofEnumFullName = commandOneofEnumFullName,
                CommandOneofPropertyName = commandOneofPropertyName,
                CommandCases = commandCases,
                EventCases = eventCases,
                Handlers = handlers,
                UnhandledCases = unhandledCases,
                UnmatchedHandleMethods = unmatchedHandleMethods,
                // ルーティング経路は ResolveRoutes で解決する
                CommandRoute = ImmutableArray<RouteSegment>.Empty,
                EventRoute = ImmutableArray<RouteSegment>.Empty,
                RouteHints = routeHints,
            };
        }

        /// <summary>
        /// oneof 索引を使ってルーティング経路を解決し、経路情報を埋めたモデルを返す。
        /// ISymbol に触らない純粋な関数のため、索引と入力モデルが等価なら結果も等価になる。
        /// </summary>
        static PresenterModel ResolveRoutes(PresenterModel model, OneofIndex index)
        {
            string commandAmbiguity = null;
            string invalidHint = null;
            var commandRoute = model.CommandTypeFullName != null
                ? DiscoverRoute(model.CommandTypeFullName, index, model.RouteHints,
                    out commandAmbiguity, out invalidHint)
                : ImmutableArray<RouteSegment>.Empty;

            string eventAmbiguity = null;
            string invalidHintFromEvent = null;
            var eventRoute = model.EventTypeFullName != null
                ? DiscoverRoute(model.EventTypeFullName, index, model.RouteHints,
                    out eventAmbiguity, out invalidHintFromEvent)
                : ImmutableArray<RouteSegment>.Empty;

            model.CommandRoute = commandRoute;
            model.EventRoute = eventRoute;
            model.CommandRouteAmbiguity = commandAmbiguity;
            model.EventRouteAmbiguity = eventAmbiguity;
            model.InvalidRouteHint = invalidHint ?? invalidHintFromEvent;
            return model;
        }

        static ImmutableArray<CaseModel> DiscoverOneofCases(INamedTypeSymbol messageType)
        {
            var builder = ImmutableArray.CreateBuilder<CaseModel>();

            var oneofEnum = messageType.GetTypeMembers()
                .FirstOrDefault(t => t.TypeKind == TypeKind.Enum
                                  && t.Name.EndsWith("OneofCase"));
            if (oneofEnum == null) return builder.ToImmutable();

            var typesClass = messageType.GetTypeMembers("Types").FirstOrDefault();

            foreach (var field in oneofEnum.GetMembers().OfType<IFieldSymbol>())
            {
                if (field.Name == "None" || !field.HasConstantValue) continue;

                var caseName = field.Name;

                // First: look for a nested type in Types class (standard protobuf nested message)
                var caseType = typesClass?.GetTypeMembers(caseName).FirstOrDefault();
                if (caseType != null)
                {
                    builder.Add(new CaseModel
                    {
                        CaseName = caseName,
                        CaseTypeFullName = caseType.ToDisplayString(),
                    });
                    continue;
                }

                // Fallback: look for a property with the same name on the message type
                // and use its return type (supports external/imported protobuf message types)
                var property = messageType.GetMembers(caseName)
                    .OfType<IPropertySymbol>()
                    .FirstOrDefault();
                if (property != null)
                {
                    builder.Add(new CaseModel
                    {
                        CaseName = caseName,
                        CaseTypeFullName = property.Type.ToDisplayString(),
                    });
                }
            }

            return builder.ToImmutable();
        }

        static ImmutableArray<HandlerModel> MatchHandlers(
            INamedTypeSymbol classSymbol,
            ImmutableArray<CaseModel> cases)
        {
            var builder = ImmutableArray.CreateBuilder<HandlerModel>();
            var casesByType = cases.ToDictionary(c => c.CaseTypeFullName);

            foreach (var member in classSymbol.GetMembers().OfType<IMethodSymbol>())
            {
                if (member.IsStatic || member.Parameters.Length != 1
                    || !member.Name.StartsWith("Handle")) continue;

                var paramTypeFullName = member.Parameters[0].Type.ToDisplayString();
                if (casesByType.TryGetValue(paramTypeFullName, out var matchedCase))
                {
                    builder.Add(new HandlerModel
                    {
                        MethodName = member.Name,
                        CaseName = matchedCase.CaseName,
                        CaseTypeFullName = matchedCase.CaseTypeFullName,
                        IsAsync = IsAsyncReturnType(member.ReturnType),
                    });
                }
            }

            return builder.ToImmutable();
        }

        static ImmutableArray<UnmatchedMethodModel> FindUnmatchedHandleMethods(
            INamedTypeSymbol classSymbol,
            ImmutableArray<CaseModel> cases)
        {
            var builder = ImmutableArray.CreateBuilder<UnmatchedMethodModel>();
            var caseTypes = new HashSet<string>(cases.Select(c => c.CaseTypeFullName));

            foreach (var member in classSymbol.GetMembers().OfType<IMethodSymbol>())
            {
                if (member.IsStatic || member.Parameters.Length != 1
                    || !member.Name.StartsWith("Handle")) continue;

                var paramTypeFullName = member.Parameters[0].Type.ToDisplayString();
                if (!caseTypes.Contains(paramTypeFullName))
                {
                    builder.Add(new UnmatchedMethodModel
                    {
                        MethodName = member.Name,
                        ParameterTypeFullName = paramTypeFullName,
                    });
                }
            }

            return builder.ToImmutable();
        }

        static bool IsAsyncReturnType(ITypeSymbol returnType)
        {
            if (returnType is not INamedTypeSymbol named) return false;

            var ns = named.ContainingNamespace?.ToDisplayString();
            var name = named.Name;

            return (ns == "Cysharp.Threading.Tasks" && (name == "UniTask" || name == "UniTaskVoid"))
                || (ns == "System.Threading.Tasks" && (name == "Task" || name == "ValueTask"));
        }

        /// <summary>
        /// targetFullName を含む親 oneof メッセージを索引から再帰的に辿り、root からの経路を返す。
        /// 親が見つからない場合（= targetFullName が root）は空配列を返す。
        /// routeHints が指定されている場合、曖昧な親からヒントに一致するものを選択する。
        /// </summary>
        static ImmutableArray<RouteSegment> DiscoverRoute(
            string targetFullName,
            OneofIndex index,
            ImmutableArray<string> routeHints,
            out string ambiguity,
            out string invalidHint)
        {
            ambiguity = null;
            invalidHint = null;
            var segments = new List<RouteSegment>();
            var current = targetFullName;
            var visited = new HashSet<string>();
            var hintSet = new HashSet<string>(routeHints);

            while (true)
            {
                if (!visited.Add(current)) break;

                var parents = index.FindParents(current);

                if (parents.Count == 0) break;

                if (parents.Count > 1)
                {
                    // Try to disambiguate using route hints
                    var matched = parents.Where(p => hintSet.Contains(p.ParentTypeFullName)).ToList();
                    if (matched.Count == 1)
                    {
                        segments.Add(matched[0]);
                        current = matched[0].ParentTypeFullName;
                        continue;
                    }

                    if (matched.Count > 1)
                    {
                        // Multiple hints match — still ambiguous
                        ambiguity = string.Join(", ", matched.Select(p => p.ParentTypeFullName));
                        return ImmutableArray<RouteSegment>.Empty;
                    }

                    // No hint matched
                    if (!hintSet.IsSubsetOf(System.Array.Empty<string>()))
                    {
                        // Hints were provided but none matched at this level
                        var unmatchedHints = routeHints.Where(h => !parents.Any(p => p.ParentTypeFullName == h)).ToArray();
                        if (unmatchedHints.Length > 0)
                        {
                            invalidHint = unmatchedHints[0];
                        }
                    }

                    ambiguity = string.Join(", ", parents.Select(p => p.ParentTypeFullName));
                    return ImmutableArray<RouteSegment>.Empty;
                }

                segments.Add(parents[0]);
                current = parents[0].ParentTypeFullName;
            }

            segments.Reverse();
            return segments.ToImmutableArray();
        }
    }
}
