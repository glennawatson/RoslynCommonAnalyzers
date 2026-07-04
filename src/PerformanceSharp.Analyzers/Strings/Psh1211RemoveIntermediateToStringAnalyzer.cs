// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Flags parameterless <c>ToString()</c> calls whose result feeds an API that can take the
/// value directly (PSH1211): an argument to a method whose same-name overload accepts the
/// receiver's type in that position, or an interpolated string hole, where the handler
/// formats the value in place — value types with span formatting write straight into the
/// buffer. Interpolation holes are only reported when the interpolated string converts to a
/// plain string, so custom handlers and FormattableString culture behavior stay untouched,
/// and single-hole wrappers are left to PSH1205. StringBuilder receivers are left to PSH1203.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Psh1211RemoveIntermediateToStringAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The invoked member name the syntax gate requires.</summary>
    internal const string ToStringMethodName = nameof(ToString);

    /// <summary>The metadata name of the builder type whose appends PSH1203 already covers.</summary>
    private const string StringBuilderMetadataName = "System.Text.StringBuilder";

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(StringRules.RemoveIntermediateToString);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(start =>
        {
            var builderType = start.Compilation.GetTypeByMetadataName(StringBuilderMetadataName);
            start.RegisterSyntaxNodeAction(nodeContext => AnalyzeInvocation(nodeContext, builderType), SyntaxKind.InvocationExpression);
        });
    }

    /// <summary>Returns whether an invocation is a plain parameterless <c>x.ToString()</c>, before any binding.</summary>
    /// <param name="invocation">The invocation to inspect.</param>
    /// <returns><see langword="true"/> when the shape matches.</returns>
    internal static bool IsBareToStringShape(InvocationExpressionSyntax invocation)
        => invocation.ArgumentList.Arguments.Count == 0
            && invocation.Expression is MemberAccessExpressionSyntax { RawKind: (int)SyntaxKind.SimpleMemberAccessExpression } access
            && access.Name.Identifier.ValueText == ToStringMethodName;

    /// <summary>Reports PSH1211 for a ToString result feeding a value-capable consumer.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="builderType">The StringBuilder type, or <see langword="null"/> when absent.</param>
    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context, INamedTypeSymbol? builderType)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (!IsBareToStringShape(invocation))
        {
            return;
        }

        var report = invocation.Parent switch
        {
            InterpolationSyntax interpolation => IsPlainStringInterpolation(context, interpolation),
            ArgumentSyntax { Parent.Parent: InvocationExpressionSyntax outer } argument
                => HasDirectOverload(context, invocation, argument, outer, builderType),
            _ => false,
        };

        if (!report)
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            StringRules.RemoveIntermediateToString,
            invocation.SyntaxTree,
            invocation.Span));
    }

    /// <summary>Returns whether an interpolation hole can format the value directly.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="interpolation">The hole holding the ToString call.</param>
    /// <returns><see langword="true"/> when the interpolated string converts to a plain string and is not a single-hole wrapper.</returns>
    private static bool IsPlainStringInterpolation(SyntaxNodeAnalysisContext context, InterpolationSyntax interpolation)
    {
        if (interpolation.Parent is not InterpolatedStringExpressionSyntax interpolated
            || interpolated.Contents.Count <= 1)
        {
            return false;
        }

        var typeInfo = context.SemanticModel.GetTypeInfo(interpolated, context.CancellationToken);
        return typeInfo.ConvertedType?.SpecialType == SpecialType.System_String;
    }

    /// <summary>Returns whether the outer call's method group has an overload taking the value directly.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="toStringCall">The ToString invocation.</param>
    /// <param name="argument">The argument holding the ToString call.</param>
    /// <param name="outer">The consuming invocation.</param>
    /// <param name="builderType">The StringBuilder type, or <see langword="null"/> when absent.</param>
    /// <returns><see langword="true"/> when a same-shape overload accepts the receiver's type in that position.</returns>
    private static bool HasDirectOverload(
        SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax toStringCall,
        ArgumentSyntax argument,
        InvocationExpressionSyntax outer,
        INamedTypeSymbol? builderType)
    {
        var model = context.SemanticModel;
        var access = (MemberAccessExpressionSyntax)toStringCall.Expression;
        var receiverType = model.GetTypeInfo(access.Expression, context.CancellationToken).Type;
        if (!IsDirectlyPassableValue(receiverType) || argument.NameColon is not null)
        {
            return false;
        }

        if (model.GetSymbolInfo(outer, context.CancellationToken).Symbol is not IMethodSymbol method
            || method.IsExtensionMethod
            || method.ReducedFrom is not null
            || SymbolEqualityComparer.Default.Equals(method.ContainingType, builderType))
        {
            return false;
        }

        var index = ((ArgumentListSyntax)argument.Parent!).Arguments.IndexOf(argument);
        if (index >= method.Parameters.Length
            || method.Parameters[index].Type.SpecialType != SpecialType.System_String)
        {
            return false;
        }

        return FindDirectOverload(context, method, index, receiverType!);
    }

    /// <summary>Returns whether a ToString receiver's type is worth passing directly.</summary>
    /// <param name="receiverType">The receiver's type.</param>
    /// <returns><see langword="false"/> for strings, dynamic values, and type parameters.</returns>
    private static bool IsDirectlyPassableValue(ITypeSymbol? receiverType)
        => receiverType is not null
            && receiverType.SpecialType != SpecialType.System_String
            && receiverType.TypeKind is not (TypeKind.Dynamic or TypeKind.TypeParameter);

    /// <summary>Scans the method group for a sibling overload accepting the value's type.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="method">The bound string-taking method.</param>
    /// <param name="index">The parameter position of the ToString result.</param>
    /// <param name="receiverType">The ToString receiver's type.</param>
    /// <returns><see langword="true"/> when a direct overload exists.</returns>
    private static bool FindDirectOverload(SyntaxNodeAnalysisContext context, IMethodSymbol method, int index, ITypeSymbol receiverType)
    {
        foreach (var member in method.ContainingType.GetMembers(method.Name))
        {
            if (member is IMethodSymbol sibling
                && !SymbolEqualityComparer.Default.Equals(sibling, method)
                && !sibling.IsGenericMethod
                && sibling.IsStatic == method.IsStatic
                && sibling.Parameters.Length == method.Parameters.Length
                && AcceptsValueAt(context, sibling, method, index, receiverType))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether a sibling overload matches the method except for a value-typed slot.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="sibling">The candidate overload.</param>
    /// <param name="method">The bound string-taking method.</param>
    /// <param name="index">The parameter position of the ToString result.</param>
    /// <param name="receiverType">The ToString receiver's type.</param>
    /// <returns><see langword="true"/> when the value converts implicitly into the slot and all other parameters match.</returns>
    private static bool AcceptsValueAt(
        SyntaxNodeAnalysisContext context,
        IMethodSymbol sibling,
        IMethodSymbol method,
        int index,
        ITypeSymbol receiverType)
    {
        for (var i = 0; i < sibling.Parameters.Length; i++)
        {
            if (i == index)
            {
                continue;
            }

            if (!SymbolEqualityComparer.Default.Equals(sibling.Parameters[i].Type, method.Parameters[i].Type))
            {
                return false;
            }
        }

        var slotType = sibling.Parameters[index].Type;
        if (slotType.SpecialType is SpecialType.System_Object or SpecialType.System_String)
        {
            return false;
        }

        var conversion = context.SemanticModel.Compilation.ClassifyConversion(receiverType, slotType);
        return conversion.IsIdentity || (conversion.IsImplicit && !conversion.IsUserDefined && !conversion.IsBoxing);
    }
}
