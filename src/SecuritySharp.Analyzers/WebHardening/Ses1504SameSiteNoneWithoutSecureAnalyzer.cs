// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace SecuritySharp.Analyzers;

/// <summary>
/// Flags a cookie object initializer that sets <c>SameSite = SameSiteMode.None</c> without also securing
/// the cookie in that same initializer (SES1504). The rule reports the <c>SameSite = None</c> member of a
/// <c>new Microsoft.AspNetCore.Http.CookieOptions { ... }</c> or
/// <c>new Microsoft.AspNetCore.Http.CookieBuilder { ... }</c> initializer when no sibling member marks the
/// cookie secure: <c>Secure = true</c> on <c>CookieOptions</c>, or a <c>SecurePolicy</c> other than
/// <c>CookieSecurePolicy.None</c> on <c>CookieBuilder</c> (the two types expose the Secure attribute through
/// different members). A browser rejects a <c>SameSite=None</c> cookie that lacks the Secure attribute, and
/// without it the cookie is also sent over plain HTTP. Detection is local to a single object initializer: a
/// <c>Secure</c> flag set on a later statement is not tracked (no data-flow), so that form is intentionally not
/// reported. The cookie types are probed once per compilation; a project without ASP.NET Core registers
/// nothing and pays nothing.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Ses1504SameSiteNoneWithoutSecureAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The initializer member whose <c>None</c> value is guarded.</summary>
    private const string SameSiteMemberName = "SameSite";

    /// <summary>The <c>CookieOptions</c> member that marks the cookie Secure.</summary>
    private const string SecureMemberName = "Secure";

    /// <summary>The <c>CookieBuilder</c> member that governs the Secure attribute.</summary>
    private const string SecurePolicyMemberName = "SecurePolicy";

    /// <summary>The enum field name that denotes the unsecured value on both relevant enums.</summary>
    private const string NoneFieldName = "None";

    /// <summary>The metadata name of the <c>CookieOptions</c> type.</summary>
    private const string CookieOptionsMetadataName = "Microsoft.AspNetCore.Http.CookieOptions";

    /// <summary>The metadata name of the <c>CookieBuilder</c> type.</summary>
    private const string CookieBuilderMetadataName = "Microsoft.AspNetCore.Http.CookieBuilder";

    /// <summary>The metadata name of the <c>SameSiteMode</c> enum.</summary>
    private const string SameSiteModeMetadataName = "Microsoft.AspNetCore.Http.SameSiteMode";

    /// <summary>The metadata name of the <c>CookieSecurePolicy</c> enum.</summary>
    private const string CookieSecurePolicyMetadataName = "Microsoft.AspNetCore.Http.CookieSecurePolicy";

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(SecurityRules.SameSiteNoneWithoutSecure);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(start =>
        {
            var cookieTypes = GetCookieTypes(start.Compilation);
            if (cookieTypes is not { } types)
            {
                return;
            }

            start.RegisterSyntaxNodeAction(
                nodeContext => AnalyzeObjectCreation(nodeContext, types),
                SyntaxKind.ObjectCreationExpression,
                SyntaxKind.ImplicitObjectCreationExpression);
        });
    }

    /// <summary>Reports SES1504 for a gated cookie initializer that sets <c>SameSite = None</c> without securing the cookie.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="types">The gated cookie types resolved for the compilation.</param>
    private static void AnalyzeObjectCreation(SyntaxNodeAnalysisContext context, CookieInitializerTypes types)
    {
        // Syntactic prefilter: an initializer that contains a 'SameSite = <...>.None' member. No semantic
        // model is touched until this cheap shape check passes, so the clean path stays allocation-free.
        if (GetInitializer(context.Node) is not { } initializer
            || GetSameSiteNoneMember(initializer) is not { } sameSiteMember)
        {
            return;
        }

        var createdType = GetGatedCookieType(context.SemanticModel, context.Node, types, context.CancellationToken, out var isCookieOptions);
        if (createdType is null
            || !IsSameSiteNoneAssignment(context.SemanticModel, sameSiteMember, types.SameSiteNone, context.CancellationToken)
            || HasSecuringSibling(context.SemanticModel, initializer, isCookieOptions, types, context.CancellationToken))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            SecurityRules.SameSiteNoneWithoutSecure,
            sameSiteMember.SyntaxTree,
            sameSiteMember.Span,
            createdType.Name));
    }

    /// <summary>Returns the created type when it is a gated cookie type, reporting whether it is <c>CookieOptions</c>.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="node">The object-creation node.</param>
    /// <param name="types">The gated cookie types resolved for the compilation.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <param name="isCookieOptions">Set to <see langword="true"/> when the created type is <c>CookieOptions</c>.</param>
    /// <returns>The gated cookie type, or <see langword="null"/> when the creation is not a gated type.</returns>
    private static INamedTypeSymbol? GetGatedCookieType(SemanticModel model, SyntaxNode node, CookieInitializerTypes types, CancellationToken cancellationToken, out bool isCookieOptions)
    {
        isCookieOptions = false;
        if (model.GetTypeInfo(node, cancellationToken).Type is not INamedTypeSymbol createdType)
        {
            return null;
        }

        if (types.CookieOptions is { } options && SymbolEqualityComparer.Default.Equals(options, createdType))
        {
            isCookieOptions = true;
            return createdType;
        }

        return types.CookieBuilder is { } builder && SymbolEqualityComparer.Default.Equals(builder, createdType) ? createdType : null;
    }

    /// <summary>Returns whether a <c>SameSite</c> initializer member binds to the property and is assigned <c>SameSiteMode.None</c>.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="member">The <c>SameSite = ...</c> assignment.</param>
    /// <param name="sameSiteNone">The resolved <c>SameSiteMode.None</c> field.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> when the member sets the cookie's <c>SameSite</c> to <c>None</c>.</returns>
    private static bool IsSameSiteNoneAssignment(SemanticModel model, AssignmentExpressionSyntax member, IFieldSymbol sameSiteNone, CancellationToken cancellationToken)
        => model.GetSymbolInfo(member.Left, cancellationToken).Symbol is IPropertySymbol { Name: SameSiteMemberName }
            && model.GetSymbolInfo(member.Right, cancellationToken).Symbol is IFieldSymbol assignedValue
            && SymbolEqualityComparer.Default.Equals(assignedValue, sameSiteNone);

    /// <summary>Returns the object initializer of an explicit or implicit object-creation node, if any.</summary>
    /// <param name="node">The object-creation node.</param>
    /// <returns>The initializer, or <see langword="null"/> when the creation has none.</returns>
    private static InitializerExpressionSyntax? GetInitializer(SyntaxNode node)
        => node switch
        {
            ObjectCreationExpressionSyntax objectCreation => objectCreation.Initializer,
            ImplicitObjectCreationExpressionSyntax implicitCreation => implicitCreation.Initializer,
            _ => null,
        };

    /// <summary>Returns the initializer member that syntactically assigns <c>SameSite</c> a <c>None</c>-named value.</summary>
    /// <param name="initializer">The object initializer to scan.</param>
    /// <returns>The <c>SameSite = ...None</c> assignment, or <see langword="null"/> when absent.</returns>
    private static AssignmentExpressionSyntax? GetSameSiteNoneMember(InitializerExpressionSyntax initializer)
    {
        var expressions = initializer.Expressions;
        for (var i = 0; i < expressions.Count; i++)
        {
            if (expressions[i] is AssignmentExpressionSyntax { Left: IdentifierNameSyntax { Identifier.ValueText: SameSiteMemberName } } assignment
                && GetTrailingName(assignment.Right) is NoneFieldName)
            {
                return assignment;
            }
        }

        return null;
    }

    /// <summary>Returns whether the initializer contains a sibling member that secures the cookie.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="initializer">The object initializer to scan.</param>
    /// <param name="isCookieOptions"><see langword="true"/> for <c>CookieOptions</c>, <see langword="false"/> for <c>CookieBuilder</c>.</param>
    /// <param name="types">The gated cookie types resolved for the compilation.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> when a securing member is present.</returns>
    private static bool HasSecuringSibling(SemanticModel model, InitializerExpressionSyntax initializer, bool isCookieOptions, CookieInitializerTypes types, CancellationToken cancellationToken)
    {
        var expressions = initializer.Expressions;
        for (var i = 0; i < expressions.Count; i++)
        {
            if (expressions[i] is not AssignmentExpressionSyntax { Left: IdentifierNameSyntax identifier } assignment)
            {
                continue;
            }

            var memberName = identifier.Identifier.ValueText;
            if (isCookieOptions)
            {
                if (memberName is SecureMemberName && IsSecuringSecureValue(model, assignment.Right, cancellationToken))
                {
                    return true;
                }
            }
            else if (memberName is SecurePolicyMemberName && IsSecuringPolicyValue(model, assignment.Right, types.SecurePolicyNone, cancellationToken))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether a <c>CookieOptions.Secure</c> value marks the cookie secure.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="value">The assigned value.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> unless the value is the constant <see langword="false"/>.</returns>
    private static bool IsSecuringSecureValue(SemanticModel model, ExpressionSyntax value, CancellationToken cancellationToken)
    {
        // 'Secure = true' secures the cookie; a non-constant value is treated as securing to avoid a false
        // positive on 'Secure = isProduction'. Only an explicit constant 'false' leaves the cookie unsecured.
        var constant = model.GetConstantValue(value, cancellationToken);
        return constant is not { HasValue: true, Value: false };
    }

    /// <summary>Returns whether a <c>CookieBuilder.SecurePolicy</c> value marks the cookie secure.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="value">The assigned value.</param>
    /// <param name="securePolicyNone">The resolved <c>CookieSecurePolicy.None</c> field, if any.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> unless the value binds to <c>CookieSecurePolicy.None</c>.</returns>
    private static bool IsSecuringPolicyValue(SemanticModel model, ExpressionSyntax value, IFieldSymbol? securePolicyNone, CancellationToken cancellationToken)
    {
        // 'Always' and 'SameAsRequest' emit the Secure attribute; only 'None' disables it. A non-'None' or
        // unresolved value is treated as securing so a developer who set 'SecurePolicy' is never second-guessed.
        if (securePolicyNone is null)
        {
            return true;
        }

        return model.GetSymbolInfo(value, cancellationToken).Symbol is not IFieldSymbol assigned
            || !SymbolEqualityComparer.Default.Equals(assigned, securePolicyNone);
    }

    /// <summary>Returns the trailing simple name of a member access or identifier expression.</summary>
    /// <param name="expression">The value expression to read.</param>
    /// <returns>The trailing name, or <see langword="null"/> when it cannot be read syntactically.</returns>
    private static string? GetTrailingName(ExpressionSyntax expression)
        => expression switch
        {
            MemberAccessExpressionSyntax memberAccess => memberAccess.Name.Identifier.ValueText,
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
            _ => null,
        };

    /// <summary>Resolves the cookie types and enum fields the rule gates on.</summary>
    /// <param name="compilation">The compilation to probe.</param>
    /// <returns>The resolved types, or <see langword="null"/> when the rule cannot apply.</returns>
    private static CookieInitializerTypes? GetCookieTypes(Compilation compilation)
    {
        if (compilation.GetTypeByMetadataName(SameSiteModeMetadataName) is not { } sameSiteMode
            || GetEnumField(sameSiteMode, NoneFieldName) is not { } sameSiteNone)
        {
            return null;
        }

        var cookieOptions = compilation.GetTypeByMetadataName(CookieOptionsMetadataName);
        var cookieBuilder = compilation.GetTypeByMetadataName(CookieBuilderMetadataName);
        if (cookieOptions is null && cookieBuilder is null)
        {
            return null;
        }

        var securePolicyNone = compilation.GetTypeByMetadataName(CookieSecurePolicyMetadataName) is { } securePolicy
            ? GetEnumField(securePolicy, NoneFieldName)
            : null;

        return new CookieInitializerTypes(cookieOptions, cookieBuilder, sameSiteNone, securePolicyNone);
    }

    /// <summary>Returns the named field of an enum type, if present.</summary>
    /// <param name="enumType">The enum type.</param>
    /// <param name="fieldName">The field name to resolve.</param>
    /// <returns>The field symbol, or <see langword="null"/> when absent.</returns>
    private static IFieldSymbol? GetEnumField(INamedTypeSymbol enumType, string fieldName)
    {
        var members = enumType.GetMembers(fieldName);
        for (var i = 0; i < members.Length; i++)
        {
            if (members[i] is IFieldSymbol field)
            {
                return field;
            }
        }

        return null;
    }

    /// <summary>The cookie types and enum fields resolved once per compilation for SES1504.</summary>
    /// <param name="CookieOptions">The resolved <c>CookieOptions</c> type, or <see langword="null"/>.</param>
    /// <param name="CookieBuilder">The resolved <c>CookieBuilder</c> type, or <see langword="null"/>.</param>
    /// <param name="SameSiteNone">The resolved <c>SameSiteMode.None</c> field.</param>
    /// <param name="SecurePolicyNone">The resolved <c>CookieSecurePolicy.None</c> field, or <see langword="null"/>.</param>
    private readonly record struct CookieInitializerTypes(
        INamedTypeSymbol? CookieOptions,
        INamedTypeSymbol? CookieBuilder,
        IFieldSymbol SameSiteNone,
        IFieldSymbol? SecurePolicyNone);
}
