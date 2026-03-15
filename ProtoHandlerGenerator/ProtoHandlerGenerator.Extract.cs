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
    }
}
