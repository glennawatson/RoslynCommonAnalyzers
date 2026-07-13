// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using VerifyMutableStatic = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst1499MutableStaticFieldAnalyzer,
    StyleSharp.Analyzers.Sst1499MutableStaticFieldCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST1499 (do not expose a mutable static field) and its fix.</summary>
public class MutableStaticFieldAnalyzerUnitTest
{
    /// <summary>Verifies a visible static field that nothing reassigns is reported and simply gains <c>readonly</c>.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NeverReassignedFieldGainsReadonlyAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public static int {|SST1499:Timeout|} = 30;
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public static readonly int Timeout = 30;
                                   }
                                   """;
        await VerifyMutableStatic.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a field only the static constructor writes still takes <c>readonly</c>.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FieldWrittenOnlyInTheStaticConstructorGainsReadonlyAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public static int {|SST1499:Timeout|};

                                  static C() => Timeout = 30;
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public static readonly int Timeout;

                                       static C() => Timeout = 30;
                                   }
                                   """;
        await VerifyMutableStatic.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a field something else reassigns is reported, and no fix is offered for it.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>Adding <c>readonly</c> would not compile; what to do instead is the author's decision.</remarks>
    [Test]
    public async Task ReassignedFieldIsReportedWithoutAFixAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public static int {|SST1499:Timeout|} = 30;

                                  public static void Configure(int value) => Timeout = value;
                              }
                              """;
        await VerifyMutableStatic.VerifyCodeFixAsync(Source, Source);
    }

    /// <summary>Verifies a write from inside a lambda in the static constructor still blocks the fix.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task WriteThroughALambdaBlocksTheFixAsync()
    {
        const string Source = """
                              using System;

                              public class C
                              {
                                  public static int {|SST1499:Timeout|};

                                  static C()
                                  {
                                      Action reset = () => Timeout = 30;
                                      reset();
                                  }
                              }
                              """;
        await VerifyMutableStatic.VerifyCodeFixAsync(Source, Source);
    }

    /// <summary>Verifies a readonly array or mutable collection is reported, and never mechanically rewritten.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>The reference cannot move; the contents can. What to expose instead is a design decision.</remarks>
    [Test]
    public async Task ReadonlyCollectionsAndArraysAreReportedWithoutAFixAsync()
    {
        const string Source = """
                              using System.Collections.Generic;

                              public class C
                              {
                                  public static readonly int[] {|SST1499:Values|} = new int[3];

                                  public static readonly List<int> {|SST1499:Items|} = new List<int>();

                                  public static readonly Dictionary<string, int> {|SST1499:Map|} = new Dictionary<string, int>();

                                  public static readonly HashSet<string> {|SST1499:Names|} = new HashSet<string>();

                                  public static readonly IList<int> {|SST1499:Exposed|} = new List<int>();
                              }
                              """;
        await VerifyMutableStatic.VerifyCodeFixAsync(Source, Source);
    }

    /// <summary>Verifies a readonly field of a type whose contents cannot be rewritten is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ImmutableReadonlyFieldsAreCleanAsync()
    {
        const string Source = """
                              using System.Collections.Frozen;
                              using System.Collections.Generic;
                              using System.Collections.Immutable;
                              using System.Collections.ObjectModel;

                              public class C
                              {
                                  public static readonly string Name = "n";

                                  public static readonly int Limit = 3;

                                  public static readonly ImmutableArray<int> Values = ImmutableArray<int>.Empty;

                                  public static readonly ImmutableList<int> Items = ImmutableList<int>.Empty;

                                  public static readonly FrozenDictionary<string, int> Map = FrozenDictionary<string, int>.Empty;

                                  public static readonly ReadOnlyCollection<int> Wrapped = new ReadOnlyCollection<int>(new List<int>());

                                  public static readonly IReadOnlyList<int> Exposed = new List<int>();
                              }
                              """;
        var test = new VerifyMutableStatic.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            TestCode = Source,
        };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies constants, private fields, and per-thread state are not shared mutable state.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConstantsPrivateFieldsAndThreadStaticAreCleanAsync()
        => await VerifyMutableStatic.VerifyAnalyzerAsync(
            """
            using System;

            public class C
            {
                public const int Limit = 3;

                private static int _counter;

                [ThreadStatic]
                public static int Current;

                public int Next() => ++_counter + Current;
            }
            """);

    /// <summary>Verifies a field only its own type can reach — a public one inside a private nested type — is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FieldInPrivateNestedTypeIsCleanAsync()
        => await VerifyMutableStatic.VerifyAnalyzerAsync(
            """
            public class Outer
            {
                public int Read() => Inner.Counter;

                private class Inner
                {
                    public static int Counter = 1;
                }
            }
            """);

    /// <summary>Verifies an assembly-visible field is reported by default.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InternalFieldIsReportedByDefaultAsync()
        => await VerifyMutableStatic.VerifyAnalyzerAsync(
            """
            internal class C
            {
                internal static int {|SST1499:Counter|};

                protected internal static int {|SST1499:Shared|};

                public static int {|SST1499:Total|};
            }
            """);

    /// <summary>Verifies the rule-specific key can narrow the rule to the assembly's public surface.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InternalFieldIsSkippedWhenIncludeInternalIsFalseAsync()
    {
        var test = new VerifyMutableStatic.Test
        {
            TestCode = """
                       public class C
                       {
                           internal static int Counter;

                           public static int {|SST1499:Total|};

                           protected static int {|SST1499:Shared|};
                       }
                       """,
        };

        test.TestState.AnalyzerConfigFiles.Add(
            ("/.editorconfig", """
            root = true
            [*.cs]
            stylesharp.SST1499.include_internal = false

            """));

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies the project-wide key applies when no rule-specific key is set.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task GeneralIncludeInternalKeyAppliesAsync()
    {
        var test = new VerifyMutableStatic.Test
        {
            TestCode = """
                       public class C
                       {
                           internal static int Counter;

                           public static int {|SST1499:Total|};
                       }
                       """,
        };

        test.TestState.AnalyzerConfigFiles.Add(
            ("/.editorconfig", """
            root = true
            [*.cs]
            stylesharp.include_internal = false

            """));

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies an unparsable value falls back to the default rather than silently disabling the rule.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UnparsableIncludeInternalFallsBackToTheDefaultAsync()
    {
        var test = new VerifyMutableStatic.Test
        {
            TestCode = """
                       public class C
                       {
                           internal static int {|SST1499:Counter|};
                       }
                       """,
        };

        test.TestState.AnalyzerConfigFiles.Add(
            ("/.editorconfig", """
            root = true
            [*.cs]
            stylesharp.SST1499.include_internal = sometimes

            """));

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies every variable of one declaration is reported, and one fix updates them all.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EveryVariableOfADeclarationIsReportedAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public static int {|SST1499:First|} = 1, {|SST1499:Second|} = 2;
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public static readonly int First = 1, Second = 2;
                                   }
                                   """;
        await VerifyMutableStatic.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a field of a mutable struct is reported but never given <c>readonly</c>.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>
    /// <c>readonly</c> would stop <c>Origin.X = 1</c> compiling, and would quietly turn a call to one of the
    /// struct's mutating methods into a call against a copy.
    /// </remarks>
    [Test]
    public async Task MutableStructFieldIsReportedWithoutAFixAsync()
    {
        const string Source = """
                              public struct Point
                              {
                                  public int X;
                              }

                              public class C
                              {
                                  public static Point {|SST1499:Origin|};

                                  static C() => Origin = default;

                                  public static void Move() => Origin.X = 1;
                              }
                              """;
        await VerifyMutableStatic.VerifyCodeFixAsync(Source, Source);
    }

    /// <summary>Verifies a volatile field is reported but never given <c>readonly</c>, which C# does not allow beside it.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task VolatileFieldIsReportedWithoutAFixAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public static volatile int {|SST1499:Flag|};

                                  static C() => Flag = 1;
                              }
                              """;
        await VerifyMutableStatic.VerifyCodeFixAsync(Source, Source);
    }
}
