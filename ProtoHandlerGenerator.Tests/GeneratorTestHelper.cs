using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using ProtoHandlerGen;

namespace ProtoHandlerGenerator.Tests;

public static class GeneratorTestHelper
{
    public static CSharpCompilation CreateCompilation(params string[] sources)
    {
        var allSources = new List<string> { Stubs.ProtoHandlerAttribute };
        allSources.AddRange(sources);

        var syntaxTrees = allSources.Select(s => CSharpSyntaxTree.ParseText(s)).ToArray();

        var references = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .Select(a => MetadataReference.CreateFromFile(a.Location))
            .Cast<MetadataReference>()
            .ToList();

        return CSharpCompilation.Create(
            assemblyName: "TestAssembly",
            syntaxTrees: syntaxTrees,
            references: references,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    public static GeneratorDriverRunResult RunGenerator(params string[] sources)
    {
        var compilation = CreateCompilation(sources);

        var generator = new ProtoHandlerGen.ProtoHandlerGenerator();

        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out _);

        return driver.GetRunResult();
    }

    /// <summary>
    /// 同一 driver で 2 回実行し、2 回目のインクリメンタル実行結果を得る。
    /// 1 回目と 2 回目の間に addedSource を Compilation へ追加する。
    /// </summary>
    public static (GeneratorDriverRunResult First, GeneratorDriverRunResult Second) RunGeneratorTwice(
        string[] sources,
        string addedSource)
    {
        var compilation = CreateCompilation(sources);

        var generator = new ProtoHandlerGen.ProtoHandlerGenerator();

        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            new[] { generator.AsSourceGenerator() },
            driverOptions: new GeneratorDriverOptions(
                disabledOutputs: IncrementalGeneratorOutputKind.None,
                trackIncrementalGeneratorSteps: true));

        driver = driver.RunGenerators(compilation);
        var first = driver.GetRunResult();

        var updated = compilation.AddSyntaxTrees(CSharpSyntaxTree.ParseText(addedSource));
        driver = driver.RunGenerators(updated);
        var second = driver.GetRunResult();

        return (first, second);
    }

    /// <summary>
    /// 指定ステージの全出力の実行理由を返す。
    /// </summary>
    public static IReadOnlyList<IncrementalStepRunReason> GetStepReasons(
        GeneratorDriverRunResult result,
        string trackingName)
    {
        if (!result.Results[0].TrackedSteps.TryGetValue(trackingName, out var steps))
            return Array.Empty<IncrementalStepRunReason>();

        return steps
            .SelectMany(step => step.Outputs)
            .Select(output => output.Reason)
            .ToList();
    }

    public static string? GetGeneratedSource(GeneratorDriverRunResult result, string hintNameContains)
    {
        return result.GeneratedTrees
            .FirstOrDefault(t => t.FilePath.Contains(hintNameContains))
            ?.GetText()
            .ToString();
    }

    public static ImmutableArray<Diagnostic> GetDiagnostics(GeneratorDriverRunResult result)
    {
        return result.Diagnostics;
    }

    public static ImmutableArray<Diagnostic> GetDiagnostics(GeneratorDriverRunResult result, string id)
    {
        return result.Diagnostics.Where(d => d.Id == id).ToImmutableArray();
    }
}
