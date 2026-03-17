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
        static PresenterModel? ExtractModel(GeneratorSyntaxContext ctx, CancellationToken ct)
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
            string invalidRouteHint = null;
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

            // Route discovery: find parent oneof chain for inner types
            var allOneofTypes = GetAllOneofMessageTypes(ctx.SemanticModel.Compilation);
            string cmdRouteAmbiguity = null;
            string evtRouteAmbiguity = null;
            var commandRoute = commandType != null
                ? DiscoverRoute(commandType, allOneofTypes, routeHints, out cmdRouteAmbiguity, out invalidRouteHint)
                : ImmutableArray<RouteSegment>.Empty;
            string invalidRouteHintEvt = null;
            var eventRoute = eventType != null
                ? DiscoverRoute(eventType, allOneofTypes, routeHints, out evtRouteAmbiguity, out invalidRouteHintEvt)
                : ImmutableArray<RouteSegment>.Empty;
            if (invalidRouteHint == null) invalidRouteHint = invalidRouteHintEvt;

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
                CommandRoute = commandRoute,
                EventRoute = eventRoute,
                CommandRouteAmbiguity = cmdRouteAmbiguity,
                EventRouteAmbiguity = evtRouteAmbiguity,
                RouteHints = routeHints,
                InvalidRouteHint = invalidRouteHint,
            };
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
        /// コンパイル中の全型から *OneofCase enum を持つ protobuf メッセージ型を収集する。
        /// </summary>
        static List<INamedTypeSymbol> GetAllOneofMessageTypes(Compilation compilation)
        {
            var result = new List<INamedTypeSymbol>();
            CollectOneofMessageTypes(compilation.GlobalNamespace, result);
            return result;
        }

        static void CollectOneofMessageTypes(INamespaceSymbol ns, List<INamedTypeSymbol> result)
        {
            foreach (var type in ns.GetTypeMembers())
                CollectOneofMessageTypesRecursive(type, result);
            foreach (var childNs in ns.GetNamespaceMembers())
                CollectOneofMessageTypes(childNs, result);
        }

        static void CollectOneofMessageTypesRecursive(INamedTypeSymbol type, List<INamedTypeSymbol> result)
        {
            if (type.GetTypeMembers().Any(t => t.TypeKind == TypeKind.Enum && t.Name.EndsWith("OneofCase")))
                result.Add(type);
            foreach (var nested in type.GetTypeMembers())
            {
                if (nested.TypeKind == TypeKind.Class)
                    CollectOneofMessageTypesRecursive(nested, result);
            }
        }

        /// <summary>
        /// targetType を含む親 oneof メッセージを再帰的に辿り、root からの経路を返す。
        /// 親が見つからない場合（= targetType が root）は空配列を返す。
        /// routeHints が指定されている場合、曖昧な親からヒントに一致するものを選択する。
        /// </summary>
        static ImmutableArray<RouteSegment> DiscoverRoute(
            INamedTypeSymbol targetType,
            List<INamedTypeSymbol> allOneofTypes,
            ImmutableArray<string> routeHints,
            out string ambiguity,
            out string invalidHint)
        {
            ambiguity = null;
            invalidHint = null;
            var segments = new List<RouteSegment>();
            var current = targetType;
            var visited = new HashSet<string>();
            var hintSet = new HashSet<string>(routeHints);

            while (true)
            {
                var fullName = current.ToDisplayString();
                if (!visited.Add(fullName)) break;

                var parents = FindParentSegments(current, allOneofTypes);

                if (parents.Count == 0) break;

                if (parents.Count > 1)
                {
                    // Try to disambiguate using route hints
                    var matched = parents.Where(p => hintSet.Contains(p.ParentTypeFullName)).ToList();
                    if (matched.Count == 1)
                    {
                        segments.Add(matched[0]);
                        current = allOneofTypes.FirstOrDefault(t => t.ToDisplayString() == matched[0].ParentTypeFullName);
                        if (current == null) break;
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
                        var validParents = string.Join(", ", parents.Select(p => p.ParentTypeFullName));
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
                current = allOneofTypes.FirstOrDefault(t => t.ToDisplayString() == parents[0].ParentTypeFullName);
                if (current == null) break;
            }

            segments.Reverse();
            return segments.ToImmutableArray();
        }

        /// <summary>
        /// targetType を oneof プロパティとして持つ親メッセージ型を探す。
        /// </summary>
        static List<RouteSegment> FindParentSegments(
            INamedTypeSymbol targetType,
            List<INamedTypeSymbol> allOneofTypes)
        {
            var results = new List<RouteSegment>();

            foreach (var candidate in allOneofTypes)
            {
                if (SymbolEqualityComparer.Default.Equals(candidate, targetType)) continue;

                var oneofEnum = candidate.GetTypeMembers()
                    .FirstOrDefault(t => t.TypeKind == TypeKind.Enum && t.Name.EndsWith("OneofCase"));
                if (oneofEnum == null) continue;

                foreach (var field in oneofEnum.GetMembers().OfType<IFieldSymbol>())
                {
                    if (field.Name == "None" || !field.HasConstantValue) continue;

                    var prop = candidate.GetMembers(field.Name)
                        .OfType<IPropertySymbol>()
                        .FirstOrDefault();
                    if (prop == null) continue;

                    if (SymbolEqualityComparer.Default.Equals(prop.Type, targetType))
                    {
                        var oneofName = oneofEnum.Name;
                        results.Add(new RouteSegment
                        {
                            ParentTypeFullName = candidate.ToDisplayString(),
                            PropertyName = field.Name,
                            OneofEnumFullName = oneofEnum.ToDisplayString(),
                            OneofCasePropertyName = oneofName.Substring(0, oneofName.Length - "OneofCase".Length) + "Case",
                        });
                    }
                }
            }

            return results;
        }
    }
}
