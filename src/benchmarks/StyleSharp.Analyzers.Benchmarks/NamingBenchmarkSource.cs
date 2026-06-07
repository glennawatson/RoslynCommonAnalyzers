// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for naming analyzer benchmark suites.</summary>
internal static class NamingBenchmarkSource
{
    /// <summary>Builds a compilation unit that exercises clean or violating parameter naming patterns.</summary>
    /// <param name="members">The number of synthetic members to emit.</param>
    /// <param name="violating">Whether to emit parameter naming violations.</param>
    /// <returns>The generated source text.</returns>
    public static string GenerateParameterSource(int members, bool violating)
        => $$"""
           namespace Bench;

           internal sealed class C
           {
           {{BenchmarkSourceText.JoinBlocks(members, i => GenerateParameterMember(i, violating))}}
           }
           """;

    /// <summary>Builds a compilation unit that exercises clean or violating local-variable naming patterns.</summary>
    /// <param name="members">The number of synthetic members to emit.</param>
    /// <param name="violating">Whether to emit local-variable naming violations.</param>
    /// <returns>The generated source text.</returns>
    public static string GenerateLocalVariableSource(int members, bool violating)
        => $$"""
           namespace Bench;

           internal sealed class C
           {
           {{BenchmarkSourceText.JoinBlocks(members, i => GenerateLocalVariableMember(i, violating))}}
           }
           """;

    /// <summary>Builds a compilation unit that exercises clean or violating field naming patterns.</summary>
    /// <param name="types">The number of synthetic types to emit.</param>
    /// <param name="violating">Whether to emit field naming violations.</param>
    /// <returns>The generated source text.</returns>
    public static string GenerateFieldSource(int types, bool violating)
        => $$"""
           namespace Bench;

           {{BenchmarkSourceText.JoinBlocks(types, i => GenerateFieldType(i, violating))}}
           """;

    /// <summary>Builds a compilation unit that exercises clean or violating element naming patterns.</summary>
    /// <param name="types">The number of synthetic type groups to emit.</param>
    /// <param name="violating">Whether to emit element naming violations.</param>
    /// <returns>The generated source text.</returns>
    public static string GenerateElementSource(int types, bool violating)
        => $$"""
           namespace Bench;

           {{BenchmarkSourceText.JoinBlocks(types, i => GenerateElementGroup(i, violating))}}
           """;

    /// <summary>Builds one clean or violating method declaration for parameter naming analysis.</summary>
    /// <param name="index">The synthetic member index.</param>
    /// <param name="violating">Whether to emit a violation.</param>
    /// <returns>The generated member block.</returns>
    private static string GenerateParameterMember(int index, bool violating)
    {
        var parameterName = violating ? $"Value{index}" : $"value{index}";
        return $$"""
               internal int M{{index}}(int {{parameterName}})
               {
                   return {{parameterName}} + {{index}};
               }
               """;
    }

    /// <summary>Builds one clean or violating method declaration for local-variable naming analysis.</summary>
    /// <param name="index">The synthetic member index.</param>
    /// <param name="violating">Whether to emit a violation.</param>
    /// <returns>The generated member block.</returns>
    private static string GenerateLocalVariableMember(int index, bool violating)
    {
        var countName = violating ? $"Count{index}" : $"count{index}";
        var itemName = violating ? $"Item{index}" : $"item{index}";

        return $$"""
               internal int M{{index}}()
               {
                   const int Max = 3;
                   int {{countName}} = 0;

                   foreach (int {{itemName}} in new[] { 1, 2, Max })
                   {
                       {{countName}} += {{itemName}};
                   }

                   return {{countName}};
               }
               """;
    }

    /// <summary>Builds one clean or violating type declaration for field naming analysis.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <param name="violating">Whether to emit a violation.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateFieldType(int index, bool violating)
    {
        var constName = violating ? $"max{index}" : $"Max{index}";
        var staticReadonlyName = violating ? $"cache{index}" : $"Cache{index}";
        var readonlyName = violating ? $"value{index}" : $"Value{index}";
        var accessibleName = violating ? $"count{index}" : $"Count{index}";
        var privateName = violating ? $"State{index}" : $"_state{index}";

        return $$"""
               internal sealed class C{{index}}
               {
                   public const int {{constName}} = {{index}};
                   private static readonly int {{staticReadonlyName}} = {{index}};
                   public readonly int {{readonlyName}} = {{index}};
                   internal int {{accessibleName}} = {{index}};
                   private int {{privateName}} = {{index}};
               }
               """;
    }

    /// <summary>Builds one clean or violating group of declarations for element naming analysis.</summary>
    /// <param name="index">The synthetic type-group index.</param>
    /// <param name="violating">Whether to emit a violation.</param>
    /// <returns>The generated declaration block.</returns>
    private static string GenerateElementGroup(int index, bool violating)
    {
        var className = violating ? $"widget{index}" : $"Widget{index}";
        var propertyName = violating ? $"value{index}" : $"Value{index}";
        var eventName = violating ? $"happened{index}" : $"Happened{index}";
        var methodName = violating ? $"doThing{index}" : $"DoThing{index}";
        var enumName = violating ? $"status{index}" : $"Status{index}";
        var enumMemberName = violating ? $"ready{index}" : $"Ready{index}";
        var delegateName = violating ? $"callback{index}" : $"Callback{index}";

        return $$"""
               internal sealed class {{className}}
               {
                   public int {{propertyName}} { get; set; }

                   public event System.EventHandler {{eventName}} = delegate { };

                   public void {{methodName}}()
                   {
                   }
               }

               internal enum {{enumName}}
               {
                   {{enumMemberName}},
               }

               internal delegate void {{delegateName}}();
               """;
    }
}
