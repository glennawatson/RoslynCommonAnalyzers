// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

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
/// <c>XsltSettings</c> both resolving in the compilation, so a project without them registers nothing and pays
/// nothing. The clean path binds nothing until a syntactic screen -- a member <c>.Load(...)</c> call carrying at
/// least two arguments -- passes. Only the local shape of the settings argument is inspected; no data-flow or
/// interprocedural tracking is performed, so settings first stored in a variable and passed later are not
/// followed and are outside this rule.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Ses1309XsltScriptExecutionAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The metadata name of the transform type whose <c>Load</c> applies the settings.</summary>
    private const string XslCompiledTransformMetadataName = "System.Xml.Xsl.XslCompiledTransform";

    /// <summary>The metadata name of the settings type that can enable script.</summary>
    private const string XsltSettingsMetadataName = "System.Xml.Xsl.XsltSettings";

    /// <summary>The name of the transform method that compiles the stylesheet under the settings.</summary>
    private const string LoadMethodName = "Load";

    /// <summary>The name of the settings property that turns on script when set to <see langword="true"/>.</summary>
    private const string EnableScriptPropertyName = "EnableScript";

    /// <summary>The name of the constructor parameter that turns on script when the argument is <see langword="true"/>.</summary>
    private const string EnableScriptParameterName = "enableScript";

    /// <summary>The name of the static settings property that enables both document() and script.</summary>
    private const string TrustedXsltPropertyName = "TrustedXslt";

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(SecurityRules.XsltScriptExecution);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(start =>
        {
            var transformType = start.Compilation.GetTypeByMetadataName(XslCompiledTransformMetadataName);
            var settingsType = start.Compilation.GetTypeByMetadataName(XsltSettingsMetadataName);
            if (transformType is null || settingsType is null)
            {
                return;
            }

            start.RegisterSyntaxNodeAction(nodeContext => AnalyzeInvocation(nodeContext, transformType, settingsType), SyntaxKind.InvocationExpression);
        });
    }

    /// <summary>Reports SES1309 for an <c>XslCompiledTransform.Load</c> call whose settings enable script.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="transformType">The gated <c>XslCompiledTransform</c> type resolved for the compilation.</param>
    /// <param name="settingsType">The gated <c>XsltSettings</c> type resolved for the compilation.</param>
    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context, INamedTypeSymbol transformType, INamedTypeSymbol settingsType)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        // Syntactic prefilter: a member '.Load(...)' call carrying at least the settings-overload arity. The
        // settings overloads take (stylesheet, settings, resolver); a leading positional settings slot needs two
        // or more arguments, so a single-argument 'Load(reader)' is rejected without binding.
        if (invocation.Expression is not MemberAccessExpressionSyntax { Name.Identifier.ValueText: LoadMethodName }
            || invocation.ArgumentList.Arguments.Count < 2)
        {
            return;
        }

        if (context.SemanticModel.GetOperation(invocation, context.CancellationToken) is not IInvocationOperation operation
            || operation.TargetMethod.Name != LoadMethodName
            || !SymbolEqualityComparer.Default.Equals(operation.TargetMethod.ContainingType, transformType)
            || GetScriptEnablingSettings(operation, settingsType) is not { } enablingSyntax)
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
    private static bool EnablesScript(IOperation settingsValue, INamedTypeSymbol settingsType)
        => settingsValue switch
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
    private static bool IsConstantTrue(IOperation operation)
        => operation.ConstantValue is { HasValue: true, Value: bool value } && value;
}
