using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;

namespace ProtoHandlerGen
{
    public sealed partial class ProtoHandlerGenerator
    {
        const string ProtobufAssemblyName = "Google.Protobuf";
        const string OneofEnumSuffix = "OneofCase";

        /// <summary>
        /// Compilation 内の oneof メッセージ型を収集して索引を構築する。
        /// Presenter ごとではなく Compilation ごとに1回だけ実行される。
        /// </summary>
        static OneofIndex BuildOneofIndex(Compilation compilation, CancellationToken ct)
        {
            var builder = ImmutableArray.CreateBuilder<OneofMessageInfo>();

            foreach (var root in ProtobufRootNamespaces(compilation))
                CollectOneofMessages(root, builder, ct);

            // 列挙順に依存せず索引の等価性が決まるようにソートする
            var messages = builder
                .OrderBy(m => m.FullName, StringComparer.Ordinal)
                .ToImmutableArray();

            return new OneofIndex(messages);
        }

        /// <summary>
        /// 走査対象の名前空間ルートを返す。protobuf 生成コードは Google.Protobuf を参照する
        /// アセンブリにしか存在しないため、それ以外の参照アセンブリは走査しない。
        /// </summary>
        static IEnumerable<INamespaceSymbol> ProtobufRootNamespaces(Compilation compilation)
        {
            yield return compilation.Assembly.GlobalNamespace;

            foreach (var reference in compilation.SourceModule.ReferencedAssemblySymbols)
            {
                if (ReferencesProtobuf(reference))
                    yield return reference.GlobalNamespace;
            }
        }

        static bool ReferencesProtobuf(IAssemblySymbol assembly)
        {
            if (assembly.Name == ProtobufAssemblyName) return true;

            foreach (var module in assembly.Modules)
            {
                foreach (var identity in module.ReferencedAssemblies)
                {
                    if (identity.Name == ProtobufAssemblyName) return true;
                }
            }

            return false;
        }

        static void CollectOneofMessages(
            INamespaceSymbol ns,
            ImmutableArray<OneofMessageInfo>.Builder builder,
            CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            foreach (var type in ns.GetTypeMembers())
                CollectOneofMessages(type, builder, ct);

            foreach (var childNs in ns.GetNamespaceMembers())
                CollectOneofMessages(childNs, builder, ct);
        }

        static void CollectOneofMessages(
            INamedTypeSymbol type,
            ImmutableArray<OneofMessageInfo>.Builder builder,
            CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            var info = TryCreateOneofMessageInfo(type);
            if (info != null) builder.Add(info.Value);

            foreach (var nested in type.GetTypeMembers())
            {
                if (nested.TypeKind == TypeKind.Class)
                    CollectOneofMessages(nested, builder, ct);
            }
        }

        /// <summary>
        /// *OneofCase enum を持つ型から、ルーティング探索に必要な情報だけを抜き出す。
        /// oneof を持たない型では null を返す。
        /// </summary>
        static OneofMessageInfo? TryCreateOneofMessageInfo(INamedTypeSymbol type)
        {
            var oneofEnum = type.GetTypeMembers()
                .FirstOrDefault(t => t.TypeKind == TypeKind.Enum
                                  && t.Name.EndsWith(OneofEnumSuffix));
            if (oneofEnum == null) return null;

            var cases = ImmutableArray.CreateBuilder<OneofCaseLink>();

            foreach (var field in oneofEnum.GetMembers().OfType<IFieldSymbol>())
            {
                if (field.Name == "None" || !field.HasConstantValue) continue;

                var property = type.GetMembers(field.Name)
                    .OfType<IPropertySymbol>()
                    .FirstOrDefault();
                if (property == null) continue;

                cases.Add(new OneofCaseLink
                {
                    CaseName = field.Name,
                    PropertyTypeFullName = property.Type.ToDisplayString(),
                });
            }

            var oneofName = oneofEnum.Name;

            return new OneofMessageInfo
            {
                FullName = type.ToDisplayString(),
                OneofEnumFullName = oneofEnum.ToDisplayString(),
                OneofCasePropertyName =
                    oneofName.Substring(0, oneofName.Length - OneofEnumSuffix.Length) + "Case",
                Cases = cases.ToImmutable(),
            };
        }
    }
}
