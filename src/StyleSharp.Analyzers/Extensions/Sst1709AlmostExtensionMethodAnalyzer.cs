// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.CSharp;

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a public or internal static method in a class named <c>…Extensions</c> whose first parameter
/// has no <c>this</c> modifier — a helper that reads like an extension that was never wired up (SST1709).
/// Disabled by default: legitimate plain helpers live in these classes too. The rule is gated on the
/// compilation's language version being C# 14 or later, because its fix converts the method into an
/// <c>extension(Receiver) { … }</c> block, which the earlier language cannot express. Only non-generic
/// methods with a body and a plain first parameter are reported, so the offered conversion is always valid.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst1709AlmostExtensionMethodAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The numeric value of <c>LanguageVersion.CSharp14</c>, the first version with extension blocks.</summary>
    private const int CSharp14 = 1400;

    /// <summary>The container-class name suffix that marks an extension helper class.</summary>
    private const string ExtensionSuffix = "Extensions";

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(ExtensionRules.AlmostExtensionMethod);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.MethodDeclaration);
    }

    /// <summary>Returns whether a method is an almost-extension helper that the fix can convert.</summary>
    /// <param name="method">The method declaration.</param>
    /// <returns><see langword="true"/> when the shape is a convertible almost-extension.</returns>
    internal static bool IsAlmostExtensionMethod(MethodDeclarationSyntax method)
    {
        if (method.TypeParameterList is not null
            || (method.Body is null && method.ExpressionBody is null)
            || method.Parent is not ClassDeclarationSyntax containingClass
            || !containingClass.Identifier.ValueText.EndsWith(ExtensionSuffix, StringComparison.Ordinal)
            || !ModifierListHelper.Contains(method.Modifiers, SyntaxKind.StaticKeyword)
            || !ModifierListHelper.ContainsEither(method.Modifiers, SyntaxKind.PublicKeyword, SyntaxKind.InternalKeyword))
        {
            return false;
        }

        var parameters = method.ParameterList.Parameters;
        return parameters.Count > 0 && parameters[0] is { Type: not null, Modifiers.Count: 0 };
    }

    /// <summary>Reports an almost-extension helper when the language supports extension blocks.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        if (context.Node.SyntaxTree.Options is not CSharpParseOptions options || (int)options.LanguageVersion < CSharp14)
        {
            return;
        }

        var method = (MethodDeclarationSyntax)context.Node;
        if (!IsAlmostExtensionMethod(method))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            ExtensionRules.AlmostExtensionMethod,
            method.Identifier.GetLocation(),
            method.Identifier.ValueText));
    }
}
