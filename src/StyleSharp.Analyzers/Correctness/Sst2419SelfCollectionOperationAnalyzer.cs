// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a set or list operation applied to the very collection it is called on (SST2419):
/// <c>set.UnionWith(set)</c>, <c>set.ExceptWith(set)</c>, <c>list.AddRange(list)</c>, and the like. Each is a
/// no-op, a constant, or a silent wipe, and almost always a copy-and-paste where the argument should have
/// named a different collection.
/// </summary>
/// <remarks>
/// Driven off the <c>ISet&lt;T&gt;</c> and <c>IList&lt;T&gt;</c> interfaces rather than a concrete type, so
/// it covers every implementation. The clean path is a switch on the method-name token that rejects
/// essentially every call before the receiver's type is resolved. No fix is offered: whether the argument or
/// the doubling was the mistake is genuinely ambiguous.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2419SelfCollectionOperationAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The name of the list insert-range operation, whose collection argument is second.</summary>
    private const string InsertRange = "InsertRange";

    /// <summary>The metadata name of the generic set interface.</summary>
    private const string SetInterfaceMetadataName = "System.Collections.Generic.ISet`1";

    /// <summary>The metadata name of the generic list interface.</summary>
    private const string ListInterfaceMetadataName = "System.Collections.Generic.IList`1";

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(CorrectnessRules.SelfCollectionOperation);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterCompilationStartAction(OnCompilationStart);
    }

    /// <summary>Resolves the collection interfaces once, then analyzes each call.</summary>
    /// <param name="context">The compilation start context.</param>
    private static void OnCompilationStart(CompilationStartAnalysisContext context)
    {
        var setInterface = context.Compilation.GetTypeByMetadataName(SetInterfaceMetadataName);
        var listInterface = context.Compilation.GetTypeByMetadataName(ListInterfaceMetadataName);
        if (setInterface is null && listInterface is null)
        {
            return;
        }

        context.RegisterSyntaxNodeAction(nodeContext => Analyze(nodeContext, setInterface, listInterface), SyntaxKind.InvocationExpression);
    }

    /// <summary>Reports one self-applied collection operation.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <param name="setInterface">The resolved <c>ISet&lt;T&gt;</c> definition, if any.</param>
    /// <param name="listInterface">The resolved <c>IList&lt;T&gt;</c> definition, if any.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context, INamedTypeSymbol? setInterface, INamedTypeSymbol? listInterface)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (invocation.Expression is not MemberAccessExpressionSyntax member)
        {
            return;
        }

        var name = member.Name.Identifier.ValueText;
        if (!IsSelfCollectionMethod(name))
        {
            return;
        }

        var arguments = invocation.ArgumentList.Arguments;
        var selfIndex = SelfArgumentIndex(name);
        if (arguments.Count <= selfIndex || !SameSideEffectFree(member.Expression, arguments[selfIndex].Expression))
        {
            return;
        }

        var required = IsListMethod(name) ? listInterface : setInterface;
        var receiverType = context.SemanticModel.GetTypeInfo(member.Expression, context.CancellationToken).Type;
        if (required is null || receiverType is null || !Implements(receiverType, required))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(CorrectnessRules.SelfCollectionOperation, invocation.GetLocation(), name, Effect(name)));
    }

    /// <summary>Returns whether a method name is one of the self-applicable collection operations.</summary>
    /// <param name="name">The method name.</param>
    /// <returns><see langword="true"/> for a known set or list operation.</returns>
    private static bool IsSelfCollectionMethod(string name)
        => name is "UnionWith" or "IntersectWith" or "ExceptWith" or "SymmetricExceptWith"
            or "SetEquals" or "IsSubsetOf" or "IsSupersetOf" or "AddRange" or InsertRange;

    /// <summary>Returns whether a method name is a list operation.</summary>
    /// <param name="name">The method name.</param>
    /// <returns><see langword="true"/> for <c>AddRange</c> or <c>InsertRange</c>.</returns>
    private static bool IsListMethod(string name) => name is "AddRange" or InsertRange;

    /// <summary>Gets the argument position that should hold the self-collection.</summary>
    /// <param name="name">The method name.</param>
    /// <returns>The argument index.</returns>
    private static int SelfArgumentIndex(string name) => name == InsertRange ? 1 : 0;

    /// <summary>Describes what a self-applied operation actually does.</summary>
    /// <param name="name">The method name.</param>
    /// <returns>The effect phrase.</returns>
    private static string Effect(string name)
    {
        if (name is "ExceptWith" or "SymmetricExceptWith")
        {
            return "this clears the collection";
        }

        if (name is "SetEquals" or "IsSubsetOf" or "IsSupersetOf")
        {
            return "this is always true";
        }

        if (name is "AddRange" or InsertRange)
        {
            return "this doubles the collection";
        }

        return "the collection cannot change";
    }

    /// <summary>Returns whether a type is, or implements, an interface definition.</summary>
    /// <param name="type">The type to test.</param>
    /// <param name="interfaceDefinition">The interface's unbound definition.</param>
    /// <returns><see langword="true"/> when the type satisfies the interface.</returns>
    private static bool Implements(ITypeSymbol type, INamedTypeSymbol interfaceDefinition)
    {
        if (SymbolEqualityComparer.Default.Equals(type.OriginalDefinition, interfaceDefinition))
        {
            return true;
        }

        var interfaces = type.AllInterfaces;
        for (var i = 0; i < interfaces.Length; i++)
        {
            if (SymbolEqualityComparer.Default.Equals(interfaces[i].OriginalDefinition, interfaceDefinition))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether two expressions are the same side-effect-free expression.</summary>
    /// <param name="first">The first expression.</param>
    /// <param name="second">The second expression.</param>
    /// <returns><see langword="true"/> when evaluating either twice is provably the same.</returns>
    private static bool SameSideEffectFree(ExpressionSyntax first, ExpressionSyntax second)
        => SideEffectFreeExpression.IsSideEffectFree(first)
            && SyntaxFactory.AreEquivalent(first, second, topLevel: false);
}
