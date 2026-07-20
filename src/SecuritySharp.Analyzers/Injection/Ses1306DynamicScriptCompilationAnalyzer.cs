// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Operations;

namespace SecuritySharp.Analyzers;

/// <summary>
/// Flags a scripting-API call that compiles and executes a non-constant C# source string (SES1306). The
/// rule reports the <c>code</c> argument of the static <c>EvaluateAsync</c>, <c>RunAsync</c>, and
/// <c>Create</c> methods on <c>Microsoft.CodeAnalysis.CSharp.Scripting.CSharpScript</c> when that argument
/// is not a compile-time constant -- a variable, a concatenation or interpolation folding in a variable, a
/// method result, or any other runtime value. A constant literal, a <c>const</c> reference, or a folded
/// constant expression is a trusted, author-written template and is never reported. The class is probed
/// once per compilation; a project that does not reference the C# scripting package registers nothing and
/// pays nothing. The clean path binds nothing: a syntactic screen requires a member call to one of the
/// three method names before any symbol resolution runs. Only the local shape is inspected -- no data-flow
/// or interprocedural tracking is performed, so the constant/non-constant decision is made at the call site.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Ses1306DynamicScriptCompilationAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The metadata name of the C# scripting entry-point class whose code channel is guarded.</summary>
    private const string CSharpScriptMetadataName = "Microsoft.CodeAnalysis.CSharp.Scripting.CSharpScript";

    /// <summary>The name of the source-code parameter inspected on every gated scripting method.</summary>
    private const string CodeParameterName = "code";

    /// <summary>The gated scripting method names that compile and run their <c>code</c> argument.</summary>
    private const string EvaluateAsyncMethodName = "EvaluateAsync";

    /// <summary>The gated scripting method name that compiles and runs the script immediately.</summary>
    private const string RunAsyncMethodName = "RunAsync";

    /// <summary>The gated scripting method name that compiles the script into a reusable object.</summary>
    private const string CreateMethodName = "Create";

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(SecurityRules.DynamicScriptCompilation);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(start =>
        {
            var scriptType = start.Compilation.GetTypeByMetadataName(CSharpScriptMetadataName);
            if (scriptType is null)
            {
                return;
            }

            start.RegisterSyntaxNodeAction(nodeContext => AnalyzeInvocation(nodeContext, scriptType), SyntaxKind.InvocationExpression);
        });
    }

    /// <summary>Reports SES1306 for a gated scripting call whose <c>code</c> argument is not a constant.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="scriptType">The gated <c>CSharpScript</c> type resolved for the compilation.</param>
    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context, INamedTypeSymbol scriptType)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        // Syntactic prefilter: a member '.EvaluateAsync/.RunAsync/.Create(...)' call carrying at least one
        // argument. The Name is a SimpleNameSyntax, so a generic 'EvaluateAsync<int>' matches too.
        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess
            || !IsGatedMethodName(memberAccess.Name.Identifier.ValueText)
            || invocation.ArgumentList.Arguments.Count == 0)
        {
            return;
        }

        if (context.SemanticModel.GetOperation(invocation, context.CancellationToken) is not IInvocationOperation operation
            || !IsGatedMethodName(operation.TargetMethod.Name)
            || !SymbolEqualityComparer.Default.Equals(operation.TargetMethod.ContainingType, scriptType)
            || GetNonConstantCodeArgument(operation) is not { } codeSyntax)
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            SecurityRules.DynamicScriptCompilation,
            codeSyntax.SyntaxTree,
            codeSyntax.Span,
            operation.TargetMethod.Name));
    }

    /// <summary>Returns whether a method name is one of the gated scripting entry points.</summary>
    /// <param name="name">The invoked simple method name.</param>
    /// <returns><see langword="true"/> when the name is <c>EvaluateAsync</c>, <c>RunAsync</c>, or <c>Create</c>.</returns>
    private static bool IsGatedMethodName(string name)
        => name is EvaluateAsyncMethodName or RunAsyncMethodName or CreateMethodName;

    /// <summary>Returns the syntax of the string <c>code</c> argument when it is not a compile-time constant.</summary>
    /// <param name="operation">The bound scripting invocation.</param>
    /// <returns>The non-constant code argument syntax, or <see langword="null"/> when the code channel is a constant or a non-string overload.</returns>
    private static SyntaxNode? GetNonConstantCodeArgument(IInvocationOperation operation)
    {
        var arguments = operation.Arguments;
        for (var i = 0; i < arguments.Length; i++)
        {
            var argument = arguments[i];

            // Only the string 'code' channel is a source-compilation risk; the Stream overloads and every
            // other parameter are ignored, and a constant string is a trusted, author-written template.
            if (argument is { ArgumentKind: ArgumentKind.Explicit, Parameter: { Name: CodeParameterName, Type.SpecialType: SpecialType.System_String } }
                && !argument.Value.ConstantValue.HasValue)
            {
                return argument.Value.Syntax;
            }
        }

        return null;
    }
}
