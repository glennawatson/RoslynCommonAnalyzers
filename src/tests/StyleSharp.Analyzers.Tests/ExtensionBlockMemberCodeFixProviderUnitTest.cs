// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Composition.Hosting;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using VerifyMixedStylesFix = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.ExtensionBlockAnalyzer,
    StyleSharp.Analyzers.ExtensionBlockMemberCodeFixProvider>;
using VerifyPreferBlockFix = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst1703PreferExtensionBlockAnalyzer,
    StyleSharp.Analyzers.ExtensionBlockMemberCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for <see cref="ExtensionBlockMemberCodeFixProvider"/> (SST1703, SST1705).</summary>
public class ExtensionBlockMemberCodeFixProviderUnitTest
{
    /// <summary>Verifies batch callbacks preserve nodes already removed or changed by another edit.</summary>
    /// <param name="currentSource">The current declaration after an earlier batch edit.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("struct Other { }")]
    [Arguments("static class Extensions { }")]
    [Arguments("static class Extensions { public static int M(string value) => 2; }")]
    public async Task BatchCallbackPreservesChangedDeclarationAsync(string currentSource)
    {
        var root = await CSharpSyntaxTree.ParseText("static class Extensions { public static int M(string value) => 1; }").GetRootAsync();
        var method = root.DescendantNodes().OfType<MethodDeclarationSyntax>().Single();
        var diagnostic = Diagnostic.Create(ExtensionRules.AlmostExtensionMethod, method.Identifier.GetLocation());
        var replacement = ExtensionBlockMemberCodeFixProvider.TryRewriteAlmostExtension(root, diagnostic);
        await Assert.That(replacement.HasValue).IsTrue();
        var current = SyntaxFactory.ParseMemberDeclaration(currentSource)!;
        var updated = replacement!.Value.RewriteCurrent!(current);
        await Assert.That(updated).IsSameReferenceAs(current);
    }

    /// <summary>Verifies receiver documentation can be the first element of a compact block comment.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task CompactReceiverDocumentationIsRemovedAsync()
    {
        var root = await CSharpSyntaxTree.ParseText(
            """
            static class Extensions
            {
                /**<param name="value">The receiver.</param><summary>Measures text.</summary>*/
                public static int M(string value) => value.Length;
            }
            """).GetRootAsync();
        var method = root.DescendantNodes().OfType<MethodDeclarationSyntax>().Single();
        var diagnostic = Diagnostic.Create(ExtensionRules.AlmostExtensionMethod, method.Identifier.GetLocation());
        var replacement = ExtensionBlockMemberCodeFixProvider.TryRewriteAlmostExtension(root, diagnostic);
        await Assert.That(replacement.HasValue).IsTrue();
        var rewritten = replacement!.Value.Replacement.ToFullString();
        await Assert.That(rewritten).DoesNotContain("<param");
        await Assert.That(rewritten).Contains("<summary>Measures text.</summary>");
    }

    /// <summary>Verifies XML-sensitive text inside a receiver type comment is escaped in the introduced documentation.</summary>
    /// <param name="isStatic">Whether the source already includes the required static modifier.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task ReceiverCommentIsEscapedInDocumentationAsync(bool isStatic)
    {
        using var workspace = new AdhocWorkspace();
        var project = workspace.AddProject("ExtensionMove", LanguageNames.CSharp).WithParseOptions(new CSharpParseOptions(LanguageVersion.Preview));
        var document = project.AddDocument(
            "Test.cs",
            $$"""static class Extensions { {{(isStatic ? "public static" : "public")}} int M(this System.Collections.Generic.List</* & */int> value) => value.Count; }""");
        var root = (await document.GetSyntaxRootAsync())!;
        var method = root.DescendantNodes().OfType<MethodDeclarationSyntax>().Single();
        var diagnostic = Diagnostic.Create(ExtensionRules.PreferExtensionBlock, method.Identifier.GetLocation());
        var actions = new List<CodeAction>();
        using var container = new ContainerConfiguration().WithPart<ExtensionBlockMemberCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        await provider.RegisterCodeFixesAsync(new(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        await Assert.That(actions.Count).IsEqualTo(1);
        var operations = await actions[0].GetOperationsAsync(CancellationToken.None);
        var changed = operations.OfType<ApplyChangesOperation>().Single().ChangedSolution.GetDocument(document.Id)!;
        var changedText = await changed.GetTextAsync();
        await Assert.That(changedText.ToString()).Contains("List&lt;/* &amp; */int&gt;");
        await Assert.That(changedText.ToString()).Contains("int M() => value.Count;");
    }

    /// <summary>Verifies both receiver modifiers survive a readonly-reference conversion.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task RefReadonlyReceiverRetainsBothModifiersAsync() => RunPreferBlockAsync(
        """
        public static class Extensions
        {
            public static int {|SST1703:Read|}(this ref readonly int value) => value;
        }
        """,
        """
        public static class Extensions
        {
            /// <summary>Extension members for <c>int</c>.</summary>
            extension(ref readonly int value)
            {
                public int Read() => value;
            }
        }
        """);

    /// <summary>Verifies multiple receiver type parameters and constraints match an existing block.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task MultipleConstrainedParametersJoinMatchingBlockAsync() => RunPreferBlockAsync(
        """
        public static class Extensions
        {
            extension<T, U>((T, U) pair) where T : class where U : struct
            {
                public int Existing() => 0;
            }

            public static int {|SST1703:Count|}<T, U>(this (T, U) pair) where T : class where U : struct => 2;
        }
        """,
        """
        public static class Extensions
        {
            extension<T, U>((T, U) pair) where T : class where U : struct
            {
                public int Existing() => 0;

                public int Count() => 2;
            }
        }
        """);

    /// <summary>Verifies a different constraint creates a separate block instead of changing the existing contract.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task DifferentConstraintCreatesNewBlockAsync() => RunPreferBlockAsync(
        """
        public static class Extensions
        {
            extension<T>(T value) where T : class
            {
                public int Existing() => 0;
            }

            public static int {|SST1703:Count|}<T>(this T value) where T : struct => 1;
        }
        """,
        """
        public static class Extensions
        {
            extension<T>(T value) where T : class
            {
                public int Existing() => 0;
            }

            /// <summary>Extension members for <c>T</c>.</summary>
            extension<T>(T value) where T : struct
            {

                public int Count() => 1;
            }
        }
        """);

    /// <summary>Verifies a leading static modifier is removed while later modifiers and documentation survive.</summary>
    /// <param name="accessibility">The optional modifier following static.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("")]
    [Arguments("public ")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task LeadingStaticModifierAndUnmatchedDocumentationArePreservedAsync(string accessibility) => RunPreferBlockAsync(
        $$"""
        public static class Extensions
        {
            /// <summary>Measures text.</summary>
            /// <typeparam name="Unused">An unmatched documentation element.</typeparam>
            static {{accessibility}}int {|SST1703:Count|}(this string text) => text.Length;
        }
        """,
        $$"""
        public static class Extensions
        {
            /// <summary>Extension members for <c>string</c>.</summary>
            extension(string text)
            {
                /// <summary>Measures text.</summary>
                /// <typeparam name="Unused">An unmatched documentation element.</typeparam>
                {{accessibility}}int Count() => text.Length;
            }
        }
        """);

    /// <summary>Verifies incompatible method syntax and directive boundaries prevent a rewrite.</summary>
    /// <param name="source">The declaration at the diagnostic location.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("static class C { static void M() { } }")]
    [Arguments("static class C { static void M(string text) { } }")]
    [Arguments("static class C { static extern void M(this string text); }")]
    [Arguments("static class C { static void M([System.Obsolete] this string text) { } }")]
    [Arguments("static class C { static void M(this string text = null) { } }")]
    [Arguments("static class C { static void M(this string @class) { } }")]
    [Arguments("struct C { static void M(this string text) { } }")]
    [Arguments("static class C { extension(string text) { }\n#region Methods\nstatic void M(this string text) { }\n#endregion\n}")]
    [Arguments("static class C {\n#region Block\nextension(string text) { }\n#endregion\nstatic int Other;\nstatic void M(this string text) { }\n}")]
    public async Task UnsupportedMethodOffersNoFixAsync(string source)
    {
        using var workspace = new AdhocWorkspace();
        var document = workspace.AddProject("ExtensionMove", LanguageNames.CSharp).AddDocument("Test.cs", source);
        var root = (await document.GetSyntaxRootAsync())!;
        var method = root.DescendantNodes().OfType<MethodDeclarationSyntax>().Single();
        var diagnostic = Diagnostic.Create(ExtensionRules.PreferExtensionBlock, method.Identifier.GetLocation());
        var actions = new List<CodeAction>();
        using var container = new ContainerConfiguration().WithPart<ExtensionBlockMemberCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        await provider.RegisterCodeFixesAsync(new(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        await Assert.That(actions).IsEmpty();
    }

    /// <summary>Verifies a classic extension method becomes a new extension block (SST1703).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ClassicExtensionMethodMovesIntoANewBlockAsync()
    {
        const string Source = """
                              public static class StringExtensions
                              {
                                  public static bool {|SST1703:IsBlank|}(this string text) => text.Length == 0;
                              }
                              """;
        const string FixedSource = """
                                   public static class StringExtensions
                                   {
                                       /// <summary>Extension members for <c>string</c>.</summary>
                                       extension(string text)
                                       {
                                           public bool IsBlank() => text.Length == 0;
                                       }
                                   }
                                   """;
        await RunPreferBlockAsync(Source, FixedSource);
    }

    /// <summary>Verifies a method mixed in beside a block joins that block when the receiver matches (SST1705).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MixedMethodJoinsTheMatchingBlockAsync()
    {
        const string Source = """
                              public static class StringExtensions
                              {
                                  public static bool {|SST1705:IsBlank|}(this string text) => text.Length == 0;

                                  extension(string text)
                                  {
                                      public int Words => text.Split(' ').Length;
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public static class StringExtensions
                                   {
                                       extension(string text)
                                       {
                                           public bool IsBlank() => text.Length == 0;
                                           public int Words => text.Split(' ').Length;
                                       }
                                   }
                                   """;
        await RunMixedStylesAsync(Source, FixedSource);
    }

    /// <summary>Verifies a method whose receiver name differs from the block's gets its own block, so the body still binds.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MethodWithADifferentReceiverNameGetsItsOwnBlockAsync()
    {
        const string Source = """
                              public static class StringExtensions
                              {
                                  public static bool {|SST1705:IsBlank|}(this string value) => value.Length == 0;

                                  extension(string text)
                                  {
                                      public int Words => text.Split(' ').Length;
                                  }
                              }
                              """;

        // Two blocks over the same receiver type then draw SST1701, which asks for them to be combined —
        // that merge has to rename one receiver, so it stays a separate decision from this fix.
        const string FixedSource = """
                                   public static class StringExtensions
                                   {
                                       /// <summary>Extension members for <c>string</c>.</summary>
                                       extension(string value)
                                       {
                                           public bool IsBlank() => value.Length == 0;
                                       }

                                       {|SST1701:extension|}(string text)
                                       {
                                           public int Words => text.Split(' ').Length;
                                       }
                                   }
                                   """;
        await RunMixedStylesAsync(Source, FixedSource);
    }

    /// <summary>Verifies a type parameter the receiver names moves onto the block with its constraint.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ReceiverTypeParameterMovesOntoTheBlockAsync()
    {
        const string Source = """
                              using System.Collections.Generic;

                              public static class CollectionExtensions
                              {
                                  public static bool {|SST1703:IsEmpty|}<T>(this IReadOnlyCollection<T> items)
                                      where T : struct => items.Count == 0;
                              }
                              """;
        const string FixedSource = """
                                   using System.Collections.Generic;

                                   public static class CollectionExtensions
                                   {
                                       /// <summary>Extension members for <c>IReadOnlyCollection&lt;T&gt;</c>.</summary>
                                       extension<T>(IReadOnlyCollection<T> items) where T : struct
                                       {
                                           public bool IsEmpty() => items.Count == 0;
                                       }
                                   }
                                   """;
        await RunPreferBlockAsync(Source, FixedSource);
    }

    /// <summary>Verifies a type parameter the receiver does not name stays on the member.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TypeParameterOutsideTheReceiverStaysOnTheMemberAsync()
    {
        const string Source = """
                              public static class StringExtensions
                              {
                                  public static string {|SST1703:Describe|}<TValue>(this string text, TValue value)
                                      where TValue : struct => text + value.ToString();
                              }
                              """;
        const string FixedSource = """
                                   public static class StringExtensions
                                   {
                                       /// <summary>Extension members for <c>string</c>.</summary>
                                       extension(string text)
                                       {
                                           public string Describe<TValue>(TValue value)
                                               where TValue : struct => text + value.ToString();
                                       }
                                   }
                                   """;
        await RunPreferBlockAsync(Source, FixedSource);
    }

    /// <summary>Verifies a method whose type parameters split across the divide takes only its own onto the member.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TypeParametersSplitBetweenTheBlockAndTheMemberAsync()
    {
        const string Source = """
                              using System.Collections.Generic;

                              public static class CollectionExtensions
                              {
                                  /// <summary>Counts the items.</summary>
                                  /// <typeparam name="T">The item type.</typeparam>
                                  /// <typeparam name="TKey">The key type.</typeparam>
                                  /// <param name="items">The items to count.</param>
                                  /// <param name="key">The key to report.</param>
                                  /// <returns>The item count.</returns>
                                  public static int {|SST1703:CountFor|}<T, TKey>(this IReadOnlyCollection<T> items, TKey key)
                                      where T : class
                                      where TKey : notnull => items.Count + key.GetHashCode();
                              }
                              """;
        const string FixedSource = """
                                   using System.Collections.Generic;

                                   public static class CollectionExtensions
                                   {
                                       /// <summary>Extension members for <c>IReadOnlyCollection&lt;T&gt;</c>.</summary>
                                       extension<T>(IReadOnlyCollection<T> items) where T : class
                                       {
                                           /// <summary>Counts the items.</summary>
                                           /// <typeparam name="TKey">The key type.</typeparam>
                                           /// <param name="key">The key to report.</param>
                                           /// <returns>The item count.</returns>
                                           public int CountFor<TKey>(TKey key)
                                               where TKey : notnull => items.Count + key.GetHashCode();
                                       }
                                   }
                                   """;
        await RunPreferBlockAsync(Source, FixedSource);
    }

    /// <summary>Verifies a constraint reaching across the divide leaves the method where it is.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>
    /// The member's type parameters are not in scope on the block, so such a constraint cannot be written
    /// on either side.
    /// </remarks>
    [Test]
    public async Task ConstraintReachingAcrossTheDivideIsNotFixedAsync()
    {
        const string Source = """
                              using System;
                              using System.Collections.Generic;

                              public static class CollectionExtensions
                              {
                                  public static int {|SST1703:RankOf|}<T, TKey>(this IReadOnlyCollection<T> items, TKey key)
                                      where T : IComparable<TKey> => items.Count + key.GetHashCode();
                              }
                              """;

        // The diagnostic survives, because no fix is offered for it — the source is expected to be untouched.
        await RunPreferBlockAsync(Source, Source);
    }

    /// <summary>Verifies a generic method joins a block only when that block declares the same type parameters.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task GenericMethodJoinsTheBlockDeclaringItsTypeParametersAsync()
    {
        const string Source = """
                              using System.Collections.Generic;

                              public static class CollectionExtensions
                              {
                                  public static bool {|SST1705:IsEmpty|}<T>(this IReadOnlyCollection<T> items) => items.Count == 0;

                                  extension<T>(IReadOnlyCollection<T> items)
                                  {
                                      public int Size => items.Count;
                                  }
                              }
                              """;
        const string FixedSource = """
                                   using System.Collections.Generic;

                                   public static class CollectionExtensions
                                   {
                                       extension<T>(IReadOnlyCollection<T> items)
                                       {
                                           public bool IsEmpty() => items.Count == 0;
                                           public int Size => items.Count;
                                       }
                                   }
                                   """;
        await RunMixedStylesAsync(Source, FixedSource);
    }

    /// <summary>Verifies the documentation travels with the member instead of landing on the block.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>
    /// A copy on the block would document parameters the block does not declare, which is CS1572.
    /// </remarks>
    [Test]
    public async Task DocumentationStaysWithTheMovedMemberAsync()
    {
        const string Source = """
                              public static class StringExtensions
                              {
                                  /// <summary>Returns whether the text is empty.</summary>
                                  /// <param name="text">The text to measure.</param>
                                  /// <returns><see langword="true"/> when the text is empty.</returns>
                                  public static bool {|SST1703:IsBlank|}(this string text) => text.Length == 0;
                              }
                              """;
        const string FixedSource = """
                                   public static class StringExtensions
                                   {
                                       /// <summary>Extension members for <c>string</c>.</summary>
                                       extension(string text)
                                       {
                                           /// <summary>Returns whether the text is empty.</summary>
                                           /// <returns><see langword="true"/> when the text is empty.</returns>
                                           public bool IsBlank() => text.Length == 0;
                                       }
                                   }
                                   """;
        await RunPreferBlockAsync(Source, FixedSource);
    }

    /// <summary>Verifies a receiver taken by readonly reference keeps its <c>in</c>.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>
    /// Dropping the modifier turns a readonly reference into a copy of the struct at every call, which
    /// is the cost the declaration was written to avoid.
    /// </remarks>
    [Test]
    public async Task ReadonlyReferenceReceiverKeepsItsModifierAsync()
    {
        const string Source = """
                              public readonly struct Point
                              {
                                  public int X { get; }
                              }

                              public static class PointExtensions
                              {
                                  public static int {|SST1703:Doubled|}(this in Point point) => point.X * 2;
                              }
                              """;
        const string FixedSource = """
                                   public readonly struct Point
                                   {
                                       public int X { get; }
                                   }

                                   public static class PointExtensions
                                   {
                                       /// <summary>Extension members for <c>Point</c>.</summary>
                                       extension(in Point point)
                                       {
                                           public int Doubled() => point.X * 2;
                                       }
                                   }
                                   """;
        await RunPreferBlockAsync(Source, FixedSource);
    }

    /// <summary>Verifies a by-value block does not take a method that receives by readonly reference.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ByValueBlockDoesNotAbsorbAReadonlyReferenceReceiverAsync()
    {
        const string Source = """
                              public readonly struct Point
                              {
                                  public int X { get; }
                              }

                              public static class PointExtensions
                              {
                                  public static int {|SST1705:Doubled|}(this in Point point) => point.X * 2;

                                  extension(Point point)
                                  {
                                      public int Tripled => point.X * 3;
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public readonly struct Point
                                   {
                                       public int X { get; }
                                   }

                                   public static class PointExtensions
                                   {
                                       /// <summary>Extension members for <c>Point</c>.</summary>
                                       extension(in Point point)
                                       {
                                           public int Doubled() => point.X * 2;
                                       }

                                       extension(Point point)
                                       {
                                           public int Tripled => point.X * 3;
                                       }
                                   }
                                   """;
        await RunMixedStylesAsync(Source, FixedSource);
    }

    /// <summary>Runs the SST1703 verifier at a language version that has extension blocks.</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <param name="fixedSource">The expected source after the fix.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task RunPreferBlockAsync(string source, string fixedSource)
    {
        var test = new VerifyPreferBlockFix.Test { TestCode = source, FixedCode = fixedSource };

        AddPreviewLanguageVersion(test);
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Runs the SST1705 verifier at a language version that has extension blocks.</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <param name="fixedSource">The expected source after the fix.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task RunMixedStylesAsync(string source, string fixedSource)
    {
        var test = new VerifyMixedStylesFix.Test { TestCode = source, FixedCode = fixedSource };

        AddPreviewLanguageVersion(test);
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Parses the test project at the language version that supports extension blocks.</summary>
    /// <param name="test">The test to configure.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void AddPreviewLanguageVersion(Microsoft.CodeAnalysis.Testing.AnalyzerTest<Microsoft.CodeAnalysis.Testing.DefaultVerifier> test) =>
        test.SolutionTransforms.Add(static (solution, projectId) =>
        {
            var parseOptions = (CSharpParseOptions)solution.GetProject(projectId)!.ParseOptions!;
            return solution.WithProjectParseOptions(
                projectId,
                parseOptions
                    .WithLanguageVersion(LanguageVersion.Preview)
                    .WithDocumentationMode(Microsoft.CodeAnalysis.DocumentationMode.Diagnose));
        });
}
