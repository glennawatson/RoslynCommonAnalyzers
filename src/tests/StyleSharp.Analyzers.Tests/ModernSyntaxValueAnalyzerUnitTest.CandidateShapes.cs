// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using RoslynCommon.Analyzers.Tests;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests syntax and semantic boundaries of value-oriented modernization candidates.</summary>
public partial class ModernSyntaxValueAnalyzerUnitTest
{
    /// <summary>Verifies interpolation rewrites retain the receiver and optional format.</summary>
    /// <param name="expression">The interpolation expression.</param>
    /// <param name="replacement">The replacement expression and optional format.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    [Arguments("value.ToString()", "value")]
    [Arguments("value.ToString(\"X\")", "value:X")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task InterpolationSimplificationPreservesTheHoleAsync(string expression, string replacement) =>
        CreateNet80Test(
            $$$"""class C { string M(int value) => $"{{|SST2220:{{{expression}}}|}}"; }""",
            $$$"""class C { string M(int value) => $"{{{{replacement}}}}"; }""").RunAsync(CancellationToken.None);

    /// <summary>Verifies nonliteral formats and unrelated interpolation expressions remain unchanged.</summary>
    /// <param name="expression">The expression inside the interpolation hole.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    [Arguments("value")]
    [Arguments("Format(value)")]
    [Arguments("value.GetHashCode()")]
    [Arguments("value.ToString(format)")]
    [Arguments("value.ToString(\"\")")]
    [Arguments("value.ToString(format: null)")]
    [Arguments("value.ToString(\"X\", System.Globalization.CultureInfo.InvariantCulture)")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task UnsupportedInterpolationShapesAreCleanAsync(string expression) =>
        CreateNet80Test($$"""class C { string M(int value, string format) => $"{ {{expression}} }"; string Format(int value) => value.ToString(); }""").RunAsync(CancellationToken.None);

    /// <summary>Verifies coalescing assignment requires the same stable target and a simple assignment.</summary>
    /// <param name="expression">The coalescing expression.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    [Arguments("value ?? other")]
    [Arguments("value ?? (other = \"fallback\")")]
    [Arguments("value ?? (value += \"fallback\")")]
    [Arguments("Get().Value ?? (Get().Value = \"fallback\")")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task CoalescingAssignmentNearMissesAreCleanAsync(string expression) =>
        CreateNet80Test($$"""class C { public string Value; C Get() => this; string M(string value, string other) => {{expression}}; }""").RunAsync(CancellationToken.None);

    /// <summary>Verifies the compact coalescing form reports and fixes matching targets.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task CoalescingExpressionWithMatchingAssignmentIsFixedAsync() =>
        CreateNet80Test(
            "class C { string M(string value) => value {|SST2223:??|} (value = \"fallback\"); }",
            "class C { string M(string value) => value ??= \"fallback\"; }").RunAsync(CancellationToken.None);

    /// <summary>Verifies null checks reject else branches, unstable receivers, and unrelated assignments.</summary>
    /// <param name="body">The method statements.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    [Arguments("if (value == null) value = other; else value = \"else\";")]
    [Arguments("if (value != null) value = other;")]
    [Arguments("if (value == other) value = other;")]
    [Arguments("if (null == null) value = other;")]
    [Arguments("if (Get().Value == null) Get().Value = other;")]
    [Arguments("if (value is string) value = other;")]
    [Arguments("if (value is \"text\") value = other;")]
    [Arguments("if (value is Text) value = other;")]
    [Arguments("if (value == null) { value = other; System.GC.KeepAlive(value); }")]
    [Arguments("if (value == null) other = value;")]
    [Arguments("if (value == null) value += other;")]
    [Arguments("if (true) if (value == null) throw new System.Exception();")]
    [Arguments("if (value == null) throw new System.Exception();")]
    [Arguments("value = other; if (other == null) throw new System.Exception();")]
    [Arguments("string copy = other; if (value == null) throw new System.Exception();")]
    [Arguments("string copy; if (value == null) throw new System.Exception();")]
    [Arguments("value = other; if (value == null) { }")]
    [Arguments("value = other; if (value == null) other = \"fallback\";")]
    [Arguments("string copy = other; if (this.Value == null) throw new System.Exception();")]
    [Arguments("string copy = null; if (copy == null) throw new System.Exception();")]
    [Arguments("try { } catch { value = other; if (value == null) throw; }")]
    [Arguments("object boxed = 1; if (boxed == null) throw new System.Exception();")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task UnsupportedNullCheckShapesAreCleanAsync(string body) =>
        CreateNet80Test($$"""class C { const string Text = "text"; public string Value; C Get() => this; void M(string value, string other) { {{body}} } }""").RunAsync(CancellationToken.None);

    /// <summary>Verifies either orientation of a built-in equality null check supports coalescing assignment.</summary>
    /// <param name="condition">The equality expression.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    [Arguments("null == value")]
    [Arguments("value == null")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task EqualityNullCheckIsFixedAsync(string condition) =>
        CreateNet80Test(
            $$"""class C { void M(string value) { {|SST2223:if|} ({{condition}}) value = "fallback"; } }""",
            "class C { void M(string value) { value ??= \"fallback\"; } }").RunAsync(CancellationToken.None);

    /// <summary>Verifies C# 7 fallback assignments fold into their preceding assignment.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task AssignmentFallbackFoldsBeforeCoalesceAssignmentExistsAsync()
    {
        var test = CreateNet80Test(
            "class C { void M(string value, string input) { value = input; {|SST2227:if|} (value == null) value = \"fallback\"; } }",
            "class C { void M(string value, string input) { value = input ?? \"fallback\"; } }");
        SetLanguageVersion(test, LanguageVersion.CSharp7);
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies language gates keep syntax suggestions compatible with the source version.</summary>
    /// <param name="source">The complete source.</param>
    /// <param name="version">The language version that cannot express the proposed replacement.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    [Arguments("class C { string M(string value) => value ?? (value = \"fallback\"); }", LanguageVersion.CSharp7)]
    [Arguments("class C { void M(string value, string input) { value = input; if (value == null) throw new System.Exception(); } }", LanguageVersion.CSharp6)]
    [Arguments("class C { bool M(object value) => value is object; }", LanguageVersion.CSharp8)]
    [Arguments("class C { bool M(object value) => value is object item; }", LanguageVersion.CSharp8)]
    [Arguments("class C { string M() => nameof(System.Collections.Generic.List<int>); }", LanguageVersion.CSharp12)]
    public async Task UnsupportedLanguageVersionsAreCleanAsync(string source, LanguageVersion version)
    {
        var test = CreateNet80Test(source);
        SetLanguageVersion(test, version);
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies delegate conversion accepts parameterless and parenthesized parameter lists.</summary>
    /// <param name="declaration">The delegate declaration.</param>
    /// <param name="invocation">The invocation after its declaration.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    [Arguments("System.Func<int> {|SST2228:compute|} = () => 1;", "compute()")]
    [Arguments("System.Func<int, int, int> {|SST2228:compute|} = (x, y) => x + y;", "compute(1, 2)")]
    [Arguments("System.Func<int, int> {|SST2228:compute|} = (int x) => x;", "compute(1)")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task ParenthesizedDelegateParametersAreReportedAsync(string declaration, string invocation) =>
        CreateNet80Test($$"""class C { int M() { {{declaration}} return {{invocation}}; } }""").RunAsync(CancellationToken.None);

    /// <summary>Verifies preceding declarations and same-named fields do not count as delegate value uses.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task SameNamedFieldDoesNotPreventLocalFunctionConversionAsync() =>
        CreateNet80Test("class C { int compute; int M() { int count = 1; System.Func<int> {|SST2228:compute|} = () => count; _ = this.compute; return compute(); } }")
            .RunAsync(CancellationToken.None);

    /// <summary>Verifies delegate value uses, missing invocations, async lambdas, and ref parameters remain delegates.</summary>
    /// <param name="body">The method statements.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    [Arguments("System.Func<int> compute = () => 1;")]
    [Arguments("System.Func<int> compute = () => 1; object saved = compute;")]
    [Arguments("System.Func<int> compute = () => 1; _ = compute.Invoke();")]
    [Arguments("System.Func<int> compute = () => 1; compute = () => 2;")]
    [Arguments("System.Func<int> compute = delegate { return 1; }; _ = compute();")]
    [Arguments("System.Func<System.Threading.Tasks.Task<int>> compute = async () => { await System.Threading.Tasks.Task.Yield(); return 1; }; _ = compute();")]
    [Arguments("RefDelegate compute = (ref int value) => value; int item = 1; _ = compute(ref item);")]
    [Arguments("System.Linq.Expressions.Expression<System.Func<int>> compute = () => 1;")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task UnsupportedDelegateUsesAreCleanAsync(string body) =>
        CreateNet80Test($$"""delegate int RefDelegate(ref int value); class C { void M() { {{body}} } }""").RunAsync(CancellationToken.None);

    /// <summary>Verifies foreach casts distinguish generic collections from legacy enumerables.</summary>
    /// <param name="collectionType">The collection type.</param>
    /// <param name="iterationType">The loop variable type.</param>
    /// <param name="keyword">The optionally annotated foreach keyword.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    [Arguments("System.Collections.Generic.IEnumerable<object>", "string", "{|SST2225:foreach|}")]
    [Arguments("System.Collections.Generic.List<object>", "string", "{|SST2225:foreach|}")]
    [Arguments("System.Collections.Generic.List<System.Exception>", "System.InvalidOperationException", "{|SST2225:foreach|}")]
    [Arguments("System.Collections.IEnumerable", "string", "foreach")]
    [Arguments("System.Collections.ArrayList", "string", "foreach")]
    [Arguments("string[]", "object", "foreach")]
    [Arguments("string[]", "string", "foreach")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task ForeachCollectionShapesAreClassifiedAsync(string collectionType, string iterationType, string keyword) =>
        CreateNet80Test($$"""class C { void M({{collectionType}} values) { {{keyword}} ({{iterationType}} value in values) { } } }""").RunAsync(CancellationToken.None);

    /// <summary>Verifies direct-null patterns require a broad built-in object test and a compatible input type.</summary>
    /// <param name="expression">The pattern expression.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    [Arguments("value is {|SST2231:object item|}")]
    [Arguments("value is {|SST2231:object { }|}")]
    [Arguments("value is not {|SST2231:object { }|}")]
    [Arguments("value is string item")]
    [Arguments("value is string { }")]
    [Arguments("value is { }")]
    [Arguments("value is System.Object")]
    [Arguments("number is object")]
    [Arguments("number is object item")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task ObjectPatternShapesAreClassifiedAsync(string expression) =>
        CreateNet80Test($$"""class C { bool M(object value, int number) => {{expression}}; }""").RunAsync(CancellationToken.None);

    /// <summary>Verifies generic value-type constraints still permit the analyzer's null-pattern suggestion.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task ConstrainedTypeParameterSupportsNullPatternAsync() =>
        CreateNet80Test("class C { bool M<T>(T value) where T : struct => value is {|SST2231:object|}; }").RunAsync(CancellationToken.None);

    /// <summary>Verifies non-generic names and already unbound generic names remain unchanged.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task NameofWithoutConcreteTypeArgumentsIsCleanAsync() =>
        CreateNet80Test("class C { string M() => nameof(C); string N() => nameof(System.Collections.Generic.Dictionary<,>); }").RunAsync(CancellationToken.None);

    /// <summary>Verifies malformed anonymous initializers do not manufacture tuple element names.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task AnonymousInitializerWithoutNameCannotBecomeTupleElementAsync()
    {
        var initializer = SyntaxFactory.AnonymousObjectMemberDeclarator(SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(1)));
        var supported = ModernSyntaxValueAnalyzer.TryGetTupleElement(initializer, out var name, out var expression);
        await Assert.That(supported).IsFalse();
        await Assert.That(name).IsEmpty();
        await Assert.That(expression).IsSameReferenceAs(initializer.Expression);
    }

    /// <summary>Verifies an incomplete explicit member name cannot become a tuple element name.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task AnonymousInitializerWithMissingExplicitNameIsRejectedAsync()
    {
        var initializer = SyntaxFactory.AnonymousObjectMemberDeclarator(
            SyntaxFactory.NameEquals(SyntaxFactory.IdentifierName(SyntaxFactory.MissingToken(SyntaxKind.IdentifierToken))),
            SyntaxFactory.IdentifierName("value"));
        var supported = ModernSyntaxValueAnalyzer.TryGetTupleElement(initializer, out var name, out _);
        await Assert.That(supported).IsFalse();
        await Assert.That(name).IsEmpty();
    }

    /// <summary>Verifies the interpolation helper refuses unsupported calls without producing replacement syntax.</summary>
    /// <param name="expression">The expression in the interpolation hole.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    [Arguments("value")]
    [Arguments("value.ToString(\"\")")]
    [Arguments("value.ToString(format)")]
    public async Task UnsupportedInterpolationHasNoReplacementAsync(string expression)
    {
        var interpolation = ((InterpolatedStringExpressionSyntax)SyntaxFactory.ParseExpression($$"""$"{ {{expression}} }""")).Contents.OfType<InterpolationSyntax>().Single();
        var supported = ModernSyntaxValueAnalyzer.TryGetSimplifiedInterpolation(interpolation, out var replacement);
        await Assert.That(supported).IsFalse();
        await Assert.That(replacement).IsNull();
    }

    /// <summary>Verifies disabled-by-default rules are silent until explicitly enabled.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task OptionalValueRulesAreDisabledByDefaultAsync()
    {
        var tree = CSharpSyntaxTree.ParseText("class C { int M(int id) { new C(); var bundle = new { id, Label = id }; return bundle.id; } }");
        var compilation = CSharpCompilation.Create(TestProjectName, [tree], RuntimeMetadataReferences.Platform, new(OutputKind.DynamicallyLinkedLibrary));
        var diagnostics = await compilation.WithAnalyzers([new ModernSyntaxValueAnalyzer()]).GetAnalyzerDiagnosticsAsync(CancellationToken.None);
        await Assert.That(diagnostics).IsEmpty();
    }

    /// <summary>Verifies object constructions can make their discarded value explicit.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task IgnoredObjectConstructionIsReportedWhenEnabledAsync()
    {
        var test = CreateNet80Test("class C { void M() { {|SST2221:new C()|}; } }", "class C { void M() { _ = new C(); } }");
        Enable(test, IgnoredExpressionValueRuleId);
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies tuple candidates require two named elements and a method-local declaration.</summary>
    /// <param name="source">The complete source.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    [Arguments("class C { object field = new { A = 1, B = 2 }; }")]
    [Arguments("class C { void M(int id) { var value = new { id }; } }")]
    [Arguments("class C { object M() { object value; value = new { A = 1, B = 2 }; return value; } }")]
    public async Task AnonymousObjectDeclarationShapesAreClassifiedAsync(string source)
    {
        var test = CreateNet80Test(source);
        Enable(test, AnonymousObjectToTupleRuleId);
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies inferred member names become inferred tuple element names.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task InferredMemberNameBecomesTupleElementAsync()
    {
        var test = CreateNet80Test(
            "class C { int M(C source) { var value = {|SST2224:new|} { source.Id, Count = 2 }; return value.Id; } public int Id; }",
            "class C { int M(C source) { var value = (source.Id, Count: 2); return value.Id; } public int Id; }");
        Enable(test, AnonymousObjectToTupleRuleId);
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies overloaded equality preserves its potentially observable null-check behavior.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task UserDefinedNullEqualityIsCleanAsync() =>
        CreateNet80Test("""
                          class C
                          {
                              public static bool operator ==(C left, C right) => false;
                              public static bool operator !=(C left, C right) => true;
                              public override bool Equals(object value) => false;
                              public override int GetHashCode() => 0;
                              void M(C value, C fallback) { if (value == null) value = fallback; }
                          }
                          """).RunAsync(CancellationToken.None);

    /// <summary>Verifies nested parentheses do not hide the conversion inside an explicit cast.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task ParenthesizedHiddenConversionIsReportedAsync() =>
        CreateNet80Test("""
                          class Base { }
                          class Derived : Base { }
                          class Castable { public static explicit operator Base(Castable value) => new Base(); }
                          class C { Derived M(Castable value) => {|SST2226:(Derived)((value))|}; }
                          """).RunAsync(CancellationToken.None);

    /// <summary>Verifies an implicit user operator inside an explicit downcast receives the current inner-cast suggestion.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task ImplicitUserOperatorInsideDowncastIsReportedAsync() =>
        CreateNet80Test("""
                          class Base { }
                          class Derived : Base { }
                          class Castable { public static implicit operator Base(Castable value) => new Base(); }
                          class C { Derived M(Castable value) => {|SST2226:(Derived)value|}; }
                          """).RunAsync(CancellationToken.None);

    /// <summary>Verifies ordinary casts have no hidden explicit conversion to expose.</summary>
    /// <param name="expression">The expression being cast.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    [Arguments("(object)value")]
    [Arguments("(string)value")]
    [Arguments("(object)(string)value")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task VisibleOrImplicitCastsAreCleanAsync(string expression) =>
        CreateNet80Test($$"""class C { object M(object value) => {{expression}}; }""").RunAsync(CancellationToken.None);

    /// <summary>Verifies incomplete source exits semantic candidate checks without analyzer failures.</summary>
    /// <param name="source">The intentionally incomplete source.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    [Arguments("class C { void M() { Missing(); } }")]
    [Arguments("class C { void M() { new(); } }")]
    [Arguments("class C { void M(int[] values) { foreach (string value in values) { } } }")]
    [Arguments("class C { void M() { foreach (string value in Missing) { } } }")]
    [Arguments("class C { bool M() => null is object item; }")]
    [Arguments("class C { int M() { return missing++; } }")]
    [Arguments("class C { string M() => nameof((int)1); }")]
    [Arguments("class C { [Missing((int)1)] void M() { } }")]
    [Arguments("class C { int M() => (int); }")]
    [Arguments("class C { void M() { System.Func<int> compute = x => 1; _ = compute(); } }")]
    [Arguments("class C { void M() { System.Func<int, int> compute = (x, y) => x; _ = compute(1); } }")]
    [Arguments("class C { void M() { System.Func<int, int, int> compute = x => x; _ = compute(1, 2); } }")]
    [Arguments("class C { void M() { var value = new { 1, 2 }; } }")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task UnresolvedCandidateShapesAreCleanAsync(string source) => VerifyInvalidSourceAsync(source);

    /// <summary>Verifies an untyped interpolation receiver retains the current syntax-based suggestion.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task UntypedInterpolationReceiverIsReportedAsync() =>
        VerifyInvalidSourceAsync("""class C { string M() => $"{null.ToString()}"; }""", "SST2220");

    /// <summary>Verifies a null literal with no intrinsic type cannot use the object-pattern suggestion.</summary>
    /// <param name="expression">The pattern expression.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    [Arguments("null is object")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task UntypedNullPatternsAreCleanAsync(string expression) =>
        CreateNet80Test($$"""class C { bool M() => {{expression}}; }""").RunAsync(CancellationToken.None);

    /// <summary>Verifies pointers cannot use a coalescing expression even though they can be compared with null.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task PointerNullCheckCannotFoldAsync()
    {
        var test = CreateNet80Test("unsafe class C { void M(int* input) { int* value = input; if (value == null) throw new System.Exception(); } }");
        test.SolutionTransforms.Add(static (solution, projectId) =>
        {
            var options = (CSharpCompilationOptions)solution.GetProject(projectId)!.CompilationOptions!;
            return solution.WithProjectCompilationOptions(projectId, options.WithAllowUnsafe(true));
        });
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies scanning past earlier statements locates the assignment immediately before the null check.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task LaterNullThrowFoldsItsImmediatelyPrecedingAssignmentAsync() =>
        CreateNet80Test("class C { void M(string input) { int count = input.Length; string value = input; {|SST2227:if|} (value == null) throw new System.Exception(); } }")
            .RunAsync(CancellationToken.None);

    /// <summary>Verifies top-level locals are outside the block required for tuple and local-function conversion.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task TopLevelLocalCandidatesAreCleanAsync()
    {
        var test = CreateNet80Test("System.Func<int> compute = () => 1; var value = new { A = 1, B = 2 }; _ = compute() + value.A;");
        test.TestState.OutputKind = OutputKind.ConsoleApplication;
        Enable(test, AnonymousObjectToTupleRuleId);
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Checks the analyzer's result while asserting that the input really contains compiler errors.</summary>
    /// <param name="source">The intentionally invalid source.</param>
    /// <param name="expectedDiagnosticId">The single expected diagnostic, or null when none is expected.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private static async Task VerifyInvalidSourceAsync(string source, string? expectedDiagnosticId = null)
    {
        var tree = CSharpSyntaxTree.ParseText(source, CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Preview));
        var compilation = CSharpCompilation.Create(
            TestProjectName,
            [tree],
            RuntimeMetadataReferences.Platform,
            new(OutputKind.DynamicallyLinkedLibrary));
        var compilerErrors = compilation.GetDiagnostics().Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ToArray();
        await Assert.That(compilerErrors).IsNotEmpty();
        var diagnostics = await compilation.WithAnalyzers([new ModernSyntaxValueAnalyzer()]).GetAnalyzerDiagnosticsAsync(CancellationToken.None);
        if (expectedDiagnosticId is null)
        {
            await Assert.That(diagnostics).IsEmpty();
            return;
        }

        await Assert.That(diagnostics).Count().IsEqualTo(1);
        await Assert.That(diagnostics[0].Id).IsEqualTo(expectedDiagnosticId);
    }

    /// <summary>Changes the parse version without changing compilation references.</summary>
    /// <param name="test">The verifier test.</param>
    /// <param name="version">The language version to parse.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void SetLanguageVersion(CSharpCodeFixVerifier<ModernSyntaxValueAnalyzer, ModernSyntaxValueCodeFixProvider>.Test test, LanguageVersion version) =>
        test.SolutionTransforms.Add((solution, projectId) =>
        {
            var options = (CSharpParseOptions)solution.GetProject(projectId)!.ParseOptions!;
            return solution.WithProjectParseOptions(projectId, options.WithLanguageVersion(version));
        });
}
