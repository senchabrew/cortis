using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ProtoHandlerGen
{
    [Generator]
    public sealed partial class ProtoHandlerGenerator : IIncrementalGenerator
    {
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            var pipeline = context.SyntaxProvider
                .CreateSyntaxProvider(
                    predicate: static (node, _) =>
                        node is ClassDeclarationSyntax cds
                        && cds.Modifiers.Any(SyntaxKind.PartialKeyword)
                        && cds.AttributeLists.Count > 0,
                    transform: static (ctx, ct) => ExtractModel(ctx, ct))
                .Where(static m => m != null)
                .Select(static (m, _) => m.Value);

            context.RegisterSourceOutput(pipeline,
                static (spc, model) => Emit(spc, model));

            // PROTO004: [ProtoHandler] on non-partial class
            var missingPartialPipeline = context.SyntaxProvider
                .CreateSyntaxProvider(
                    predicate: static (node, _) =>
                        node is ClassDeclarationSyntax cds
                        && !cds.Modifiers.Any(SyntaxKind.PartialKeyword)
                        && cds.AttributeLists.Count > 0,
                    transform: static (ctx, ct) =>
                    {
                        var classDecl = (ClassDeclarationSyntax)ctx.Node;
                        var classSymbol = ctx.SemanticModel.GetDeclaredSymbol(classDecl, ct) as INamedTypeSymbol;
                        if (classSymbol == null) return null;

                        var attr = classSymbol.GetAttributes().FirstOrDefault(a =>
                            a.AttributeClass?.Name == "ProtoHandlerAttribute");
                        if (attr == null) return null;

                        return (MissingPartialInfo?)new MissingPartialInfo
                        {
                            ClassName = classSymbol.Name,
                            Location = classDecl.GetLocation(),
                        };
                    })
                .Where(static d => d != null)
                .Select(static (d, _) => d!.Value);

            context.RegisterSourceOutput(missingPartialPipeline,
                static (spc, info) => spc.ReportDiagnostic(Diagnostic.Create(
                    Descriptors.MissingPartial,
                    info.Location,
                    info.ClassName)));
        }
    }
}
