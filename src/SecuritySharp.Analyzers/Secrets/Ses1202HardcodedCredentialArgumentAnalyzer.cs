// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Operations;

namespace SecuritySharp.Analyzers;

/// <summary>
/// Flags a hard-coded credential value written directly into source (SES1202). The rule reports a
/// non-empty string literal in a position that expects a credential in one of two shapes: an argument
/// bound to a parameter named like a credential (<c>apiKey</c>, <c>password</c>, <c>secret</c>,
/// <c>token</c>, <c>connectionString</c>, <c>accessKey</c>, <c>privateKey</c>, and their underscore
/// spellings -- matched case-insensitively, and needing no type gate); or the secret position of a
/// known credential-type constructor -- <c>System.Net.NetworkCredential</c>,
/// <c>Azure.AzureKeyCredential</c>/<c>Azure.Core.AzureKeyCredential</c>,
/// <c>Azure.Identity.ClientSecretCredential</c>, and <c>System.ClientModel.ApiKeyCredential</c> -- where
/// a generic <c>key</c> parameter is treated as a secret only inside those gated types. It complements
/// the pattern-based secret rule by catching a hard-coded credential whose text is not a recognizable
/// secret shape. Only a genuine string literal is reported; a variable, constant reference, configuration
/// lookup, or environment read is the correct way to supply a secret and is never reported. Obvious
/// placeholders (an empty string, a <c>your-...</c> or <c>&lt;...&gt;</c> template, <c>changeme</c> and a
/// small curated set, or an all-same-character mask) are skipped. The clean path binds nothing: a
/// syntactic screen requires a reportable string-literal argument before any symbol resolution runs.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Ses1202HardcodedCredentialArgumentAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The generic secret-position parameter name treated as a credential only inside a gated credential type.</summary>
    private const string GatedSecretParameterName = "key";

    /// <summary>The metadata names of the credential types whose constructor secret position is guarded.</summary>
    private static readonly string[] CredentialTypeMetadataNames =
    [
        "System.Net.NetworkCredential",
        "Azure.AzureKeyCredential",
        "Azure.Core.AzureKeyCredential",
        "Azure.Identity.ClientSecretCredential",
        "System.ClientModel.ApiKeyCredential"
    ];

    /// <summary>Parameter names that mark a credential position on any method or constructor (case-insensitive).</summary>
    private static readonly HashSet<string> CredentialParameterNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "apikey", "api_key",
        "password", "passwd", "pwd",
        "secret", "clientsecret", "client_secret",
        "token", "accesstoken", "access_token", "sastoken", "sas_token",
        "connectionstring", "connection_string",
        "accesskey", "access_key", "accountkey", "account_key",
        "privatekey", "private_key"
    };

    /// <summary>Exact literal values treated as obvious placeholders rather than real secrets (case-insensitive).</summary>
    private static readonly HashSet<string> PlaceholderValues = new(StringComparer.OrdinalIgnoreCase)
    {
        "changeme", "change-me", "changeit",
        "placeholder", "example", "sample", "dummy",
        "test", "todo", "tbd", "none", "null", "n/a"
    };

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(SecurityRules.HardcodedCredentialArgument);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(start =>
        {
            // The parameter-name shape needs no gate; the constructor shape only widens 'key' to a secret
            // for the resolved credential types, so an absent set simply narrows detection to named params.
            var credentialTypes = GetCredentialTypes(start.Compilation);

            start.RegisterSyntaxNodeAction(
                nodeContext => AnalyzeCall(nodeContext, credentialTypes),
                SyntaxKind.InvocationExpression,
                SyntaxKind.ObjectCreationExpression,
                SyntaxKind.ImplicitObjectCreationExpression);
        });
    }

    /// <summary>Reports SES1202 for each string-literal argument bound to a credential position.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="credentialTypes">The resolved credential types whose constructor <c>key</c> position is guarded, or <see langword="null"/> when none resolve.</param>
    private static void AnalyzeCall(SyntaxNodeAnalysisContext context, INamedTypeSymbol?[]? credentialTypes)
    {
        // Syntactic prefilter: bind nothing unless the call carries a reportable string-literal argument.
        if (GetArgumentList(context.Node) is not { Arguments: { Count: > 0 } arguments }
            || !HasReportableLiteral(arguments))
        {
            return;
        }

        var (methodArguments, containingType) = GetBoundCall(context.SemanticModel, context.Node, context.CancellationToken);
        if (methodArguments.IsDefaultOrEmpty || containingType is null)
        {
            return;
        }

        var isCredentialType = IsGatedCredentialType(containingType, credentialTypes);
        for (var i = 0; i < methodArguments.Length; i++)
        {
            if (TryGetCredentialLiteral(methodArguments[i], isCredentialType, out var literalSyntax, out var parameterName))
            {
                context.ReportDiagnostic(DiagnosticHelper.Create(
                    SecurityRules.HardcodedCredentialArgument,
                    literalSyntax.SyntaxTree,
                    literalSyntax.Span,
                    parameterName));
            }
        }
    }

    /// <summary>Returns whether an argument is a reportable credential string literal, yielding its syntax and parameter name.</summary>
    /// <param name="argument">The bound argument operation.</param>
    /// <param name="isCredentialType">Whether the containing type is a gated credential type.</param>
    /// <param name="literalSyntax">The reported literal syntax when the argument is reportable.</param>
    /// <param name="parameterName">The bound credential parameter name when the argument is reportable.</param>
    /// <returns><see langword="true"/> when the argument is a hard-coded credential literal.</returns>
    private static bool TryGetCredentialLiteral(
        IArgumentOperation argument,
        bool isCredentialType,
        [NotNullWhen(true)] out LiteralExpressionSyntax? literalSyntax,
        [NotNullWhen(true)] out string? parameterName)
    {
        literalSyntax = null;
        parameterName = null;

        if (argument.ArgumentKind != ArgumentKind.Explicit
            || argument.Parameter is not { } parameter
            || argument.Value is not ILiteralOperation { ConstantValue.Value: string value }
            || argument.Value.Syntax is not LiteralExpressionSyntax syntax
            || IsPlaceholderOrEmpty(value)
            || !IsCredentialParameter(parameter.Name, isCredentialType))
        {
            return false;
        }

        literalSyntax = syntax;
        parameterName = parameter.Name;
        return true;
    }

    /// <summary>Returns the argument list of a call/creation node, or <see langword="null"/> when it has none.</summary>
    /// <param name="node">The invocation or object-creation node.</param>
    /// <returns>The argument list, or <see langword="null"/>.</returns>
    private static ArgumentListSyntax? GetArgumentList(SyntaxNode node)
        => node switch
        {
            InvocationExpressionSyntax invocation => invocation.ArgumentList,
            ObjectCreationExpressionSyntax objectCreation => objectCreation.ArgumentList,
            ImplicitObjectCreationExpressionSyntax implicitCreation => implicitCreation.ArgumentList,
            _ => null,
        };

    /// <summary>Returns whether any argument is a non-empty, non-placeholder string literal.</summary>
    /// <param name="arguments">The syntactic argument list.</param>
    /// <returns><see langword="true"/> when at least one argument is a reportable string literal.</returns>
    private static bool HasReportableLiteral(SeparatedSyntaxList<ArgumentSyntax> arguments)
    {
        for (var i = 0; i < arguments.Count; i++)
        {
            if (arguments[i].Expression is LiteralExpressionSyntax literal
                && literal.IsKind(SyntaxKind.StringLiteralExpression)
                && !IsPlaceholderOrEmpty(literal.Token.ValueText))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Binds a call/creation node to its argument operations and containing type.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="node">The invocation or object-creation node.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The bound argument operations and the target's containing type, or defaults when it does not bind.</returns>
    private static (ImmutableArray<IArgumentOperation> Arguments, INamedTypeSymbol? ContainingType) GetBoundCall(
        SemanticModel model,
        SyntaxNode node,
        CancellationToken cancellationToken)
        => model.GetOperation(node, cancellationToken) switch
        {
            IInvocationOperation invocation => (invocation.Arguments, invocation.TargetMethod.ContainingType),
            IObjectCreationOperation { Constructor: { } constructor } creation => (creation.Arguments, constructor.ContainingType),
            _ => (default, null),
        };

    /// <summary>Returns whether a parameter name marks a credential position.</summary>
    /// <param name="parameterName">The bound parameter name.</param>
    /// <param name="isCredentialType">Whether the containing type is a gated credential type.</param>
    /// <returns><see langword="true"/> when the parameter is a credential position.</returns>
    private static bool IsCredentialParameter(string parameterName, bool isCredentialType)
        => CredentialParameterNames.Contains(parameterName)
            || (isCredentialType && string.Equals(parameterName, GatedSecretParameterName, StringComparison.OrdinalIgnoreCase));

    /// <summary>Returns whether a literal value is empty or an obvious placeholder.</summary>
    /// <param name="value">The decoded literal value.</param>
    /// <returns><see langword="true"/> when the value should not be treated as a real secret.</returns>
    private static bool IsPlaceholderOrEmpty(string value)
        => value.Length == 0
            || IsAllSameCharacter(value)
            || value[0] == '<'
            || value.StartsWith("your", StringComparison.OrdinalIgnoreCase)
            || PlaceholderValues.Contains(value);

    /// <summary>Returns whether every character of a value is identical (an all-same-character mask).</summary>
    /// <param name="value">The value to test.</param>
    /// <returns><see langword="true"/> when all characters are the same.</returns>
    private static bool IsAllSameCharacter(string value)
    {
        var first = value[0];
        for (var i = 1; i < value.Length; i++)
        {
            if (value[i] != first)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Returns whether a containing type is one of the resolved gated credential types.</summary>
    /// <param name="containingType">The bound constructor's containing type.</param>
    /// <param name="credentialTypes">The resolved credential types, or <see langword="null"/> when none resolve.</param>
    /// <returns><see langword="true"/> when the type is a gated credential type.</returns>
    private static bool IsGatedCredentialType(INamedTypeSymbol containingType, INamedTypeSymbol?[]? credentialTypes)
    {
        if (credentialTypes is null)
        {
            return false;
        }

        for (var i = 0; i < credentialTypes.Length; i++)
        {
            if (credentialTypes[i] is { } credentialType && SymbolEqualityComparer.Default.Equals(credentialType, containingType))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Resolves the credential types present in the compilation.</summary>
    /// <param name="compilation">The compilation to probe.</param>
    /// <returns>An array whose slots hold each resolved credential type, or <see langword="null"/> when none resolve.</returns>
    private static INamedTypeSymbol?[]? GetCredentialTypes(Compilation compilation)
    {
        INamedTypeSymbol?[]? types = null;
        for (var i = 0; i < CredentialTypeMetadataNames.Length; i++)
        {
            if (compilation.GetTypeByMetadataName(CredentialTypeMetadataNames[i]) is { } type)
            {
                types ??= new INamedTypeSymbol?[CredentialTypeMetadataNames.Length];
                types[i] = type;
            }
        }

        return types;
    }
}
