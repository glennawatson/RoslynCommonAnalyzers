// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Operations;

namespace SecuritySharp.Analyzers;

/// <summary>
/// Flags a constant or predictable salt passed to a password-based key-derivation function (SES1002).
/// The rule reports the salt argument of a <c>System.Security.Cryptography.Rfc2898DeriveBytes</c>
/// constructor and of the static <c>Rfc2898DeriveBytes.Pbkdf2</c> method when that argument is a fixed
/// value: an inline <c>new byte[N]</c> (an all-zero buffer no statement writes to before the call), an
/// inline byte array of constant elements, a reference to a <c>static readonly</c> field (allocated once
/// and shared across every call), or <c>Encoding.GetBytes</c> over a constant string. A predictable salt
/// defeats the per-secret uniqueness that a salt exists to provide, so an attacker can precompute a
/// rainbow table once and crack every password hashed with it. The rule is resolved once per compilation
/// by probing <c>Rfc2898DeriveBytes</c>; on a target framework without that type nothing is registered,
/// so a project that cannot call these APIs pays nothing and never receives a diagnostic it cannot act
/// on. The random-salt <c>Rfc2898DeriveBytes(string, int, …)</c> overloads (whose parameter is
/// <c>saltSize</c>, not <c>salt</c>) generate the salt internally and are never reported.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Ses1002ConstantKdfSaltAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The metadata name of the password-based key-derivation type whose salt is guarded.</summary>
    private const string Rfc2898MetadataName = "System.Security.Cryptography.Rfc2898DeriveBytes";

    /// <summary>The simple name of the key-derivation type, used by the allocation-free syntactic prefilter.</summary>
    private const string Rfc2898TypeSimpleName = "Rfc2898DeriveBytes";

    /// <summary>The name of the static one-shot key-derivation method whose salt is guarded.</summary>
    private const string Pbkdf2MethodName = "Pbkdf2";

    /// <summary>The name of the salt parameter on every guarded overload.</summary>
    private const string SaltParameterName = "salt";

    /// <summary>The metadata name of the text-encoding type used to detect a salt derived from a constant string.</summary>
    private const string EncodingMetadataName = "System.Text.Encoding";

    /// <summary>The name of the <c>Encoding.GetBytes</c> method that turns a constant string into a fixed salt.</summary>
    private const string GetBytesMethodName = "GetBytes";

    /// <summary>The fewest arguments a guarded overload takes (a password and a salt), used by the prefilters.</summary>
    private const int MinimumGuardedArgumentCount = 2;

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(SecurityRules.ConstantKdfSalt);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(start =>
        {
            var kdfType = start.Compilation.GetTypeByMetadataName(Rfc2898MetadataName);
            if (kdfType is null)
            {
                return;
            }

            // Encoding is present on every framework that has Rfc2898DeriveBytes, but resolve it here so
            // the 'Encoding.GetBytes' salt check never touches metadata on the hot path.
            var encodingType = start.Compilation.GetTypeByMetadataName(EncodingMetadataName);

            start.RegisterSyntaxNodeAction(nodeContext => AnalyzeObjectCreation(nodeContext, kdfType, encodingType), SyntaxKind.ObjectCreationExpression);
            start.RegisterSyntaxNodeAction(nodeContext => AnalyzeInvocation(nodeContext, kdfType, encodingType), SyntaxKind.InvocationExpression);
        });
    }

    /// <summary>Reports SES1002 for a <c>new Rfc2898DeriveBytes(…)</c> call whose salt argument is a fixed value.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="kdfType">The gated <c>Rfc2898DeriveBytes</c> type resolved for the compilation.</param>
    /// <param name="encodingType">The <c>Encoding</c> type, or <see langword="null"/> when it is absent.</param>
    private static void AnalyzeObjectCreation(SyntaxNodeAnalysisContext context, INamedTypeSymbol kdfType, INamedTypeSymbol? encodingType)
    {
        var creation = (ObjectCreationExpressionSyntax)context.Node;

        // Syntactic prefilter: a 'new Rfc2898DeriveBytes(...)' carrying at least a password and a salt.
        if (creation.ArgumentList is not { Arguments.Count: >= MinimumGuardedArgumentCount }
            || !IsRfc2898TypeName(creation.Type))
        {
            return;
        }

        if (context.SemanticModel.GetOperation(creation, context.CancellationToken) is not IObjectCreationOperation operation
            || !SymbolEqualityComparer.Default.Equals(operation.Type, kdfType))
        {
            return;
        }

        ReportIfFixedSalt(context, operation.Arguments, encodingType, "the " + Rfc2898TypeSimpleName + " constructor");
    }

    /// <summary>Reports SES1002 for a <c>Rfc2898DeriveBytes.Pbkdf2(…)</c> call whose salt argument is a fixed value.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="kdfType">The gated <c>Rfc2898DeriveBytes</c> type resolved for the compilation.</param>
    /// <param name="encodingType">The <c>Encoding</c> type, or <see langword="null"/> when it is absent.</param>
    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context, INamedTypeSymbol kdfType, INamedTypeSymbol? encodingType)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        // Syntactic prefilter: a member '.Pbkdf2(...)' call carrying at least a password and a salt.
        if (invocation.Expression is not MemberAccessExpressionSyntax { Name.Identifier.ValueText: Pbkdf2MethodName }
            || invocation.ArgumentList.Arguments.Count < MinimumGuardedArgumentCount)
        {
            return;
        }

        if (context.SemanticModel.GetOperation(invocation, context.CancellationToken) is not IInvocationOperation operation
            || !SymbolEqualityComparer.Default.Equals(operation.TargetMethod.ContainingType, kdfType))
        {
            return;
        }

        ReportIfFixedSalt(context, operation.Arguments, encodingType, Rfc2898TypeSimpleName + "." + Pbkdf2MethodName);
    }

    /// <summary>Reports SES1002 when the salt argument of a bound key-derivation call is a fixed value.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="arguments">The bound call's arguments, mapped to their parameters.</param>
    /// <param name="encodingType">The <c>Encoding</c> type, or <see langword="null"/> when it is absent.</param>
    /// <param name="apiLabel">The human-readable label for the call, used in the diagnostic message.</param>
    private static void ReportIfFixedSalt(
        SyntaxNodeAnalysisContext context,
        ImmutableArray<IArgumentOperation> arguments,
        INamedTypeSymbol? encodingType,
        string apiLabel)
    {
        if (GetSaltExpression(arguments) is not { } saltExpression
            || !IsFixedSalt(context.SemanticModel, saltExpression, encodingType, context.CancellationToken))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            SecurityRules.ConstantKdfSalt,
            saltExpression.SyntaxTree,
            saltExpression.Span,
            apiLabel));
    }

    /// <summary>Returns the expression bound to the byte-array <c>salt</c> parameter, or null when there is none.</summary>
    /// <param name="arguments">The bound call's arguments, mapped to their parameters.</param>
    /// <returns>The salt argument expression, or <see langword="null"/>. The random-salt <c>saltSize</c> overloads return null.</returns>
    private static ExpressionSyntax? GetSaltExpression(ImmutableArray<IArgumentOperation> arguments)
    {
        for (var i = 0; i < arguments.Length; i++)
        {
            // The operation model maps each argument to its parameter regardless of positional or named
            // syntax, so the byte-array 'salt' parameter is matched by name and the random-salt 'saltSize'
            // overloads (which have no 'salt' parameter) are skipped without any manual position juggling.
            if (arguments[i].Parameter is { Name: SaltParameterName }
                && arguments[i].Value.Syntax is ExpressionSyntax saltExpression)
            {
                return saltExpression;
            }
        }

        return null;
    }

    /// <summary>Returns whether a salt expression is a compile-time-fixed or shared-field value.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="expression">The salt argument expression.</param>
    /// <param name="encodingType">The <c>Encoding</c> type, or <see langword="null"/> when it is absent.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> when the salt is a fixed value.</returns>
    private static bool IsFixedSalt(SemanticModel model, ExpressionSyntax expression, INamedTypeSymbol? encodingType, CancellationToken cancellationToken)
        => expression switch
        {
            // An inline 'new byte[N]' has no writes before the call: no initializer means an all-zero
            // buffer, and an initializer is fixed only when every element is a compile-time constant.
            ArrayCreationExpressionSyntax arrayCreation =>
                arrayCreation.Initializer is null || IsConstantInitializer(model, arrayCreation.Initializer, cancellationToken),

            // 'Encoding.X.GetBytes("literal")' bakes a constant string into the salt.
            InvocationExpressionSyntax invocation =>
                IsConstantEncodingGetBytes(model, invocation, encodingType, cancellationToken),

            // A reference to a field allocated once and reused across every call.
            IdentifierNameSyntax or MemberAccessExpressionSyntax =>
                IsFixedFieldReference(model, expression, cancellationToken),

            _ => false,
        };

    /// <summary>Returns whether every element of an array initializer is a compile-time constant.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="initializer">The array initializer.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> when all elements are constants.</returns>
    private static bool IsConstantInitializer(SemanticModel model, InitializerExpressionSyntax initializer, CancellationToken cancellationToken)
    {
        var expressions = initializer.Expressions;
        for (var i = 0; i < expressions.Count; i++)
        {
            if (!model.GetConstantValue(expressions[i], cancellationToken).HasValue)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Returns whether an invocation is <c>Encoding.GetBytes</c> over a constant string.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="invocation">The salt invocation expression.</param>
    /// <param name="encodingType">The <c>Encoding</c> type, or <see langword="null"/> when it is absent.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> for a constant <c>Encoding.GetBytes</c> salt.</returns>
    private static bool IsConstantEncodingGetBytes(SemanticModel model, InvocationExpressionSyntax invocation, INamedTypeSymbol? encodingType, CancellationToken cancellationToken)
    {
        if (encodingType is null
            || invocation.Expression is not MemberAccessExpressionSyntax { Name.Identifier.ValueText: GetBytesMethodName } memberAccess
            || invocation.ArgumentList.Arguments.Count != 1
            || !model.GetConstantValue(invocation.ArgumentList.Arguments[0].Expression, cancellationToken).HasValue)
        {
            return false;
        }

        // The receiver of a genuine 'Encoding.GetBytes' is an Encoding (a static property such as
        // 'Encoding.UTF8' or an Encoding-typed value); a static call on an unrelated type (for example
        // 'RandomNumberGenerator.GetBytes') has a type-only receiver whose value type is null.
        return IsEncoding(model.GetTypeInfo(memberAccess.Expression, cancellationToken).Type as INamedTypeSymbol, encodingType);
    }

    /// <summary>Returns whether a type is <c>Encoding</c> or derives from it.</summary>
    /// <param name="type">The candidate type, or <see langword="null"/> when the receiver has no value type.</param>
    /// <param name="encodingType">The resolved <c>Encoding</c> type.</param>
    /// <returns><see langword="true"/> when the type is or derives from <c>Encoding</c>.</returns>
    private static bool IsEncoding(INamedTypeSymbol? type, INamedTypeSymbol encodingType)
    {
        for (; type is not null; type = type.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(type, encodingType))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether an expression binds to a <c>static readonly</c> field allocated once and shared.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="expression">The salt reference expression.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> for a static readonly field reference.</returns>
    private static bool IsFixedFieldReference(SemanticModel model, ExpressionSyntax expression, CancellationToken cancellationToken)
        => model.GetSymbolInfo(expression, cancellationToken).Symbol is IFieldSymbol { IsStatic: true, IsReadOnly: true };

    /// <summary>Returns whether an object-creation type name is spelled <c>Rfc2898DeriveBytes</c>.</summary>
    /// <param name="type">The created type syntax.</param>
    /// <returns><see langword="true"/> when the right-most name is <c>Rfc2898DeriveBytes</c>.</returns>
    private static bool IsRfc2898TypeName(TypeSyntax type)
    {
        // Peel a namespace qualification ('System.Security.Cryptography.Rfc2898DeriveBytes') down to its
        // right-most simple name so both the imported and fully-qualified spellings are matched.
        while (type is QualifiedNameSyntax qualified)
        {
            type = qualified.Right;
        }

        return type is SimpleNameSyntax { Identifier.ValueText: Rfc2898TypeSimpleName };
    }
}
