// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Testing;
using RoslynCommon.Analyzers.Tests;
using VerifyGuard = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.ArgumentGuardAnalyzer,
    StyleSharp.Analyzers.ArgumentGuardCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST2000 (use ArgumentNullException.ThrowIfNull) and its code fix.</summary>
public class ArgumentGuardAnalyzerUnitTest
{
    /// <summary>Shared primitive references for synthetic framework helper declarations.</summary>
    private static readonly MetadataReference[] CoreReferences = [RuntimeMetadataReferences.CoreLibrary];

    /// <summary>Verifies an <c>is null</c> guard is reported (SST2000) and rewritten to ThrowIfNull.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task IsNullGuardReplacedAsync()
    {
        const string Source = """
                              using System;

                              public class C
                              {
                                  public void M(object value)
                                  {
                                      {|SST2000:if (value is null) throw new ArgumentNullException(nameof(value));|}
                                  }
                              }
                              """;
        const string FixedSource = """
                                   using System;

                                   public class C
                                   {
                                       public void M(object value)
                                       {
                                           ArgumentNullException.ThrowIfNull(value);
                                       }
                                   }
                                   """;
        await VerifyNet80Async(Source, FixedSource);
    }

    /// <summary>Verifies an <c>== null</c> guard with a block body is reported and rewritten.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EqualsNullBlockGuardReplacedAsync()
    {
        const string Source = """
                              using System;

                              public class C
                              {
                                  public void M(object value)
                                  {
                                      {|SST2000:if (value == null)
                                      {
                                          throw new ArgumentNullException(nameof(value));
                                      }|}
                                  }
                              }
                              """;
        const string FixedSource = """
                                   using System;

                                   public class C
                                   {
                                       public void M(object value)
                                       {
                                           ArgumentNullException.ThrowIfNull(value);
                                       }
                                   }
                                   """;
        await VerifyNet80Async(Source, FixedSource);
    }

    /// <summary>Verifies an existing ThrowIfNull call and a guard carrying a custom message are not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AlreadyModernOrCustomMessageIsCleanAsync()
    {
        const string Source = """
                              using System;

                              public class C
                              {
                                  public void M(object value)
                                  {
                                      ArgumentNullException.ThrowIfNull(value);
                                  }

                                  public void N(object value)
                                  {
                                      if (value is null) throw new ArgumentNullException(nameof(value), "must not be null");
                                  }
                              }
                              """;
        await VerifyNet80Async(Source, Source);
    }

    /// <summary>Verifies an IsNullOrEmpty guard is reported (SST2001) and rewritten to ThrowIfNullOrEmpty.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task IsNullOrEmptyGuardReplacedAsync()
    {
        const string Source = """
                              using System;

                              public class C
                              {
                                  public void M(string value)
                                  {
                                      {|SST2001:if (string.IsNullOrEmpty(value)) throw new ArgumentException("Value required.", nameof(value));|}
                                  }
                              }
                              """;
        const string FixedSource = """
                                   using System;

                                   public class C
                                   {
                                       public void M(string value)
                                       {
                                           ArgumentException.ThrowIfNullOrEmpty(value);
                                       }
                                   }
                                   """;
        await VerifyNet80Async(Source, FixedSource);
    }

    /// <summary>Verifies an IsNullOrWhiteSpace guard is reported (SST2002) and rewritten to ThrowIfNullOrWhiteSpace.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task IsNullOrWhiteSpaceGuardReplacedAsync()
    {
        const string Source = """
                              using System;

                              public class C
                              {
                                  public void M(string value)
                                  {
                                      {|SST2002:if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Value required.", nameof(value));|}
                                  }
                              }
                              """;
        const string FixedSource = """
                                   using System;

                                   public class C
                                   {
                                       public void M(string value)
                                       {
                                           ArgumentException.ThrowIfNullOrWhiteSpace(value);
                                       }
                                   }
                                   """;
        await VerifyNet80Async(Source, FixedSource);
    }

    /// <summary>Verifies a standard disposed guard is replaced by ObjectDisposedException.ThrowIf.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DisposedGuardReplacedAsync()
    {
        const string Source = """
                              using System;

                              public class C
                              {
                                  private bool _disposed;

                                  public void M()
                                  {
                                      {|SST2003:if (_disposed) throw new ObjectDisposedException(nameof(C));|}
                                  }
                              }
                              """;
        const string FixedSource = """
                                   using System;

                                   public class C
                                   {
                                       private bool _disposed;

                                       public void M()
                                       {
                                           ObjectDisposedException.ThrowIf(_disposed, this);
                                       }
                                   }
                                   """;
        await VerifyNet80Async(Source, FixedSource);
    }

    /// <summary>Verifies a negative range guard is replaced by ThrowIfNegative.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NegativeRangeGuardReplacedAsync()
    {
        const string Source = """
                              using System;

                              public class C
                              {
                                  public void M(int value)
                                  {
                                      {|SST2004:if (value < 0) throw new ArgumentOutOfRangeException(nameof(value));|}
                                  }
                              }
                              """;
        const string FixedSource = """
                                   using System;

                                   public class C
                                   {
                                       public void M(int value)
                                       {
                                           ArgumentOutOfRangeException.ThrowIfNegative(value);
                                       }
                                   }
                                   """;
        await VerifyNet80Async(Source, FixedSource);
    }

    /// <summary>Verifies an ambiguous range comparison is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AmbiguousRangeGuardIsCleanAsync()
    {
        const string Source = """
                              using System;

                              public class C
                              {
                                  public void M(int value, int other)
                                  {
                                      if (other < 0) throw new ArgumentOutOfRangeException(nameof(value));
                                  }
                              }
                              """;
        await VerifyNet80Async(Source, Source);
    }

    /// <summary>Verifies Fix All rewrites every guard clause in a single document in one pass.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FixAllRewritesEveryOccurrenceAsync()
    {
        const string Source = """
                              using System;

                              public class C
                              {
                                  public void A(object value)
                                  {
                                      {|SST2000:if (value is null) throw new ArgumentNullException(nameof(value));|}
                                  }

                                  public void B(object value)
                                  {
                                      {|SST2000:if (value == null) throw new ArgumentNullException(nameof(value));|}
                                  }

                                  public void D(int value)
                                  {
                                      {|SST2004:if (value < 0) throw new ArgumentOutOfRangeException(nameof(value));|}
                                  }
                              }
                              """;
        const string FixedSource = """
                                   using System;

                                   public class C
                                   {
                                       public void A(object value)
                                       {
                                           ArgumentNullException.ThrowIfNull(value);
                                       }

                                       public void B(object value)
                                       {
                                           ArgumentNullException.ThrowIfNull(value);
                                       }

                                       public void D(int value)
                                       {
                                           ArgumentOutOfRangeException.ThrowIfNegative(value);
                                       }
                                   }
                                   """;
        await VerifyNet80Async(Source, FixedSource);
    }

    /// <summary>Verifies the rule stays silent where ThrowIfNull does not exist (pre-.NET 6 reference assemblies).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>
    /// The reference set is named rather than left to the verifier's default. <c>ArgumentNullException.ThrowIfNull</c>
    /// arrived in .NET 6, so the framework this runs against is the whole subject of the test; a default that moves
    /// with the testing package would turn it into an assertion about nothing.
    /// </remarks>
    [Test]
    public async Task SilentWhenHelperUnavailableAsync()
    {
        var test = new VerifyGuard.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.NetStandard.NetStandard20,
            TestCode = """
                       using System;

                       public class C
                       {
                           public void M(object value)
                           {
                               if (value is null) throw new ArgumentNullException(nameof(value));
                           }
                       }
                       """,
        };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies helper availability can be resolved from a compilation in one place.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CreateHelpersFindsCurrentRuntimeThrowHelpersAsync()
    {
        var compilation = CreateCompilation();
        var helpers = ArgumentGuardAnalyzer.CreateHelpers(compilation);

        await Assert.That(helpers.ThrowIfNull).IsTrue();
        await Assert.That(helpers.Any).IsTrue();
    }

    /// <summary>Verifies the ArgumentException helper scan can resolve both string throw helpers in one pass.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ReadArgumentExceptionHelpersFindsBothStringHelpersAsync()
    {
        var compilation = CreateCompilation();
        var argumentException = compilation.GetTypeByMetadataName("System.ArgumentException");

        ArgumentGuardAnalyzer.ReadArgumentExceptionHelpers(argumentException, out var throwIfNullOrEmpty, out var throwIfNullOrWhiteSpace);

        await Assert.That(throwIfNullOrEmpty).IsTrue();
        await Assert.That(throwIfNullOrWhiteSpace).IsTrue();
    }

    /// <summary>Verifies an unresolved framework disables every helper without throwing.</summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Test]
    public async Task MissingFrameworkTypesDisableEveryHelperAsync()
    {
        var helpers = ArgumentGuardAnalyzer.CreateHelpers(CSharpCompilation.Create("MissingFramework"));

        await Assert.That(helpers.ThrowIfNull).IsFalse();
        await Assert.That(helpers.ThrowIfNullOrEmpty).IsFalse();
        await Assert.That(helpers.ThrowIfNullOrWhiteSpace).IsFalse();
        await Assert.That(helpers.ThrowIfDisposed).IsFalse();
        await Assert.That(helpers.Range).IsEmpty();
        await Assert.That(helpers.Any).IsFalse();
    }

    /// <summary>Verifies helper discovery ignores fields, instance methods, and unrelated static methods.</summary>
    /// <param name="members">Members declared on the candidate type.</param>
    /// <param name="empty">Whether the empty-string helper is available.</param>
    /// <param name="whiteSpace">Whether the whitespace helper is available.</param>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Test]
    [Arguments("", false, false)]
    [Arguments("public static int ThrowIfNullOrEmpty; public static int ThrowIfNullOrWhiteSpace;", false, false)]
    [Arguments("public void ThrowIfNullOrEmpty() { } public void ThrowIfNullOrWhiteSpace() { }", false, false)]
    [Arguments("public static void Other() { }", false, false)]
    [Arguments("public static void ThrowIfNullOrEmpty() { }", true, false)]
    [Arguments("public static void ThrowIfNullOrWhiteSpace() { }", false, true)]
    [Arguments("public static void ThrowIfNullOrWhiteSpace() { } public static void ThrowIfNullOrEmpty() { }", true, true)]
    public async Task StringHelperDiscoveryRequiresRecognizedStaticMethodsAsync(string members, bool empty, bool whiteSpace)
    {
        var compilation = CreateHelperCompilation($"class Candidate {{ {members} }}");
        var type = compilation.GetTypeByMetadataName("Candidate");

        ArgumentGuardAnalyzer.ReadArgumentExceptionHelpers(type, out var actualEmpty, out var actualWhiteSpace);

        await Assert.That(actualEmpty).IsEqualTo(empty);
        await Assert.That(actualWhiteSpace).IsEqualTo(whiteSpace);
    }

    /// <summary>Verifies null and disposal helpers must be static methods with their exact names.</summary>
    /// <param name="nullMembers">Members of the null-exception stub.</param>
    /// <param name="disposedMembers">Members of the disposed-exception stub.</param>
    /// <param name="expected">Whether both helpers are available.</param>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Test]
    [Arguments("", "", false)]
    [Arguments("public static int ThrowIfNull;", "public static int ThrowIf;", false)]
    [Arguments("public void ThrowIfNull() { }", "public void ThrowIf() { }", false)]
    [Arguments("public static void Other() { }", "public static void Other() { }", false)]
    [Arguments("public static void ThrowIfNull() { }", "public static void ThrowIf() { }", true)]
    [Arguments("public void ThrowIfNull(int value) { } public static void ThrowIfNull() { }", "public void ThrowIf(int value) { } public static void ThrowIf() { }", true)]
    public async Task NullAndDisposedHelperDiscoveryRequiresStaticMethodsAsync(string nullMembers, string disposedMembers, bool expected)
    {
        var compilation = CreateHelperCompilation($$"""
            namespace System
            {
                public class ArgumentNullException { {{nullMembers}} }
                public class ObjectDisposedException { {{disposedMembers}} }
            }
            """);
        var helpers = ArgumentGuardAnalyzer.CreateHelpers(compilation);

        await Assert.That(helpers.ThrowIfNull).IsEqualTo(expected);
        await Assert.That(helpers.ThrowIfDisposed).IsEqualTo(expected);
    }

    /// <summary>Verifies range discovery retains only static methods whose names start with ThrowIf.</summary>
    /// <param name="members">Members of the range-exception stub.</param>
    /// <param name="expected">The discovered names in declaration order.</param>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Test]
    [Arguments("", "")]
    [Arguments("public static int ThrowIfZero; public void ThrowIfNegative() { } public static void Other() { }", "")]
    [Arguments("public static void ThrowIfNegative() { }", "ThrowIfNegative")]
    [Arguments(
        """
        public static int ThrowIfZero;
        public void ThrowIfNegative() { }
        public static void Other() { }
        public static void ThrowIfEqual() { }
        public static void ThrowIfNotEqual() { }
        """,
        "ThrowIfEqual,ThrowIfNotEqual")]
    public async Task RangeHelperDiscoveryFiltersMemberKindAndPrefixAsync(string members, string expected)
    {
        var compilation = CreateHelperCompilation($"namespace System {{ public class ArgumentOutOfRangeException {{ {members} }} }}");
        var helpers = ArgumentGuardAnalyzer.CreateHelpers(compilation);

        await Assert.That(string.Join(",", helpers.Range)).IsEqualTo(expected);
    }

    /// <summary>Verifies any one helper is sufficient and an empty helper set is unavailable.</summary>
    /// <param name="nullGuard">Whether the null helper exists.</param>
    /// <param name="empty">Whether the empty-string helper exists.</param>
    /// <param name="whiteSpace">Whether the whitespace helper exists.</param>
    /// <param name="disposed">Whether the disposal helper exists.</param>
    /// <param name="range">Whether a range helper exists.</param>
    /// <param name="expected">Whether any helper is available.</param>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Test]
    [Arguments(false, false, false, false, false, false)]
    [Arguments(true, false, false, false, false, true)]
    [Arguments(false, true, false, false, false, true)]
    [Arguments(false, false, true, false, false, true)]
    [Arguments(false, false, false, true, false, true)]
    [Arguments(false, false, false, false, true, true)]
    public async Task AnyHelperIncludesEachIndependentFamilyAsync(bool nullGuard, bool empty, bool whiteSpace, bool disposed, bool range, bool expected)
    {
        var helpers = new ArgumentGuardAnalyzer.GuardHelpers(nullGuard, empty, whiteSpace, disposed, range ? ["ThrowIfZero"] : []);

        await Assert.That(helpers.Any).IsEqualTo(expected);
    }

    /// <summary>Verifies the benchmark predicate recognizes all guard families and rejects near misses.</summary>
    /// <param name="guard">The candidate guard statement.</param>
    /// <param name="expected">Whether the guard can use an available helper.</param>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Test]
    [Arguments("if (value is null) throw new ArgumentNullException(nameof(value));", true)]
    [Arguments("if (value is not null) throw new ArgumentNullException(nameof(value));", false)]
    [Arguments("if (disposed) throw new ObjectDisposedException(nameof(C));", true)]
    [Arguments("if (disposed) throw new ObjectDisposedException(nameof(C), \"message\");", false)]
    [Arguments("if (string.IsNullOrEmpty(value)) throw new ArgumentException();", true)]
    [Arguments("if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException();", true)]
    [Arguments("if (string.Equals(value, other)) throw new ArgumentException();", false)]
    [Arguments("if (value < 0) throw new ArgumentOutOfRangeException(nameof(value));", true)]
    [Arguments("if (value <= 0) throw new ArgumentOutOfRangeException(nameof(value));", true)]
    [Arguments("if (value == 0) throw new ArgumentOutOfRangeException(nameof(value));", true)]
    [Arguments("if (value > 1) throw new ArgumentOutOfRangeException(nameof(value));", true)]
    [Arguments("if (value >= 1) throw new ArgumentOutOfRangeException(nameof(value));", true)]
    [Arguments("if (value < 1) throw new ArgumentOutOfRangeException(nameof(value));", true)]
    [Arguments("if (value <= 1) throw new ArgumentOutOfRangeException(nameof(value));", true)]
    [Arguments("if (value == 1) throw new ArgumentOutOfRangeException(nameof(value));", true)]
    [Arguments("if (value != 1) throw new ArgumentOutOfRangeException(nameof(value));", true)]
    [Arguments("if (other < 0) throw new ArgumentOutOfRangeException(nameof(value));", false)]
    [Arguments("if (disposed) { }", false)]
    public async Task BenchmarkPredicateMatchesSupportedGuardsAsync(string guard, bool expected)
    {
        var tree = CSharpSyntaxTree.ParseText($"class C {{ void M() {{ {guard} }} }}");
        var root = await tree.GetRootAsync();
        var statement = root.DescendantNodes().OfType<IfStatementSyntax>().Single();
        var helpers = ArgumentGuardAnalyzer.CreateBenchmarkHelpers();

        await Assert.That(ArgumentGuardAnalyzer.WouldReportForBenchmark(statement, helpers)).IsEqualTo(expected);
    }

    /// <summary>Verifies a matching guard remains silent when its particular helper is unavailable.</summary>
    /// <param name="guard">The candidate guard statement.</param>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Test]
    [Arguments("if (value is null) throw new ArgumentNullException(nameof(value));")]
    [Arguments("if (disposed) throw new ObjectDisposedException(nameof(C));")]
    [Arguments("if (string.IsNullOrEmpty(value)) throw new ArgumentException();")]
    [Arguments("if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException();")]
    [Arguments("if (value < 0) throw new ArgumentOutOfRangeException(nameof(value));")]
    public async Task BenchmarkPredicateRequiresTheMatchingHelperAsync(string guard)
    {
        var tree = CSharpSyntaxTree.ParseText($"class C {{ void M() {{ {guard} }} }}");
        var root = await tree.GetRootAsync();
        var statement = root.DescendantNodes().OfType<IfStatementSyntax>().Single();
        var helpers = new ArgumentGuardAnalyzer.GuardHelpers(false, false, false, false, ["ThrowIfZero"]);

        await Assert.That(ArgumentGuardAnalyzer.WouldReportForBenchmark(statement, helpers)).IsFalse();
    }

    /// <summary>Verifies disposal suggestions respect member and local-function static contexts.</summary>
    /// <param name="source">The source containing one disposed guard.</param>
    /// <param name="expected">Whether the current syntactic context permits a suggestion.</param>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Test]
    [Arguments("class C { void M() { if (disposed) throw new ObjectDisposedException(nameof(C)); } }", true)]
    [Arguments("class C { static void M() { if (disposed) throw new ObjectDisposedException(nameof(C)); } }", false)]
    [Arguments("class C { void M() { void Local() { if (disposed) throw new ObjectDisposedException(nameof(C)); } } }", true)]
    [Arguments("class C { void M() { static void Local() { if (disposed) throw new ObjectDisposedException(nameof(C)); } } }", false)]
    [Arguments("if (disposed) throw new ObjectDisposedException(nameof(C));", true)]
    public async Task BenchmarkDisposedGuardUsesItsNearestDeclarationAsync(string source, bool expected)
    {
        var tree = CSharpSyntaxTree.ParseText(source);
        var root = await tree.GetRootAsync();
        var statement = root.DescendantNodes().OfType<IfStatementSyntax>().Single();
        var helpers = ArgumentGuardAnalyzer.CreateBenchmarkHelpers();

        await Assert.That(ArgumentGuardAnalyzer.WouldReportForBenchmark(statement, helpers)).IsEqualTo(expected);
    }

    /// <summary>Verifies a detached disposal guard has no instance context.</summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Test]
    public async Task DetachedDisposalGuardIsRejectedAsync()
    {
        var statement = (IfStatementSyntax)SyntaxFactory.ParseStatement("if (disposed) throw new ObjectDisposedException(nameof(C));");
        var helpers = ArgumentGuardAnalyzer.CreateBenchmarkHelpers();

        await Assert.That(ArgumentGuardAnalyzer.WouldReportForBenchmark(statement, helpers)).IsFalse();
    }

    /// <summary>Pins the current suggestion for a top-level guard even though no containing instance exists.</summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Test]
    public async Task TopLevelDisposalGuardCurrentlyReportsAsync()
    {
        var test = new CSharpAnalyzerVerifier<ArgumentGuardAnalyzer>.Test
        {
            ReferenceAssemblies = AnalyzerFrameworks.Net80,
            TestCode = """
                using System;
                {|SST2003:if (DateTime.Now.Ticks > 0) throw new ObjectDisposedException(nameof(Object));|}
                """,
        };
        test.SolutionTransforms.Add(static (solution, projectId) =>
            solution.WithProjectCompilationOptions(projectId, new CSharpCompilationOptions(OutputKind.ConsoleApplication)));

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies every guard family remains silent on a framework without runtime helpers.</summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Test]
    public async Task LegacyFrameworkLeavesAllGuardFamiliesUnchangedAsync()
    {
        var test = new VerifyGuard.Test
        {
            ReferenceAssemblies = AnalyzerFrameworks.NetStandard20,
            TestCode = """
                using System;
                public class C
                {
                    public void M(object value, string text, int count, bool disposed)
                    {
                        if (value is null) throw new ArgumentNullException(nameof(value));
                        if (string.IsNullOrEmpty(text)) throw new ArgumentException();
                        if (string.IsNullOrWhiteSpace(text)) throw new ArgumentException();
                        if (disposed) throw new ObjectDisposedException(nameof(C));
                        if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
                    }
                }
                """,
        };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies disposal guards in static members and static local functions have no fix.</summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Test]
    public async Task StaticDisposalGuardsAreCleanAsync()
    {
        const string Source = """
            using System;
            public class C
            {
                public static void M(bool disposed)
                {
                    if (disposed) throw new ObjectDisposedException(nameof(C));
                }

                public void N(bool disposed)
                {
                    static void Local(bool value)
                    {
                        if (value) throw new ObjectDisposedException(nameof(C));
                    }

                    Local(disposed);
                }
            }
            """;

        await VerifyNet80Async(Source, Source);
    }

    /// <summary>Verifies an instance local function can use its containing instance in the disposal fix.</summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task InstanceLocalDisposalGuardIsReplacedAsync() =>
        VerifyNet80Async(
            """
            using System;
            public class C
            {
                public void M(bool disposed)
                {
                    void Local()
                    {
                        {|SST2003:if (disposed) throw new ObjectDisposedException(nameof(C));|}
                    }

                    Local();
                }
            }
            """,
            """
            using System;
            public class C
            {
                public void M(bool disposed)
                {
                    void Local()
                    {
                        ObjectDisposedException.ThrowIf(disposed, this);
                    }

                    Local();
                }
            }
            """);

    /// <summary>Compiles framework stubs against the shared primitive reference.</summary>
    /// <param name="source">The framework declarations.</param>
    /// <returns>The compilation containing the stubs.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static CSharpCompilation CreateHelperCompilation(string source) =>
        CSharpCompilation.Create("GuardHelpers", [CSharpSyntaxTree.ParseText(source)], CoreReferences);

    /// <summary>Runs a code-fix verification against the .NET 8 reference assemblies (where the helper exists).</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <param name="fixedSource">The expected fixed source.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyNet80Async(string source, string fixedSource)
    {
        var test = new VerifyGuard.Test { ReferenceAssemblies = ReferenceAssemblies.Net.Net80, TestCode = source, FixedCode = fixedSource };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Creates a minimal compilation against the current runtime reference set.</summary>
    /// <returns>The compilation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static CSharpCompilation CreateCompilation() =>
        CSharpCompilation.Create("Bench", references: RuntimeMetadataReferences.Platform);
}
