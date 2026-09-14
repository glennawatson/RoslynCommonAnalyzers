// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.Composition.Hosting;
using System.Globalization;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests date-format fixes with stale diagnostic properties and alternate provider syntax.</summary>
public class Sst2445CultureSensitiveDateFormatCodeFixProviderTests
{
    /// <summary>The source document name.</summary>
    private const string FileName = "Test.cs";

    /// <summary>The two alternative fixes for an invocation with a supported provider.</summary>
    private const int InvocationFixCount = 2;

    /// <summary>The diagnostic property identifying a format's syntax shape.</summary>
    private const string ShapeKey = "SST2445.Shape";

    /// <summary>The diagnostic property locating the culture provider.</summary>
    private const string ProviderSpanKey = "SST2445.ProviderSpan";

    /// <summary>Checks unknown shapes and malformed provider locations never register an unsafe edit.</summary>
    /// <param name="shape">The diagnostic shape, or null.</param>
    /// <param name="span">The serialized provider span, or null.</param>
    /// <param name="includeProperties">Whether the diagnostic contains the properties.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments(null, null, false)]
    [Arguments(null, null, true)]
    [Arguments("Unknown", "0:1", true)]
    [Arguments("Interpolation", "0:1", true)]
    [Arguments("Invocation", null, false)]
    [Arguments("Invocation", null, true)]
    [Arguments("Invocation", "invalid", true)]
    [Arguments("Invocation", ":1", true)]
    [Arguments("Invocation", "0:", true)]
    [Arguments("Invocation", "x:1", true)]
    [Arguments("Invocation", "0:x", true)]
    [Arguments("Invocation", "2147483648:1", true)]
    [Arguments("Invocation", "0:2147483648", true)]
    [Arguments("Invocation", "0:5", true)]
    public async Task InvalidPropertiesDoNotRegisterFixesAsync(string? shape, string? span, bool includeProperties)
    {
        using var workspace = new AdhocWorkspace();
        var document = workspace.AddProject("DateProperties", LanguageNames.CSharp).AddDocument(FileName, "class C { int M() => 42; }");
        var root = (await document.GetSyntaxRootAsync())!;
        var properties = shape is not null || includeProperties
            ? ImmutableDictionary<string, string?>.Empty.Add(ShapeKey, shape)
            : ImmutableDictionary<string, string?>.Empty;
        if (includeProperties)
        {
            properties = properties.Add(ProviderSpanKey, span);
        }

        var literal = root.DescendantNodes().OfType<LiteralExpressionSyntax>().Single();
        var diagnostic = Diagnostic.Create(CorrectnessRules.CultureSensitiveDateFormat, literal.GetLocation(), properties);
        using var container = new ContainerConfiguration().WithPart<Sst2445CultureSensitiveDateFormatCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        var actions = new List<CodeAction>();
        await provider.RegisterCodeFixesAsync(new(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        await Assert.That(actions).IsEmpty();
    }

    /// <summary>Checks non-name provider expressions retain the quote fix without an invariant replacement.</summary>
    /// <param name="providerExpression">The unsupported culture-provider expression.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("GetCulture()")]
    [Arguments("(CurrentCulture)")]
    [Arguments("null")]
    public async Task UnsupportedProviderOnlyOffersSeparatorQuotingAsync(string providerExpression)
    {
        using var workspace = new AdhocWorkspace();
        var document = workspace.AddProject("DateProvider", LanguageNames.CSharp)
            .AddDocument(FileName, $"class C {{ object M() => Format(\"dd/MM\", {providerExpression}); }}");
        var root = (await document.GetSyntaxRootAsync())!;
        var invocation = root.DescendantNodes().OfType<InvocationExpressionSyntax>().First();
        var format = invocation.ArgumentList.Arguments[0].Expression;
        var providerExpressionNode = invocation.ArgumentList.Arguments[1].Expression;
        var properties = ImmutableDictionary<string, string?>.Empty.Add(ShapeKey, "Invocation")
            .Add(ProviderSpanKey, FormattableString.Invariant($"{providerExpressionNode.SpanStart}:{providerExpressionNode.Span.Length}"));
        var diagnostic = Diagnostic.Create(CorrectnessRules.CultureSensitiveDateFormat, format.GetLocation(), properties);
        using var container = new ContainerConfiguration().WithPart<Sst2445CultureSensitiveDateFormatCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        var actions = new List<CodeAction>();
        await provider.RegisterCodeFixesAsync(new(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        await Assert.That(actions.Count).IsEqualTo(1);
        await Assert.That(actions[0].EquivalenceKey).IsEqualTo("SST2445.Quote");
        var operations = await actions[0].GetOperationsAsync(CancellationToken.None);
        var changed = operations.OfType<ApplyChangesOperation>().Single().ChangedSolution.GetDocument(document.Id)!;
        var changedRoot = (await changed.GetSyntaxRootAsync())!;
        var changedInvocation = changedRoot.DescendantNodes().OfType<InvocationExpressionSyntax>().First();
        await Assert.That(((LiteralExpressionSyntax)changedInvocation.ArgumentList.Arguments[0].Expression).Token.ValueText).IsEqualTo("dd'/'MM");
        await Assert.That(changedInvocation.ArgumentList.Arguments[1].Expression.ToString()).IsEqualTo(providerExpression);
    }

    /// <summary>Checks imported provider names and signed span components preserve the invariant replacement.</summary>
    /// <param name="positiveSign">The current culture's positive sign.</param>
    /// <param name="signedComponents">Whether to serialize whitespace and explicit positive signs.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("+", false)]
    [Arguments("+", true)]
    [Arguments("p", true)]
    public async Task ImportedProviderAndCultureSpecificSpansAreRewrittenAsync(string positiveSign, bool signedComponents)
    {
        var previousCulture = CultureInfo.CurrentCulture;
        var culture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
        culture.NumberFormat.PositiveSign = positiveSign;
        CultureInfo.CurrentCulture = culture;
        try
        {
            using var workspace = new AdhocWorkspace();
            var document = workspace.AddProject("ImportedCulture", LanguageNames.CSharp)
                .AddDocument(FileName, "using static System.Globalization.CultureInfo; class C { string M(System.DateTime value) => value.ToString(\"dd/MM\", /*keep*/ CurrentCulture); }");
            var root = (await document.GetSyntaxRootAsync())!;
            var invocation = root.DescendantNodes().OfType<InvocationExpressionSyntax>().Single();
            var format = invocation.ArgumentList.Arguments[0].Expression;
            var providerExpression = invocation.ArgumentList.Arguments[1].Expression;
            var span = signedComponents
                ? FormattableString.Invariant($" {positiveSign}{providerExpression.SpanStart} : {positiveSign}{providerExpression.Span.Length} ")
                : FormattableString.Invariant($"{providerExpression.SpanStart}:{providerExpression.Span.Length}");
            var properties = ImmutableDictionary<string, string?>.Empty.Add(ShapeKey, "Invocation").Add(ProviderSpanKey, span);
            var diagnostic = Diagnostic.Create(CorrectnessRules.CultureSensitiveDateFormat, format.GetLocation(), properties);
            using var container = new ContainerConfiguration().WithPart<Sst2445CultureSensitiveDateFormatCodeFixProvider>().CreateContainer();
            var provider = container.GetExport<CodeFixProvider>();
            var actions = new List<CodeAction>();
            await provider.RegisterCodeFixesAsync(new(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
            await Assert.That(actions.Count).IsEqualTo(InvocationFixCount);
            var action = actions.Single(static action => action.EquivalenceKey == "SST2445.Invariant");
            var operations = await action.GetOperationsAsync(CancellationToken.None);
            var changed = operations.OfType<ApplyChangesOperation>().Single().ChangedSolution.GetDocument(document.Id)!;
            await Assert.That((await changed.GetTextAsync()).ToString()).Contains("/*keep*/ InvariantCulture");
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
        }
    }
}
