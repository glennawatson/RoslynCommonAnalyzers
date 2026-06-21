// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>Shared syntax helpers for the collection-expression conversion rules.</summary>
internal static class CollectionExpressionAdvancedAnalysis
{
    /// <summary>Returns whether the referenced framework exposes collection-expression runtime support.</summary>
    /// <param name="compilation">The compilation.</param>
    /// <returns><see langword="true"/> when <c>CollectionBuilderAttribute</c> is available.</returns>
    public static bool HasCollectionBuilderAttribute(Compilation compilation)
        => compilation.GetTypeByMetadataName("System.Runtime.CompilerServices.CollectionBuilderAttribute") is not null;

    /// <summary>Gets an initializer from an array or stackalloc collection source.</summary>
    /// <param name="expression">The candidate expression.</param>
    /// <param name="initializer">The initializer.</param>
    /// <returns><see langword="true"/> when an initializer was found.</returns>
    public static bool TryGetInlineInitializer(ExpressionSyntax expression, out InitializerExpressionSyntax initializer)
    {
        initializer = expression switch
        {
            ArrayCreationExpressionSyntax array => array.Initializer!,
            ImplicitArrayCreationExpressionSyntax array => array.Initializer,
            StackAllocArrayCreationExpressionSyntax stackallocArray => stackallocArray.Initializer!,
            ImplicitStackAllocArrayCreationExpressionSyntax stackallocArray => stackallocArray.Initializer,
            _ => null!
        };

        return initializer is not null;
    }

    /// <summary>Returns whether an expression is a stackalloc initializer.</summary>
    /// <param name="expression">The expression.</param>
    /// <param name="initializer">The initializer.</param>
    /// <returns><see langword="true"/> for explicit and implicit stackalloc initializers.</returns>
    public static bool TryGetStackallocInitializer(ExpressionSyntax expression, out InitializerExpressionSyntax initializer)
    {
        initializer = expression switch
        {
            StackAllocArrayCreationExpressionSyntax stackallocArray => stackallocArray.Initializer!,
            ImplicitStackAllocArrayCreationExpressionSyntax stackallocArray => stackallocArray.Initializer,
            _ => null!
        };

        return initializer is not null;
    }

    /// <summary>Gets a collection-expression replacement for an inline initializer.</summary>
    /// <param name="initializer">The initializer.</param>
    /// <returns>The collection expression text.</returns>
    public static string CollectionExpressionText(InitializerExpressionSyntax initializer)
    {
        var full = initializer.ToFullString().AsSpan();
        return "[" + full[1..^1].ToString() + "]";
    }

    /// <summary>Gets a collection-expression replacement for a factory or fluent invocation.</summary>
    /// <param name="invocation">The invocation.</param>
    /// <param name="text">The replacement text.</param>
    /// <returns><see langword="true"/> when a replacement can be built from syntax.</returns>
    public static bool TryBuildInvocationCollectionExpression(InvocationExpressionSyntax invocation, out string text)
    {
        text = string.Empty;
        if (invocation.Expression is not MemberAccessExpressionSyntax { Name.Identifier.ValueText: var name } access)
        {
            return false;
        }

        if ((name == "ToArray" || name == "ToList")
            && invocation.ArgumentList.Arguments.Count == 0
            && TryGetInlineInitializer(access.Expression, out var initializer))
        {
            text = CollectionExpressionText(initializer);
            return true;
        }

        if (name is not ("Create" or "CreateRange"))
        {
            return false;
        }

        return TryBuildCreateExpression(invocation, name == "CreateRange", out text);
    }

    /// <summary>Returns whether the invocation's target type is backed by the invoked collection-builder method.</summary>
    /// <param name="targetType">The target type.</param>
    /// <param name="method">The invoked method.</param>
    /// <returns><see langword="true"/> when the target's collection builder points at the invoked method.</returns>
    public static bool TargetUsesBuilderMethod(ITypeSymbol? targetType, IMethodSymbol method)
    {
        if (targetType is not INamedTypeSymbol namedTarget
            || !method.IsStatic)
        {
            return false;
        }

        var methodName = method.Name;
        var attributes = namedTarget.OriginalDefinition.GetAttributes();
        for (var i = 0; i < attributes.Length; i++)
        {
            var attribute = attributes[i];
            if (!IsCollectionBuilderAttribute(attribute)
                || attribute.ConstructorArguments.Length != 2
                || attribute.ConstructorArguments[0].Value is not INamedTypeSymbol builderType
                || attribute.ConstructorArguments[1].Value is not string builderMethodName
                || !string.Equals(builderMethodName, methodName, StringComparison.Ordinal))
            {
                continue;
            }

            return SymbolEqualityComparer.Default.Equals(builderType.OriginalDefinition, method.ContainingType.OriginalDefinition);
        }

        return false;
    }

    /// <summary>Returns whether the target can receive a collection expression without a builder variable.</summary>
    /// <param name="type">The target type.</param>
    /// <returns><see langword="true"/> for arrays and collection-builder-backed named types.</returns>
    public static bool IsCollectionExpressionTarget(ITypeSymbol? type)
    {
        if (type is IArrayTypeSymbol { Rank: 1 })
        {
            return true;
        }

        if (type is not INamedTypeSymbol named)
        {
            return false;
        }

        return HasCollectionBuilderAttribute(named);
    }

    /// <summary>Returns whether a method is one of the LINQ materialization calls handled by the fluent rule.</summary>
    /// <param name="method">The resolved method symbol.</param>
    /// <returns><see langword="true"/> for <c>Enumerable.ToArray</c> and <c>Enumerable.ToList</c>.</returns>
    public static bool IsLinqMaterialization(IMethodSymbol method)
    {
        var original = method.ReducedFrom ?? method;
        if (!original.IsExtensionMethod || original.Name is not ("ToArray" or "ToList"))
        {
            return false;
        }

        return original.ContainingType is
        {
            Name: "Enumerable",
            ContainingNamespace: { Name: "Linq", ContainingNamespace.Name: "System" }
        };
    }

    /// <summary>Gets the initialized element expressions from a narrow builder pattern.</summary>
    /// <param name="local">The builder declaration.</param>
    /// <param name="elements">The element expressions.</param>
    /// <param name="returnStatement">The conversion return statement.</param>
    /// <returns><see langword="true"/> when a compact builder sequence was found.</returns>
    public static bool TryGetBuilderSequence(
        LocalDeclarationStatementSyntax local,
        out ExpressionSyntax[] elements,
        out ReturnStatementSyntax returnStatement)
    {
        elements = [];
        returnStatement = null!;
        return TryGetBuilderLocal(local, out var builderName, out var block, out var index)
            && TryCollectBuilderElements(block, index, builderName, out elements, out returnStatement);
    }

    /// <summary>Returns whether an invocation creates one of the supported builder locals.</summary>
    /// <param name="invocation">The invocation.</param>
    /// <returns><see langword="true"/> for narrow ImmutableArray and ArrayBuilder creation calls.</returns>
    public static bool IsBuilderCreation(InvocationExpressionSyntax invocation)
        => invocation.Expression is MemberAccessExpressionSyntax { Name.Identifier.ValueText: "CreateBuilder" or "GetInstance" };

    /// <summary>Returns whether a type has a collection builder attribute.</summary>
    /// <param name="named">The named type.</param>
    /// <returns><see langword="true"/> when the type declares the attribute.</returns>
    private static bool HasCollectionBuilderAttribute(INamedTypeSymbol named)
    {
        var attributes = named.OriginalDefinition.GetAttributes();
        for (var i = 0; i < attributes.Length; i++)
        {
            if (IsCollectionBuilderAttribute(attributes[i]))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether an attribute is <c>CollectionBuilderAttribute</c>.</summary>
    /// <param name="attribute">The attribute data.</param>
    /// <returns><see langword="true"/> for the framework collection builder attribute.</returns>
    private static bool IsCollectionBuilderAttribute(AttributeData attribute)
        => attribute.AttributeClass is
        {
            Name: "CollectionBuilderAttribute",
            ContainingNamespace.Name: "CompilerServices"
        };

    /// <summary>Gets the builder local identity and containing statement index.</summary>
    /// <param name="local">The local declaration.</param>
    /// <param name="builderName">The builder variable name.</param>
    /// <param name="block">The containing block.</param>
    /// <param name="index">The statement index.</param>
    /// <returns><see langword="true"/> when the declaration matches the supported builder shape.</returns>
    private static bool TryGetBuilderLocal(
        LocalDeclarationStatementSyntax local,
        out string builderName,
        out BlockSyntax block,
        out int index)
    {
        builderName = string.Empty;
        block = null!;
        index = -1;
        if (local.Declaration.Variables.Count != 1
            || local.Declaration.Variables[0] is not { Identifier.ValueText: { Length: > 0 } name, Initializer.Value: InvocationExpressionSyntax initializer }
            || !IsBuilderCreation(initializer)
            || local.Parent is not BlockSyntax parentBlock
            || !TryGetStatementIndex(parentBlock, local, out var localIndex))
        {
            return false;
        }

        builderName = name;
        block = parentBlock;
        index = localIndex;
        return true;
    }

    /// <summary>Collects the builder Add sequence and terminal return statement.</summary>
    /// <param name="block">The containing block.</param>
    /// <param name="index">The builder declaration index.</param>
    /// <param name="builderName">The builder variable name.</param>
    /// <param name="elements">The collected element expressions.</param>
    /// <param name="returnStatement">The terminal return statement.</param>
    /// <returns><see langword="true"/> when the sequence is compact and complete.</returns>
    private static bool TryCollectBuilderElements(
        BlockSyntax block,
        int index,
        string builderName,
        out ExpressionSyntax[] elements,
        out ReturnStatementSyntax returnStatement)
    {
        elements = [];
        returnStatement = null!;
        var maxElements = block.Statements.Count - index - 2;
        if (maxElements <= 0)
        {
            return false;
        }

        var collected = new ExpressionSyntax[maxElements];
        var count = 0;
        for (var i = index + 1; i < block.Statements.Count; i++)
        {
            if (block.Statements[i] is ReturnStatementSyntax candidateReturn)
            {
                if (count == 0 || !IsBuilderMaterialization(candidateReturn.Expression, builderName))
                {
                    return false;
                }

                elements = Resize(collected, count);
                returnStatement = candidateReturn;
                return true;
            }

            if (!TryGetBuilderAdd(block.Statements[i], builderName, out var element))
            {
                return false;
            }

            collected[count] = element;
            count++;
        }

        return false;
    }

    /// <summary>Gets the element from a <c>builder.Add(element)</c> statement.</summary>
    /// <param name="statement">The statement.</param>
    /// <param name="builderName">The builder local name.</param>
    /// <param name="element">The added element.</param>
    /// <returns><see langword="true"/> for a simple Add call.</returns>
    private static bool TryGetBuilderAdd(StatementSyntax statement, string builderName, out ExpressionSyntax element)
    {
        element = null!;
        if (!TryGetBuilderAddParts(statement, out var receiver, out var value)
            || !string.Equals(receiver.Identifier.ValueText, builderName, StringComparison.Ordinal))
        {
            return false;
        }

        element = value;
        return true;
    }

    /// <summary>Returns whether an expression materializes the supported builder local.</summary>
    /// <param name="expression">The return expression.</param>
    /// <param name="builderName">The builder local name.</param>
    /// <returns><see langword="true"/> for simple ToImmutable/ToArray conversion calls.</returns>
    private static bool IsBuilderMaterialization(ExpressionSyntax? expression, string builderName)
    {
        if (!TryGetMaterializationReceiver(expression, out var receiver))
        {
            return false;
        }

        return string.Equals(receiver.Identifier.ValueText, builderName, StringComparison.Ordinal);
    }

    /// <summary>Gets the receiver and value for a simple builder Add statement.</summary>
    /// <param name="statement">The statement.</param>
    /// <param name="receiver">The Add call receiver.</param>
    /// <param name="value">The value expression.</param>
    /// <returns><see langword="true"/> when the statement is a single-argument Add call.</returns>
    private static bool TryGetBuilderAddParts(StatementSyntax statement, out IdentifierNameSyntax receiver, out ExpressionSyntax value)
    {
        receiver = null!;
        value = null!;
        if (statement is not ExpressionStatementSyntax { Expression: InvocationExpressionSyntax invocation }
            || invocation.Expression is not MemberAccessExpressionSyntax access
            || invocation.ArgumentList.Arguments is not [ArgumentSyntax { Expression: { } argumentValue }]
            || access.Name.Identifier.ValueText != "Add"
            || access.Expression is not IdentifierNameSyntax receiverName)
        {
            return false;
        }

        receiver = receiverName;
        value = argumentValue;
        return true;
    }

    /// <summary>Gets the builder receiver from a supported materialization call.</summary>
    /// <param name="expression">The return expression.</param>
    /// <param name="receiver">The materialization receiver.</param>
    /// <returns><see langword="true"/> when the expression materializes the builder.</returns>
    private static bool TryGetMaterializationReceiver(ExpressionSyntax? expression, out IdentifierNameSyntax receiver)
    {
        receiver = null!;
        if (expression is not InvocationExpressionSyntax invocation
            || invocation.ArgumentList.Arguments.Count != 0
            || invocation.Expression is not MemberAccessExpressionSyntax access
            || access.Name.Identifier.ValueText is not ("ToImmutable" or "ToImmutableAndClear" or "ToImmutableAndFree" or "ToArray" or "ToArrayAndFree")
            || access.Expression is not IdentifierNameSyntax receiverName)
        {
            return false;
        }

        receiver = receiverName;
        return true;
    }

    /// <summary>Builds a replacement for a collection factory call.</summary>
    /// <param name="invocation">The invocation.</param>
    /// <param name="isRange">Whether the factory is a range factory.</param>
    /// <param name="text">The collection expression text.</param>
    /// <returns><see langword="true"/> when the replacement can be built.</returns>
    private static bool TryBuildCreateExpression(InvocationExpressionSyntax invocation, bool isRange, out string text)
    {
        text = string.Empty;
        var arguments = invocation.ArgumentList.Arguments;
        if (arguments.Count == 0)
        {
            text = "[]";
            return true;
        }

        if (arguments.Count == 1 && TryGetInlineInitializer(arguments[0].Expression, out var initializer))
        {
            text = CollectionExpressionText(initializer);
            return true;
        }

        if (isRange && arguments.Count == 1)
        {
            text = "[.. " + arguments[0].Expression.WithoutTrivia() + "]";
            return true;
        }

        var builder = new System.Text.StringBuilder();
        builder.Append('[');
        for (var i = 0; i < arguments.Count; i++)
        {
            if (i > 0)
            {
                builder.Append(", ");
            }

            builder.Append(arguments[i].Expression.WithoutTrivia());
        }

        builder.Append(']');
        text = builder.ToString();
        return true;
    }

    /// <summary>Gets a statement index inside a block.</summary>
    /// <param name="block">The block.</param>
    /// <param name="statement">The statement.</param>
    /// <param name="index">The index.</param>
    /// <returns><see langword="true"/> when the statement is in the block.</returns>
    private static bool TryGetStatementIndex(BlockSyntax block, StatementSyntax statement, out int index)
    {
        for (var i = 0; i < block.Statements.Count; i++)
        {
            if (block.Statements[i].Span == statement.Span)
            {
                index = i;
                return true;
            }
        }

        index = -1;
        return false;
    }

    /// <summary>Trims a collected expression array to the populated length.</summary>
    /// <param name="source">The source array.</param>
    /// <param name="count">The populated count.</param>
    /// <returns>The trimmed array.</returns>
    private static ExpressionSyntax[] Resize(ExpressionSyntax[] source, int count)
    {
        if (source.Length == count)
        {
            return source;
        }

        var resized = new ExpressionSyntax[count];
        Array.Copy(source, resized, count);
        return resized;
    }
}
