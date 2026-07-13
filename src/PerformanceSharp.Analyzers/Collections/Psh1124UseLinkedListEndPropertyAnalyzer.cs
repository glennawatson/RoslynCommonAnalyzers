// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Flags the parameterless <c>Enumerable.First()</c> and <c>Enumerable.Last()</c> extensions called
/// on a <c>LinkedList&lt;T&gt;</c> (PSH1124). The list holds a reference to both of its ends, so its
/// own <c>First</c> and <c>Last</c> properties are constant-time reads. The LINQ extensions know
/// nothing about the receiver: they enumerate through the interface — boxing the list's struct
/// enumerator — and <c>Last()</c> walks every node to arrive where the list could have pointed
/// straight away.
/// </summary>
/// <remarks>
/// The properties return <c>LinkedListNode&lt;T&gt;</c> while the extensions return <c>T</c>, so the
/// fix is <c>list.First.Value</c>, never <c>list.First</c>. The rule matches on the invoked name
/// before it binds anything, and reports only the overloads whose single parameter is the source —
/// a predicate overload is asking a different question. The call must bind to
/// <c>System.Linq.Enumerable</c>, which keeps <c>Queryable</c> out, and the receiver's <em>static</em>
/// type must be the linked list: the properties do not exist on the interfaces it implements.
/// <c>FirstOrDefault</c>/<c>LastOrDefault</c> are left alone; they answer an empty list with
/// <c>default(T)</c>, which no property read reproduces without changing the expression's type.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Psh1124UseLinkedListEndPropertyAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The first-element member name, shared by the extension and the property.</summary>
    internal const string FirstMemberName = "First";

    /// <summary>The last-element member name, shared by the extension and the property.</summary>
    internal const string LastMemberName = "Last";

    /// <summary>The member name that reads a node's element.</summary>
    internal const string ValueMemberName = "Value";

    /// <summary>How the first-element extension call is written into the message.</summary>
    private const string FirstCallText = "First()";

    /// <summary>How the last-element extension call is written into the message.</summary>
    private const string LastCallText = "Last()";

    /// <summary>The metadata name of the LINQ extension class.</summary>
    private const string EnumerableMetadataName = "System.Linq.Enumerable";

    /// <summary>The unqualified name of the linked list type.</summary>
    private const string LinkedListTypeName = "LinkedList";

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(CollectionRules.UseLinkedListEndProperty);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(start =>
        {
            if (start.Compilation.GetTypeByMetadataName(EnumerableMetadataName) is not { } enumerableType)
            {
                return;
            }

            start.RegisterSyntaxNodeAction(nodeContext => AnalyzeInvocation(nodeContext, enumerableType), SyntaxKind.InvocationExpression);
        });
    }

    /// <summary>Returns whether an invocation is a parameterless <c>First</c>/<c>Last</c> member call, before any binding.</summary>
    /// <param name="invocation">The invocation to inspect.</param>
    /// <returns><see langword="true"/> when the call has the end-element extension shape.</returns>
    internal static bool IsEndExtensionShape(InvocationExpressionSyntax invocation)
        => invocation.ArgumentList.Arguments.Count == 0
            && invocation.Expression is MemberAccessExpressionSyntax memberAccess
            && memberAccess.IsKind(SyntaxKind.SimpleMemberAccessExpression)
            && memberAccess.Name.Identifier.ValueText is FirstMemberName or LastMemberName;

    /// <summary>Reports PSH1124 for a linked list whose end element is fetched through LINQ.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="enumerableType">The LINQ extension class.</param>
    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context, INamedTypeSymbol enumerableType)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (!IsEndExtensionShape(invocation))
        {
            return;
        }

        var memberAccess = (MemberAccessExpressionSyntax)invocation.Expression;
        var memberName = memberAccess.Name.Identifier.ValueText;
        if (!IsSourceOnlyEnumerableExtension(context, invocation, enumerableType)
            || !HasEndNodeProperty(context.SemanticModel.GetTypeInfo(memberAccess.Expression, context.CancellationToken).Type, memberName))
        {
            return;
        }

        var isFirst = memberName == FirstMemberName;
        context.ReportDiagnostic(DiagnosticHelper.Create(
            CollectionRules.UseLinkedListEndProperty,
            memberAccess.Name.GetLocation(),
            isFirst ? FirstCallText : LastCallText,
            isFirst ? FirstMemberName : LastMemberName));
    }

    /// <summary>Returns whether an invocation binds to an Enumerable extension whose only parameter is the source.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="invocation">The invocation to bind.</param>
    /// <param name="enumerableType">The LINQ extension class.</param>
    /// <returns><see langword="true"/> when the call is a reduced source-only Enumerable extension.</returns>
    private static bool IsSourceOnlyEnumerableExtension(
        SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax invocation,
        INamedTypeSymbol enumerableType)
        => context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is IMethodSymbol { ReducedFrom: { Parameters.Length: 1 } reduced }
            && SymbolEqualityComparer.Default.Equals(reduced.ContainingType, enumerableType);

    /// <summary>
    /// Returns whether the receiver is a linked list that really does expose the end property this
    /// rule is about to suggest, holding a node with a readable value. The replacement is proved
    /// against the analyzed compilation rather than assumed from the type's name, so a target
    /// framework whose linked list lacked the property could never be told to use it.
    /// </summary>
    /// <param name="type">The receiver's static type.</param>
    /// <param name="memberName">The end member the call would be rewritten to.</param>
    /// <returns><see langword="true"/> when the property and the node's value are both available.</returns>
    private static bool HasEndNodeProperty(ITypeSymbol? type, string memberName)
        => IsLinkedList(type, out var linkedList) && ExposesNodeProperty(linkedList, memberName);

    /// <summary>Returns whether a receiver's static type is <c>System.Collections.Generic.LinkedList&lt;T&gt;</c>.</summary>
    /// <param name="type">The receiver's static type.</param>
    /// <param name="linkedList">The linked list type.</param>
    /// <returns><see langword="true"/> for the linked list; its interfaces do not count.</returns>
    private static bool IsLinkedList(ITypeSymbol? type, [NotNullWhen(true)] out INamedTypeSymbol? linkedList)
    {
        linkedList = type as INamedTypeSymbol;
        return linkedList is { OriginalDefinition: { Name: LinkedListTypeName, Arity: 1 } definition }
            && definition.ContainingNamespace is
            {
                Name: "Generic",
                ContainingNamespace: { Name: "Collections", ContainingNamespace: { Name: "System", ContainingNamespace.IsGlobalNamespace: true } },
            };
    }

    /// <summary>Returns whether a linked list exposes the named end property, holding a node with a value.</summary>
    /// <param name="linkedList">The linked list type.</param>
    /// <param name="memberName">The end member name.</param>
    /// <returns><see langword="true"/> when the property and <c>LinkedListNode&lt;T&gt;.Value</c> both exist.</returns>
    private static bool ExposesNodeProperty(INamedTypeSymbol linkedList, string memberName)
    {
        var members = linkedList.GetMembers(memberName);
        for (var index = 0; index < members.Length; index++)
        {
            if (members[index] is IPropertySymbol { Type: INamedTypeSymbol node } && !node.GetMembers(ValueMemberName).IsEmpty)
            {
                return true;
            }
        }

        return false;
    }
}
