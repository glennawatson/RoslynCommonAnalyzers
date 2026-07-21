// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace SecuritySharp.Analyzers;

/// <summary>
/// Flags a secret-shaped string literal in a WebAssembly-reachable compilation (SES1707). A Blazor WebAssembly
/// assembly is downloaded to and runs in the browser, so any string it holds is readable on the client; a
/// credential that is safe in server-only code is a guaranteed disclosure here. The rule reuses the SES1201
/// credential classifier (<see cref="HardcodedSecretClassifier.Classify"/>) for the secret shape, then confirms
/// WebAssembly reachability: the compilation is a standalone WebAssembly host (or a Blazor Web App client
/// project), whose whole assembly is downloaded, or the literal's enclosing component type carries an Interactive
/// WebAssembly or Interactive Auto render-mode attribute. The WebAssembly render-mode marker
/// (<c>Microsoft.AspNetCore.Components.Web.InteractiveWebAssemblyRenderMode</c>) and the host builder
/// (<c>Microsoft.AspNetCore.Components.WebAssembly.Hosting.WebAssemblyHostBuilder</c>) are probed once per
/// compilation and gate the rule, so a project with neither pays nothing. The classifier keeps the no-diagnostic
/// path allocation-free, and the reachability check runs only after a secret shape is found.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Ses1707WebAssemblySecretDisclosureAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The metadata name of the Interactive WebAssembly render-mode marker gating the rule.</summary>
    private const string WebAssemblyRenderModeMetadataName = "Microsoft.AspNetCore.Components.Web.InteractiveWebAssemblyRenderMode";

    /// <summary>The metadata name of the standalone WebAssembly host builder, whose whole assembly downloads to the browser.</summary>
    private const string WebAssemblyHostBuilderMetadataName = "Microsoft.AspNetCore.Components.WebAssembly.Hosting.WebAssemblyHostBuilder";

    /// <summary>The metadata name of the base render-mode attribute a component's fixed render mode derives from.</summary>
    private const string RenderModeAttributeMetadataName = "Microsoft.AspNetCore.Components.RenderModeAttribute";

    /// <summary>The identifier spellings that mark a render-mode attribute as WebAssembly- or Auto-hosted (both download to the browser).</summary>
    private static readonly string[] WebAssemblyRenderModeMarkers =
    [
        "InteractiveWebAssembly",
        "InteractiveAuto",
        "InteractiveWebAssemblyRenderMode",
        "InteractiveAutoRenderMode",
    ];

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(SecurityRules.WebAssemblyHardcodedSecret);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(start =>
        {
            var compilation = start.Compilation;
            var hostBuilder = compilation.GetTypeByMetadataName(WebAssemblyHostBuilderMetadataName);
            var webAssemblyRenderMode = compilation.GetTypeByMetadataName(WebAssemblyRenderModeMetadataName);

            // Gate on the WebAssembly render-mode marker (or a standalone WebAssembly host): a project that
            // references neither ships nothing to the browser and pays no analysis cost.
            if (webAssemblyRenderMode is null && hostBuilder is null)
            {
                return;
            }

            var wholeAssemblyDownloads = hostBuilder is not null;
            var renderModeAttribute = compilation.GetTypeByMetadataName(RenderModeAttributeMetadataName);

            start.RegisterSyntaxNodeAction(
                nodeContext => AnalyzeStringLiteral(nodeContext, wholeAssemblyDownloads, renderModeAttribute),
                SyntaxKind.StringLiteralExpression);
        });
    }

    /// <summary>Reports SES1707 for a secret-shaped literal that is reachable from the browser.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="wholeAssemblyDownloads">Whether the compilation is a WebAssembly host whose whole assembly downloads to the browser.</param>
    /// <param name="renderModeAttribute">The base render-mode attribute type, or <see langword="null"/> when absent.</param>
    private static void AnalyzeStringLiteral(SyntaxNodeAnalysisContext context, bool wholeAssemblyDownloads, INamedTypeSymbol? renderModeAttribute)
    {
        var literal = (LiteralExpressionSyntax)context.Node;

        // Cheap, allocation-free screen first: only a recognised secret shape is worth the reachability check.
        if (HardcodedSecretClassifier.Classify(literal.Token.ValueText) is not { } kind)
        {
            return;
        }

        if (!IsWebAssemblyReachable(literal, context, wholeAssemblyDownloads, renderModeAttribute))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(SecurityRules.WebAssemblyHardcodedSecret, literal.GetLocation(), kind));
    }

    /// <summary>Returns whether a literal downloads to the browser: a WebAssembly host, or a client-rendered component.</summary>
    /// <param name="literal">The secret-shaped literal.</param>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="wholeAssemblyDownloads">Whether the whole compilation downloads to the browser.</param>
    /// <param name="renderModeAttribute">The base render-mode attribute type, or <see langword="null"/> when absent.</param>
    /// <returns><see langword="true"/> when the literal is reachable from the browser.</returns>
    private static bool IsWebAssemblyReachable(
        SyntaxNode literal,
        SyntaxNodeAnalysisContext context,
        bool wholeAssemblyDownloads,
        INamedTypeSymbol? renderModeAttribute)
    {
        // A standalone WebAssembly host (or a Blazor Web App client project) ships its whole assembly to the browser.
        if (wholeAssemblyDownloads)
        {
            return true;
        }

        if (renderModeAttribute is null)
        {
            return false;
        }

        // Otherwise the literal is disclosed only when its enclosing component type is rendered on the client.
        for (var node = literal.Parent; node is not null; node = node.Parent)
        {
            if (node is TypeDeclarationSyntax typeDeclaration
                && context.SemanticModel.GetDeclaredSymbol(typeDeclaration, context.CancellationToken) is { } typeSymbol
                && CarriesWebAssemblyRenderMode(typeSymbol, renderModeAttribute, context.CancellationToken))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether a type carries a render-mode attribute that selects WebAssembly or Auto hosting.</summary>
    /// <param name="type">The enclosing type to inspect.</param>
    /// <param name="renderModeAttribute">The base render-mode attribute type.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="true"/> when a client-hosted render-mode attribute is applied.</returns>
    private static bool CarriesWebAssemblyRenderMode(INamedTypeSymbol type, INamedTypeSymbol renderModeAttribute, CancellationToken cancellationToken)
    {
        var attributes = type.GetAttributes();
        for (var i = 0; i < attributes.Length; i++)
        {
            var attributeClass = attributes[i].AttributeClass;
            if (attributeClass is not null
                && DerivesFrom(attributeClass, renderModeAttribute)
                && RenderModeSelectsWebAssembly(attributeClass, cancellationToken))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether a type derives from (or is) a given base type.</summary>
    /// <param name="type">The candidate type.</param>
    /// <param name="baseType">The base type to look for.</param>
    /// <returns><see langword="true"/> when <paramref name="baseType"/> is in the type's base chain.</returns>
    private static bool DerivesFrom(INamedTypeSymbol type, INamedTypeSymbol baseType)
    {
        for (INamedTypeSymbol? current = type; current is not null; current = current.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(current, baseType))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether a render-mode attribute's source selects the WebAssembly or Auto render mode.</summary>
    /// <param name="attributeClass">The render-mode attribute type.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="true"/> when the attribute's mode is Interactive WebAssembly or Interactive Auto.</returns>
    private static bool RenderModeSelectsWebAssembly(INamedTypeSymbol attributeClass, CancellationToken cancellationToken)
    {
        // A render-mode attribute names its mode in its own source. The compiler-generated
        // '__PrivateComponentRenderModeAttribute' emitted for '@rendermode InteractiveWebAssembly' returns
        // 'InteractiveWebAssembly' from its 'Mode' member; a hand-written attribute constructs an
        // 'InteractiveWebAssemblyRenderMode'/'InteractiveAutoRenderMode'. Reading that source tells a client mode
        // (WebAssembly/Auto) apart from a server-only mode. This runs only after a secret shape is found and only
        // over a small attribute declaration, so a token scan here is off the hot path; when no source is available
        // the mode cannot be proven and nothing is reported.
        var references = attributeClass.DeclaringSyntaxReferences;
        for (var i = 0; i < references.Length; i++)
        {
            foreach (var token in references[i].GetSyntax(cancellationToken).DescendantTokens())
            {
                if (token.IsKind(SyntaxKind.IdentifierToken) && IsWebAssemblyRenderModeMarker(token.ValueText))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>Returns whether an identifier is one of the WebAssembly/Auto render-mode marker names.</summary>
    /// <param name="identifier">The identifier text to test.</param>
    /// <returns><see langword="true"/> for a WebAssembly or Auto render-mode marker.</returns>
    private static bool IsWebAssemblyRenderModeMarker(string identifier)
    {
        for (var i = 0; i < WebAssemblyRenderModeMarkers.Length; i++)
        {
            if (string.Equals(identifier, WebAssemblyRenderModeMarkers[i], StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
