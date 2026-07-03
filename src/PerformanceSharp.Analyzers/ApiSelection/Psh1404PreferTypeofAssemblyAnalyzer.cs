// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Flags calls to <c>Assembly.GetExecutingAssembly()</c> (PSH1404), which discovers its caller by
/// walking the stack at runtime even though the answer is known statically:
/// <c>typeof(EnclosingType).Assembly</c> resolves the same assembly without the walk. The message
/// names the nearest enclosing type so the suggested <c>typeof</c> form is copy-pastable; inside
/// top-level statements the rule still reports (using the compiler-synthesized type name) but the
/// code fix is withheld because no declared type is in scope.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Psh1404PreferTypeofAssemblyAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The name of the stack-walking factory method this rule replaces.</summary>
    internal const string GetExecutingAssemblyMethodName = "GetExecutingAssembly";

    /// <summary>The metadata name of the reflection assembly type.</summary>
    private const string AssemblyMetadataName = "System.Reflection.Assembly";

    /// <summary>The compiler-synthesized type name used when top-level statements have no declared enclosing type.</summary>
    private const string TopLevelProgramTypeName = "Program";

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(ApiSelectionRules.PreferTypeofAssembly);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(start =>
        {
            var assemblyType = start.Compilation.GetTypeByMetadataName(AssemblyMetadataName);
            if (assemblyType is null || !HasStaticGetExecutingAssembly(assemblyType))
            {
                return;
            }

            start.RegisterSyntaxNodeAction(nodeContext => AnalyzeInvocation(nodeContext, assemblyType), SyntaxKind.InvocationExpression);
        });
    }

    /// <summary>Returns whether an invocation has the argument-free <c>GetExecutingAssembly()</c> syntax shape.</summary>
    /// <param name="invocation">The invocation to inspect.</param>
    /// <returns><see langword="true"/> when the syntax-only shape matches (member access or using-static call).</returns>
    internal static bool IsGetExecutingAssemblyShape(InvocationExpressionSyntax invocation)
        => invocation.ArgumentList.Arguments.Count == 0
            && GetInvokedSimpleName(invocation) is { Identifier.ValueText: GetExecutingAssemblyMethodName };

    /// <summary>Builds the enclosing type's display name — its declared identifier plus its own type parameters.</summary>
    /// <param name="typeDeclaration">The nearest enclosing type declaration.</param>
    /// <returns>The name usable inside the type, such as <c>Cache&lt;T&gt;</c>.</returns>
    internal static string GetEnclosingTypeDisplayName(TypeDeclarationSyntax typeDeclaration)
    {
        var identifier = typeDeclaration.Identifier.ValueText;
        if (typeDeclaration.TypeParameterList is not { Parameters.Count: > 0 } typeParameters)
        {
            return identifier;
        }

        var builder = new System.Text.StringBuilder(identifier);
        builder.Append('<');
        for (var i = 0; i < typeParameters.Parameters.Count; i++)
        {
            if (i > 0)
            {
                builder.Append(", ");
            }

            builder.Append(typeParameters.Parameters[i].Identifier.ValueText);
        }

        builder.Append('>');
        return builder.ToString();
    }

    /// <summary>Reports PSH1404 for an invocation bound to <c>Assembly.GetExecutingAssembly()</c>.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="assemblyType">The reflection assembly type.</param>
    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context, INamedTypeSymbol assemblyType)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (!IsGetExecutingAssemblyShape(invocation))
        {
            return;
        }

        if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol { IsStatic: true, Parameters.Length: 0 } method
            || !SymbolEqualityComparer.Default.Equals(method.ContainingType, assemblyType))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            ApiSelectionRules.PreferTypeofAssembly,
            invocation.SyntaxTree,
            invocation.Span,
            GetEnclosingTypeName(context, invocation)));
    }

    /// <summary>Returns the invoked member's simple name for the supported call shapes.</summary>
    /// <param name="invocation">The invocation to inspect.</param>
    /// <returns>The invoked simple name, or <see langword="null"/> for unsupported expression shapes.</returns>
    private static SimpleNameSyntax? GetInvokedSimpleName(InvocationExpressionSyntax invocation)
        => invocation.Expression switch
        {
            MemberAccessExpressionSyntax access => access.Name,
            SimpleNameSyntax simpleName => simpleName,
            _ => null
        };

    /// <summary>Returns the display name of the type enclosing the reported invocation.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="invocation">The reported invocation.</param>
    /// <returns>The declared type's display name, or the synthesized containing type's name inside top-level statements.</returns>
    private static string GetEnclosingTypeName(SyntaxNodeAnalysisContext context, InvocationExpressionSyntax invocation)
    {
        if (invocation.FirstAncestorOrSelf<TypeDeclarationSyntax>() is { } typeDeclaration)
        {
            return GetEnclosingTypeDisplayName(typeDeclaration);
        }

        return context.ContainingSymbol?.ContainingType?.Name ?? TopLevelProgramTypeName;
    }

    /// <summary>Returns whether the assembly type exposes the static parameterless <c>GetExecutingAssembly</c> method.</summary>
    /// <param name="assemblyType">The reflection assembly type to probe.</param>
    /// <returns><see langword="true"/> when the probed method exists.</returns>
    private static bool HasStaticGetExecutingAssembly(INamedTypeSymbol assemblyType)
    {
        var members = assemblyType.GetMembers(GetExecutingAssemblyMethodName);
        for (var i = 0; i < members.Length; i++)
        {
            if (members[i] is IMethodSymbol { IsStatic: true, Parameters.Length: 0 })
            {
                return true;
            }
        }

        return false;
    }
}
