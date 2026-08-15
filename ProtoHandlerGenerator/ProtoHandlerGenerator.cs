using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ProtoHandlerGen
{
    /// <summary>
    /// インクリメンタル実行の追跡用ステージ名。テストから参照する。
    /// </summary>
    public static class TrackingNames
    {
        public const string OneofIndex = "OneofIndex";
        public const string Presenters = "Presenters";
        public const string ResolveRoutes = "ResolveRoutes";
    }

    [Generator]
    public sealed partial class ProtoHandlerGenerator : IIncrementalGenerator
    {
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            // oneof メッセージ型の索引は Compilation ごとに1回だけ構築する。
            // 索引は文字列のみで構成された値なので、proto 定義に変化がなければ
            // 下流の ResolveRoutes は再実行されない。
            var oneofIndex = context.CompilationProvider
                .Select(static (compilation, ct) => BuildOneofIndex(compilation, ct))
                .WithTrackingName(TrackingNames.OneofIndex);

            var presenters = context.SyntaxProvider
                .CreateSyntaxProvider(
                    predicate: static (node, _) =>
                        node is ClassDeclarationSyntax cds
                        && cds.Modifiers.Any(SyntaxKind.PartialKeyword)
                        && cds.AttributeLists.Count > 0,
                    transform: static (ctx, ct) => ExtractPresenter(ctx, ct))
                .Where(static m => m != null)
                .Select(static (m, _) => m.Value)
                .WithTrackingName(TrackingNames.Presenters);

            var pipeline = presenters
                .Combine(oneofIndex)
                .Select(static (pair, _) => ResolveRoutes(pair.Left, pair.Right))
                .WithTrackingName(TrackingNames.ResolveRoutes);

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
