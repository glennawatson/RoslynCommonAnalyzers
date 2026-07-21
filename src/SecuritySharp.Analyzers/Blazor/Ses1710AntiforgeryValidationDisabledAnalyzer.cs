// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace SecuritySharp.Analyzers;

/// <summary>
/// Flags <c>[RequireAntiforgeryToken(required: false)]</c> (SES1710). Antiforgery validation checks that a
/// state-changing form post carries a token issued to the same user, defeating cross-site request forgery; a form
/// or component is protected by default, and passing <c>required: false</c> turns that check off. The rule reports
/// an application of <c>Microsoft.AspNetCore.Antiforgery.RequireAntiforgeryTokenAttribute</c> -- on a component or
/// a method -- whose <c>required</c> argument is the constant <c>false</c>, in either the named
/// (<c>required: false</c>) or positional (<c>false</c>) form. A syntactic name screen runs before the attribute is
/// bound, and the constructor's containing type is confirmed against the gated attribute. The attribute type is
/// probed once per compilation and gates the rule, so a project without antiforgery pays nothing.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Ses1710AntiforgeryValidationDisabledAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The short attribute-name spelling used by the syntactic prefilter.</summary>
    private const string AttributeShortName = "RequireAntiforgeryToken";

    /// <summary>The long attribute-name spelling used by the syntactic prefilter.</summary>
    private const string AttributeLongName = "RequireAntiforgeryTokenAttribute";

    /// <summary>The name of the constructor parameter that, when false, disables antiforgery validation.</summary>
    private const string RequiredParameterName = "required";

    /// <summary>The metadata name of the attribute whose <c>required: false</c> application disables validation.</summary>
    private const string RequireAntiforgeryTokenAttributeMetadataName = "Microsoft.AspNetCore.Antiforgery.RequireAntiforgeryTokenAttribute";

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(SecurityRules.AntiforgeryValidationDisabled);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(start =>
        {
            if (start.Compilation.GetTypeByMetadataName(RequireAntiforgeryTokenAttributeMetadataName) is not { } attributeType)
            {
                return;
            }

            start.RegisterSyntaxNodeAction(nodeContext => AnalyzeAttribute(nodeContext, attributeType), SyntaxKind.Attribute);
        });
    }

    /// <summary>Reports SES1710 for a <c>[RequireAntiforgeryToken(required: false)]</c> application.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="attributeType">The gated <c>RequireAntiforgeryTokenAttribute</c> type.</param>
    private static void AnalyzeAttribute(SyntaxNodeAnalysisContext context, INamedTypeSymbol attributeType)
    {
        var attribute = (AttributeSyntax)context.Node;

        // Syntactic prefilter: the attribute is spelled 'RequireAntiforgeryToken' or 'RequireAntiforgeryTokenAttribute'
        // and carries at least one argument (with none, 'required' keeps its protective default and is not reported).
        if (attribute.ArgumentList is not { Arguments.Count: > 0 } argumentList
            || GetAttributeSimpleName(attribute.Name) is not (AttributeShortName or AttributeLongName))
        {
            return;
        }

        if (context.SemanticModel.GetSymbolInfo(attribute, context.CancellationToken).Symbol is not IMethodSymbol constructor
            || !SymbolEqualityComparer.Default.Equals(constructor.ContainingType, attributeType)
            || !RequiredArgumentIsFalse(argumentList, constructor, context.SemanticModel, context.CancellationToken))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            SecurityRules.AntiforgeryValidationDisabled,
            attribute.SyntaxTree,
            attribute.Span));
    }

    /// <summary>Returns whether the attribute's <c>required</c> constructor argument is the constant <c>false</c>.</summary>
    /// <param name="argumentList">The attribute's argument list.</param>
    /// <param name="constructor">The bound attribute constructor.</param>
    /// <param name="semanticModel">The semantic model used to read constant values.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="true"/> when the <c>required</c> argument evaluates to <c>false</c>.</returns>
    private static bool RequiredArgumentIsFalse(
        AttributeArgumentListSyntax argumentList,
        IMethodSymbol constructor,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        var arguments = argumentList.Arguments;
        var positionalIndex = 0;
        for (var i = 0; i < arguments.Count; i++)
        {
            var argument = arguments[i];

            // A property/field initializer ('Name = ...') is not a constructor argument and never carries 'required'.
            if (argument.NameEquals is not null)
            {
                continue;
            }

            string? parameterName;
            if (argument.NameColon is { } nameColon)
            {
                parameterName = nameColon.Name.Identifier.ValueText;
            }
            else
            {
                parameterName = positionalIndex < constructor.Parameters.Length ? constructor.Parameters[positionalIndex].Name : null;
                positionalIndex++;
            }

            if (string.Equals(parameterName, RequiredParameterName, StringComparison.Ordinal)
                && semanticModel.GetConstantValue(argument.Expression, cancellationToken) is { HasValue: true, Value: false })
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns the simple identifier text of an attribute name, ignoring any qualifier or alias.</summary>
    /// <param name="name">The attribute's name syntax.</param>
    /// <returns>The rightmost simple name, or <see langword="null"/> when it cannot be read syntactically.</returns>
    private static string? GetAttributeSimpleName(NameSyntax name)
        => name switch
        {
            SimpleNameSyntax simple => simple.Identifier.ValueText,
            QualifiedNameSyntax qualified => qualified.Right.Identifier.ValueText,
            AliasQualifiedNameSyntax alias => alias.Name.Identifier.ValueText,
            _ => null,
        };
}
