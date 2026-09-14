// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.Composition.Hosting;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.Text;
using RoslynCommon.Analyzers.Tests;

using Verify = StyleSharp.Analyzers.Tests.CSharpCodeRefactoringVerifier<StyleSharp.Analyzers.Sst2444GeneratedRegexRefactoringProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>
/// Unit tests for the SST2444 source-generated regular-expression refactoring. The generated partial method
/// is completed by the framework's own source generator at build time; the test host does not run that
/// generator, so the converted declaration is expected to carry the "needs an implementation part" compiler
/// diagnostic that the generator would otherwise satisfy.
/// </summary>
public class GeneratedRegexRefactoringUnitTest
{
    /// <summary>The cached minimal references used to test incomplete framework surfaces.</summary>
    private static readonly ImmutableArray<MetadataReference> MinimalReferences = [RuntimeMetadataReferences.CoreLibrary];

    /// <summary>Checks selection inside a qualified construction retains the pattern's literal spelling.</summary>
    /// <param name="type">The qualified regex type.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("R::Regex")]
    [Arguments("global::System.Text.RegularExpressions.Regex")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task QualifiedConstructionKeepsVerbatimPatternAsync(string type) =>
        VerifyAsync(
            $$"""
            using System.Text.RegularExpressions;
            using R = System.Text.RegularExpressions;

            public struct C
            {
                public Regex Build() => new {{type}}([|@"\d+"|]);
            }
            """,
            """
            using System.Text.RegularExpressions;
            using R = System.Text.RegularExpressions;

            public partial struct C
            {
                public Regex Build() => PatternRegex();

                [GeneratedRegex(@"\d+")]
                private static partial Regex {|CS8795:PatternRegex|}();
            }
            """);

    /// <summary>Verifies missing framework symbols, unresolved constructors, and top-level code receive no action.</summary>
    /// <param name="source">The document containing exactly one object construction.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("class Regex { public Regex(string pattern) { } } class C { Regex M() => new Regex(\"a\"); }")]
    [Arguments("namespace System.Text.RegularExpressions { class Regex { } } class C { object M() => new System.Text.RegularExpressions.Regex(\"a\"); }")]
    [Arguments("new System.Text.RegularExpressions.Regex(\"a\"); namespace System.Text.RegularExpressions { class Regex { public Regex(string pattern) { } } }")]
    [Arguments("class C { object M() => new int(); }")]
    [Arguments("class C { object M() => new Regex(null); }")]
    [Arguments("class C { object M() => new Regex(); }")]
    [Arguments("class C { object M() => new Regex; }")]
    public async Task IncompleteCompilationHasNoRefactoringAsync(string source)
    {
        const string AttributeStub = "namespace System.Text.RegularExpressions { class GeneratedRegexAttribute : System.Attribute { } }";
        using var workspace = new AdhocWorkspace();
        using var container = new ContainerConfiguration().WithPart<Sst2444GeneratedRegexRefactoringProvider>().CreateContainer();
        var provider = container.GetExport<CodeRefactoringProvider>();
        var project = workspace.AddProject(nameof(Test), LanguageNames.CSharp)
            .WithParseOptions(new CSharpParseOptions(LanguageVersion.CSharp11))
            .WithMetadataReferences(MinimalReferences);
        var document = project.AddDocument("Incomplete.cs", SourceText.From($"{source}\n{AttributeStub}"));
        var root = (await document.GetSyntaxRootAsync())!;
        var creation = root.DescendantNodes().OfType<ObjectCreationExpressionSyntax>().Single();
        var actions = new List<CodeAction>();
        await provider.ComputeRefactoringsAsync(new(document, creation.Span, actions.Add, CancellationToken.None));
        await Assert.That(actions).IsEmpty();
    }

    /// <summary>Verifies unmodified host declarations receive a partial modifier without losing leading trivia.</summary>
    /// <param name="kind">The host declaration kind.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("class")]
    [Arguments("struct")]
    [Arguments("record")]
    [Arguments("record struct")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task UnmodifiedHostBecomesPartialAsync(string kind) =>
        VerifyAsync(
            $$"""
            using System.Text.RegularExpressions;

            // Matcher
            {{kind}} C
            {
                public Regex Build() => [|new Regex("abc")|];
            }
            """,
            $$"""
            using System.Text.RegularExpressions;

            // Matcher
            partial {{kind}} C
            {
                public Regex Build() => PatternRegex();

                [GeneratedRegex("abc")]
                private static partial Regex {|CS8795:PatternRegex|}();
            }
            """);

    /// <summary>Verifies method names avoid existing methods, properties, and all field declarators.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task GeneratedMethodAvoidsEveryNamedMemberAsync() =>
        VerifyAsync(
            """
            using System.Text.RegularExpressions;

            public class C
            {
                public C() { }
                public int PatternRegex => 0;
                public int PatternRegex2, PatternRegex3;
                public void PatternRegex4() { }
                public Regex Build() => [|new System.Text.RegularExpressions.Regex("abc")|];
            }
            """,
            """
            using System.Text.RegularExpressions;

            public partial class C
            {
                public C() { }
                public int PatternRegex => 0;
                public int PatternRegex2, PatternRegex3;
                public void PatternRegex4() { }
                public Regex Build() => PatternRegex5();

                [GeneratedRegex("abc")]
                private static partial Regex {|CS8795:PatternRegex5|}();
            }
            """);

    /// <summary>Verifies exhausted numeric suffixes fall back to the host type name.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ExhaustedSuffixesUseHostNameAsync()
    {
        const int FirstSuffix = 2;
        const int SuffixCount = 98;
        var names = string.Join(", ", Enumerable.Range(FirstSuffix, SuffixCount).Select(static suffix => $"PatternRegex{suffix}"));
        var fields = $"public int PatternRegex, {names};";
        var source = $$"""
            using System.Text.RegularExpressions;
            public class C
            {
                {{fields}}
                public Regex Build() => [|new Regex("abc")|];
            }
            """;
        var expected = $$"""
            using System.Text.RegularExpressions;
            public partial class C
            {
                {{fields}}
                public Regex Build() => PatternRegexC();

                [GeneratedRegex("abc")]
                private static partial Regex {|CS8795:PatternRegexC|}();
            }
            """;
        await VerifyAsync(source, expected);
    }

    /// <summary>Verifies unsupported selections and construction shapes do not offer a refactoring.</summary>
    /// <param name="source">The source and selection.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("using System.Text.RegularExpressions; class C { [|int value;|] }")]
    [Arguments("class C { object M() => [|new object()|]; }")]
    [Arguments("using System.Text.RegularExpressions; class C { Regex M() => [|new Regex(\"a\") { }|]; }")]
    [Arguments("using System.Text.RegularExpressions; class C { Regex M(string pattern) => [|new Regex(pattern)|]; }")]
    [Arguments("using System.Text.RegularExpressions; interface I { Regex M() => [|new Regex(\"a\")|]; }")]
    [Arguments("class Regex { public Regex(string pattern) { } } class C { Regex M() => [|new global::Regex(\"a\")|]; }")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task UnsupportedConstructionIsNotOfferedAsync(string source) => VerifyNoRefactoringAsync(source);

    /// <summary>Verifies frameworks without GeneratedRegexAttribute keep runtime construction.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task MissingGeneratedRegexAttributeIsNotOfferedAsync()
    {
        var test = new Verify.Test
        {
            ReferenceAssemblies = AnalyzerFrameworks.NetStandard20,
            TestCode = "using System.Text.RegularExpressions; class C { Regex M() => [|new Regex(\"a\")|]; }",
            FixedCode = "using System.Text.RegularExpressions; class C { Regex M() => new Regex(\"a\"); }",
        };
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies C# 10 does not offer the generated-regex rewrite.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task OlderLanguageIsNotOfferedAsync()
    {
        var test = new Verify.Test
        {
            ReferenceAssemblies = AnalyzerFrameworks.Net90,
            TestCode = "using System.Text.RegularExpressions; class C { Regex M() => [|new Regex(\"a\")|]; }",
            FixedCode = "using System.Text.RegularExpressions; class C { Regex M() => new Regex(\"a\"); }",
        };
        test.SolutionTransforms.Add(static (solution, projectId) => solution.WithProjectParseOptions(projectId, new CSharpParseOptions(LanguageVersion.CSharp10)));
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies a valid single-literal construction is converted to a generated regular expression.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ValidConstructionIsConvertedAsync()
    {
        const string Source = """
                              using System.Text.RegularExpressions;

                              public class C
                              {
                                  public Regex Build() => [|new Regex("[a-z]+")|];
                              }
                              """;
        const string FixedSource = """
                                   using System.Text.RegularExpressions;

                                   public partial class C
                                   {
                                       public Regex Build() => PatternRegex();

                                       [GeneratedRegex("[a-z]+")]
                                       private static partial Regex {|CS8795:PatternRegex|}();
                                   }
                                   """;
        await VerifyAsync(Source, FixedSource);
    }

    /// <summary>Verifies an already-partial type keeps its single partial modifier.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AlreadyPartialTypeKeepsOneModifierAsync()
    {
        const string Source = """
                              using System.Text.RegularExpressions;

                              public partial class C
                              {
                                  public Regex Build() => [|new Regex("[a-z]+")|];
                              }
                              """;
        const string FixedSource = """
                                   using System.Text.RegularExpressions;

                                   public partial class C
                                   {
                                       public Regex Build() => PatternRegex();

                                       [GeneratedRegex("[a-z]+")]
                                       private static partial Regex {|CS8795:PatternRegex|}();
                                   }
                                   """;
        await VerifyAsync(Source, FixedSource);
    }

    /// <summary>Verifies an invalid pattern is not offered the refactoring.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task InvalidPatternIsNotOfferedAsync() =>
        VerifyNoRefactoringAsync(
            """
            using System.Text.RegularExpressions;

            public class C
            {
                public Regex Build() => [|new Regex("[a-z")|];
            }
            """);

    /// <summary>Verifies a construction with an options argument is not offered the refactoring.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ConstructionWithOptionsIsNotOfferedAsync() =>
        VerifyNoRefactoringAsync(
            """
            using System.Text.RegularExpressions;

            public class C
            {
                public Regex Build() => [|new Regex("[a-z]+", RegexOptions.Compiled)|];
            }
            """);

    /// <summary>Verifies a generic host type is not offered the refactoring.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task GenericHostTypeIsNotOfferedAsync() =>
        VerifyNoRefactoringAsync(
            """
            using System.Text.RegularExpressions;

            public class C<T>
            {
                public Regex Build() => [|new Regex("[a-z]+")|];
            }
            """);

    /// <summary>Verifies a nested host type is not offered the refactoring.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task NestedHostTypeIsNotOfferedAsync() =>
        VerifyNoRefactoringAsync(
            """
            using System.Text.RegularExpressions;

            public class Outer
            {
                public class C
                {
                    public Regex Build() => [|new Regex("[a-z]+")|];
                }
            }
            """);

    /// <summary>Runs a refactoring verification against the .NET 9 reference assemblies.</summary>
    /// <param name="source">The source with the selection span.</param>
    /// <param name="fixedSource">The expected source after the refactoring.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyAsync(string source, string fixedSource)
    {
        var test = new Verify.Test { ReferenceAssemblies = ReferenceAssemblies.Net.Net90, TestCode = source, FixedCode = fixedSource, };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Runs a verification that no refactoring is offered at the selection.</summary>
    /// <param name="source">The source with the selection span.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Task VerifyNoRefactoringAsync(string source) =>
        VerifyAsync(
            source,
            source.Replace("[|", string.Empty, StringComparison.Ordinal).Replace("|]", string.Empty, StringComparison.Ordinal));
}
