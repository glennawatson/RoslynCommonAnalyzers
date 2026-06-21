// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>Shared syntax-first helpers for the modern readability analyzer and its code fixes.</summary>
internal static class ModernSyntaxReadabilityAnalysis
{
    /// <summary>Diagnostic property that records whether a UTF-8 literal replacement needs <c>ToArray()</c>.</summary>
    public const string Utf8TargetKey = "Utf8Target";

    /// <summary>Diagnostic property value for <see cref="ReadOnlySpan{T}"/> byte targets.</summary>
    public const string Utf8SpanTarget = "Span";

    /// <summary>Diagnostic property value for <c>byte[]</c> targets.</summary>
    public const string Utf8ArrayTarget = "Array";

    /// <summary>The minimum input count supported by <c>System.HashCode.Combine</c>.</summary>
    public const int HashCodeCombineMinInputs = 2;

    /// <summary>The maximum input count supported by <c>System.HashCode.Combine</c>.</summary>
    public const int HashCodeCombineMaxInputs = 8;

    /// <summary>The multiplier commonly used by generated hash-code implementations.</summary>
    private const int HashMultiplier397 = 397;

    /// <summary>The multiplier commonly used by generated hash-code implementations.</summary>
    private const int HashMultiplier31 = 31;

    /// <summary>The number of statements in a local-swap pattern after the temporary declaration.</summary>
    private const int SwapAssignmentCount = 2;

    /// <summary>The number of tuple element locals currently rewritten by the code fix.</summary>
    private const int SupportedDeconstructionElementCount = 2;

    /// <summary>UTF-8 decoder that throws on invalid byte sequences.</summary>
    private static readonly System.Text.UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    /// <summary>Returns whether an expression can be replaced with a UTF-8 literal.</summary>
    /// <param name="expression">The candidate expression.</param>
    /// <param name="target">The UTF-8 target kind from the diagnostic.</param>
    /// <param name="replacement">The replacement expression.</param>
    /// <returns><see langword="true"/> when the replacement can be created syntactically.</returns>
    public static bool TryCreateUtf8Replacement(ExpressionSyntax expression, string target, out ExpressionSyntax replacement)
    {
        replacement = null!;
        if (!TryGetUtf8Text(expression, out var text))
        {
            return false;
        }

        var literal = SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression, SyntaxFactory.Literal(text));
        var suffix = target == Utf8ArrayTarget ? "u8.ToArray()" : "u8";
        replacement = SyntaxFactory.ParseExpression(literal.Token.Text + suffix).WithTriviaFrom(expression);
        return true;
    }

    /// <summary>Returns whether a tuple argument repeats an inferred element name.</summary>
    /// <param name="argument">The tuple argument.</param>
    /// <param name="inferredName">The inferred name.</param>
    /// <returns><see langword="true"/> when the name can be omitted.</returns>
    public static bool TryGetInferredTupleElementName(ArgumentSyntax argument, out string inferredName)
    {
        inferredName = string.Empty;
        if (argument.NameColon is null || ExpressionSimplificationAnalyzer.InferredName(argument.Expression) is not { } inferred)
        {
            return false;
        }

        inferredName = inferred;
        return string.Equals(argument.NameColon.Name.Identifier.ValueText, inferred, StringComparison.Ordinal);
    }

    /// <summary>Finds tuple element local declarations following a tuple temporary.</summary>
    /// <param name="local">The tuple temporary declaration.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="cancellationToken">A token that cancels analysis.</param>
    /// <param name="candidate">The deconstruction candidate.</param>
    /// <returns><see langword="true"/> when a conservative deconstruction was found.</returns>
    public static bool TryGetDeconstructionCandidate(
        LocalDeclarationStatementSyntax local,
        SemanticModel model,
        CancellationToken cancellationToken,
        out DeconstructionCandidate candidate)
    {
        candidate = default;
        if (!TryGetTupleTemporary(local, model, cancellationToken, out var temporary)
            || temporary.TupleElements.Length != SupportedDeconstructionElementCount
            || !TryReadTupleElementLocals(temporary, model, cancellationToken)
            || IdentifierAppearsAfter(
                temporary.Block,
                temporary.Variable.Identifier.ValueText,
                temporary.StatementIndex + temporary.TupleElements.Length + 1))
        {
            return false;
        }

        candidate = new DeconstructionCandidate(local);
        return true;
    }

    /// <summary>Finds a three-statement local swap that can use tuple assignment.</summary>
    /// <param name="local">The temporary local declaration.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="cancellationToken">A token that cancels analysis.</param>
    /// <param name="firstAssignment">The first assignment statement.</param>
    /// <param name="secondAssignment">The second assignment statement.</param>
    /// <returns><see langword="true"/> when the local declaration starts a conservative swap.</returns>
    public static bool TryGetTupleSwapCandidate(
        LocalDeclarationStatementSyntax local,
        SemanticModel model,
        CancellationToken cancellationToken,
        out ExpressionStatementSyntax firstAssignment,
        out ExpressionStatementSyntax secondAssignment)
    {
        firstAssignment = null!;
        secondAssignment = null!;
        if (!TryGetTupleSwapShape(local, out var shape)
            || IdentifierAppearsAfter(shape.Block, shape.TemporaryName, shape.StatementIndex + SwapAssignmentCount + 1)
            || !IsLocalOrParameter(shape.Left, model, cancellationToken)
            || !IsLocalOrParameter(shape.Right, model, cancellationToken))
        {
            return false;
        }

        firstAssignment = shape.FirstAssignment;
        secondAssignment = shape.SecondAssignment;
        return true;
    }

    /// <summary>Collects the inputs from a manual hash-code expression.</summary>
    /// <param name="expression">The hash expression.</param>
    /// <param name="inputs">The hash input expressions.</param>
    /// <returns><see langword="true"/> when inputs were collected.</returns>
    public static bool TryCollectHashInputs(ExpressionSyntax expression, out List<ExpressionSyntax> inputs)
    {
        inputs = new List<ExpressionSyntax>(HashCodeCombineMaxInputs);
        if (!TryCollectHashInputsCore(ExpressionSimplificationAnalyzer.Unwrap(expression), inputs))
        {
            inputs.Clear();
        }

        return inputs.Count >= HashCodeCombineMinInputs;
    }

    /// <summary>Returns whether a manual hash-code expression can be replaced without changing receiver null behaviour.</summary>
    /// <param name="expression">The hash expression.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="cancellationToken">A token that cancels analysis.</param>
    /// <returns><see langword="true"/> when the expression is a safe <c>HashCode.Combine</c> candidate.</returns>
    public static bool HasSafeHashCodeCombineInputs(
        ExpressionSyntax expression,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        var count = 0;
        return TryValidateHashInputsCore(
                ExpressionSimplificationAnalyzer.Unwrap(expression),
                model,
                cancellationToken,
                ref count)
            && count >= HashCodeCombineMinInputs;
    }

    /// <summary>Returns whether the invocation is <c>Encoding.UTF8.GetBytes(string)</c>.</summary>
    /// <param name="invocation">The candidate invocation.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="cancellationToken">A token that cancels analysis.</param>
    /// <returns><see langword="true"/> when the invocation is the supported encoding call.</returns>
    public static bool IsEncodingUtf8GetBytes(InvocationExpressionSyntax invocation, SemanticModel model, CancellationToken cancellationToken)
    {
        if (invocation.Expression is not MemberAccessExpressionSyntax
            {
                Name.Identifier.ValueText: "GetBytes",
                Expression: MemberAccessExpressionSyntax { Name.Identifier.ValueText: "UTF8" } utf8Access
            })
        {
            return false;
        }

        return IsUtf8EncodingProperty(model.GetSymbolInfo(utf8Access, cancellationToken).Symbol)
            && IsUtf8GetBytesMethod(model.GetSymbolInfo(invocation, cancellationToken).Symbol);
    }

    /// <summary>Returns whether the expression's converted type can receive a UTF-8 literal replacement.</summary>
    /// <param name="expression">The candidate expression.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="cancellationToken">A token that cancels analysis.</param>
    /// <param name="target">The target kind.</param>
    /// <returns><see langword="true"/> for byte array and read-only byte span targets.</returns>
    public static bool TryGetUtf8Target(ExpressionSyntax expression, SemanticModel model, CancellationToken cancellationToken, out string target)
    {
        target = string.Empty;
        var type = model.GetTypeInfo(expression, cancellationToken).ConvertedType;
        if (type is IArrayTypeSymbol { ElementType.SpecialType: SpecialType.System_Byte })
        {
            target = Utf8ArrayTarget;
        }
        else if (IsReadOnlySpanOfByte(type))
        {
            target = Utf8SpanTarget;
        }

        return target.Length > 0;
    }

    /// <summary>Returns whether the expression is a byte array creation.</summary>
    /// <param name="expression">The candidate expression.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="cancellationToken">A token that cancels analysis.</param>
    /// <returns><see langword="true"/> when the expression's natural type is <c>byte[]</c>.</returns>
    public static bool IsByteArrayExpression(ExpressionSyntax expression, SemanticModel model, CancellationToken cancellationToken)
        => model.GetTypeInfo(expression, cancellationToken).Type is IArrayTypeSymbol { ElementType.SpecialType: SpecialType.System_Byte };

    /// <summary>Decodes a byte-array initializer as UTF-8.</summary>
    /// <param name="expression">The array creation expression.</param>
    /// <param name="text">The decoded text.</param>
    /// <returns><see langword="true"/> when every element is a byte literal and the sequence is valid UTF-8.</returns>
    public static bool TryDecodeUtf8Initializer(ExpressionSyntax expression, out string text)
    {
        text = string.Empty;
        var initializer = expression switch
        {
            ArrayCreationExpressionSyntax array => array.Initializer,
            ImplicitArrayCreationExpressionSyntax array => array.Initializer,
            _ => null
        };

        if (initializer is null || initializer.Expressions.Count == 0)
        {
            return false;
        }

        var bytes = new byte[initializer.Expressions.Count];
        for (var i = 0; i < initializer.Expressions.Count; i++)
        {
            if (!TryGetByteLiteral(initializer.Expressions[i], out bytes[i]))
            {
                return false;
            }
        }

        return TryDecodeStrictUtf8(bytes, out text);
    }

    /// <summary>Returns whether an expression sits inside the current type's <c>GetHashCode</c> member.</summary>
    /// <param name="expression">The candidate expression.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="cancellationToken">A token that cancels analysis.</param>
    /// <returns><see langword="true"/> when the containing method is <c>GetHashCode()</c>.</returns>
    public static bool IsGetHashCodeBody(ExpressionSyntax expression, SemanticModel model, CancellationToken cancellationToken)
        => model.GetEnclosingSymbol(expression.SpanStart, cancellationToken) is IMethodSymbol
        {
            Name: nameof(GetHashCode),
            Parameters.Length: 0,
            ReturnType.SpecialType: SpecialType.System_Int32
        };

    /// <summary>Returns the text represented by a UTF-8 replacement candidate.</summary>
    /// <param name="expression">The candidate expression.</param>
    /// <param name="text">The decoded text.</param>
    /// <returns><see langword="true"/> when the expression carries literal UTF-8 text.</returns>
    private static bool TryGetUtf8Text(ExpressionSyntax expression, out string text)
    {
        text = string.Empty;
        if (expression is InvocationExpressionSyntax { ArgumentList.Arguments: [ArgumentSyntax { Expression: LiteralExpressionSyntax literal }] }
            && literal.IsKind(SyntaxKind.StringLiteralExpression))
        {
            text = literal.Token.ValueText;
            return true;
        }

        return TryDecodeUtf8Initializer(expression, out text);
    }

    /// <summary>Returns whether a symbol is the <c>Encoding.UTF8</c> property.</summary>
    /// <param name="symbol">The candidate symbol.</param>
    /// <returns><see langword="true"/> when the symbol is the expected property.</returns>
    private static bool IsUtf8EncodingProperty(ISymbol? symbol)
        => symbol is IPropertySymbol
        {
            Name: "UTF8",
            ContainingType: { Name: "Encoding", ContainingNamespace: { Name: "Text", ContainingNamespace.Name: "System" } }
        };

    /// <summary>Returns whether a symbol is <c>Encoding.GetBytes(string)</c>.</summary>
    /// <param name="symbol">The candidate symbol.</param>
    /// <returns><see langword="true"/> when the symbol is the supported method.</returns>
    private static bool IsUtf8GetBytesMethod(ISymbol? symbol)
        => symbol is IMethodSymbol
        {
            Name: "GetBytes",
            Parameters.Length: 1,
            Parameters: [{ Type.SpecialType: SpecialType.System_String }],
            ReturnType: IArrayTypeSymbol { ElementType.SpecialType: SpecialType.System_Byte },
            ContainingType: { Name: "Encoding", ContainingNamespace: { Name: "Text", ContainingNamespace.Name: "System" } }
        };

    /// <summary>Returns whether a type is <c>System.ReadOnlySpan&lt;byte&gt;</c>.</summary>
    /// <param name="type">The type to inspect.</param>
    /// <returns><see langword="true"/> for read-only byte spans.</returns>
    private static bool IsReadOnlySpanOfByte(ITypeSymbol? type)
        => type is INamedTypeSymbol
        {
            Name: "ReadOnlySpan",
            TypeArguments: [{ SpecialType: SpecialType.System_Byte }],
            ContainingNamespace: { Name: "System", ContainingNamespace.IsGlobalNamespace: true }
        };

    /// <summary>Returns a literal byte value from an initializer expression.</summary>
    /// <param name="expression">The initializer expression.</param>
    /// <param name="value">The byte value.</param>
    /// <returns><see langword="true"/> when the expression is a byte-sized numeric literal.</returns>
    private static bool TryGetByteLiteral(ExpressionSyntax expression, out byte value)
    {
        value = 0;
        expression = ExpressionSimplificationAnalyzer.Unwrap(expression);
        if (expression is CastExpressionSyntax { Type: PredefinedTypeSyntax { Keyword.RawKind: (int)SyntaxKind.ByteKeyword }, Expression: { } castOperand })
        {
            expression = ExpressionSimplificationAnalyzer.Unwrap(castOperand);
        }

        if (!TryGetIntegralLiteralValue(expression, out var numeric) || numeric is < byte.MinValue or > byte.MaxValue)
        {
            return false;
        }

        value = (byte)numeric;
        return true;
    }

    /// <summary>Reads a numeric literal as a signed 64-bit value when it can fit.</summary>
    /// <param name="expression">The candidate expression.</param>
    /// <param name="value">The numeric value.</param>
    /// <returns><see langword="true"/> when the expression is an integral literal.</returns>
    private static bool TryGetIntegralLiteralValue(ExpressionSyntax expression, out long value)
    {
        value = -1;
        if (expression is not LiteralExpressionSyntax literal)
        {
            return false;
        }

        switch (literal.Token.Value)
        {
            case byte byteValue:
                {
                    value = byteValue;
                    return true;
                }

            case sbyte signedByte:
                {
                    value = signedByte;
                    return true;
                }

            case short shortValue:
                {
                    value = shortValue;
                    return true;
                }

            case ushort unsignedShort:
                {
                    value = unsignedShort;
                    return true;
                }

            case int intValue:
                {
                    value = intValue;
                    return true;
                }

            case uint unsignedInt:
                {
                    value = unsignedInt;
                    return true;
                }

            case long longValue:
                {
                    value = longValue;
                    return true;
                }

            case ulong unsignedLong when unsignedLong <= byte.MaxValue:
                {
                    value = (long)unsignedLong;
                    return true;
                }

            default:
                {
                    return false;
                }
        }
    }

    /// <summary>Decodes UTF-8 with invalid byte checking.</summary>
    /// <param name="bytes">The bytes to decode.</param>
    /// <param name="text">The decoded text.</param>
    /// <returns><see langword="true"/> when the byte sequence is valid UTF-8.</returns>
    private static bool TryDecodeStrictUtf8(byte[] bytes, out string text)
    {
        try
        {
            text = StrictUtf8.GetString(bytes, 0, bytes.Length);
            return true;
        }
        catch (System.Text.DecoderFallbackException)
        {
            text = string.Empty;
            return false;
        }
    }

    /// <summary>Reads the tuple temporary shape at the start of a deconstruction candidate.</summary>
    /// <param name="local">The tuple local.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="cancellationToken">A token that cancels analysis.</param>
    /// <param name="temporary">The tuple temporary details.</param>
    /// <returns><see langword="true"/> when the declaration is a tuple temporary.</returns>
    private static bool TryGetTupleTemporary(
        LocalDeclarationStatementSyntax local,
        SemanticModel model,
        CancellationToken cancellationToken,
        out TupleTemporary temporary)
    {
        temporary = default;
        if (local.Declaration.Variables.Count != 1
            || local.Declaration.Variables[0] is not { Initializer.Value: { } initializer } tupleVariable
            || tupleVariable.Identifier.ValueText.Length == 0
            || local.Parent is not BlockSyntax block
            || !TryGetStatementIndex(block, local, out var index)
            || model.GetTypeInfo(initializer, cancellationToken).Type is not INamedTypeSymbol { IsTupleType: true } tupleType
            || index + tupleType.TupleElements.Length >= block.Statements.Count)
        {
            return false;
        }

        temporary = new TupleTemporary(block, tupleVariable, tupleType.TupleElements, index);
        return true;
    }

    /// <summary>Reads the tuple element local declarations that follow a tuple temporary.</summary>
    /// <param name="temporary">The tuple temporary.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="cancellationToken">A token that cancels analysis.</param>
    /// <returns><see langword="true"/> when every tuple element is copied to a local.</returns>
    private static bool TryReadTupleElementLocals(
        in TupleTemporary temporary,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        for (var i = 0; i < temporary.TupleElements.Length; i++)
        {
            if (temporary.Block.Statements[temporary.StatementIndex + i + 1] is not LocalDeclarationStatementSyntax elementLocal
                || !TryGetTupleElementLocal(
                    elementLocal,
                    temporary.Variable.Identifier.ValueText,
                    temporary.TupleElements[i],
                    model,
                    cancellationToken))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Returns whether a local declaration copies one tuple element to one local.</summary>
    /// <param name="local">The local declaration.</param>
    /// <param name="tupleName">The tuple variable name.</param>
    /// <param name="tupleElement">The expected tuple element.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="cancellationToken">A token that cancels analysis.</param>
    /// <returns><see langword="true"/> when the local reads the expected tuple element.</returns>
    private static bool TryGetTupleElementLocal(
        LocalDeclarationStatementSyntax local,
        string tupleName,
        IFieldSymbol tupleElement,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        if (local.Declaration.Variables.Count != 1
            || local.Declaration.Variables[0] is not { Initializer.Value: MemberAccessExpressionSyntax memberAccess } variable
            || memberAccess.Expression is not IdentifierNameSyntax receiver
            || receiver.Identifier.ValueText != tupleName
            || model.GetSymbolInfo(memberAccess.Name, cancellationToken).Symbol is not IFieldSymbol field)
        {
            return false;
        }

        var canonicalField = field.CorrespondingTupleField ?? field;
        if (!SymbolEqualityComparer.Default.Equals(canonicalField, tupleElement.CorrespondingTupleField ?? tupleElement))
        {
            return false;
        }

        return variable.Identifier.ValueText.Length > 0;
    }

    /// <summary>Reads the syntax shape of a local swap.</summary>
    /// <param name="local">The temporary local declaration.</param>
    /// <param name="shape">The swap shape.</param>
    /// <returns><see langword="true"/> when the syntax is the supported local-swap shape.</returns>
    private static bool TryGetTupleSwapShape(LocalDeclarationStatementSyntax local, out TupleSwapShape shape)
    {
        shape = default;
        if (local.Declaration.Variables.Count != 1
            || local.Declaration.Variables[0] is not { Initializer.Value: IdentifierNameSyntax left } temporary
            || local.Parent is not BlockSyntax block
            || !TryGetStatementIndex(block, local, out var index)
            || index + SwapAssignmentCount >= block.Statements.Count
            || !TryGetSwapAssignments(block, index, temporary.Identifier.ValueText, left, out var assignments))
        {
            return false;
        }

        shape = new TupleSwapShape(
            block,
            temporary.Identifier.ValueText,
            left,
            assignments.Right,
            assignments.First,
            assignments.Second,
            index);
        return true;
    }

    /// <summary>Reads the two assignments that follow a local-swap temporary.</summary>
    /// <param name="block">The containing block.</param>
    /// <param name="index">The temporary declaration index.</param>
    /// <param name="temporaryName">The temporary variable name.</param>
    /// <param name="left">The original left-side local.</param>
    /// <param name="assignments">The swap assignments.</param>
    /// <returns><see langword="true"/> when the assignments complete a local swap.</returns>
    private static bool TryGetSwapAssignments(
        BlockSyntax block,
        int index,
        string temporaryName,
        IdentifierNameSyntax left,
        out TupleSwapAssignments assignments)
    {
        assignments = default;
        if (!TryGetSimpleIdentifierAssignment(block.Statements[index + 1], out var first, out var firstLeft, out var right)
            || !TryGetSimpleIdentifierAssignment(block.Statements[index + SwapAssignmentCount], out var second, out var secondLeft, out var temporaryRead)
            || firstLeft.Identifier.ValueText != left.Identifier.ValueText
            || secondLeft.Identifier.ValueText != right.Identifier.ValueText
            || temporaryRead.Identifier.ValueText != temporaryName)
        {
            return false;
        }

        assignments = new TupleSwapAssignments(first, second, right);
        return true;
    }

    /// <summary>Reads a simple identifier-to-identifier assignment statement.</summary>
    /// <param name="statement">The candidate statement.</param>
    /// <param name="expressionStatement">The expression statement.</param>
    /// <param name="left">The assignment target.</param>
    /// <param name="right">The assignment value.</param>
    /// <returns><see langword="true"/> when the statement is a simple identifier assignment.</returns>
    private static bool TryGetSimpleIdentifierAssignment(
        StatementSyntax statement,
        out ExpressionStatementSyntax expressionStatement,
        out IdentifierNameSyntax left,
        out IdentifierNameSyntax right)
    {
        expressionStatement = null!;
        left = null!;
        right = null!;
        if (statement is not ExpressionStatementSyntax candidate
            || candidate.Expression is not AssignmentExpressionSyntax assignment
            || assignment.RawKind != (int)SyntaxKind.SimpleAssignmentExpression
            || assignment.Left is not IdentifierNameSyntax target
            || assignment.Right is not IdentifierNameSyntax value)
        {
            return false;
        }

        expressionStatement = candidate;
        left = target;
        right = value;
        return true;
    }

    /// <summary>Returns whether an identifier is a local or parameter.</summary>
    /// <param name="identifier">The identifier expression.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="cancellationToken">A token that cancels analysis.</param>
    /// <returns><see langword="true"/> for locals and parameters.</returns>
    private static bool IsLocalOrParameter(IdentifierNameSyntax identifier, SemanticModel model, CancellationToken cancellationToken)
        => model.GetSymbolInfo(identifier, cancellationToken).Symbol is ILocalSymbol or IParameterSymbol;

    /// <summary>Returns whether an identifier appears in a block after a statement index.</summary>
    /// <param name="block">The containing block.</param>
    /// <param name="name">The identifier name.</param>
    /// <param name="start">The first statement index to inspect.</param>
    /// <returns><see langword="true"/> when a matching identifier token appears.</returns>
    private static bool IdentifierAppearsAfter(BlockSyntax block, string name, int start)
    {
        for (var i = start; i < block.Statements.Count; i++)
        {
            foreach (var token in block.Statements[i].DescendantTokens())
            {
                if (token.ValueText == name)
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>Returns the index of a statement inside a block.</summary>
    /// <param name="block">The block.</param>
    /// <param name="statement">The statement.</param>
    /// <param name="index">The statement index.</param>
    /// <returns><see langword="true"/> when found.</returns>
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

    /// <summary>Collects hash inputs recursively from a multiplier-based expression.</summary>
    /// <param name="expression">The expression to inspect.</param>
    /// <param name="inputs">The collected hash inputs.</param>
    /// <returns><see langword="true"/> when the expression matches the supported shape.</returns>
    private static bool TryCollectHashInputsCore(ExpressionSyntax expression, List<ExpressionSyntax> inputs)
    {
        expression = ExpressionSimplificationAnalyzer.Unwrap(expression);
        if (TryGetHashInput(expression, out var input))
        {
            inputs.Add(input);
            return true;
        }

        if (expression is not BinaryExpressionSyntax binary
            || (!binary.IsKind(SyntaxKind.ExclusiveOrExpression) && !binary.IsKind(SyntaxKind.AddExpression))
            || !TryGetMultipliedHash(binary.Left, out var multiplied)
            || !TryCollectHashInputsCore(multiplied, inputs)
            || !TryCollectHashInputsCore(binary.Right, inputs))
        {
            return false;
        }

        return inputs.Count <= HashCodeCombineMaxInputs;
    }

    /// <summary>Validates hash inputs recursively without materializing expressions for analyzer reporting.</summary>
    /// <param name="expression">The expression to inspect.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="cancellationToken">A token that cancels analysis.</param>
    /// <param name="count">The number of validated hash inputs.</param>
    /// <returns><see langword="true"/> when the expression matches the supported shape.</returns>
    private static bool TryValidateHashInputsCore(
        ExpressionSyntax expression,
        SemanticModel model,
        CancellationToken cancellationToken,
        ref int count)
    {
        expression = ExpressionSimplificationAnalyzer.Unwrap(expression);
        if (TryGetHashInput(expression, out var input))
        {
            if (count >= HashCodeCombineMaxInputs || !IsValueTypeHashReceiver(input, model, cancellationToken))
            {
                return false;
            }

            count++;
            return true;
        }

        if (expression is not BinaryExpressionSyntax binary
            || (!binary.IsKind(SyntaxKind.ExclusiveOrExpression) && !binary.IsKind(SyntaxKind.AddExpression))
            || !TryGetMultipliedHash(binary.Left, out var multiplied)
            || !TryValidateHashInputsCore(multiplied, model, cancellationToken, ref count)
            || !TryValidateHashInputsCore(binary.Right, model, cancellationToken, ref count))
        {
            return false;
        }

        return count <= HashCodeCombineMaxInputs;
    }

    /// <summary>Gets the hash expression multiplied by a known hash-code multiplier.</summary>
    /// <param name="expression">The candidate multiply expression.</param>
    /// <param name="hashExpression">The expression being multiplied.</param>
    /// <returns><see langword="true"/> for supported multiplier shapes.</returns>
    private static bool TryGetMultipliedHash(ExpressionSyntax expression, out ExpressionSyntax hashExpression)
    {
        hashExpression = null!;
        if (ExpressionSimplificationAnalyzer.Unwrap(expression) is not BinaryExpressionSyntax binary
            || !binary.IsKind(SyntaxKind.MultiplyExpression))
        {
            return false;
        }

        if (IsHashMultiplier(binary.Right))
        {
            hashExpression = binary.Left;
            return true;
        }

        if (!IsHashMultiplier(binary.Left))
        {
            return false;
        }

        hashExpression = binary.Right;
        return true;
    }

    /// <summary>Returns whether an expression is a supported hash multiplier literal.</summary>
    /// <param name="expression">The expression.</param>
    /// <returns><see langword="true"/> for supported multiplier literals.</returns>
    private static bool IsHashMultiplier(ExpressionSyntax expression)
        => ExpressionSimplificationAnalyzer.Unwrap(expression) is LiteralExpressionSyntax literal
        && literal.Token.Value is int value
        && value is HashMultiplier397 or HashMultiplier31;

    /// <summary>Extracts the receiver from a simple <c>GetHashCode()</c> invocation.</summary>
    /// <param name="expression">The candidate expression.</param>
    /// <param name="input">The hash input expression.</param>
    /// <returns><see langword="true"/> when an input was found.</returns>
    private static bool TryGetHashInput(ExpressionSyntax expression, out ExpressionSyntax input)
    {
        input = null!;
        if (expression is not InvocationExpressionSyntax
            {
                ArgumentList.Arguments.Count: 0,
                Expression: MemberAccessExpressionSyntax { Name.Identifier.ValueText: nameof(GetHashCode), Expression: { } receiver }
            })
        {
            return false;
        }

        receiver = ExpressionSimplificationAnalyzer.Unwrap(receiver);
        if (receiver is not IdentifierNameSyntax and not MemberAccessExpressionSyntax)
        {
            return false;
        }

        input = receiver;
        return true;
    }

    /// <summary>Returns whether a hash receiver is value-typed, avoiding null-behaviour changes.</summary>
    /// <param name="expression">The hash-code receiver.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="cancellationToken">A token that cancels analysis.</param>
    /// <returns><see langword="true"/> when the receiver cannot be <see langword="null"/>.</returns>
    private static bool IsValueTypeHashReceiver(ExpressionSyntax expression, SemanticModel model, CancellationToken cancellationToken)
        => model.GetTypeInfo(expression, cancellationToken).Type is { IsValueType: true };

    /// <summary>Describes a deconstruction rewrite candidate.</summary>
    /// <param name="TupleLocal">The temporary tuple declaration.</param>
    public readonly record struct DeconstructionCandidate(LocalDeclarationStatementSyntax TupleLocal);

    /// <summary>Details of a tuple temporary declaration.</summary>
    /// <param name="Block">The containing block.</param>
    /// <param name="Variable">The tuple variable.</param>
    /// <param name="TupleElements">The tuple element fields.</param>
    /// <param name="StatementIndex">The declaration statement index.</param>
    private readonly record struct TupleTemporary(
        BlockSyntax Block,
        VariableDeclaratorSyntax Variable,
        ImmutableArray<IFieldSymbol> TupleElements,
        int StatementIndex);

    /// <summary>Details of a tuple-swap candidate.</summary>
    /// <param name="Block">The containing block.</param>
    /// <param name="TemporaryName">The temporary local name.</param>
    /// <param name="Left">The left local.</param>
    /// <param name="Right">The right local.</param>
    /// <param name="FirstAssignment">The first assignment statement.</param>
    /// <param name="SecondAssignment">The second assignment statement.</param>
    /// <param name="StatementIndex">The temporary declaration statement index.</param>
    private readonly record struct TupleSwapShape(
        BlockSyntax Block,
        string TemporaryName,
        IdentifierNameSyntax Left,
        IdentifierNameSyntax Right,
        ExpressionStatementSyntax FirstAssignment,
        ExpressionStatementSyntax SecondAssignment,
        int StatementIndex);

    /// <summary>The assignments that make up a local swap.</summary>
    /// <param name="First">The first assignment.</param>
    /// <param name="Second">The second assignment.</param>
    /// <param name="Right">The right local.</param>
    private readonly record struct TupleSwapAssignments(
        ExpressionStatementSyntax First,
        ExpressionStatementSyntax Second,
        IdentifierNameSyntax Right);
}
