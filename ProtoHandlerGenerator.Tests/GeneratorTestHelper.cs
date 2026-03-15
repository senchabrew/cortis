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
    public static GeneratorDriverRunResult RunGenerator(params string[] sources)
    {
        var allSources = new List<string> { Stubs.ProtoHandlerAttribute };
        allSources.AddRange(sources);

        var syntaxTrees = allSources.Select(s => CSharpSyntaxTree.ParseText(s)).ToArray();

        var references = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .Select(a => MetadataReference.CreateFromFile(a.Location))
            .Cast<MetadataReference>()
            .ToList();

        var compilation = CSharpCompilation.Create(
            assemblyName: "TestAssembly",
            syntaxTrees: syntaxTrees,
            references: references,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generator = new ProtoHandlerGen.ProtoHandlerGenerator();

        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out _);

        return driver.GetRunResult();
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
