// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for the unique-lines analyzer benchmark family.</summary>
internal static class UniqueLinesBenchmarkSource
{
    /// <summary>Builds synthetic source for method-declaration parameter benchmarks.</summary>
    /// <param name="members">The number of synthetic members to emit.</param>
    /// <param name="violating">Whether to emit layout violations.</param>
    /// <returns>The generated source text.</returns>
    public static string GenerateMethodDeclarationParameters(int members, bool violating)
        => $$"""
           namespace Bench;
           internal static class MethodDeclarationParameterBench
           {
           {{BenchmarkSourceText.JoinBlocks(members, i => GenerateMethodDeclarationParameterMember(i, violating))}}
           }
           """;

    /// <summary>Builds synthetic source for invocation-expression argument benchmarks.</summary>
    /// <param name="members">The number of synthetic members to emit.</param>
    /// <param name="violating">Whether to emit layout violations.</param>
    /// <returns>The generated source text.</returns>
    public static string GenerateInvocationArguments(int members, bool violating)
        => $$"""
           namespace Bench;
           internal static class InvocationExpressionArgumentBench
           {
           {{BenchmarkSourceText.JoinBlocks(members, i => GenerateInvocationArgumentMember(i, violating))}}
           }
           """;

    /// <summary>Builds one clean or violating invocation-expression block.</summary>
    /// <param name="index">The synthetic member index.</param>
    /// <param name="violating">Whether to emit a violating layout.</param>
    /// <returns>The generated member block.</returns>
    public static string GenerateInvocationMember(int index, bool violating)
        => GenerateInvocationArgumentMember(index, violating);

    /// <summary>Builds synthetic source for object-creation argument benchmarks.</summary>
    /// <param name="members">The number of synthetic members to emit.</param>
    /// <param name="violating">Whether to emit layout violations.</param>
    /// <returns>The generated source text.</returns>
    public static string GenerateObjectCreationArguments(int members, bool violating)
        => $$"""
           namespace Bench;
           internal static class ObjectCreationExpressionArgumentBench
           {
               private sealed class Item
               {
                   public Item(int x, int y, int z)
                   {
                   }
               }

           {{BenchmarkSourceText.JoinBlocks(members, i => GenerateObjectCreationArgumentMember(i, violating))}}
           }
           """;

    /// <summary>Builds synthetic source for type-argument-list benchmarks.</summary>
    /// <param name="members">The number of synthetic members to emit.</param>
    /// <param name="violating">Whether to emit layout violations.</param>
    /// <returns>The generated source text.</returns>
    public static string GenerateTypeArgumentLists(int members, bool violating)
        => $$"""
           namespace Bench;
           internal sealed class TypeArgumentListBench
           {
           {{BenchmarkSourceText.JoinBlocks(members, i => GenerateTypeArgumentListMember(i, violating))}}
           }
           """;

    /// <summary>Builds synthetic source for constructor-declaration parameter benchmarks.</summary>
    /// <param name="members">The number of synthetic declarations to emit.</param>
    /// <param name="violating">Whether to emit layout violations.</param>
    /// <returns>The generated source text.</returns>
    public static string GenerateConstructorDeclarationParameters(int members, bool violating)
        => $$"""
           namespace Bench;
           {{BenchmarkSourceText.JoinBlocks(members, i => GenerateConstructorDeclarationType(i, violating))}}
           """;

    /// <summary>Builds synthetic source for delegate-declaration parameter benchmarks.</summary>
    /// <param name="members">The number of synthetic declarations to emit.</param>
    /// <param name="violating">Whether to emit layout violations.</param>
    /// <returns>The generated source text.</returns>
    public static string GenerateDelegateDeclarationParameters(int members, bool violating)
        => $$"""
           namespace Bench;
           {{BenchmarkSourceText.JoinBlocks(members, i => GenerateDelegateDeclaration(i, violating))}}
           """;

    /// <summary>Builds synthetic source for indexer-declaration parameter benchmarks.</summary>
    /// <param name="members">The number of synthetic declarations to emit.</param>
    /// <param name="violating">Whether to emit layout violations.</param>
    /// <returns>The generated source text.</returns>
    public static string GenerateIndexerDeclarationParameters(int members, bool violating)
        => $$"""
           namespace Bench;
           {{BenchmarkSourceText.JoinBlocks(members, i => GenerateIndexerDeclarationType(i, violating))}}
           """;

    /// <summary>Builds synthetic source for element-access argument benchmarks.</summary>
    /// <param name="members">The number of synthetic members to emit.</param>
    /// <param name="violating">Whether to emit layout violations.</param>
    /// <returns>The generated source text.</returns>
    public static string GenerateElementAccessArguments(int members, bool violating)
        => $$"""
           namespace Bench;
           internal static class ElementAccessExpressionArgumentBench
           {
               private sealed class IndexedValues
               {
                   public int this[int x, int y, int z] => x + y + z;
               }

               private static readonly IndexedValues Values = new();

           {{BenchmarkSourceText.JoinBlocks(members, i => GenerateElementAccessArgumentMember(i, violating))}}
           }
           """;

    /// <summary>Builds synthetic source for attribute-argument benchmarks.</summary>
    /// <param name="members">The number of synthetic declarations to emit.</param>
    /// <param name="violating">Whether to emit layout violations.</param>
    /// <returns>The generated source text.</returns>
    public static string GenerateAttributeArguments(int members, bool violating)
        => $$"""
           namespace Bench;

           [global::System.AttributeUsage(global::System.AttributeTargets.Class)]
           internal sealed class DemoAttribute : global::System.Attribute
           {
               public DemoAttribute(int x, int y, int z)
               {
               }
           }

           {{BenchmarkSourceText.JoinBlocks(members, i => GenerateAttributeArgumentType(i, violating))}}
           """;

    /// <summary>Builds synthetic source for anonymous-method parameter benchmarks.</summary>
    /// <param name="members">The number of synthetic members to emit.</param>
    /// <param name="violating">Whether to emit layout violations.</param>
    /// <returns>The generated source text.</returns>
    public static string GenerateAnonymousMethodExpressionParameters(int members, bool violating)
        => $$"""
           namespace Bench;
           internal static class AnonymousMethodExpressionBench
           {
           {{BenchmarkSourceText.JoinBlocks(members, i => GenerateAnonymousMethodExpressionMember(i, violating))}}
           }
           """;

    /// <summary>Builds synthetic source for parenthesized-lambda parameter benchmarks.</summary>
    /// <param name="members">The number of synthetic members to emit.</param>
    /// <param name="violating">Whether to emit layout violations.</param>
    /// <returns>The generated source text.</returns>
    public static string GenerateParenthesizedLambdaExpressionParameters(int members, bool violating)
        => $$"""
           namespace Bench;
           internal static class ParenthesizedLambdaExpressionBench
           {
           {{BenchmarkSourceText.JoinBlocks(members, i => GenerateParenthesizedLambdaExpressionMember(i, violating))}}
           }
           """;

    /// <summary>Builds synthetic source for record-declaration parameter benchmarks.</summary>
    /// <param name="members">The number of synthetic declarations to emit.</param>
    /// <param name="violating">Whether to emit layout violations.</param>
    /// <returns>The generated source text.</returns>
    public static string GenerateRecordDeclarationParameters(int members, bool violating)
        => $$"""
           namespace Bench;
           {{BenchmarkSourceText.JoinBlocks(members, i => GenerateRecordDeclarationType(i, violating))}}
           """;

    /// <summary>Builds synthetic source for class-declaration primary-constructor parameter benchmarks.</summary>
    /// <param name="members">The number of synthetic declarations to emit.</param>
    /// <param name="violating">Whether to emit layout violations.</param>
    /// <returns>The generated source text.</returns>
    public static string GenerateClassDeclarationParameters(int members, bool violating)
        => $$"""
           namespace Bench;
           {{BenchmarkSourceText.JoinBlocks(members, i => GenerateClassDeclarationType(i, violating))}}
           """;

    /// <summary>Builds synthetic source for struct-declaration primary-constructor parameter benchmarks.</summary>
    /// <param name="members">The number of synthetic declarations to emit.</param>
    /// <param name="violating">Whether to emit layout violations.</param>
    /// <returns>The generated source text.</returns>
    public static string GenerateStructDeclarationParameters(int members, bool violating)
        => $$"""
           namespace Bench;
           {{BenchmarkSourceText.JoinBlocks(members, i => GenerateStructDeclarationType(i, violating))}}
           """;

    /// <summary>Builds synthetic source for implicit-object-creation argument benchmarks.</summary>
    /// <param name="members">The number of synthetic members to emit.</param>
    /// <param name="violating">Whether to emit layout violations.</param>
    /// <returns>The generated source text.</returns>
    public static string GenerateImplicitObjectCreationArguments(int members, bool violating)
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

           {{BenchmarkSourceText.JoinBlocks(members, i => GenerateImplicitObjectCreationArgumentMember(i, violating))}}
           }
           """;

    /// <summary>Builds synthetic source for constructor-initializer argument benchmarks.</summary>
    /// <param name="members">The number of synthetic declarations to emit.</param>
    /// <param name="violating">Whether to emit layout violations.</param>
    /// <returns>The generated source text.</returns>
    public static string GenerateConstructorInitializerArguments(int members, bool violating)
        => $$"""
           namespace Bench;
           {{BenchmarkSourceText.JoinBlocks(members, i => GenerateConstructorInitializerType(i, violating))}}
           """;

    /// <summary>Builds synthetic source for primary-constructor base-type argument benchmarks.</summary>
    /// <param name="members">The number of synthetic declarations to emit.</param>
    /// <param name="violating">Whether to emit layout violations.</param>
    /// <returns>The generated source text.</returns>
    public static string GeneratePrimaryConstructorBaseTypeArguments(int members, bool violating)
        => $$"""
           namespace Bench;

           internal class BasePrimaryConstructor(int x, int y, int z)
           {
           }

           {{BenchmarkSourceText.JoinBlocks(members, i => GeneratePrimaryConstructorBaseTypeType(i, violating))}}
           """;

    /// <summary>Builds synthetic source for local-function parameter benchmarks.</summary>
    /// <param name="members">The number of synthetic members to emit.</param>
    /// <param name="violating">Whether to emit layout violations.</param>
    /// <returns>The generated source text.</returns>
    public static string GenerateLocalFunctionStatementParameters(int members, bool violating)
        => $$"""
           namespace Bench;
           internal static class LocalFunctionStatementBench
           {
           {{BenchmarkSourceText.JoinBlocks(members, i => GenerateLocalFunctionStatementMember(i, violating))}}
           }
           """;

    /// <summary>Builds synthetic source for operator-declaration parameter benchmarks.</summary>
    /// <param name="members">The number of synthetic declarations to emit.</param>
    /// <param name="violating">Whether to emit layout violations.</param>
    /// <returns>The generated source text.</returns>
    public static string GenerateOperatorDeclarationParameters(int members, bool violating)
        => $$"""
           namespace Bench;
           {{BenchmarkSourceText.JoinBlocks(members, i => GenerateOperatorDeclarationType(i, violating))}}
           """;

    /// <summary>Builds synthetic source for conversion-operator-declaration parameter benchmarks.</summary>
    /// <param name="members">The number of synthetic declarations to emit.</param>
    /// <param name="violating">Whether to emit layout violations. A single-parameter conversion operator cannot produce a jagged layout, so this only varies the wrapping.</param>
    /// <returns>The generated source text.</returns>
    public static string GenerateConversionOperatorDeclarationParameters(int members, bool violating)
        => $$"""
           namespace Bench;
           {{BenchmarkSourceText.JoinBlocks(members, i => GenerateConversionOperatorDeclarationType(i, violating))}}
           """;

    /// <summary>Builds synthetic source for type-parameter-list benchmarks.</summary>
    /// <param name="members">The number of synthetic declarations to emit.</param>
    /// <param name="violating">Whether to emit layout violations.</param>
    /// <returns>The generated source text.</returns>
    public static string GenerateTypeParameterLists(int members, bool violating)
        => $$"""
           namespace Bench;
           {{BenchmarkSourceText.JoinBlocks(members, i => GenerateTypeParameterListType(i, violating))}}
           """;

    /// <summary>Builds synthetic source for function-pointer parameter-list benchmarks.</summary>
    /// <param name="members">The number of synthetic declarations to emit.</param>
    /// <param name="violating">Whether to emit layout violations.</param>
    /// <returns>The generated source text.</returns>
    public static string GenerateFunctionPointerParameterLists(int members, bool violating)
        => $$"""
           namespace Bench;
           internal unsafe class FunctionPointerParameterListBench
           {
           {{BenchmarkSourceText.JoinBlocks(members, i => GenerateFunctionPointerField(i, violating))}}
           }
           """;

    /// <summary>Builds one clean or violating method declaration.</summary>
    /// <param name="index">The synthetic member index.</param>
    /// <param name="violating">Whether to emit a violating layout.</param>
    /// <returns>The generated member block.</returns>
    private static string GenerateMethodDeclarationParameterMember(int index, bool violating)
        => violating
            ? $$"""
               private static int Add{{index}}(int x,
                   int y,
                   int z) => x + y + z;
               """
            : $$"""
               private static int Add{{index}}(int x, int y, int z) => x + y + z;
               """;

    /// <summary>Builds one clean or violating invocation-expression block.</summary>
    /// <param name="index">The synthetic member index.</param>
    /// <param name="violating">Whether to emit a violating layout.</param>
    /// <returns>The generated member block.</returns>
    private static string GenerateInvocationArgumentMember(int index, bool violating)
        => violating
            ? $$"""
               private static int Add{{index}}(int x, int y, int z) => x + y + z;

               internal static int Use{{index}}()
                   => Add{{index}}(1,
                       2,
                       3);
               """
            : $$"""
               private static int Add{{index}}(int x, int y, int z) => x + y + z;

               internal static int Use{{index}}()
                   => Add{{index}}(1, 2, 3);
               """;

    /// <summary>Builds one clean or violating object-creation-expression block.</summary>
    /// <param name="index">The synthetic member index.</param>
    /// <param name="violating">Whether to emit a violating layout.</param>
    /// <returns>The generated member block.</returns>
    private static string GenerateObjectCreationArgumentMember(int index, bool violating)
        => violating
            ? $$"""
               internal static object Use{{index}}()
                    => new Item(1,
                        2,
                        3);
               """
            : $$"""
               internal static object Use{{index}}()
                    => new Item(1, 2, 3);
               """;

    /// <summary>Builds one clean or violating type-argument-list field.</summary>
    /// <param name="index">The synthetic member index.</param>
    /// <param name="violating">Whether to emit a violating layout.</param>
    /// <returns>The generated member block.</returns>
    private static string GenerateTypeArgumentListMember(int index, bool violating)
        => violating
            ? $$"""
               private readonly global::System.Collections.Generic.Dictionary<
                   int, string> _map{{index}} = new();
               """
            : $$"""
               private readonly global::System.Collections.Generic.Dictionary<int, string> _map{{index}} = new();
               """;

    /// <summary>Builds one clean or violating constructor declaration type.</summary>
    /// <param name="index">The synthetic declaration index.</param>
    /// <param name="violating">Whether to emit a violating layout.</param>
    /// <returns>The generated declaration block.</returns>
    private static string GenerateConstructorDeclarationType(int index, bool violating)
        => violating
            ? $$"""
               internal sealed class ConstructorDeclarationBench{{index}}
               {
                   public ConstructorDeclarationBench{{index}}(int x,
                       int y,
                       int z)
                   {
                   }
               }
               """
            : $$"""
               internal sealed class ConstructorDeclarationBench{{index}}
               {
                   public ConstructorDeclarationBench{{index}}(int x, int y, int z)
                   {
                   }
               }
               """;

    /// <summary>Builds one clean or violating delegate declaration.</summary>
    /// <param name="index">The synthetic declaration index.</param>
    /// <param name="violating">Whether to emit a violating layout.</param>
    /// <returns>The generated declaration block.</returns>
    private static string GenerateDelegateDeclaration(int index, bool violating)
        => violating
            ? $$"""
               internal delegate int DelegateDeclarationBench{{index}}(int x,
                   int y,
                   int z);
               """
            : $$"""
               internal delegate int DelegateDeclarationBench{{index}}(int x, int y, int z);
               """;

    /// <summary>Builds one clean or violating indexer declaration type.</summary>
    /// <param name="index">The synthetic declaration index.</param>
    /// <param name="violating">Whether to emit a violating layout.</param>
    /// <returns>The generated declaration block.</returns>
    private static string GenerateIndexerDeclarationType(int index, bool violating)
        => violating
            ? $$"""
               internal sealed class IndexerDeclarationBench{{index}}
               {
                   public int this[int x,
                       int y,
                       int z] => x + y + z;
               }
               """
            : $$"""
               internal sealed class IndexerDeclarationBench{{index}}
               {
                   public int this[int x, int y, int z] => x + y + z;
               }
               """;

    /// <summary>Builds one clean or violating element-access argument member.</summary>
    /// <param name="index">The synthetic member index.</param>
    /// <param name="violating">Whether to emit a violating layout.</param>
    /// <returns>The generated member block.</returns>
    private static string GenerateElementAccessArgumentMember(int index, bool violating)
        => violating
            ? $$"""
               internal static int Use{{index}}()
                   => Values[1,
                       2,
                       3];
               """
            : $$"""
               internal static int Use{{index}}()
                   => Values[1, 2, 3];
               """;

    /// <summary>Builds one clean or violating attribute-argument type.</summary>
    /// <param name="index">The synthetic declaration index.</param>
    /// <param name="violating">Whether to emit a violating layout.</param>
    /// <returns>The generated declaration block.</returns>
    private static string GenerateAttributeArgumentType(int index, bool violating)
        => violating
            ? $$"""
               [Demo(1,
                   2,
                   3)]
               internal sealed class AttributeArgumentBench{{index}}
               {
               }
               """
            : $$"""
               [Demo(1, 2, 3)]
               internal sealed class AttributeArgumentBench{{index}}
               {
               }
               """;

    /// <summary>Builds one clean or violating anonymous-method member.</summary>
    /// <param name="index">The synthetic member index.</param>
    /// <param name="violating">Whether to emit a violating layout.</param>
    /// <returns>The generated member block.</returns>
    private static string GenerateAnonymousMethodExpressionMember(int index, bool violating)
        => violating
            ? $$"""
               internal static System.Action<int, int, int> M{{index}}()
               {
                   return delegate(int x,
                       int y,
                       int z)
                   {
                       _ = x + y + z;
                   };
               }
               """
            : $$"""
               internal static System.Action<int, int, int> M{{index}}()
               {
                   return delegate(int x, int y, int z)
                   {
                       _ = x + y + z;
                   };
               }
               """;

    /// <summary>Builds one clean or violating parenthesized-lambda member.</summary>
    /// <param name="index">The synthetic member index.</param>
    /// <param name="violating">Whether to emit a violating layout.</param>
    /// <returns>The generated member block.</returns>
    private static string GenerateParenthesizedLambdaExpressionMember(int index, bool violating)
        => violating
            ? $$"""
               internal static System.Func<int, int, int, int> M{{index}}()
               {
                   return (int x,
                       int y,
                       int z) => x + y + z;
               }
               """
            : $$"""
               internal static System.Func<int, int, int, int> M{{index}}()
               {
                   return (int x, int y, int z) => x + y + z;
               }
               """;

    /// <summary>Builds one clean or violating record declaration type.</summary>
    /// <param name="index">The synthetic declaration index.</param>
    /// <param name="violating">Whether to emit a violating layout.</param>
    /// <returns>The generated declaration block.</returns>
    private static string GenerateRecordDeclarationType(int index, bool violating)
        => violating
            ? $$"""
               internal record RecordDeclarationBench{{index}}(int x,
                   int y,
                   int z);
               """
            : $$"""
               internal record RecordDeclarationBench{{index}}(int x, int y, int z);
               """;

    /// <summary>Builds one clean or violating class declaration type.</summary>
    /// <param name="index">The synthetic declaration index.</param>
    /// <param name="violating">Whether to emit a violating layout.</param>
    /// <returns>The generated declaration block.</returns>
    private static string GenerateClassDeclarationType(int index, bool violating)
        => violating
            ? $$"""
               internal class ClassDeclarationBench{{index}}(int x,
                   int y,
                   int z)
               {
               }
               """
            : $$"""
               internal class ClassDeclarationBench{{index}}(int x, int y, int z)
               {
               }
               """;

    /// <summary>Builds one clean or violating struct declaration type.</summary>
    /// <param name="index">The synthetic declaration index.</param>
    /// <param name="violating">Whether to emit a violating layout.</param>
    /// <returns>The generated declaration block.</returns>
    private static string GenerateStructDeclarationType(int index, bool violating)
        => violating
            ? $$"""
               internal struct StructDeclarationBench{{index}}(int x,
                   int y,
                   int z)
               {
               }
               """
            : $$"""
               internal struct StructDeclarationBench{{index}}(int x, int y, int z)
               {
               }
               """;

    /// <summary>Builds one clean or violating implicit-object-creation argument member.</summary>
    /// <param name="index">The synthetic member index.</param>
    /// <param name="violating">Whether to emit a violating layout.</param>
    /// <returns>The generated member block.</returns>
    private static string GenerateImplicitObjectCreationArgumentMember(int index, bool violating)
        => violating
            ? $$"""
               internal static object Use{{index}}()
               {
                   Item item = new(1,
                       2,
                       3);
                   return item;
               }
               """
            : $$"""
               internal static object Use{{index}}()
               {
                   Item item = new(1, 2, 3);
                   return item;
               }
               """;

    /// <summary>Builds one clean or violating constructor-initializer type.</summary>
    /// <param name="index">The synthetic declaration index.</param>
    /// <param name="violating">Whether to emit a violating layout.</param>
    /// <returns>The generated declaration block.</returns>
    private static string GenerateConstructorInitializerType(int index, bool violating)
        => violating
            ? $$"""
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
               """
            : $$"""
               internal sealed class ConstructorInitializerBench{{index}}
               {
                   public ConstructorInitializerBench{{index}}(int x, int y, int z)
                   {
                   }

                   public ConstructorInitializerBench{{index}}()
                       : this(1, 2, 3)
                   {
                   }
               }
               """;

    /// <summary>Builds one clean or violating primary-constructor base-type declaration.</summary>
    /// <param name="index">The synthetic declaration index.</param>
    /// <param name="violating">Whether to emit a violating layout.</param>
    /// <returns>The generated declaration block.</returns>
    private static string GeneratePrimaryConstructorBaseTypeType(int index, bool violating)
        => violating
            ? $$"""
               internal class PrimaryConstructorBaseTypeBench{{index}}()
                   : BasePrimaryConstructor(1,
                       2,
                       3)
               {
               }
               """
            : $$"""
               internal class PrimaryConstructorBaseTypeBench{{index}}()
                   : BasePrimaryConstructor(1, 2, 3)
               {
               }
               """;

    /// <summary>Builds one clean or violating local-function statement member.</summary>
    /// <param name="index">The synthetic member index.</param>
    /// <param name="violating">Whether to emit a violating layout.</param>
    /// <returns>The generated member block.</returns>
    private static string GenerateLocalFunctionStatementMember(int index, bool violating)
        => violating
            ? $$"""
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
               """
            : $$"""
               internal static int Use{{index}}()
               {
                   int Local(int x, int y, int z)
                   {
                       return x + y + z;
                   }

                   return Local(1, 2, 3);
               }
               """;

    /// <summary>Builds one clean or violating operator-declaration type.</summary>
    /// <param name="index">The synthetic declaration index.</param>
    /// <param name="violating">Whether to emit a violating layout.</param>
    /// <returns>The generated declaration block.</returns>
    private static string GenerateOperatorDeclarationType(int index, bool violating)
        => violating
            ? $$"""
               internal sealed class OperatorDeclarationBench{{index}}
               {
                   public static OperatorDeclarationBench{{index}} operator +(OperatorDeclarationBench{{index}} left,
                       OperatorDeclarationBench{{index}} right) => left;
               }
               """
            : $$"""
               internal sealed class OperatorDeclarationBench{{index}}
               {
                   public static OperatorDeclarationBench{{index}} operator +(OperatorDeclarationBench{{index}} left, OperatorDeclarationBench{{index}} right) => left;
               }
               """;

    /// <summary>Builds one conversion-operator-declaration type. A single parameter cannot produce a jagged layout, so the violating branch only wraps the parameter.</summary>
    /// <param name="index">The synthetic declaration index.</param>
    /// <param name="violating">Whether to emit the wrapped layout.</param>
    /// <returns>The generated declaration block.</returns>
    private static string GenerateConversionOperatorDeclarationType(int index, bool violating)
        => violating
            ? $$"""
               internal sealed class ConversionOperatorDeclarationBench{{index}}
               {
                   public static implicit operator int(
                       ConversionOperatorDeclarationBench{{index}} value) => 0;
               }
               """
            : $$"""
               internal sealed class ConversionOperatorDeclarationBench{{index}}
               {
                   public static implicit operator int(ConversionOperatorDeclarationBench{{index}} value) => 0;
               }
               """;

    /// <summary>Builds one clean or violating type-parameter-list type.</summary>
    /// <param name="index">The synthetic declaration index.</param>
    /// <param name="violating">Whether to emit a violating layout.</param>
    /// <returns>The generated declaration block.</returns>
    private static string GenerateTypeParameterListType(int index, bool violating)
        => violating
            ? $$"""
               internal sealed class TypeParameterListBench{{index}}<T1,
                   T2>
               {
               }
               """
            : $$"""
               internal sealed class TypeParameterListBench{{index}}<T1, T2>
               {
               }
               """;

    /// <summary>Builds one clean or violating function-pointer field.</summary>
    /// <param name="index">The synthetic declaration index.</param>
    /// <param name="violating">Whether to emit a violating layout.</param>
    /// <returns>The generated field block.</returns>
    private static string GenerateFunctionPointerField(int index, bool violating)
        => violating
            ? $$"""
               private delegate*<int,
                   string,
                   void> _field{{index}};
               """
            : $$"""
               private delegate*<int, string, void> _field{{index}};
               """;
}
