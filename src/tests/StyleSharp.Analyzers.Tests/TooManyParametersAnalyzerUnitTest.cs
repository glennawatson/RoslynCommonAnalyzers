// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;
using RoslynCommon.Analyzers.Tests;
using VerifyParameters = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<StyleSharp.Analyzers.Sst1472TooManyParametersAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST1472 (signatures should not declare too many parameters).</summary>
public class TooManyParametersAnalyzerUnitTest
{
    /// <summary>The path the analyzer config file is added at in the test workspace.</summary>
    private const string EditorConfigPath = "/.editorconfig";

    /// <summary>The <c>init</c>-accessor polyfill positional records require on the test reference assemblies.</summary>
    private const string IsExternalInit = """

        namespace System.Runtime.CompilerServices { internal static class IsExternalInit { } }
        """;

    /// <summary>Verifies similarly spelled attributes cannot exempt caller-written parameters.</summary>
    /// <param name="name">The attribute name that nearly matches a caller-info attribute.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    [Arguments("CallerMemberNamo")]
    [Arguments("CallerFilePatx")]
    [Arguments("CallerLineNumbex")]
    [Arguments("CallerArgumentExpressiom")]
    [Arguments("CallerMemberNameAttributx")]
    [Arguments("CallerFilePathAttributx")]
    [Arguments("CallerLineNumberAttributx")]
    [Arguments("CallerArgumentExpressionAttributx")]
    [Arguments("CallerArgumentExpressionAttributes")]
    [Arguments("DallerMemberName")]
    [Arguments("DallerFilePath")]
    [Arguments("DallerLineNumber")]
    [Arguments("DallerArgumentExpression")]
    [Arguments("CallerXemberName")]
    [Arguments("CallerXemberNameAttribute")]
    public Task CallerInfoNearMissesRemainCountedAsync(string name) =>
        VerifyParameters.VerifyAnalyzerAsync($$"""
            class {{name}} : System.Attribute { }
            class C
            {
                void {|SST1472:M|}(int a, int b, int c, int d, int e, int f, int g, [{{name}}] int h = 0) { }
            }
            """);

    /// <summary>Verifies malformed namespace-level methods remain measurable without a containing type.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task MethodWithoutContainingTypeIsMeasuredAsync() =>
        new VerifyParameters.Test { TestCode = "namespace N { void {|SST1472:M|}(int a, int b, int c, int d, int e, int f, int g, int h) { } }", CompilerDiagnostics = CompilerDiagnostics.None }
            .RunAsync(CancellationToken.None);

    /// <summary>Verifies indexer implementations are exempt while their defining interface is measured.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task InterfaceIndexersAreReportedAtTheirDefinitionAsync() =>
        VerifyParameters.VerifyAnalyzerAsync("""
            interface I
            {
                int {|SST1472:this|}[int a, int b, int c, int d, int e, int f, int g, int h] { get; }
                void Small();
            }
            class C : I
            {
                public int this[int a, int b, int c, int d, int e, int f, int g, int h] => a;
                public void Small() { }
                public void {|SST1472:Unrelated|}(int a, int b, int c, int d, int e, int f, int g, int h) { }
                public void {|SST1472:Small|}(int a, int b, int c, int d, int e, int f, int g, int h) { }
            }
            class D : I
            {
                int I.this[int a, int b, int c, int d, int e, int f, int g, int h] => a;
                public void Small() { }
            }
            """);

    /// <summary>Verifies each body shape identifies the implementing half of a partial indexer.</summary>
    /// <param name="body">The implementation body.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    [Arguments("=> a;")]
    [Arguments("{ get { return a; } }")]
    [Arguments("{ get => a; }")]
    public Task PartialIndexerIsReportedOnlyAtDefinitionAsync(string body) =>
        VerifyParameters.VerifyAnalyzerAsync($$"""
            partial class C
            {
                public partial int {|SST1472:this|}[int a, int b, int c, int d, int e, int f, int g, int h] { get; }
                public partial int this[int a, int b, int c, int d, int e, int f, int g, int h] {{body}}
            }
            """);

    /// <summary>Verifies incomplete partial signatures still follow their syntax's definition/body distinction.</summary>
    /// <param name="member">The incomplete signature and expected diagnostic.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    [Arguments("partial C(int a, int b, int c, int d, int e, int f, int g, int h) { }")]
    [Arguments("partial C(int a, int b, int c, int d, int e, int f, int g, int h) => M();")]
    [Arguments("partial C(int a, int b, int c, int d, int e, int f, int g, int h);")]
    [Arguments("partial int {|SST1472:this|}[int a, int b, int c, int d, int e, int f, int g, int h];")]
    public Task IncompletePartialSignatureUsesExistingBodyAsync(string member) =>
        new VerifyParameters.Test { TestCode = $"partial class C {{ {member} void M() {{ }} }}", CompilerDiagnostics = CompilerDiagnostics.None }.RunAsync(CancellationToken.None);

    /// <summary>Verifies a partial indexer with no accessor list remains a measured definition.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task PartialIndexerWithoutAccessorListIsMeasuredAsync()
    {
        var root = SyntaxFactory.ParseCompilationUnit("partial class C { partial int this[int a, int b, int c, int d, int e, int f, int g, int h] { get; } }");
        var indexer = root.DescendantNodes().OfType<IndexerDeclarationSyntax>().Single();
        var tree = CSharpSyntaxTree.Create(root.ReplaceNode(indexer, indexer.WithAccessorList(null)));
        var compilation = CSharpCompilation.Create(
            nameof(PartialIndexerWithoutAccessorListIsMeasuredAsync),
            [tree],
            RuntimeMetadataReferences.Platform);
        var diagnostics = await compilation.WithAnalyzers([new Sst1472TooManyParametersAnalyzer()]).GetAnalyzerDiagnosticsAsync();
        await Assert.That(diagnostics.Length).IsEqualTo(1);
        await Assert.That(diagnostics[0].Id).IsEqualTo("SST1472");
        await Assert.That(diagnostics[0].Location.SourceSpan).IsEqualTo(indexer.ThisKeyword.Span);
    }

    /// <summary>Verifies a partial primary constructor remains author-controlled.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task PartialTypePrimaryConstructorIsMeasuredAsync() =>
        VerifyParameters.VerifyAnalyzerAsync("partial class {|SST1472:C|}(int a, int b, int c, int d, int e, int f, int g, int h);");

    /// <summary>Verifies native import spellings are recognized after unrelated attributes.</summary>
    /// <param name="attribute">The attribute name spelling.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    [Arguments("DllImport")]
    [Arguments("DllImportAttribute")]
    [Arguments("System.Runtime.InteropServices.LibraryImport")]
    [Arguments("global::System.Runtime.InteropServices.LibraryImportAttribute")]
    public Task NativeImportNamesExemptSignaturesAsync(string attribute) =>
        new VerifyParameters.Test
        {
            TestCode = $$"""
                using System.Runtime.InteropServices;
                class C
                {
                    [System.Obsolete, {{attribute}}("native")]
                    public static int M(int a, int b, int c, int d, int e, int f, int g, int h) => a;
                }
                namespace System.Runtime.InteropServices
                {
                    class LibraryImportAttribute : System.Attribute
                    {
                        public LibraryImportAttribute(string library) { }
                    }
                }
                """,
            CompilerDiagnostics = CompilerDiagnostics.None,
        }.RunAsync(CancellationToken.None);

    /// <summary>Verifies unrelated attributes do not exempt methods or optional parameters.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task UnrelatedAttributesKeepParametersCountedAsync() =>
        VerifyParameters.VerifyAnalyzerAsync("""
            class MarkerAttribute : System.Attribute { }
            class C
            {
                [Marker]
                void {|SST1472:M|}(int a, int b, int c, int d, int e, int f, int g, [Marker] int h = 0) { }
            }
            """);

    /// <summary>Verifies qualified, suffixed and aliased caller-info attributes are all excluded.</summary>
    /// <param name="attribute">The caller-info spelling.</param>
    /// <param name="parameter">The optional parameter matching the attribute.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    [Arguments("CallerMemberNameAttribute", "string h = null")]
    [Arguments("System.Runtime.CompilerServices.CallerFilePathAttribute", "string h = null")]
    [Arguments("global::System.Runtime.CompilerServices.CallerLineNumberAttribute", "int h = 0")]
    [Arguments("CallerArgumentExpression(\"a\")", "string h = null")]
    [Arguments("CallerArgumentExpressionAttribute(\"a\")", "string h = null")]
    [Arguments("info::CallerMemberName", "string h = null")]
    public Task CallerInfoNameVariantsAreExcludedAsync(string attribute, string parameter) =>
        VerifyParameters.VerifyAnalyzerAsync($$"""
            using System.Runtime.CompilerServices;
            using info = System.Runtime.CompilerServices;
            class MarkerAttribute : System.Attribute { }
            class C
            {
                void M(int a, int b, int c, int d, int e, int f, int g, [Marker, {{attribute}}] {{parameter}}) { }
            }
            namespace System.Runtime.CompilerServices
            {
                class CallerArgumentExpressionAttribute : System.Attribute
                {
                    public CallerArgumentExpressionAttribute(string parameter) { }
                }
            }
            """);

    /// <summary>Verifies explicit option values retain the documented defaults.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ExplicitDefaultOptionsPreserveCountingAsync()
    {
        var test = new VerifyParameters.Test
        {
            TestCode = $$"""
                class C { void {|SST1472:M|}(int a, int b, int c, int d, int e, int f, int g, int h = 0) { } }
                record R(int A, int B, int C, int D, int E, int F, int G, int H);{{IsExternalInit}}
                """,
        };
        test.TestState.AnalyzerConfigFiles.Add((EditorConfigPath, "root = true\n[*.cs]\nstylesharp.SST1472.count_optional_parameters = true\nstylesharp.SST1472.check_positional_records = false\n"));
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies a method over the default maximum is reported and one at the maximum is not.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task MethodOverTheMaximumIsReportedAsync() =>
        VerifyParameters.VerifyAnalyzerAsync(
            """
            public class C
            {
                public void {|SST1472:Wide|}(int a, int b, int c, int d, int e, int f, int g, int h)
                {
                }

                public void AtLimit(int a, int b, int c, int d, int e, int f, int g)
                {
                }
            }
            """);

    /// <summary>Verifies a constructor, a delegate, a local function and an indexer are all measured.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task EverySignatureKindIsMeasuredAsync() =>
        VerifyParameters.VerifyAnalyzerAsync(
            """
            public delegate void {|SST1472:Handler|}(int a, int b, int c, int d, int e, int f, int g, int h);

            public class C
            {
                public {|SST1472:C|}(int a, int b, int c, int d, int e, int f, int g, int h)
                {
                }

                public int {|SST1472:this|}[int a, int b, int c, int d, int e, int f, int g, int h] => a;

                public void Outer()
                {
                    void {|SST1472:Inner|}(int a, int b, int c, int d, int e, int f, int g, int h)
                    {
                    }

                    Inner(1, 2, 3, 4, 5, 6, 7, 8);
                }
            }
            """);

    /// <summary>Verifies a class primary constructor is measured like any other constructor.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ClassPrimaryConstructorIsMeasuredAsync() =>
        VerifyParameters.VerifyAnalyzerAsync(
            """
            public class {|SST1472:Service|}(int a, int b, int c, int d, int e, int f, int g, int h);

            public struct {|SST1472:Point|}(int a, int b, int c, int d, int e, int f, int g, int h);
            """);

    /// <summary>Verifies a positional record is the parameter object the rule asks for, so it is not reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task PositionalRecordIsCleanByDefaultAsync() =>
        VerifyParameters.VerifyAnalyzerAsync(
            $$"""
            public record Person(int A, int B, int C, int D, int E, int F, int G, int H, int I, int J);

            public record struct Point(int A, int B, int C, int D, int E, int F, int G, int H);{{IsExternalInit}}
            """);

    /// <summary>Verifies a positional record is measured once it is opted back in.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task PositionalRecordIsReportedWhenOptedInAsync()
    {
        var test = new VerifyParameters.Test
        {
            TestCode = $$"""
                       public record {|SST1472:Person|}(int A, int B, int C, int D, int E, int F, int G, int H);

                       public record Small(int A, int B);{{IsExternalInit}}
                       """,
        };

        test.TestState.AnalyzerConfigFiles.Add(
            (EditorConfigPath, """
            root = true
            [*.cs]
            stylesharp.SST1472.check_positional_records = true

            """));

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies a signature whose shape a base type or an interface dictates is not reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>The declaration that owns the shape is reported instead, which is where the fix belongs.</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task InheritedSignaturesAreReportedAtTheirSourceAsync() =>
        VerifyParameters.VerifyAnalyzerAsync(
            """
            public interface IProcessor
            {
                void {|SST1472:Run|}(int a, int b, int c, int d, int e, int f, int g, int h);
            }

            public abstract class Base
            {
                public abstract void {|SST1472:Execute|}(int a, int b, int c, int d, int e, int f, int g, int h);
            }

            public class Processor : Base, IProcessor
            {
                public void Run(int a, int b, int c, int d, int e, int f, int g, int h)
                {
                }

                public override void Execute(int a, int b, int c, int d, int e, int f, int g, int h)
                {
                }
            }
            """);

    /// <summary>Verifies an explicit interface implementation is not reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ExplicitInterfaceImplementationIsCleanAsync() =>
        VerifyParameters.VerifyAnalyzerAsync(
            """
            public interface IProcessor
            {
                void {|SST1472:Run|}(int a, int b, int c, int d, int e, int f, int g, int h);
            }

            public class Processor : IProcessor
            {
                void IProcessor.Run(int a, int b, int c, int d, int e, int f, int g, int h)
                {
                }
            }
            """);

    /// <summary>Verifies a P/Invoke keeps the signature the native API dictates.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task NativeImportIsCleanAsync() =>
        VerifyParameters.VerifyAnalyzerAsync(
            """
            using System.Runtime.InteropServices;

            public sealed class LibraryImportAttribute : System.Attribute
            {
                public LibraryImportAttribute(string name) => Name = name;

                public string Name { get; }
            }

            public partial class Native
            {
                [DllImport("user32.dll")]
                public static extern int MessageBox(int a, int b, int c, int d, int e, int f, int g, int h);

                [LibraryImport("native")]
                public static partial int Send(int a, int b, int c, int d, int e, int f, int g, int h);

                public static partial int Send(int a, int b, int c, int d, int e, int f, int g, int h) => 0;
            }
            """);

    /// <summary>Verifies only the defining half of a partial method is reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task PartialMethodIsReportedOnceAsync() =>
        VerifyParameters.VerifyAnalyzerAsync(
            """
            public partial class C
            {
                public partial void {|SST1472:Run|}(int a, int b, int c, int d, int e, int f, int g, int h);

                public partial void Run(int a, int b, int c, int d, int e, int f, int g, int h)
                {
                }
            }
            """);

    /// <summary>Verifies a deconstructor's parameters mirror the type's state and are not reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task DeconstructorIsCleanAsync() =>
        VerifyParameters.VerifyAnalyzerAsync(
            """
            public class C
            {
                public void Deconstruct(out int a, out int b, out int c, out int d, out int e, out int f, out int g, out int h)
                {
                    a = b = c = d = e = f = g = h = 0;
                }
            }
            """);

    /// <summary>Verifies a lambda takes its shape from its delegate and is never measured.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task LambdaIsCleanAsync() =>
        VerifyParameters.VerifyAnalyzerAsync(
            """
            public class C
            {
                public void M()
                {
                    System.Action<int, int, int, int, int, int, int, int> run =
                        (a, b, c, d, e, f, g, h) => System.Console.WriteLine(a + h);
                    run(1, 2, 3, 4, 5, 6, 7, 8);
                }
            }
            """);

    /// <summary>Verifies an extension method's receiver is written as the receiver, not as an argument.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ExtensionReceiverIsNotCountedAsync() =>
        VerifyParameters.VerifyAnalyzerAsync(
            """
            public static class Extensions
            {
                public static int Sum(this int[] values, int a, int b, int c, int d, int e, int f, int g) => a;

                public static int {|SST1472:Wide|}(this int[] values, int a, int b, int c, int d, int e, int f, int g, int h) => a;
            }
            """);

    /// <summary>Verifies an extension block's receiver is not counted against its members.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ExtensionBlockReceiverIsNotCountedAsync() =>
        VerifyParameters.VerifyAnalyzerAsync(
            """
            public static class Extensions
            {
                extension(int[] values)
                {
                    public int Sum(int a, int b, int c, int d, int e, int f, int g) => a;

                    public int {|SST1472:Wide|}(int a, int b, int c, int d, int e, int f, int g, int h) => a;
                }
            }
            """);

    /// <summary>Verifies the compiler-supplied caller-info parameters do not count against the maximum.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task CallerInfoParametersAreNotCountedAsync() =>
        VerifyParameters.VerifyAnalyzerAsync(
            """
            using System.Runtime.CompilerServices;

            public class C
            {
                public void Log(
                    int a,
                    int b,
                    int c,
                    int d,
                    int e,
                    int f,
                    int g,
                    [CallerMemberName] string member = "",
                    [CallerFilePath] string file = "",
                    [CallerLineNumber] int line = 0)
                {
                }
            }
            """);

    /// <summary>Verifies optional parameters count by default.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task OptionalParametersCountByDefaultAsync() =>
        VerifyParameters.VerifyAnalyzerAsync(
            """
            public class C
            {
                public void {|SST1472:Configure|}(int a, int b, int c, int d, int e, int f, int g, int h = 0)
                {
                }
            }
            """);

    /// <summary>Verifies optional parameters can be excluded from the count.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task OptionalParametersCanBeExcludedAsync()
    {
        var test = new VerifyParameters.Test
        {
            TestCode = """
                       public class C
                       {
                           public void Configure(int a, int b, int c, int d, int e, int f, int g, int h = 0, int i = 0)
                           {
                           }

                           public void {|SST1472:Required|}(int a, int b, int c, int d, int e, int f, int g, int h)
                           {
                           }
                       }
                       """,
        };

        test.TestState.AnalyzerConfigFiles.Add(
            (EditorConfigPath, """
            root = true
            [*.cs]
            stylesharp.SST1472.count_optional_parameters = false

            """));

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies the rule-specific maximum overrides the project-wide one.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task RuleSpecificMaximumWinsOverGeneralAsync()
    {
        var test = new VerifyParameters.Test
        {
            TestCode = """
                       public class C
                       {
                           public void {|SST1472:Four|}(int a, int b, int c, int d)
                           {
                           }

                           public void Three(int a, int b, int c)
                           {
                           }
                       }
                       """,
        };

        test.TestState.AnalyzerConfigFiles.Add(
            (EditorConfigPath, """
            root = true
            [*.cs]
            stylesharp.max_parameters = 20
            stylesharp.SST1472.max_parameters = 3

            """));

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies the project-wide maximum applies when no rule-specific key is set.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task GeneralMaximumAppliesAsync()
    {
        var test = new VerifyParameters.Test
        {
            TestCode = """
                       public class C
                       {
                           public void {|SST1472:Three|}(int a, int b, int c)
                           {
                           }

                           public void Two(int a, int b)
                           {
                           }
                       }
                       """,
        };

        test.TestState.AnalyzerConfigFiles.Add(
            (EditorConfigPath, """
            root = true
            [*.cs]
            stylesharp.max_parameters = 2

            """));

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies an unparsable maximum falls back to the default rather than reporting everything.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task UnparsableMaximumFallsBackToTheDefaultAsync()
    {
        var test = new VerifyParameters.Test
        {
            TestCode = """
                       public class C
                       {
                           public void AtLimit(int a, int b, int c, int d, int e, int f, int g)
                           {
                           }

                           public void {|SST1472:Wide|}(int a, int b, int c, int d, int e, int f, int g, int h)
                           {
                           }
                       }
                       """,
        };

        test.TestState.AnalyzerConfigFiles.Add(
            (EditorConfigPath, """
            root = true
            [*.cs]
            stylesharp.SST1472.max_parameters = lots

            """));

        await test.RunAsync(CancellationToken.None);
    }
}
