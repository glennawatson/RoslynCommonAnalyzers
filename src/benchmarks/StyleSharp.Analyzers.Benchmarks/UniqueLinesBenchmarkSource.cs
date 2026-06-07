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
}
