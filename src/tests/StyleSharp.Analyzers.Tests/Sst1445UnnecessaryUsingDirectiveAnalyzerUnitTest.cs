// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;
using RoslynCommon.Analyzers.Tests;

using Verify = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    StyleSharp.Analyzers.Sst1445UnnecessaryUsingDirectiveAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests for <see cref="Sst1445UnnecessaryUsingDirectiveAnalyzer"/> (SST1445 unnecessary using directives).</summary>
public class Sst1445UnnecessaryUsingDirectiveAnalyzerUnitTest
{
    /// <summary>Verifies an unused namespace using is flagged.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task UnusedNamespaceUsingIsFlaggedAsync() =>
        Verify.VerifyAnalyzerAsync(
            """
            {|SST1445:using System.Text;|}

            public class C
            {
                public int M() => 42;
            }
            """);

    /// <summary>Verifies a using consumed by a simple type reference is clean.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task UsedNamespaceUsingIsCleanAsync() =>
        Verify.VerifyAnalyzerAsync(
            """
            using System.Text;

            public class C
            {
                public string M() => new StringBuilder().ToString();
            }
            """);

    /// <summary>Verifies a using consumed only by an extension-method invocation is clean.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task UsingConsumedByExtensionMethodIsCleanAsync() =>
        Verify.VerifyAnalyzerAsync(
            """
            using System.Linq;

            public class C
            {
                public bool M(int[] values) => values.Any();
            }
            """);

    /// <summary>Verifies a using consumed only by query syntax is clean.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task UsingConsumedByQuerySyntaxIsCleanAsync() =>
        Verify.VerifyAnalyzerAsync(
            """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                public IEnumerable<int> M(int[] values) => from v in values where v > 0 select v;
            }
            """);

    /// <summary>Verifies an unused using static is flagged and a used one is clean.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task UsingStaticUsageIsTrackedAsync() =>
        Verify.VerifyAnalyzerAsync(
            """
            using static System.Math;
            {|SST1445:using static System.Environment;|}

            public class C
            {
                public int M(int value) => Max(value, 0);
            }
            """);

    /// <summary>Verifies an unused alias is flagged and a used one is clean.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task AliasUsageIsTrackedAsync() =>
        Verify.VerifyAnalyzerAsync(
            """
            using SB = System.Text.StringBuilder;
            {|SST1445:using SR = System.IO.StringReader;|}

            public class C
            {
                public string M() => new SB().ToString();
            }
            """);

    /// <summary>Verifies a using consumed only by an attribute is clean.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task UsingConsumedByAttributeIsCleanAsync() =>
        Verify.VerifyAnalyzerAsync(
            """
            using System;

            public class C
            {
                [Obsolete("old")]
                public void M()
                {
                }
            }
            """);

    /// <summary>Verifies a using consumed only inside nameof is clean.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task UsingConsumedByNameofIsCleanAsync() =>
        Verify.VerifyAnalyzerAsync(
            """
            using System.Text;

            public class C
            {
                public string M() => nameof(StringBuilder);
            }
            """);

    /// <summary>Verifies a using consumed only from an XML doc cref is clean.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task UsingConsumedByDocCrefIsCleanAsync() =>
        Verify.VerifyAnalyzerAsync(
            """
            using System.Text;

            /// <summary>Builds like <see cref="StringBuilder"/>.</summary>
            public class C
            {
                /// <summary>Does nothing.</summary>
                public void M()
                {
                }
            }
            """);

    /// <summary>Verifies an unused using inside a namespace block is flagged.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task UnusedUsingInsideNamespaceIsFlaggedAsync() =>
        Verify.VerifyAnalyzerAsync(
            """
            namespace N
            {
                {|SST1445:using System.Text;|}

                public class C
                {
                    public int M() => 42;
                }
            }
            """);

    /// <summary>Verifies a using consumed by a base type reference is clean.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task UsingConsumedByBaseTypeIsCleanAsync() =>
        Verify.VerifyAnalyzerAsync(
            """
            using System;

            public class C : EventArgs
            {
            }
            """);

    /// <summary>Verifies a using consumed only by a collection-initializer extension Add is clean.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task UsingConsumedByCollectionInitializerAddIsCleanAsync() =>
        Verify.VerifyAnalyzerAsync(
            """
            using System.Collections.Generic;
            using N2;

            namespace N2
            {
                public static class StackExtensions
                {
                    public static void Add<T>(this Stack<T> stack, T item) => stack.Push(item);
                }
            }

            public class C
            {
                public Stack<int> M() => new Stack<int> { 1, 2 };
            }
            """);

    /// <summary>Verifies mixed used and unused usings only flag the unused ones.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task MixedUsingsOnlyFlagUnusedAsync() =>
        Verify.VerifyAnalyzerAsync(
            """
            using System;
            {|SST1445:using System.Collections.Generic;|}
            using System.Text;

            public class C
            {
                public string M() => new StringBuilder().Append(Environment.NewLine).ToString();
            }
            """);

    /// <summary>Verifies a using consumed only by a C# 14 extension-block member invocation is clean.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task UsingConsumedByExtensionBlockMemberIsCleanAsync() =>
        RunWithExtensionBlocksAsync(
            """
            using Lib.Internal;

            namespace Lib.Internal
            {
                internal static class Ext
                {
                    extension<T>(System.Collections.Generic.IEnumerable<T> source)
                    {
                        public int CountItems() => System.Linq.Enumerable.Count(source);
                    }
                }
            }

            namespace Consumer
            {
                internal static class Use
                {
                    public static int N(System.Collections.Generic.IEnumerable<int> xs) => xs.CountItems();
                }
            }
            """);

    /// <summary>Verifies a using consumed only by a C# 14 extension-block property access is clean.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task UsingConsumedByExtensionBlockPropertyIsCleanAsync() =>
        RunWithExtensionBlocksAsync(
            """
            using Lib.Internal;

            namespace Lib.Internal
            {
                internal static class Ext
                {
                    extension(string text)
                    {
                        public bool IsBlank => text.Length == 0;
                    }
                }
            }

            namespace Consumer
            {
                internal static class Use
                {
                    public static bool N(string s) => s.IsBlank;
                }
            }
            """);

    /// <summary>Verifies a using whose only consumer sits in an inactive branch is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>
    /// The identifiers in that branch are trivia, not bound syntax, so nothing there counts as a use and the
    /// directive looks unused. Removing it breaks every framework that does take the branch, where the type
    /// it named is no longer in scope.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task UsingConsumedOnlyByAnInactiveBranchIsCleanAsync() =>
        Verify.VerifyAnalyzerAsync(
            """
            using System.Reflection;
            using System.Runtime.CompilerServices;

            internal static class Helpers
            {
            #if NET9_0_OR_GREATER
                internal static string Name(FieldInfo field) => field.Name;
            #else
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                internal static string Name(FieldInfo field) => field.Name;
            #endif
            }
            """);

    /// <summary>Verifies disabled text nested in structured trivia prevents an unsafe removal.</summary>
    /// <param name="trailing">Whether the nested token carries disabled text in trailing trivia.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task NestedDisabledTriviaKeepsUsingAsync(bool trailing)
    {
        var disabled = SyntaxFactory.TriviaList(SyntaxFactory.DisabledText("StringBuilder hidden;"));
        var token = SyntaxFactory.Identifier("inactive");
        token = trailing ? token.WithTrailingTrivia(disabled) : token.WithLeadingTrivia(disabled);
        var skipped = SyntaxFactory.Trivia(SyntaxFactory.SkippedTokensTrivia(SyntaxFactory.TokenList(token)));
        var directive = SyntaxFactory.RegionDirectiveTrivia(isActive: true)
            .WithEndOfDirectiveToken(SyntaxFactory.Token(SyntaxKind.EndOfDirectiveToken).WithLeadingTrivia(skipped));
        var root = SyntaxFactory.ParseCompilationUnit("using System.Text; class C { }")
            .WithLeadingTrivia(SyntaxFactory.Trivia(directive));
        var tree = CSharpSyntaxTree.Create(root);
        var compilation = CSharpCompilation.Create(nameof(NestedDisabledTriviaKeepsUsingAsync), [tree], RuntimeMetadataReferences.Platform);
        var diagnostics = await compilation.WithAnalyzers([new Sst1445UnnecessaryUsingDirectiveAnalyzer()])
            .GetAnalyzerDiagnosticsAsync(CancellationToken.None);
        await Assert.That(diagnostics).IsEmpty();
    }

    /// <summary>Verifies active directives and empty inactive regions do not disable the unused-import scan.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task DirectivesWithoutDisabledTextStillReportAsync() =>
        Verify.VerifyAnalyzerAsync("""
            #region Active
            #if true
            {|SST1445:using System.Text;|}
            #endif
            #if false
            #endif
            /// <summary>A visible type.</summary>
            public class C { }
            #endregion
            """);

    /// <summary>Verifies files without local directives need no usage tracker.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task GlobalOnlyAndNoUsingFilesAreCleanAsync() =>
        Verify.VerifyAnalyzerAsync("""
            global using System.Text;
            public class C { }
            """);

    /// <summary>Verifies aliases left after namespace imports are consumed remain independently reportable.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task OnlyAliasesRemainingAreReportedAsync() =>
        Verify.VerifyAnalyzerAsync("""
            using System.Text;
            {|SST1445:using Unused = System.IO.StringReader;|}
            public class C
            {
                public StringBuilder M(int Unused) => new StringBuilder().Append(Unused);
            }
            """);

    /// <summary>Verifies consuming the last alias still leaves non-alias imports available for binding.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task NonAliasImportsRemainingAreBoundAsync() =>
        Verify.VerifyAnalyzerAsync("""
            using Alias = System.Text.StringBuilder;
            using System.IO;
            {|SST1445:using System.Collections;|}
            public class C
            {
                public Alias M() => new Alias();
                public StringReader Read() => new StringReader("");
            }
            """);

    /// <summary>Verifies alias qualification and shadowed alias names select their own declaration.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ScopedAndQualifiedAliasesAreTrackedAsync() =>
        Verify.VerifyAnalyzerAsync("""
            {|SST1445:using Other = System.IO;|}
            {|SST1445:using Text = System.Text;|}
            namespace N
            {
                using Text = System.IO;
                public class C { public Text::StringReader M() => null; }
            }
            namespace N.Inner
            {
                using Builder = System.Text.StringBuilder;
                public class D { public Builder M() => null; }
            }
            """);

    /// <summary>Verifies static nested types, fields, properties and events keep their imports.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task StaticMemberKindsKeepTheirImportsAsync() =>
        Verify.VerifyAnalyzerAsync("""
            using static Members;
            {|SST1445:using System.Text;|}
            public static class Members
            {
                public class Nested { }
                public static int Field;
                public static int Property => 0;
                public static event System.Action Event { add { } remove { } }
            }
            public class C
            {
                public Nested M()
                {
                    Field = Property;
                    Event += () => { };
                    return new Nested();
                }
            }
            """);

    /// <summary>Verifies namespace chains with the same final name are compared to their roots.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task DifferentLengthNamespaceChainsStayDistinctAsync() =>
        Verify.VerifyAnalyzerAsync("""
            {|SST1445:using N;|}
            using Outer.N;
            namespace N { public class A { } }
            namespace Outer.N { public class B { } }
            public class C { public B M() => null; }
            """);

    /// <summary>Verifies ambiguous and inaccessible symbols conservatively keep their contributing imports.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task CandidateSymbolsKeepImportsAsync() =>
        VerifyIgnoringCompilerDiagnosticsAsync("""
            using A;
            using B;
            {|SST1445:using System.Text;|}
            namespace A { public class Item { } }
            namespace B { public class Item { } }
            public class C { public Item M() => null; }
            """);

    /// <summary>Verifies unresolved import targets cannot produce removal diagnostics.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task InvalidUsingTargetsAreCleanAsync() =>
        VerifyIgnoringCompilerDiagnosticsAsync("""
            using Missing;
            using static MissingType;
            using System.String;
            public class C { }
            """);

    /// <summary>Verifies both documentation comment forms can consume an alias or namespace import.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task MultilineDocumentationAndAliasCrefsAreTrackedAsync() =>
        Verify.VerifyAnalyzerAsync("""
            using System.Text;
            using Reader = System.IO.StringReader;
            {|SST1445:using System.Collections;|}
            /** <summary><see cref="StringBuilder"/> and <see cref="Reader"/>.</summary> */
            public class C { }
            """);

    /// <summary>Verifies conditional extension calls and inferred locals reach the fallback walker.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ConditionalExtensionAndVarUsagesAreTrackedAsync() =>
        Verify.VerifyAnalyzerAsync("""
            using System.Linq;
            {|SST1445:using System.Text;|}
            public class C
            {
                public int M(int[] values)
                {
                    var count = values?.Count() ?? 0;
                    count += values.Length;
                    return count;
                }
            }
            """);

    /// <summary>Verifies a type literally named var is resolved by the fallback scan.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task TypeNamedVarKeepsImportAsync() =>
        Verify.VerifyAnalyzerAsync("""
            using Names;
            namespace Names { public class var { } }
            public class C { public var M() => null; }
            """);

    /// <summary>Verifies query continuations and select operators are scanned while imports remain.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task QueryContinuationsKeepLinqImportAsync() =>
        Verify.VerifyAnalyzerAsync("""
            using System.Linq;
            {|SST1445:using System.Text;|}
            public class C
            {
                public object M(int[] values) => from value in values select value + 1 into next where next > 1 select next;
            }
            """);

    /// <summary>Verifies nested deconstruction, tuple foreach and extension enumerators keep the required imports.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task EnumerationAndDeconstructionExtensionsKeepImportsAsync() =>
        Verify.VerifyAnalyzerAsync("""
            using Extensions;
            {|SST1445:using System.Text;|}
            public class Pair { }
            public class Sequence { }
            namespace Extensions
            {
                public static class Methods
                {
                    public static void Deconstruct(this Pair pair, out int x, out (int, int) rest) { x = 0; rest = default; }
                    public static System.Collections.Generic.IEnumerator<Pair> GetEnumerator(this Sequence sequence) => null;
                }
            }
            public class C
            {
                public void M(Sequence sequence, Pair pair)
                {
                    foreach (var item in sequence) { }
                    foreach (var (x, (y, z)) in sequence) { }
                    var (a, (b, c)) = pair;
                    (a, (b, c)) = pair;
                    a += b;
                }
            }
            """);

    /// <summary>Verifies positional extension patterns conservatively retain every remaining import.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ExtensionPositionalPatternKeepsRemainingImportsAsync() =>
        Verify.VerifyAnalyzerAsync("""
            using Extensions;
            using System.Text;
            using Alias = System.IO.StringReader;
            public class Pair { }
            namespace Extensions
            {
                public static class Methods
                {
                    public static void Deconstruct(this Pair pair, out int x, out int y) { x = y = 0; }
                }
            }
            public class C { public bool M(Pair pair) => pair is (0, 0); }
            """);

    /// <summary>Verifies tuple, property and instance deconstruction patterns do not keep unrelated imports.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task InstanceAndTuplePatternsStillReportUnusedImportsAsync() =>
        Verify.VerifyAnalyzerAsync("""
            {|SST1445:using System.Text;|}
            public class Pair
            {
                public int Value => 0;
                public void Deconstruct(out int x, out int y) { x = y = 0; }
            }
            public class C
            {
                public bool M(Pair pair, (int, int) tuple) => pair is (0, 0) && pair is { Value: 0 } && tuple is (0, 0);
            }
            """);

    /// <summary>Verifies extension awaiters keep imports while normal awaiters leave unrelated imports unused.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ExtensionAwaiterKeepsImportAsync() =>
        Verify.VerifyAnalyzerAsync("""
            using Extensions;
            {|SST1445:using System.Text;|}
            public class Awaitable { }
            namespace Extensions
            {
                public static class Methods
                {
                    public static System.Runtime.CompilerServices.TaskAwaiter GetAwaiter(this Awaitable value) => default;
                }
            }
            public class C
            {
                public async System.Threading.Tasks.Task M(Awaitable value)
                {
                    await value;
                    await System.Threading.Tasks.Task.CompletedTask;
                }
            }
            """);

    /// <summary>Verifies await-using declarations and statements recognize instance and interface disposal.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task InstanceAsyncDisposalStillReportsUnusedImportsAsync() =>
        VerifyIgnoringCompilerDiagnosticsAsync("""
            {|SST1445:using System.Text;|}
            public class Resource : System.IAsyncDisposable, System.IDisposable
            {
                System.Threading.Tasks.ValueTask System.IAsyncDisposable.DisposeAsync() => default;
                public void Dispose() { }
            }
            public class Instance
            {
                public System.Threading.Tasks.ValueTask DisposeAsync() => default;
            }
            public class C
            {
                public async System.Threading.Tasks.Task M(Resource resource)
                {
                    await using var first = new Instance();
                    await using (resource) { }
                    await using (Resource second = resource) { }
                    using (resource) { }
                }
            }
            """);

    /// <summary>Verifies a missing async-disposal member conservatively keeps imports during incomplete editing.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task MissingAsyncDisposalKeepsImportsAsync() =>
        VerifyIgnoringCompilerDiagnosticsAsync("""
            using System.Text;
            public class C
            {
                public async System.Threading.Tasks.Task M()
                {
                    await using var resource = new object();
                }
            }
            """);

    /// <summary>Verifies global directives are skipped while similarly named local aliases stay independently unused.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task GlobalAliasDoesNotConsumeUnrelatedLocalAliasAsync() =>
        Verify.VerifyAnalyzerAsync("""
            global using Text = System.IO;
            namespace N
            {
                {|SST1445:using Text = System.Text;|}
                public class C { }
            }
            public class D { public Text::StringReader M() => null; }
            """);

    /// <summary>Verifies direct calls inside an extension holder conservatively retain its namespace import.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task DirectExtensionCallConservativelyKeepsImportAsync() =>
        Verify.VerifyAnalyzerAsync("""
            using Extensions;
            {|SST1445:using System.Text;|}
            namespace Extensions
            {
                public static class Methods
                {
                    public static bool IsEmpty(this string text) => text.Length == 0;
                    public static bool M(string text) => IsEmpty(text);
                }
            }
            """);

    /// <summary>Verifies a shorter symbol namespace does not consume a longer import with the same suffix.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ShorterNamespaceDoesNotConsumeLongerImportAsync() =>
        Verify.VerifyAnalyzerAsync("""
            {|SST1445:using Outer.N;|}
            using N;
            namespace N { public class A { } }
            namespace Outer.N { public class B { } }
            public class C { public A M() => null; }
            """);

    /// <summary>Verifies initializer member names and array-valued var locals do not consume imports.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task InitializerNamesAndArrayVarLeaveImportsUnusedAsync() =>
        Verify.VerifyAnalyzerAsync("""
            {|SST1445:using System.Text;|}
            public class C
            {
                public int Value { get; set; }
                public object M()
                {
                    var values = new int[] { 1, 2 };
                    return new C { Value = values.Length };
                }
            }
            """);

    /// <summary>Verifies unbound member calls do not attribute usage to an unrelated namespace.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task UnboundMemberAccessLeavesImportUnusedAsync() =>
        VerifyIgnoringCompilerDiagnosticsAsync("""
            {|SST1445:using System.Text;|}
            public class C { public void M(object value) => value.Missing(); }
            """);

    /// <summary>Verifies static disposal members and unrelated disposable interfaces do not prove instance disposal.</summary>
    /// <param name="member">A member named DisposeAsync that cannot dispose an instance.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    [Arguments("public static void DisposeAsync() { }")]
    [Arguments("public int DisposeAsync;")]
    public Task StaticDisposalAndLookalikeInterfacesKeepImportsAsync(string member) =>
        VerifyIgnoringCompilerDiagnosticsAsync($$"""
            using System.Text;
            namespace Other { public interface IAsyncDisposable { } }
            namespace Outer.System { public interface IAsyncDisposable { } }
            public class Resource : Other.IAsyncDisposable, Outer.System.IAsyncDisposable, System.IDisposable
            {
                {{member}}
                public void Dispose() { }
            }
            public class C
            {
                public async System.Threading.Tasks.Task M(Resource resource)
                {
                    await using (resource) { }
                }
            }
            """);

    /// <summary>Runs snippets whose unresolved symbols intentionally exercise conservative binding paths.</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private static async Task VerifyIgnoringCompilerDiagnosticsAsync(string source)
    {
        var test = new Verify.Test { TestCode = source, ReferenceAssemblies = AnalyzerFrameworks.Net90, CompilerDiagnostics = CompilerDiagnostics.None };
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Runs the analyzer verifier with a language version that parses extension blocks.</summary>
    /// <param name="source">The source code, including diagnostic markup, to analyze.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private static async Task RunWithExtensionBlocksAsync(string source)
    {
        var test = new Verify.Test { TestCode = source };
        test.SolutionTransforms.Add(static (solution, projectId) =>
        {
            var parseOptions = (CSharpParseOptions)solution.GetProject(projectId)!.ParseOptions!;
            return solution.WithProjectParseOptions(projectId, parseOptions.WithLanguageVersion(LanguageVersion.Preview));
        });
        await test.RunAsync(CancellationToken.None);
    }
}
