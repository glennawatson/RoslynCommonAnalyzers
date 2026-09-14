// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;
using RoslynCommon.Analyzers.Tests;

using AnalyzeEmptyCollection = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    StyleSharp.Analyzers.Sst2100EmptyCollectionExpressionAnalyzer>;
using VerifyEmptyCollection = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst2100EmptyCollectionExpressionAnalyzer,
    StyleSharp.Analyzers.CollectionExpressionCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST2100 (use an empty collection expression).</summary>
public class EmptyCollectionExpressionAnalyzerUnitTest
{
    /// <summary>Verifies empty factories and arrays are replaced for every supported explicit target.</summary>
    /// <param name="type">The explicit target type.</param>
    /// <param name="expression">The empty collection creation.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("int[]", "new int[0]")]
    [Arguments("int[]", "new int[] { }")]
    [Arguments("int[]", "System.Array.Empty<int>()")]
    [Arguments("IEnumerable<int>", "Enumerable.Empty<int>()")]
    [Arguments("IEnumerable<int>", "System.Linq.Enumerable.Empty<int>()")]
    [Arguments("IList<int>", "new List<int>()")]
    [Arguments("IReadOnlyList<int>", "new List<int>()")]
    [Arguments("ICollection<int>", "new List<int>()")]
    [Arguments("IReadOnlyCollection<int>", "new List<int>()")]
    public async Task SupportedEmptyCreationsAreFixedAsync(string type, string expression)
    {
        var test = new VerifyEmptyCollection.Test
        {
            ReferenceAssemblies = AnalyzerFrameworks.Net80,
            TestCode = $$"""using System.Collections.Generic; using System.Linq; class C { {{type}} M() => {|SST2100:{{expression}}|}; }""",
            FixedCode = $$"""using System.Collections.Generic; using System.Linq; class C { {{type}} M() => []; }""",
        };
        await test.RunAsync();
    }

    /// <summary>Verifies nonempty creations and unsupported targets do not offer a replacement.</summary>
    /// <param name="member">The member containing a near miss.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("int[] M() => new int[1];")]
    [Arguments("int[] M(int count) => new int[count];")]
    [Arguments("int[] M() => new int[] { 1 };")]
    [Arguments("int[,] M() => new int[,] { };")]
    [Arguments("int[,] M() => new int[0, 0];")]
    [Arguments("int[][] M() => new int[0][];")]
    [Arguments("List<int> M() => new List<int>(1);")]
    [Arguments("List<int> M() => new List<int>() { };")]
    [Arguments("List<int> M() => new List<int> { };")]
    [Arguments("List<int> M() => new List<int>() { 1 };")]
    [Arguments("object M() => new List<int>();")]
    [Arguments("HashSet<int> M() => new HashSet<int>();")]
    [Arguments("IEnumerable<int> M() => Enumerable.Range(0, 0);")]
    [Arguments("IEnumerable<int> M() => Enumerable.Repeat(0, 0);")]
    [Arguments("int[] M() => Empty<int>(); int[] Empty<T>() => null;")]
    [Arguments("int[] M() => Factory.Empty<int>(); class Factory { public static int[] Empty<T>() => null; }")]
    [Arguments("int[] M() => Array.Empty(); class Array { public static int[] Empty() => null; }")]
    [Arguments("int[] M() => Array.Empty<int, int>(); class Array { public static int[] Empty<T, U>() => null; }")]
    [Arguments("void M() { Use(System.Array.Empty<int>()); } void Use<T>(T[] values) { }")]
    public async Task UnsupportedCreationHasNoFixAsync(string member)
    {
        var source = $$"""using System.Collections.Generic; using System.Linq; class C { {{member}} }""";
        var test = new VerifyEmptyCollection.Test { ReferenceAssemblies = AnalyzerFrameworks.Net80, TestCode = source, FixedCode = source };
        await test.RunAsync();
    }

    /// <summary>Verifies empty creations remain unchanged before collection expressions are available.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task CSharp11HasNoFixAsync()
    {
        const string Source = "class C { int[] M() => new int[0]; }";
        var test = new VerifyEmptyCollection.Test { ReferenceAssemblies = AnalyzerFrameworks.Net80, TestCode = Source, FixedCode = Source };
        test.SolutionTransforms.Add(static (solution, projectId) => solution.WithProjectParseOptions(projectId, new CSharpParseOptions(LanguageVersion.CSharp11)));
        await test.RunAsync();
    }

    /// <summary>Verifies invalid conversions to a type parameter and unresolved return types are ignored.</summary>
    /// <param name="member">The incomplete member.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("T M<T>() => new int[0];")]
    [Arguments("Missing M() => new object();")]
    public async Task UnresolvedTargetIsIgnoredAsync(string member)
    {
        var test = new VerifyEmptyCollection.Test { ReferenceAssemblies = AnalyzerFrameworks.Net80, TestCode = $$"""class C { {{member}} }""", CompilerDiagnostics = CompilerDiagnostics.None };
        await test.RunAsync();
    }

    /// <summary>Verifies an invalid void return still reports because its expression resolves to an array target.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task InvalidVoidReturnStillReportsArrayAsync()
    {
        const string Source = "class C { void M() { return {|SST2100:new int[0]|}; } }";
        var test = new AnalyzeEmptyCollection.Test { ReferenceAssemblies = AnalyzerFrameworks.Net80, CompilerDiagnostics = CompilerDiagnostics.None, TestCode = Source };
        await test.RunAsync();
    }

    /// <summary>Verifies missing framework collection definitions leave a user-defined target unchanged.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task MissingFrameworkCollectionsAreIgnoredAsync()
    {
        var compilation = CSharpCompilation.Create(
            nameof(MissingFrameworkCollectionsAreIgnoredAsync),
            [CSharpSyntaxTree.ParseText("class C { C M() => new C(); }")]);
        var diagnostics = await compilation.WithAnalyzers([new Sst2100EmptyCollectionExpressionAnalyzer()]).GetAnalyzerDiagnosticsAsync();
        await Assert.That(diagnostics).IsEmpty();
    }

    /// <summary>Verifies standard empty collection creations are replaced with brackets.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task EmptyCreationsAreFixedAsync()
    {
        const string Source = """
                              using System;
                              using System.Collections.Generic;

                              public class C
                              {
                                  public int[] A = {|SST2100:Array.Empty<int>()|};
                                  public List<int> B = {|SST2100:new List<int>()|};
                              }
                              """;
        const string FixedSource = """
                                   using System;
                                   using System.Collections.Generic;

                                   public class C
                                   {
                                       public int[] A = [];
                                       public List<int> B = [];
                                   }
                                   """;
        var test = new VerifyEmptyCollection.Test { ReferenceAssemblies = ReferenceAssemblies.Net.Net80, TestCode = Source, FixedCode = FixedSource };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies Fix All rewrites every empty-collection occurrence in one pass.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task FixAllRewritesEveryOccurrenceAsync()
    {
        const string Source = """
                              using System;
                              using System.Collections.Generic;

                              public class C
                              {
                                  public int[] A = {|SST2100:Array.Empty<int>()|};
                                  public List<int> B = {|SST2100:new List<int>()|};
                                  public string[] D = {|SST2100:Array.Empty<string>()|};
                              }
                              """;
        const string FixedSource = """
                                   using System;
                                   using System.Collections.Generic;

                                   public class C
                                   {
                                       public int[] A = [];
                                       public List<int> B = [];
                                       public string[] D = [];
                                   }
                                   """;
        var test = new VerifyEmptyCollection.Test { ReferenceAssemblies = ReferenceAssemblies.Net.Net80, TestCode = Source, FixedCode = FixedSource };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies a targetless var initialization is not reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task VarInitializationIsCleanAsync() =>
        VerifyEmptyCollection.VerifyAnalyzerAsync(
            """
            using System;

            public class C
            {
                public void M()
                {
                    var values = Array.Empty<int>();
                }
            }
            """);
}
