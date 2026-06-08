// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for the unique-lines code-fix benchmark family.</summary>
internal static class UniqueLinesCodeFixBenchmarkSource
{
    /// <summary>The number of members emitted into each synthetic type for lambda-like unique-lines benchmarks.</summary>
    private const int LambdaMembersPerType = 25;

    /// <summary>Builds violating constructor declaration source.</summary>
    /// <param name="members">The number of synthetic declarations to emit.</param>
    /// <returns>The generated source text.</returns>
    public static string GenerateConstructorDeclarationParameters(int members)
        => $$"""
           namespace Bench;
           {{BenchmarkSourceText.JoinBlocks(members, GenerateConstructorDeclarationType)}}
           """;

    /// <summary>Builds violating method declaration source.</summary>
    /// <param name="members">The number of synthetic members to emit.</param>
    /// <returns>The generated source text.</returns>
    public static string GenerateMethodDeclarationParameters(int members)
        => UniqueLinesBenchmarkSource.GenerateMethodDeclarationParameters(members, violating: true);

    /// <summary>Builds violating delegate declaration source.</summary>
    /// <param name="members">The number of synthetic declarations to emit.</param>
    /// <returns>The generated source text.</returns>
    public static string GenerateDelegateDeclarationParameters(int members)
        => $$"""
           namespace Bench;
           {{BenchmarkSourceText.JoinBlocks(members, GenerateDelegateDeclaration)}}
           """;

    /// <summary>Builds violating indexer declaration source.</summary>
    /// <param name="members">The number of synthetic declarations to emit.</param>
    /// <returns>The generated source text.</returns>
    public static string GenerateIndexerDeclarationParameters(int members)
        => $$"""
           namespace Bench;
           {{BenchmarkSourceText.JoinBlocks(members, GenerateIndexerDeclarationType)}}
           """;

    /// <summary>Builds violating invocation expression source.</summary>
    /// <param name="members">The number of synthetic members to emit.</param>
    /// <returns>The generated source text.</returns>
    public static string GenerateInvocationArguments(int members)
        => UniqueLinesBenchmarkSource.GenerateInvocationArguments(members, violating: true);

    /// <summary>Builds violating object creation expression source.</summary>
    /// <param name="members">The number of synthetic members to emit.</param>
    /// <returns>The generated source text.</returns>
    public static string GenerateObjectCreationArguments(int members)
        => UniqueLinesBenchmarkSource.GenerateObjectCreationArguments(members, violating: true);

    /// <summary>Builds violating element access expression source.</summary>
    /// <param name="members">The number of synthetic members to emit.</param>
    /// <returns>The generated source text.</returns>
    public static string GenerateElementAccessArguments(int members)
        => $$"""
           namespace Bench;
           internal static class ElementAccessExpressionArgumentBench
           {
               private sealed class IndexedValues
               {
                   public int this[int x, int y, int z] => x + y + z;
               }

               private static readonly IndexedValues Values = new();

           {{BenchmarkSourceText.JoinBlocks(members, GenerateElementAccessArgumentMember)}}
           }
           """;

    /// <summary>Builds violating attribute argument source.</summary>
    /// <param name="members">The number of synthetic declarations to emit.</param>
    /// <returns>The generated source text.</returns>
    public static string GenerateAttributeArguments(int members)
        => $$"""
           namespace Bench;

           [global::System.AttributeUsage(global::System.AttributeTargets.Class)]
           internal sealed class DemoAttribute : global::System.Attribute
           {
               public DemoAttribute(int x, int y, int z)
               {
               }
           }

           {{BenchmarkSourceText.JoinBlocks(members, GenerateAttributeArgumentType)}}
           """;

    /// <summary>Builds violating anonymous method source.</summary>
    /// <param name="members">The number of synthetic members to emit.</param>
    /// <returns>The generated source text.</returns>
    public static string GenerateAnonymousMethodExpressionParameters(int members)
        => GenerateDistributedLambdaLikeMembers(
            members,
            "AnonymousMethodExpressionBench",
            AppendAnonymousMethodExpressionMember);

    /// <summary>Builds violating parenthesized lambda source.</summary>
    /// <param name="members">The number of synthetic members to emit.</param>
    /// <returns>The generated source text.</returns>
    public static string GenerateParenthesizedLambdaExpressionParameters(int members)
        => GenerateDistributedLambdaLikeMembers(
            members,
            "ParenthesizedLambdaExpressionBench",
            AppendParenthesizedLambdaExpressionMember);

    /// <summary>Builds violating record declaration source.</summary>
    /// <param name="members">The number of synthetic declarations to emit.</param>
    /// <returns>The generated source text.</returns>
    public static string GenerateRecordDeclarationParameters(int members)
        => $$"""
           namespace Bench;
           {{BenchmarkSourceText.JoinBlocks(members, GenerateRecordDeclarationType)}}
           """;

    /// <summary>Builds violating class declaration source.</summary>
    /// <param name="members">The number of synthetic declarations to emit.</param>
    /// <returns>The generated source text.</returns>
    public static string GenerateClassDeclarationParameters(int members)
        => $$"""
           namespace Bench;
           {{BenchmarkSourceText.JoinBlocks(members, GenerateClassDeclarationType)}}
           """;

    /// <summary>Builds violating struct declaration source.</summary>
    /// <param name="members">The number of synthetic declarations to emit.</param>
    /// <returns>The generated source text.</returns>
    public static string GenerateStructDeclarationParameters(int members)
        => $$"""
           namespace Bench;
           {{BenchmarkSourceText.JoinBlocks(members, GenerateStructDeclarationType)}}
           """;

    /// <summary>Builds violating implicit object creation expression source.</summary>
    /// <param name="members">The number of synthetic members to emit.</param>
    /// <returns>The generated source text.</returns>
    public static string GenerateImplicitObjectCreationArguments(int members)
        => $$"""
           namespace Bench;
           internal static class ImplicitObjectCreationExpressionArgumentBench
           {
               private sealed class Item
               {
                   public Item(int x, int y, int z)
                   {
                   }
               }

           {{BenchmarkSourceText.JoinBlocks(members, GenerateImplicitObjectCreationArgumentMember)}}
           }
           """;

    /// <summary>Builds violating constructor initializer source.</summary>
    /// <param name="members">The number of synthetic declarations to emit.</param>
    /// <returns>The generated source text.</returns>
    public static string GenerateConstructorInitializerArguments(int members)
        => $$"""
           namespace Bench;
           {{BenchmarkSourceText.JoinBlocks(members, GenerateConstructorInitializerType)}}
           """;

    /// <summary>Builds violating primary-constructor base-type source.</summary>
    /// <param name="members">The number of synthetic declarations to emit.</param>
    /// <returns>The generated source text.</returns>
    public static string GeneratePrimaryConstructorBaseTypeArguments(int members)
        => $$"""
           namespace Bench;

           internal class BasePrimaryConstructor(int x, int y, int z)
           {
           }

           {{BenchmarkSourceText.JoinBlocks(members, GeneratePrimaryConstructorBaseTypeType)}}
           """;

    /// <summary>Builds violating local function source.</summary>
    /// <param name="members">The number of synthetic members to emit.</param>
    /// <returns>The generated source text.</returns>
    public static string GenerateLocalFunctionStatementParameters(int members)
        => $$"""
           namespace Bench;
           internal static class LocalFunctionStatementBench
           {
           {{BenchmarkSourceText.JoinBlocks(members, GenerateLocalFunctionStatementMember)}}
           }
           """;

    /// <summary>Builds violating operator declaration source.</summary>
    /// <param name="members">The number of synthetic declarations to emit.</param>
    /// <returns>The generated source text.</returns>
    public static string GenerateOperatorDeclarationParameters(int members)
        => $$"""
           namespace Bench;
           {{BenchmarkSourceText.JoinBlocks(members, GenerateOperatorDeclarationType)}}
           """;

    /// <summary>Builds conversion operator declaration source where the helper still takes the no-op path.</summary>
    /// <param name="members">The number of synthetic declarations to emit.</param>
    /// <returns>The generated source text.</returns>
    public static string GenerateConversionOperatorDeclarationParameters(int members)
        => $$"""
           namespace Bench;
           {{BenchmarkSourceText.JoinBlocks(members, GenerateConversionOperatorDeclarationType)}}
           """;

    /// <summary>Builds violating type parameter list source.</summary>
    /// <param name="members">The number of synthetic declarations to emit.</param>
    /// <returns>The generated source text.</returns>
    public static string GenerateTypeParameterLists(int members)
        => $$"""
           namespace Bench;
           {{BenchmarkSourceText.JoinBlocks(members, GenerateTypeParameterListType)}}
           """;

    /// <summary>Builds violating type argument list source.</summary>
    /// <param name="members">The number of synthetic members to emit.</param>
    /// <returns>The generated source text.</returns>
    public static string GenerateTypeArgumentLists(int members)
        => UniqueLinesBenchmarkSource.GenerateTypeArgumentLists(members, violating: true);

    /// <summary>Builds violating function pointer parameter list source.</summary>
    /// <param name="members">The number of synthetic declarations to emit.</param>
    /// <returns>The generated source text.</returns>
    public static string GenerateFunctionPointerParameterLists(int members)
        => $$"""
           namespace Bench;
           internal unsafe class FunctionPointerParameterListBench
           {
           {{BenchmarkSourceText.JoinBlocks(members, GenerateFunctionPointerField)}}
           }
           """;

    /// <summary>Builds a violating constructor declaration type.</summary>
    /// <param name="index">The synthetic declaration index.</param>
    /// <returns>The generated source text.</returns>
    private static string GenerateConstructorDeclarationType(int index)
        => $$"""
           internal sealed class ConstructorDeclarationBench{{index}}
           {
               public ConstructorDeclarationBench{{index}}(int x,
                   int y,
                   int z)
               {
               }
           }
           """;

    /// <summary>Builds a violating delegate declaration.</summary>
    /// <param name="index">The synthetic declaration index.</param>
    /// <returns>The generated source text.</returns>
    private static string GenerateDelegateDeclaration(int index)
        => $$"""
           internal delegate int DelegateDeclarationBench{{index}}(int x,
               int y,
               int z);
           """;

    /// <summary>Builds a violating indexer declaration type.</summary>
    /// <param name="index">The synthetic declaration index.</param>
    /// <returns>The generated source text.</returns>
    private static string GenerateIndexerDeclarationType(int index)
        => $$"""
           internal sealed class IndexerDeclarationBench{{index}}
           {
               public int this[int x,
                   int y,
                   int z] => x + y + z;
           }
           """;

    /// <summary>Builds a violating element access argument member.</summary>
    /// <param name="index">The synthetic member index.</param>
    /// <returns>The generated source text.</returns>
    private static string GenerateElementAccessArgumentMember(int index)
        => $$"""
           internal static int Use{{index}}()
               => Values[1,
                   2,
                   3];
           """;

    /// <summary>Builds a violating attribute argument type.</summary>
    /// <param name="index">The synthetic declaration index.</param>
    /// <returns>The generated source text.</returns>
    private static string GenerateAttributeArgumentType(int index)
        => $$"""
           [Demo(1,
               2,
               3)]
           internal sealed class AttributeArgumentBench{{index}}
           {
           }
           """;

    /// <summary>Builds a violating record declaration type.</summary>
    /// <param name="index">The synthetic declaration index.</param>
    /// <returns>The generated source text.</returns>
    private static string GenerateRecordDeclarationType(int index)
        => $$"""
           internal record RecordDeclarationBench{{index}}(int x,
               int y,
               int z);
           """;

    /// <summary>Builds a violating class declaration type.</summary>
    /// <param name="index">The synthetic declaration index.</param>
    /// <returns>The generated source text.</returns>
    private static string GenerateClassDeclarationType(int index)
        => $$"""
           internal class ClassDeclarationBench{{index}}(int x,
               int y,
               int z)
           {
           }
           """;

    /// <summary>Builds a violating struct declaration type.</summary>
    /// <param name="index">The synthetic declaration index.</param>
    /// <returns>The generated source text.</returns>
    private static string GenerateStructDeclarationType(int index)
        => $$"""
           internal struct StructDeclarationBench{{index}}(int x,
               int y,
               int z)
           {
           }
           """;

    /// <summary>Builds a violating implicit object creation argument member.</summary>
    /// <param name="index">The synthetic member index.</param>
    /// <returns>The generated source text.</returns>
    private static string GenerateImplicitObjectCreationArgumentMember(int index)
        => $$"""
           internal static object Use{{index}}()
           {
               Item item = new(1,
                   2,
                   3);
               return item;
           }
           """;

    /// <summary>Builds a violating constructor initializer type.</summary>
    /// <param name="index">The synthetic declaration index.</param>
    /// <returns>The generated source text.</returns>
    private static string GenerateConstructorInitializerType(int index)
        => $$"""
           internal sealed class ConstructorInitializerBench{{index}}
           {
               public ConstructorInitializerBench{{index}}(int x, int y, int z)
               {
               }

               public ConstructorInitializerBench{{index}}()
                   : this(1,
                       2,
                       3)
               {
               }
           }
           """;

    /// <summary>Builds a violating primary-constructor base-type declaration.</summary>
    /// <param name="index">The synthetic declaration index.</param>
    /// <returns>The generated source text.</returns>
    private static string GeneratePrimaryConstructorBaseTypeType(int index)
        => $$"""
           internal class PrimaryConstructorBaseTypeBench{{index}}()
               : BasePrimaryConstructor(1,
                   2,
                   3)
           {
           }
           """;

    /// <summary>Builds a violating local function statement member.</summary>
    /// <param name="index">The synthetic member index.</param>
    /// <returns>The generated source text.</returns>
    private static string GenerateLocalFunctionStatementMember(int index)
        => $$"""
           internal static int Use{{index}}()
           {
               int Local(int x,
                   int y,
                   int z)
               {
                   return x + y + z;
               }

               return Local(1, 2, 3);
           }
           """;

    /// <summary>Builds a violating operator declaration type.</summary>
    /// <param name="index">The synthetic declaration index.</param>
    /// <returns>The generated source text.</returns>
    private static string GenerateOperatorDeclarationType(int index)
        => $$"""
           internal sealed class OperatorDeclarationBench{{index}}
           {
               public static OperatorDeclarationBench{{index}} operator +(OperatorDeclarationBench{{index}} left,
                   OperatorDeclarationBench{{index}} right) => left;
           }
           """;

    /// <summary>Builds a violating conversion operator declaration type.</summary>
    /// <param name="index">The synthetic declaration index.</param>
    /// <returns>The generated source text.</returns>
    private static string GenerateConversionOperatorDeclarationType(int index)
        => $$"""
           internal sealed class ConversionOperatorDeclarationBench{{index}}
           {
               public static implicit operator int(
                   ConversionOperatorDeclarationBench{{index}} value) => 0;
           }
           """;

    /// <summary>Builds a violating type parameter list type.</summary>
    /// <param name="index">The synthetic declaration index.</param>
    /// <returns>The generated source text.</returns>
    private static string GenerateTypeParameterListType(int index)
        => $$"""
           internal sealed class TypeParameterListBench{{index}}<T1,
               T2>
           {
           }
           """;

    /// <summary>Builds a violating function pointer field.</summary>
    /// <param name="index">The synthetic declaration index.</param>
    /// <returns>The generated source text.</returns>
    private static string GenerateFunctionPointerField(int index)
        => $$"""
           private delegate*<int,
               string,
               void> _field{{index}};
           """;

    /// <summary>Builds a distributed benchmark corpus for lambda-like unique-lines fixes.</summary>
    /// <param name="members">The total number of synthetic members to emit.</param>
    /// <param name="typeNamePrefix">The synthetic type-name prefix.</param>
    /// <param name="appendMember">Appends one synthetic member.</param>
    /// <returns>The generated source text.</returns>
    private static string GenerateDistributedLambdaLikeMembers(
        int members,
        string typeNamePrefix,
        Action<System.Text.StringBuilder, int> appendMember)
    {
        var builder = new System.Text.StringBuilder();
        builder.AppendLine("namespace Bench;").AppendLine();

        var typeIndex = 0;
        var memberIndex = 0;
        while (memberIndex < members)
        {
            builder.Append("internal static class ")
                .Append(typeNamePrefix)
                .Append(typeIndex)
                .AppendLine()
                .AppendLine("{");

            var typeMemberCount = Math.Min(LambdaMembersPerType, members - memberIndex);
            for (var i = 0; i < typeMemberCount; i++)
            {
                if (i > 0)
                {
                    builder.AppendLine().AppendLine();
                }

                appendMember(builder, memberIndex + i);
            }

            builder.AppendLine().AppendLine("}");
            memberIndex += typeMemberCount;
            typeIndex++;
            if (memberIndex < members)
            {
                builder.AppendLine();
            }
        }

        return builder.ToString();
    }

    /// <summary>Appends one anonymous-method unique-lines benchmark member.</summary>
    /// <param name="builder">The destination source builder.</param>
    /// <param name="index">The synthetic member index.</param>
    private static void AppendAnonymousMethodExpressionMember(System.Text.StringBuilder builder, int index)
    {
        builder.Append("    internal static System.Action<int, int, int> M")
            .Append(index)
            .AppendLine("()")
            .AppendLine("    {")
            .AppendLine("        return delegate(int x,")
            .AppendLine("            int y,")
            .AppendLine("            int z)")
            .AppendLine("        {")
            .AppendLine("            _ = x + y + z;")
            .AppendLine("        };")
            .Append("    }");
    }

    /// <summary>Appends one parenthesized-lambda unique-lines benchmark member.</summary>
    /// <param name="builder">The destination source builder.</param>
    /// <param name="index">The synthetic member index.</param>
    private static void AppendParenthesizedLambdaExpressionMember(System.Text.StringBuilder builder, int index)
    {
        builder.Append("    internal static System.Func<int, int, int, int> M")
            .Append(index)
            .AppendLine("()")
            .AppendLine("    {")
            .AppendLine("        return (int x,")
            .AppendLine("            int y,")
            .AppendLine("            int z) => x + y + z;")
            .Append("    }");
    }
}
