// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using RoslynCommon.Analyzers.Tests;
using VerifyNameSimplification = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.NameSimplificationAnalyzer,
    StyleSharp.Analyzers.NameSimplificationCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for shortest-equivalent-name analysis (SST1116/SST1117).</summary>
public class NameSimplificationAnalyzerUnitTest
{
    /// <summary>The path the analyzer config file is added at in the test workspace.</summary>
    private const string EditorConfigPath = "/.editorconfig";

    /// <summary>The editorconfig body that requires explicit <c>this.</c> on instance members.</summary>
    private const string RequireThisEditorConfig = """
                                                   root = true

                                                   [*.cs]
                                                   stylesharp.instance_member_qualification = require_this
                                                   """;

    /// <summary>Verifies a single foreach designation shadows only the member name it spells.</summary>
    /// <param name="variableName">The variable designation in the loop.</param>
    /// <param name="shadowsMember">Whether the designation hides the member named <c>value</c>.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    /// <remarks>The compiler cannot bind a foreach deconstruction written as one designation, so the helper is checked on the syntax alone.</remarks>
    [Test]
    [Arguments("value", true)]
    [Arguments("other", false)]
    public async Task SingleForeachDesignationShadowsOnlyItsOwnNameAsync(string variableName, bool shadowsMember)
    {
        var variable = SyntaxFactory.DeclarationExpression(
            SyntaxFactory.IdentifierName("var"),
            SyntaxFactory.SingleVariableDesignation(SyntaxFactory.Identifier(variableName)));
        await Assert.That(NameSimplificationAnalyzer.PatternDeclaresName(variable, "value")).IsEqualTo(shadowsMember);
    }

    /// <summary>Verifies a bound member in an incomplete script can simplify without an enclosing type declaration.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task IncompleteScriptMemberWithoutTypeDeclarationIsSimplifiedAsync()
    {
        var tree = CSharpSyntaxTree.ParseText(
            "int value; _ = this.value;",
            new(kind: SourceCodeKind.Script));
        var compilation = CSharpCompilation.CreateScriptCompilation(
            nameof(IncompleteScriptMemberWithoutTypeDeclarationIsSimplifiedAsync),
            tree,
            RuntimeMetadataReferences.Platform);
        var diagnostics = await compilation.WithAnalyzers([new NameSimplificationAnalyzer()]).GetAnalyzerDiagnosticsAsync();
        await Assert.That(diagnostics.Length).IsEqualTo(1);
        await Assert.That(diagnostics[0].Id).IsEqualTo("SST1117");
        var text = await tree.GetTextAsync();
        await Assert.That(text.ToString(diagnostics[0].Location.SourceSpan)).IsEqualTo("this.value");
    }

    /// <summary>Verifies a namespace used in an incomplete type position still binds consistently when shortened.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task NamespaceInIncompleteTypePositionUsesNamespaceArityAsync()
    {
        var compilation = CSharpCompilation.Create(
            nameof(NamespaceInIncompleteTypePositionUsesNamespaceArityAsync),
            [CSharpSyntaxTree.ParseText("namespace Alpha { namespace Beta { } class C { Alpha.Beta field; } }")],
            RuntimeMetadataReferences.Platform);
        var diagnostics = await compilation.WithAnalyzers([new NameSimplificationAnalyzer()]).GetAnalyzerDiagnosticsAsync();
        await Assert.That(diagnostics.Length).IsEqualTo(1);
        await Assert.That(diagnostics[0].Id).IsEqualTo("SST1116");
        var text = await diagnostics[0].Location.SourceTree!.GetTextAsync();
        await Assert.That(text.ToString(diagnostics[0].Location.SourceSpan)).IsEqualTo("Alpha.Beta");
    }

    /// <summary>Verifies nullable generic names remain qualified when a nearer generic type would capture them.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task ShadowedNullableGenericNameIsNotSimplifiedAsync() => VerifyNameSimplification.VerifyAnalyzerAsync(
        """
        #nullable enable
        namespace Other { public class Item<T> { } }
        class Item<T> { }
        class C { Other.Item<int>? Value; }
        """);

    /// <summary>Verifies a global qualifier remains when the surrounding namespace shadows its type.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task ShadowedGlobalTypeIsNotSimplifiedAsync() => VerifyNameSimplification.VerifyAnalyzerAsync(
        "class Item { } namespace Other { class Item { } class C { global::Item Value; } }");

    /// <summary>Verifies unresolved source symbols do not produce simplification diagnostics.</summary>
    /// <param name="source">The incomplete code being edited.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("class C { void M() { this.Missing(); } }")]
    [Arguments("class C { Unknown.Namespace.Type field; }")]
    [Arguments("class C { global::Missing field; }")]
    [Arguments("extern alias Custom; class C { Custom::Missing field; }")]
    public async Task UnresolvedSymbolsAreNotSimplifiedAsync(string source)
    {
        var compilation = CSharpCompilation.Create(
            nameof(UnresolvedSymbolsAreNotSimplifiedAsync),
            [CSharpSyntaxTree.ParseText(source)],
            RuntimeMetadataReferences.Platform);
        var diagnostics = await compilation.WithAnalyzers([new NameSimplificationAnalyzer()]).GetAnalyzerDiagnosticsAsync();
        await Assert.That(diagnostics).IsEmpty();
    }

    /// <summary>Verifies local declarations and parameter scopes preserve explicit member access.</summary>
    /// <param name="body">The member body with expected simplifications marked.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("System.Func<int, int> f = (value) => this.value;")]
    [Arguments("System.Func<int, int> f = value => this.value;")]
    [Arguments("System.Func<int, int> f = delegate(int value) { return this.value; };")]
    [Arguments("int Local(int value) => this.value;")]
    [Arguments("foreach (var value in new int[0]) { _ = this.value; }")]
    [Arguments("foreach (var (value, other) in new (int, int)[0]) { } _ = this.value;")]
    [Arguments("foreach (var (other, (value, third)) in new (int, (int, int))[0]) { } _ = this.value;")]
    [Arguments("foreach (var (other, third) in new (int, int)[0]) { } _ = {|SST1117:this.value|};")]
    [Arguments("using (System.IDisposable value = null) { } _ = this.value;")]
    [Arguments("fixed (int* value = new int[1]) { } _ = this.value;")]
    [Arguments("int other = 0; _ = {|SST1117:this.value|};")]
    [Arguments("_ = {|SST1117:this.value|}; int other = 0;")]
    [Arguments("foreach ((int other, int third) in new (int, int)[0]) { } _ = {|SST1117:this.value|};")]
    [Arguments("System.Func<int, int> f = (other) => {|SST1117:this.value|};")]
    [Arguments("System.Func<int, int> f = other => {|SST1117:this.value|};")]
    [Arguments("System.Func<int> f = delegate { return {|SST1117:this.value|}; };")]
    [Arguments("System.Func<int, int> f = delegate(int other) { return {|SST1117:this.value|}; };")]
    [Arguments("int Local(int other) => {|SST1117:this.value|};")]
    [Arguments("foreach (var other in new int[0]) { _ = {|SST1117:this.value|}; }")]
    [Arguments("using (System.IDisposable other = null) { } _ = {|SST1117:this.value|};")]
    [Arguments("using (new System.IO.MemoryStream()) { } _ = {|SST1117:this.value|};")]
    [Arguments("fixed (int* other = new int[1]) { } _ = {|SST1117:this.value|};")]
    public async Task LocalScopesControlThisSimplificationAsync(string body)
    {
        var test = new VerifyNameSimplification.Test { TestCode = $$"""class C { int value; unsafe void M() { {{body}} } }""" };
        test.SolutionTransforms.Add(static (solution, projectId) => solution.WithProjectCompilationOptions(
            projectId,
            ((CSharpCompilationOptions)solution.GetProject(projectId)!.CompilationOptions!).WithAllowUnsafe(true)));
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies global aliases simplify only when their unqualified spelling has the same binding.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task GlobalAliasesAndNamespaceContextsAreHandledAsync() =>
        VerifyNameSimplification.VerifyAnalyzerAsync(
            """
            using Alias = global::C;
            using System.Text;
            class C
            {
                {|SST1116:global::C|} Self;
                {|SST1116:global::System.Text.StringBuilder|} Builder;
                Alias Other;
            }
            namespace Alpha.Beta
            {
                class D { }
            }
            """);

    /// <summary>Verifies file-scoped namespace declarations retain their qualified name.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task FileScopedNamespaceIsNotSimplifiedAsync() =>
        VerifyNameSimplification.VerifyAnalyzerAsync("namespace Alpha.Beta; class C { }");

    /// <summary>Verifies types of a different generic arity do not make a short name ambiguous.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task DifferentGenericArityDoesNotShadowShortNameAsync() =>
        VerifyNameSimplification.VerifyAnalyzerAsync(
            """
            using Alpha;
            using Beta;
            namespace Alpha { public class Item { } }
            namespace Beta { public class Item<T> { } }
            class C { {|SST1116:Alpha.Item|} value; }
            """);

    /// <summary>Verifies importing a namespace does not import its child namespaces as competing type names.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task ImportedSubnamespaceDoesNotShadowTypeNameAsync() =>
        VerifyNameSimplification.VerifyCodeFixAsync(
            "using Alpha; using Beta; namespace Alpha { public class Item { } } namespace Beta.Item { } class C { {|SST1116:Alpha.Item|} field; }",
            "using Alpha; using Beta; namespace Alpha { public class Item { } } namespace Beta.Item { } class C { Item field; }");

    /// <summary>Verifies namespace documentation references keep their existing qualification.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task QualifiedNamespaceInDocumentationIsPreservedAsync() =>
        VerifyNameSimplification.VerifyAnalyzerAsync(
            """
            namespace Alpha
            {
                namespace Beta { }
                /// <summary>Uses <see cref="Alpha.Beta"/>.</summary>
                class C { }
            }
            """);

    /// <summary>Verifies a catch variable preserves the qualifier on a shadowed member.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task CatchVariablePreservesThisAsync() =>
        VerifyNameSimplification.VerifyAnalyzerAsync(
            "class C { int value; void M() { try { } catch (System.Exception value) { _ = this.value; } } }");

    /// <summary>Verifies catch variables shadow members only inside their own body and filter.</summary>
    /// <param name="body">The catch scopes and expected simplifications.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("try { } catch (System.Exception value) when (this.value > 0) { _ = this.value; }")]
    [Arguments("try { } catch (System.Exception other) { _ = {|SST1117:this.value|}; }")]
    [Arguments("try { } catch (System.Exception) { _ = {|SST1117:this.value|}; }")]
    [Arguments("try { } catch { _ = {|SST1117:this.value|}; }")]
    [Arguments("try { _ = {|SST1117:this.value|}; } catch (System.Exception value) { } _ = {|SST1117:this.value|};")]
    [Arguments("try { } catch (System.ArgumentException value) { _ = this.value; } catch (System.Exception other) { _ = {|SST1117:this.value|}; }")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task CatchScopeControlsThisSimplificationAsync(string body) =>
        VerifyNameSimplification.VerifyAnalyzerAsync($$"""class C { int value; void M() { {{body}} } }""");

    /// <summary>Verifies required qualification covers properties, methods, and events but skips naming syntax.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task RequiredThisSkipsNameofAndNamedArgumentsAsync()
    {
        var test = new VerifyNameSimplification.Test
        {
            TestCode = """
                       using Alias = System.Action;
                       using static C;
                       class C
                       {
                           int Property { get; set; }
                           event Alias Changed;
                           void Helper(int value) { }
                           void M(C other)
                           {
                               {|SST1117:Property|} = 1;
                               {|SST1117:Helper|}(value: {|SST1117:Property|});
                               {|SST1117:Changed|}?.Invoke();
                               _ = nameof(Property);
                               _ = other?.Property;
                               _ = other.Property;
                               int Local() => 0;
                               _ = Local();
                           }
                       }
                       """,
        };
        test.TestState.AnalyzerConfigFiles.Add((EditorConfigPath, RequireThisEditorConfig));
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies explicit qualification options and unknown values choose the expected style.</summary>
    /// <param name="option">The configured spelling.</param>
    /// <param name="requireThis">Whether the option requires an explicit receiver.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("require_this", true)]
    [Arguments("this", true)]
    [Arguments(" REQUIRE_THIS ", true)]
    [Arguments("omit_this", false)]
    [Arguments("omit", false)]
    [Arguments("unknown", false)]
    [Arguments("", false)]
    public async Task QualificationOptionSelectsStyleAsync(string option, bool requireThis)
    {
        var expression = requireThis ? "{|SST1117:value|} + this.value" : "value + {|SST1117:this.value|}";
        var test = new VerifyNameSimplification.Test { TestCode = $$"""class C { int value; int M() => {{expression}}; }""" };
        test.TestState.AnalyzerConfigFiles.Add((EditorConfigPath, $"root = true\n[*.cs]\nstylesharp.instance_member_qualification = {option}\n"));
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies a qualified type name is shortened only when the shorter name binds to the same symbol.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task QualifiedNameCandidateIsFixedAsync()
    {
        const string Source = """
                              using System.Text;

                              public sealed class C
                              {
                                  public int M({|SST1116:System.Text.StringBuilder|} builder) => builder.Length;
                              }
                              """;
        const string FixedSource = """
                                   using System.Text;

                                   public sealed class C
                                   {
                                       public int M(StringBuilder builder) => builder.Length;
                                   }
                                   """;
        await VerifyNameSimplification.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a name two imported namespaces both answer to is left qualified, because the short form is ambiguous.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task QualifiedNameAmbiguousAcrossImportedNamespacesIsNotReportedAsync()
    {
        const string Source = """
                              using Alpha.Serialization;
                              using Beta.Serialization;

                              namespace Alpha.Serialization
                              {
                                  public sealed class PayloadException
                                  {
                                  }
                              }

                              namespace Beta.Serialization
                              {
                                  public sealed class PayloadException
                                  {
                                  }
                              }

                              public sealed class C
                              {
                                  public string Describe(Alpha.Serialization.PayloadException alpha, Beta.Serialization.PayloadException beta)
                                      => alpha.ToString() + beta.ToString();
                              }
                              """;
        await VerifyNameSimplification.VerifyAnalyzerAsync(Source);
    }

    /// <summary>Verifies a redundant this-qualified member access is shortened.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task ThisMemberAccessCandidateIsFixedAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  private readonly int _value = 1;

                                  public int M() => {|SST1117:this._value|};
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       private readonly int _value = 1;

                                       public int M() => _value;
                                   }
                                   """;
        await VerifyNameSimplification.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a this-qualified extension-method invocation is not reported, since dropping the receiver breaks compilation.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task ThisExtensionMethodInvocationIsNotReportedAsync()
    {
        const string Source = """
                              using Splat;

                              namespace Splat
                              {
                                  public interface IEnableLogger
                                  {
                                  }

                                  public sealed class Logger
                                  {
                                      public void Debug(string message)
                                      {
                                      }
                                  }

                                  public static class LoggingExtensions
                                  {
                                      public static Logger Log(this IEnableLogger source) => new Logger();
                                  }
                              }

                              public sealed class C : IEnableLogger
                              {
                                  public void M() => this.Log().Debug("x");
                              }
                              """;
        var test = new VerifyNameSimplification.Test { TestCode = Source };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies a this-qualified extension-block member invocation is not reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task ThisExtensionBlockMemberInvocationIsNotReportedAsync()
    {
        const string Source = """
                              public interface IThing;

                              public static class ThingExtensions
                              {
                                  extension(IThing item)
                                  {
                                      public int Compute() => 42;
                                  }
                              }

                              public sealed class MyThing : IThing
                              {
                                  public int M() => this.Compute();
                              }
                              """;
        var test = new VerifyNameSimplification.Test { TestCode = Source };
        ApplyPreviewParseOptions(test.SolutionTransforms);

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies a redundant this-qualified instance-method call is shortened.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task ThisInstanceMethodCallIsFixedAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  private int Helper() => 1;

                                  public int M() => {|SST1117:this.Helper|}();
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       private int Helper() => 1;

                                       public int M() => Helper();
                                   }
                                   """;
        await VerifyNameSimplification.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies configured <c>this.</c>-qualification keeps explicit instance-member access.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task RequireThisStyleKeepsThisMemberAccessAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  private readonly int _value = 1;

                                  public int M() => this._value;
                              }
                              """;
        var test = new VerifyNameSimplification.Test { TestCode = Source };
        test.TestState.AnalyzerConfigFiles.Add((EditorConfigPath, RequireThisEditorConfig));

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies configured <c>this.</c>-qualification reports a bare instance member through the same rule.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task RequireThisStyleQualifiesBareInstanceMemberAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  private readonly int _value = 1;

                                  public int M() => {|SST1117:_value|};
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       private readonly int _value = 1;

                                       public int M() => this._value;
                                   }
                                   """;
        var test = CreateRequireThisTest(Source, FixedSource);
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies configured <c>this.</c>-qualification skips static, local, qualified, and initializer names.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task RequireThisStyleSkipsNamesThatCannotBeInstanceQualifiedAsync()
    {
        const string Source = """
                              internal sealed class C
                              {
                                  private static int shared;

                                  private int _field;

                                  private int M()
                                  {
                                      var local = 1;
                                      return {|SST1117:_field|} + shared + local + this._field;
                                  }

                                  private C Create() => new C { _field = 1 };
                              }
                              """;
        var test = new VerifyNameSimplification.Test { TestCode = Source };
        test.TestState.AnalyzerConfigFiles.Add((EditorConfigPath, RequireThisEditorConfig));

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies names are not shortened when the shorter spelling would bind elsewhere.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task AmbiguousShortNamesAreCleanAsync()
    {
        const string Source = """
                              namespace Other
                              {
                                  public sealed class Widget
                                  {
                                  }
                              }

                              public sealed class Widget
                              {
                              }

                              public sealed class C
                              {
                                  private readonly int _value = 1;

                                  public Other.Widget Create() => new Other.Widget();

                                  public int M()
                                  {
                                      var _value = 2;
                                      return this._value + _value;
                                  }
                              }
                              """;
        await VerifyNameSimplification.VerifyAnalyzerAsync(Source);
    }

    /// <summary>Verifies parameters that shadow member names keep the explicit <c>this.</c> qualifier.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task ParameterShadowKeepsThisQualifierAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  private readonly int value = 1;

                                  public int M(int value) => this.value + value;
                              }
                              """;
        await VerifyNameSimplification.VerifyAnalyzerAsync(Source);
    }

    /// <summary>Verifies a local function that shadows a method name keeps the explicit <c>this.</c> qualifier.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task LocalFunctionShadowKeepsThisQualifierAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  private int GetValue() => 1;

                                  public int M()
                                  {
                                      int GetValue() => 2;
                                      return this.GetValue();
                                  }
                              }
                              """;
        await VerifyNameSimplification.VerifyAnalyzerAsync(Source);
    }

    /// <summary>Verifies generic qualified names keep the slower semantic fallback and still simplify correctly.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task GenericQualifiedNameCandidateIsFixedAsync()
    {
        const string Source = """
                              using System.Collections.Generic;

                              public sealed class C
                              {
                                  public int M({|SST1116:System.Collections.Generic.List<int>|} values) => values.Count;
                              }
                              """;
        const string FixedSource = """
                                   using System.Collections.Generic;

                                   public sealed class C
                                   {
                                       public int M(List<int> values) => values.Count;
                                   }
                                   """;
        await VerifyNameSimplification.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a qualified parameter type inside a documentation reference is shortened.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>
    /// A documentation comment is trivia, so the reported name only comes back from a search that descends
    /// into it. Searching the surrounding tokens alone lands on the member declaration, which nothing knows
    /// how to shorten, and the reported name is left as written.
    /// </remarks>
    [Test]
    public async Task QualifiedNameInsideDocumentationReferenceParameterIsFixedAsync()
    {
        const string Source = """
                              using System.Threading;

                              public sealed class C
                              {
                                  /// <summary>Reads a value.</summary>
                                  /// <param name="cancellationToken">A token that cancels the read.</param>
                                  /// <remarks>Mirrors <see cref="C.Read({|SST1116:System.Threading.CancellationToken|})"/>.</remarks>
                                  public void Read(CancellationToken cancellationToken) => cancellationToken.ThrowIfCancellationRequested();
                              }
                              """;
        const string FixedSource = """
                                   using System.Threading;

                                   public sealed class C
                                   {
                                       /// <summary>Reads a value.</summary>
                                       /// <param name="cancellationToken">A token that cancels the read.</param>
                                       /// <remarks>Mirrors <see cref="C.Read(CancellationToken)"/>.</remarks>
                                       public void Read(CancellationToken cancellationToken) => cancellationToken.ThrowIfCancellationRequested();
                                   }
                                   """;
        await VerifyNameSimplification.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies generated files are not analyzed even when diagnostic reporting is optimized.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task GeneratedFilesStayCleanAsync()
    {
        const string Source = """
                              using System.Text;

                              public sealed class C
                              {
                                  private readonly int _value = 1;

                                  public int M(System.Text.StringBuilder builder) => this._value + builder.Length;
                              }
                              """;
        var test = new VerifyNameSimplification.Test();
        test.TestState.Sources.Add(("NameSimplificationBench.g.cs", Source));

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Creates a verifier test with instance-member access configured to require <c>this.</c>.</summary>
    /// <param name="source">The test source.</param>
    /// <param name="fixedSource">The fixed source.</param>
    /// <returns>The configured verifier test.</returns>
    private static VerifyNameSimplification.Test CreateRequireThisTest(string source, string fixedSource)
    {
        var test = new VerifyNameSimplification.Test { TestCode = source, FixedCode = fixedSource };
        test.TestState.AnalyzerConfigFiles.Add((EditorConfigPath, RequireThisEditorConfig));
        test.FixedState.AnalyzerConfigFiles.Add((EditorConfigPath, RequireThisEditorConfig));
        return test;
    }

    /// <summary>Applies preview parse options so extension blocks parse.</summary>
    /// <param name="solutionTransforms">The solution-transform collection to update.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ApplyPreviewParseOptions(List<Func<Solution, ProjectId, Solution>> solutionTransforms) => solutionTransforms.Add(static (solution, projectId) =>
        {
            var parseOptions = (CSharpParseOptions)solution.GetProject(projectId)!.ParseOptions!;
            return solution.WithProjectParseOptions(projectId, parseOptions.WithLanguageVersion(LanguageVersion.Preview));
        });
}
