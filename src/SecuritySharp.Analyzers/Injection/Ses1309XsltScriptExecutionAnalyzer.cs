// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis.Operations;

namespace SecuritySharp.Analyzers;

/// <summary>
/// Flags an <c>XslCompiledTransform.Load</c> call whose <c>XsltSettings</c> enable embedded script (SES1309).
/// The rule reports the settings argument when it is, at the call site, one of the script-enabling shapes: an
/// object initializer that sets <c>EnableScript = true</c>, a constructor whose <c>enableScript</c> argument is
/// the constant <c>true</c>, or the static <c>XsltSettings.TrustedXslt</c> (which turns on both the document()
/// function and script). Enabling script
/// lets a script block in the stylesheet compile and run in the host process, so a stylesheet drawn from
/// untrusted input is arbitrary code execution. The rule is gated on <c>XslCompiledTransform</c> and
/// <c>XsltSettings</c> both resolving in the compilation. These types are resolved once, on first demand.
/// The clean path resolves and binds nothing until a syntactic screen -- a member <c>.Load(...)</c> call carrying at
/// least two arguments -- passes. Only the local shape of the settings argument is inspected; no data-flow or
/// interprocedural tracking is performed, so settings first stored in a variable and passed later are not
/// followed and are outside this rule.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Ses1309XsltScriptExecutionAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The name of the transform method that compiles the stylesheet under the settings.</summary>
    private const string LoadMethodName = "Load";

    /// <summary>The name of the settings property that turns on script when set to <see langword="true"/>.</summary>
    private const string EnableScriptPropertyName = "EnableScript";

    /// <summary>The name of the constructor parameter that turns on script when the argument is <see langword="true"/>.</summary>
    private const string EnableScriptParameterName = "enableScript";

    /// <summary>The name of the static settings property that enables both document() and script.</summary>
    private const string TrustedXsltPropertyName = "TrustedXslt";

    /// <summary>The descriptors this analyzer reports, built once rather than on every access.</summary>
    private static readonly ImmutableArray<DiagnosticDescriptor> SupportedDiagnosticsValue = ImmutableArrays.Of(SecurityRules.XsltScriptExecution);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => SupportedDiagnosticsValue;

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(static start =>
        {
            var types = new XsltTypes(start.Compilation);
            start.RegisterSyntaxNodeAction(nodeContext => AnalyzeInvocation(nodeContext, types), SyntaxKind.InvocationExpression);
        });
    }

    /// <summary>Reports SES1309 for an <c>XslCompiledTransform.Load</c> call whose settings enable script.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="types">The XSLT types resolved on first demand for the compilation.</param>
    private static void AnalyzeInvocation(in SyntaxNodeAnalysisContext context, XsltTypes types)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        // Only inline settings constructions and TrustedXslt can enable script under this rule.
        // Scan every argument so named and reordered arguments retain the same behavior.
        if (invocation.Expression is not MemberAccessExpressionSyntax { Name.Identifier.ValueText: LoadMethodName }
            || invocation.ArgumentList.Arguments.Count < 2
            || !HasSettingsShape(invocation.ArgumentList))
        {
            return;
        }

        if (types.Get() is not [{ } transform, { } settings]
            || context.SemanticModel.GetOperation(invocation, context.CancellationToken) is not IInvocationOperation operation
            || operation.TargetMethod.Name != LoadMethodName
            || !SymbolEqualityComparer.Default.Equals(operation.TargetMethod.ContainingType, transform)
            || GetScriptEnablingSettings(operation, settings) is not { } enablingSyntax)
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            SecurityRules.XsltScriptExecution,
            enablingSyntax.SyntaxTree,
            enablingSyntax.Span));
    }

    /// <summary>Returns the settings-argument syntax when that argument, at the call site, enables script.</summary>
    /// <param name="operation">The bound <c>Load</c> invocation.</param>
    /// <param name="settingsType">The gated <c>XsltSettings</c> type.</param>
    /// <returns>The script-enabling settings expression syntax, or <see langword="null"/> when no settings argument enables script.</returns>
    private static SyntaxNode? GetScriptEnablingSettings(IInvocationOperation operation, INamedTypeSymbol settingsType)
    {
        var arguments = operation.Arguments;
        for (var i = 0; i < arguments.Length; i++)
        {
            var argument = arguments[i];

            // The settings overload has exactly one XsltSettings parameter; once it is found the decision is made
            // from that argument alone -- a Load overload without an XsltSettings parameter never matches here.
            if (argument.Parameter is { } parameter && SymbolEqualityComparer.Default.Equals(parameter.Type, settingsType))
            {
                return EnablesScript(argument.Value, settingsType) ? argument.Value.Syntax : null;
            }
        }

        return null;
    }

    /// <summary>Returns whether a settings expression enables script at the call site.</summary>
    /// <param name="settingsValue">The settings argument value operation.</param>
    /// <param name="settingsType">The gated <c>XsltSettings</c> type.</param>
    /// <returns><see langword="true"/> when the expression is a script-enabling settings shape.</returns>
    private static bool EnablesScript(IOperation settingsValue, INamedTypeSymbol settingsType) =>
        settingsValue switch
        {
            // 'new XsltSettings(...)' or 'new XsltSettings { ... }': script is on when the constructor's
            // 'enableScript' argument is true or the initializer sets 'EnableScript = true'.
            IObjectCreationOperation creation when SymbolEqualityComparer.Default.Equals(creation.Type, settingsType) =>
                ConstructorEnablesScript(creation) || InitializerEnablesScript(creation),

            // 'XsltSettings.TrustedXslt' is the pre-built settings that turn on both document() and script.
            IPropertyReferenceOperation { Property: { IsStatic: true, Name: TrustedXsltPropertyName } property } =>
                SymbolEqualityComparer.Default.Equals(property.ContainingType, settingsType),

            _ => false,
        };

    /// <summary>Returns whether a constructor call passes a constant <see langword="true"/> for <c>enableScript</c>.</summary>
    /// <param name="creation">The <c>XsltSettings</c> object-creation operation.</param>
    /// <returns><see langword="true"/> when the <c>enableScript</c> argument is the constant <see langword="true"/>.</returns>
    private static bool ConstructorEnablesScript(IObjectCreationOperation creation)
    {
        var arguments = creation.Arguments;
        for (var i = 0; i < arguments.Length; i++)
        {
            var argument = arguments[i];
            if (argument.Parameter is { Name: EnableScriptParameterName } && IsConstantTrue(argument.Value))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether an object initializer sets <c>EnableScript</c> to a constant <see langword="true"/>.</summary>
    /// <param name="creation">The <c>XsltSettings</c> object-creation operation.</param>
    /// <returns><see langword="true"/> when the initializer assigns <c>EnableScript = true</c>.</returns>
    private static bool InitializerEnablesScript(IObjectCreationOperation creation)
    {
        if (creation.Initializer is not { } initializer)
        {
            return false;
        }

        var initializers = initializer.Initializers;
        for (var i = 0; i < initializers.Length; i++)
        {
            if (initializers[i] is ISimpleAssignmentOperation { Target: IPropertyReferenceOperation { Property.Name: EnableScriptPropertyName } } assignment
                && IsConstantTrue(assignment.Value))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether an operation is a compile-time-constant <see langword="true"/>.</summary>
    /// <param name="operation">The operation to inspect.</param>
    /// <returns><see langword="true"/> when the operation folds to the boolean constant <see langword="true"/>.</returns>
    private static bool IsConstantTrue(IOperation operation) =>
        operation.ConstantValue is { HasValue: true, Value: bool value } && value;

    /// <summary>Checks for an argument whose local syntax can enable script, before binding.</summary>
    /// <param name="argumentList">The candidate Load arguments.</param>
    /// <returns>Whether any argument can produce a recognized settings operation.</returns>
    private static bool HasSettingsShape(ArgumentListSyntax argumentList)
    {
        foreach (var argument in argumentList.Arguments)
        {
            if (IsSettingsShape(argument.Expression))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Recognizes settings constructions and TrustedXslt through transparent syntax wrappers.</summary>
    /// <param name="expression">The supplied argument expression.</param>
    /// <returns>Whether the expression could enable script locally.</returns>
    private static bool IsSettingsShape(ExpressionSyntax expression) => expression switch
    {
        BaseObjectCreationExpressionSyntax
            or MemberAccessExpressionSyntax { Name.Identifier.ValueText: TrustedXsltPropertyName }
            or IdentifierNameSyntax { Identifier.ValueText: TrustedXsltPropertyName } => true,
        ParenthesizedExpressionSyntax parenthesized => IsSettingsShape(parenthesized.Expression),
        CheckedExpressionSyntax checkedExpression => IsSettingsShape(checkedExpression.Expression),
        PostfixUnaryExpressionSyntax { RawKind: (int)SyntaxKind.SuppressNullableWarningExpression } suppressed => IsSettingsShape(suppressed.Operand),
        _ => false,
    };

    /// <summary>Resolves the XSLT types once, only after a settings candidate is found.</summary>
    /// <param name="compilation">The compilation whose XSLT types are cached.</param>
    private sealed class XsltTypes(Compilation compilation)
    {
        /// <summary>The metadata name of the transform type whose <c>Load</c> applies the settings.</summary>
        private const string XslCompiledTransformMetadataName = "System.Xml.Xsl.XslCompiledTransform";

        /// <summary>The metadata name of the settings type that can enable script.</summary>
        private const string XsltSettingsMetadataName = "System.Xml.Xsl.XsltSettings";

        /// <summary>Serializes the first metadata lookup across invocation callbacks.</summary>
        private readonly object _gate = new();

        /// <summary>The cached transform and settings types, including unavailable types.</summary>
        private INamedTypeSymbol?[]? _resolved;

        /// <summary>Gets the XSLT types on first demand.</summary>
        /// <returns>The cached type pair, or an empty array when the transform type is absent.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public INamedTypeSymbol?[] Get() => Volatile.Read(ref _resolved) ?? Resolve();

        /// <summary>Publishes the XSLT metadata lookup once for the compilation.</summary>
        /// <returns>The cached XSLT type pair.</returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private INamedTypeSymbol?[] Resolve()
        {
            lock (_gate)
            {
                var resolved = _resolved;
                if (resolved is null)
                {
                    resolved = compilation.GetTypeByMetadataName(XslCompiledTransformMetadataName) is { } transform
                        ? [transform, compilation.GetTypeByMetadataName(XsltSettingsMetadataName)]
                        : [];
                    Volatile.Write(ref _resolved, resolved);
                }

                return resolved;
            }
        }
    }
}
