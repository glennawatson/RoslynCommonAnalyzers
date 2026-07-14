// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Flags hand-written guard clauses the framework throw helpers replace (PSH1409):
/// <c>if (x is null) throw new ArgumentNullException(nameof(x))</c> becomes
/// <c>ArgumentNullException.ThrowIfNull(x)</c>, string emptiness guards become
/// <c>ArgumentException.ThrowIfNullOrEmpty</c>/<c>ThrowIfNullOrWhiteSpace</c>, disposal
/// guards become <c>ObjectDisposedException.ThrowIf</c>, and numeric range guards map onto
/// the <c>ArgumentOutOfRangeException.ThrowIf*</c> family. The guard's parameter name must
/// match the checked value, and each helper is suggested only where it exists in the
/// compilation. The helpers' standard messages replace hand-written ones.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Psh1409ThrowHelperAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The metadata names of the guard exception types, indexed like <see cref="ExceptionSimpleNames"/>.</summary>
    private static readonly string[] ExceptionMetadataNames =
    [
        "System.ArgumentNullException",
        "System.ArgumentException",
        "System.ObjectDisposedException",
        "System.ArgumentOutOfRangeException",
    ];

    /// <summary>The simple names of the guard exception types.</summary>
    private static readonly string[] ExceptionSimpleNames =
    [
        nameof(ArgumentNullException),
        nameof(ArgumentException),
        nameof(ObjectDisposedException),
        nameof(ArgumentOutOfRangeException),
    ];

    /// <summary>The helper-alias names probed for null guards (the Primitives polyfill convention).</summary>
    private static readonly string[] NullCheckAliases = ["ArgumentNullExceptionHelper", "ArgumentExceptionHelper"];

    /// <summary>The helper-alias names probed for string emptiness guards.</summary>
    private static readonly string[] EmptinessAliases = ["ArgumentExceptionHelper", "ArgumentNullExceptionHelper"];

    /// <summary>The helper-alias names probed for disposal guards.</summary>
    private static readonly string[] DisposedAliases = ["ObjectDisposedExceptionHelper"];

    /// <summary>The helper-alias names probed for comparison guards.</summary>
    private static readonly string[] ComparisonAliases = ["ArgumentOutOfRangeExceptionHelper"];

    /// <summary>The guard shapes the analyzer classifies.</summary>
    internal enum GuardKind
    {
        /// <summary>A reference null check throwing ArgumentNullException.</summary>
        NullCheck,

        /// <summary>A string.IsNullOrEmpty guard.</summary>
        NullOrEmpty,

        /// <summary>A string.IsNullOrWhiteSpace guard.</summary>
        NullOrWhiteSpace,

        /// <summary>A disposal guard throwing ObjectDisposedException.</summary>
        Disposed,

        /// <summary>A numeric comparison guard throwing ArgumentOutOfRangeException.</summary>
        Comparison,
    }

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(ApiSelectionRules.UseThrowHelpers);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(start =>
        {
            // No helper-existence gate here: even on frameworks without the BCL helpers, an
            // aliased polyfill helper (the Primitives model) can make a guard fixable, and
            // aliases are only visible through position-based lookup during analysis.
            var exceptionTypes = new INamedTypeSymbol?[ExceptionMetadataNames.Length];
            var anyException = false;
            for (var i = 0; i < ExceptionMetadataNames.Length; i++)
            {
                exceptionTypes[i] = start.Compilation.GetTypeByMetadataName(ExceptionMetadataNames[i]);
                anyException |= exceptionTypes[i] is not null;
            }

            if (!anyException)
            {
                return;
            }

            start.RegisterSyntaxNodeAction(nodeContext => AnalyzeIf(nodeContext, exceptionTypes), SyntaxKind.IfStatement);
        });
    }

    /// <summary>Classifies a guard clause, before any binding.</summary>
    /// <param name="ifStatement">The if statement to classify.</param>
    /// <returns>The guard shape, or <see langword="null"/> when no helper applies.</returns>
    internal static GuardShape? TryClassify(IfStatementSyntax ifStatement)
    {
        if (ifStatement.Else is not null
            || TryGetThrownCreation(ifStatement.Statement) is not { } creation
            || GetRightmostName(creation.Type) is not { } exceptionName)
        {
            return null;
        }

        return exceptionName.Identifier.ValueText switch
        {
            nameof(ArgumentNullException) => TryClassifyNullGuard(ifStatement.Condition, creation, allowNullCheck: true),
            nameof(ArgumentException) => TryClassifyNullGuard(ifStatement.Condition, creation, allowNullCheck: false),
            nameof(ObjectDisposedException) => TryClassifyDisposedGuard(ifStatement.Condition, creation),
            nameof(ArgumentOutOfRangeException) => TryClassifyComparisonGuard(ifStatement.Condition, creation),
            _ => null,
        };
    }

    /// <summary>Returns the exception index of a guard shape's exception simple name.</summary>
    /// <param name="creation">The thrown creation.</param>
    /// <returns>The index into the exception tables, or -1.</returns>
    internal static int GetExceptionIndex(ObjectCreationExpressionSyntax creation)
    {
        var name = GetRightmostName(creation.Type)?.Identifier.ValueText;
        for (var i = 0; i < ExceptionSimpleNames.Length; i++)
        {
            if (ExceptionSimpleNames[i] == name)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>Resolves the receiver spelling whose helper the guard can move to, alias-aware.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="position">The guard's position, anchoring alias lookups.</param>
    /// <param name="shape">The classified guard.</param>
    /// <returns>The receiver spelling, or <see langword="null"/> when no helper is available.</returns>
    internal static string? TryGetHelperReceiver(SemanticModel model, int position, in GuardShape shape)
    {
        // Alias names first: projects following the Primitives polyfill model alias
        // e.g. ArgumentExceptionHelper to an internal polyfill on net4x and to the BCL
        // exception on net8+, so the alias spelling compiles on every target framework.
        foreach (var alias in GetAliasCandidates(shape.Kind))
        {
            foreach (var candidate in model.LookupNamespacesAndTypes(position, name: alias))
            {
                var type = candidate switch
                {
                    IAliasSymbol { Target: INamedTypeSymbol aliased } => aliased,
                    INamedTypeSymbol named => named,
                    _ => null,
                };

                if (type is not null && HasHelperMember(type, shape.HelperName))
                {
                    return alias;
                }
            }
        }

        return TryGetBclReceiver(model, position, shape);
    }

    /// <summary>Resolves the BCL receiver spelling when the framework itself carries the helper.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="position">The guard's position, anchoring lookups.</param>
    /// <param name="shape">The classified guard.</param>
    /// <returns>The receiver spelling, or <see langword="null"/>.</returns>
    private static string? TryGetBclReceiver(SemanticModel model, int position, in GuardShape shape)
    {
        var isEmptiness = shape.Kind is GuardKind.NullOrEmpty or GuardKind.NullOrWhiteSpace;
        var ownerIndex = isEmptiness ? 1 : GetExceptionIndex(shape.Creation);
        if (ownerIndex < 0
            || model.Compilation.GetTypeByMetadataName(ExceptionMetadataNames[ownerIndex]) is not { } owner
            || !HasHelperMember(owner, shape.HelperName))
        {
            return null;
        }

        if (!isEmptiness || GetExceptionIndex(shape.Creation) == 1)
        {
            return shape.Creation.Type.ToString();
        }

        return ResolvesInSystem(model, position, nameof(ArgumentException))
            ? nameof(ArgumentException)
            : "global::System.ArgumentException";
    }

    /// <summary>Returns the thrown object creation of a guard body.</summary>
    /// <param name="statement">The guarded statement.</param>
    /// <returns>The creation, or <see langword="null"/> when the body is not a single throw.</returns>
    private static ObjectCreationExpressionSyntax? TryGetThrownCreation(StatementSyntax statement)
    {
        var throwStatement = statement switch
        {
            ThrowStatementSyntax direct => direct,
            BlockSyntax { Statements: [ThrowStatementSyntax single] } => single,
            _ => null,
        };

        return throwStatement?.Expression as ObjectCreationExpressionSyntax;
    }

    /// <summary>Returns the rightmost simple name of a type syntax.</summary>
    /// <param name="type">The type syntax.</param>
    /// <returns>The simple name, or <see langword="null"/>.</returns>
    private static SimpleNameSyntax? GetRightmostName(TypeSyntax type)
        => type switch
        {
            SimpleNameSyntax simple => simple,
            QualifiedNameSyntax qualified => qualified.Right,
            AliasQualifiedNameSyntax alias => alias.Name,
            _ => null,
        };

    /// <summary>Classifies null and string-emptiness guards.</summary>
    /// <param name="condition">The guard condition.</param>
    /// <param name="creation">The thrown creation.</param>
    /// <param name="allowNullCheck">Whether a plain null check maps to a helper for this exception.</param>
    /// <returns>The guard shape, or <see langword="null"/>.</returns>
    private static GuardShape? TryClassifyNullGuard(ExpressionSyntax condition, ObjectCreationExpressionSyntax creation, bool allowNullCheck)
    {
        if (allowNullCheck && TryGetNullCheckedIdentifier(condition) is { } checkedValue)
        {
            return MatchesParamName(creation, checkedValue.Identifier.ValueText, maximumArguments: 2)
                ? new GuardShape(GuardKind.NullCheck, GuardShape.NullHelperName, checkedValue, null, creation)
                : null;
        }

        if (TryGetStringProbe(condition) is not { } probe)
        {
            return null;
        }

        var kind = probe.MethodName == "IsNullOrEmpty" ? GuardKind.NullOrEmpty : GuardKind.NullOrWhiteSpace;
        var helper = kind == GuardKind.NullOrEmpty ? GuardShape.NullOrEmptyHelperName : GuardShape.NullOrWhiteSpaceHelperName;
        return MatchesAnyArgument(creation, probe.Value.Identifier.ValueText)
            ? new GuardShape(kind, helper, probe.Value, null, creation)
            : null;
    }

    /// <summary>Returns the identifier compared against null in a guard condition.</summary>
    /// <param name="condition">The guard condition.</param>
    /// <returns>The checked identifier, or <see langword="null"/>.</returns>
    private static IdentifierNameSyntax? TryGetNullCheckedIdentifier(ExpressionSyntax condition)
        => condition switch
        {
            IsPatternExpressionSyntax { Expression: IdentifierNameSyntax value, Pattern: ConstantPatternSyntax constant }
                when constant.Expression.IsKind(SyntaxKind.NullLiteralExpression) => value,
            BinaryExpressionSyntax { RawKind: (int)SyntaxKind.EqualsExpression } binary
                when binary.Right.IsKind(SyntaxKind.NullLiteralExpression) && binary.Left is IdentifierNameSyntax left => left,
            BinaryExpressionSyntax { RawKind: (int)SyntaxKind.EqualsExpression } binary
                when binary.Left.IsKind(SyntaxKind.NullLiteralExpression) && binary.Right is IdentifierNameSyntax right => right,
            _ => null,
        };

    /// <summary>Returns the parts of a <c>string.IsNullOrEmpty(x)</c>-style condition.</summary>
    /// <param name="condition">The guard condition.</param>
    /// <returns>The probe method name and checked identifier, or <see langword="null"/>.</returns>
    private static (string MethodName, IdentifierNameSyntax Value)? TryGetStringProbe(ExpressionSyntax condition)
    {
        if (condition is not InvocationExpressionSyntax { ArgumentList.Arguments: [{ Expression: IdentifierNameSyntax value }] } invocation
            || invocation.Expression is not MemberAccessExpressionSyntax access
            || access.Name.Identifier.ValueText is not ("IsNullOrEmpty" or "IsNullOrWhiteSpace"))
        {
            return null;
        }

        return (access.Name.Identifier.ValueText, value);
    }

    /// <summary>Classifies disposal guards whose thrown name comes from the type.</summary>
    /// <param name="condition">The guard condition.</param>
    /// <param name="creation">The thrown creation.</param>
    /// <returns>The guard shape, or <see langword="null"/>.</returns>
    private static GuardShape? TryClassifyDisposedGuard(ExpressionSyntax condition, ObjectCreationExpressionSyntax creation)
        => creation.ArgumentList is { Arguments: [{ Expression: var name }] } && IsTypeNameExpression(name)
            ? new GuardShape(GuardKind.Disposed, GuardShape.DisposedHelperName, condition, null, creation)
            : null;

    /// <summary>Returns whether an expression produces the containing type's name.</summary>
    /// <param name="expression">The objectName argument.</param>
    /// <returns><see langword="true"/> for nameof, GetType, and typeof shapes.</returns>
    private static bool IsTypeNameExpression(ExpressionSyntax expression)
        => expression switch
        {
            InvocationExpressionSyntax { Expression: IdentifierNameSyntax { Identifier.ValueText: "nameof" } } => true,
            MemberAccessExpressionSyntax { Expression: InvocationExpressionSyntax { Expression: IdentifierNameSyntax { Identifier.ValueText: "GetType" } } } => true,
            MemberAccessExpressionSyntax { Expression: InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax { Name.Identifier.ValueText: "GetType" } } } => true,
            MemberAccessExpressionSyntax { Expression: TypeOfExpressionSyntax } => true,
            _ => false,
        };

    /// <summary>Classifies numeric comparison guards.</summary>
    /// <param name="condition">The guard condition.</param>
    /// <param name="creation">The thrown creation.</param>
    /// <returns>The guard shape, or <see langword="null"/>.</returns>
    private static GuardShape? TryClassifyComparisonGuard(ExpressionSyntax condition, ObjectCreationExpressionSyntax creation)
    {
        if (condition is not BinaryExpressionSyntax binary
            || TryGetParamName(creation, maximumArguments: 2) is not { } paramName
            || TryOrientComparison(binary, paramName) is not { } oriented)
        {
            return null;
        }

        var comparesToZero = IsZeroLiteral(oriented.Operand);
        if (MapComparisonHelper(oriented.Kind, comparesToZero) is not { } helper)
        {
            return null;
        }

        return new GuardShape(GuardKind.Comparison, helper, oriented.Value, comparesToZero ? null : oriented.Operand, creation);
    }

    /// <summary>Orients a comparison so the named value sits on the left.</summary>
    /// <param name="binary">The comparison condition.</param>
    /// <param name="paramName">The parameter name the creation spells.</param>
    /// <returns>The oriented parts, or <see langword="null"/> when neither side names the parameter.</returns>
    private static (IdentifierNameSyntax Value, ExpressionSyntax Operand, SyntaxKind Kind)? TryOrientComparison(
        BinaryExpressionSyntax binary,
        string paramName)
        => binary switch
        {
            { Left: IdentifierNameSyntax left } when left.Identifier.ValueText == paramName
                => (left, binary.Right, binary.Kind()),
            { Right: IdentifierNameSyntax right } when right.Identifier.ValueText == paramName
                => (right, binary.Left, Mirror(binary.Kind())),
            _ => null,
        };

    /// <summary>Mirrors a comparison kind for reversed operand order.</summary>
    /// <param name="kind">The original comparison kind.</param>
    /// <returns>The kind with the value on the left.</returns>
    private static SyntaxKind Mirror(SyntaxKind kind)
        => kind switch
        {
            SyntaxKind.LessThanExpression => SyntaxKind.GreaterThanExpression,
            SyntaxKind.LessThanOrEqualExpression => SyntaxKind.GreaterThanOrEqualExpression,
            SyntaxKind.GreaterThanExpression => SyntaxKind.LessThanExpression,
            SyntaxKind.GreaterThanOrEqualExpression => SyntaxKind.LessThanOrEqualExpression,
            _ => kind,
        };

    /// <summary>Maps a value-on-the-left comparison to its throw helper.</summary>
    /// <param name="kind">The comparison kind.</param>
    /// <param name="comparesToZero">Whether the other operand is the zero literal.</param>
    /// <returns>The helper name, or <see langword="null"/>.</returns>
    [SuppressMessage(
        "Critical Code Smell",
        "S1541:Methods and properties should not be too complex",
        Justification = "A flat operator-to-helper switch is the whole mapping; splitting it would hide the table.")]
    private static string? MapComparisonHelper(SyntaxKind kind, bool comparesToZero)
        => kind switch
        {
            SyntaxKind.LessThanExpression => comparesToZero ? "ThrowIfNegative" : "ThrowIfLessThan",
            SyntaxKind.LessThanOrEqualExpression => comparesToZero ? "ThrowIfNegativeOrZero" : "ThrowIfLessThanOrEqual",
            SyntaxKind.GreaterThanExpression => "ThrowIfGreaterThan",
            SyntaxKind.GreaterThanOrEqualExpression => "ThrowIfGreaterThanOrEqual",
            SyntaxKind.EqualsExpression => comparesToZero ? "ThrowIfZero" : "ThrowIfEqual",
            SyntaxKind.NotEqualsExpression when !comparesToZero => "ThrowIfNotEqual",
            _ => null,
        };

    /// <summary>Returns whether an expression is the integer literal zero.</summary>
    /// <param name="expression">The operand.</param>
    /// <returns><see langword="true"/> for <c>0</c>.</returns>
    private static bool IsZeroLiteral(ExpressionSyntax expression)
        => expression is LiteralExpressionSyntax { RawKind: (int)SyntaxKind.NumericLiteralExpression } literal
            && literal.Token.ValueText == "0";

    /// <summary>Returns whether the creation's first argument names the checked value.</summary>
    /// <param name="creation">The thrown creation.</param>
    /// <param name="valueName">The checked value's name.</param>
    /// <param name="maximumArguments">The most constructor arguments the helper can absorb.</param>
    /// <returns><see langword="true"/> when the parameter name matches.</returns>
    private static bool MatchesParamName(ObjectCreationExpressionSyntax creation, string valueName, int maximumArguments)
        => TryGetParamName(creation, maximumArguments) == valueName;

    /// <summary>Returns the parameter name spelled by the creation's first argument.</summary>
    /// <param name="creation">The thrown creation.</param>
    /// <param name="maximumArguments">The most constructor arguments the helper can absorb.</param>
    /// <returns>The parameter name, or <see langword="null"/>.</returns>
    private static string? TryGetParamName(ObjectCreationExpressionSyntax creation, int maximumArguments)
    {
        if (creation.ArgumentList is not { Arguments.Count: >= 1 } argumentList
            || argumentList.Arguments.Count > maximumArguments)
        {
            return null;
        }

        return TryGetNameText(argumentList.Arguments[0].Expression);
    }

    /// <summary>Returns whether any creation argument names the checked value.</summary>
    /// <param name="creation">The thrown creation.</param>
    /// <param name="valueName">The checked value's name.</param>
    /// <returns><see langword="true"/> when a nameof or literal argument matches.</returns>
    private static bool MatchesAnyArgument(ObjectCreationExpressionSyntax creation, string valueName)
    {
        if (creation.ArgumentList is not { } argumentList)
        {
            return false;
        }

        foreach (var argument in argumentList.Arguments)
        {
            if (TryGetNameText(argument.Expression) == valueName)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns the name text of a nameof expression or string literal.</summary>
    /// <param name="expression">The argument expression.</param>
    /// <returns>The name text, or <see langword="null"/>.</returns>
    private static string? TryGetNameText(ExpressionSyntax expression)
        => expression switch
        {
            InvocationExpressionSyntax { Expression: IdentifierNameSyntax { Identifier.ValueText: "nameof" }, ArgumentList.Arguments: [{ Expression: IdentifierNameSyntax name }] }
                => name.Identifier.ValueText,
            LiteralExpressionSyntax { RawKind: (int)SyntaxKind.StringLiteralExpression } literal => literal.Token.ValueText,
            _ => null,
        };

    /// <summary>Returns the alias names probed for a guard kind.</summary>
    /// <param name="kind">The guard kind.</param>
    /// <returns>The candidate alias names.</returns>
    private static string[] GetAliasCandidates(GuardKind kind)
        => kind switch
        {
            GuardKind.NullCheck => NullCheckAliases,
            GuardKind.NullOrEmpty or GuardKind.NullOrWhiteSpace => EmptinessAliases,
            GuardKind.Disposed => DisposedAliases,
            _ => ComparisonAliases,
        };

    /// <summary>Returns whether a type or one of its bases declares a helper member.</summary>
    /// <param name="type">The candidate helper owner.</param>
    /// <param name="helperName">The helper method name.</param>
    /// <returns><see langword="true"/> when the member exists; base types matter because static helpers resolve through derived exception names.</returns>
    private static bool HasHelperMember(INamedTypeSymbol type, string helperName)
    {
        for (INamedTypeSymbol? current = type; current is not null; current = current.BaseType)
        {
            if (!current.GetMembers(helperName).IsEmpty)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether a simple name resolves to a System type at a position.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="position">The lookup position.</param>
    /// <param name="name">The simple type name.</param>
    /// <returns><see langword="true"/> when the simple spelling binds.</returns>
    private static bool ResolvesInSystem(SemanticModel model, int position, string name)
    {
        foreach (var candidate in model.LookupNamespacesAndTypes(position, name: name))
        {
            if (candidate is INamedTypeSymbol { ContainingNamespace: { Name: nameof(System), ContainingNamespace.IsGlobalNamespace: true } })
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Reports PSH1409 for a guard whose helper exists and whose shape binds correctly.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="exceptionTypes">The resolved exception types, indexed like the name tables.</param>
    private static void AnalyzeIf(SyntaxNodeAnalysisContext context, INamedTypeSymbol?[] exceptionTypes)
    {
        var ifStatement = (IfStatementSyntax)context.Node;
        if (TryClassify(ifStatement) is not { } shape)
        {
            return;
        }

        var index = GetExceptionIndex(shape.Creation);
        if (index < 0
            || exceptionTypes[index] is not { } thrownType
            || !IsBoundGuard(context, shape, thrownType)
            || TryGetHelperReceiver(context.SemanticModel, ifStatement.SpanStart, shape) is not { } receiver)
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            ApiSelectionRules.UseThrowHelpers,
            ifStatement.SyntaxTree,
            ifStatement.Span,
            receiver + "." + shape.HelperName));
    }

    /// <summary>Verifies a classified guard's semantics: real exception and suitable value type.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="shape">The classified guard.</param>
    /// <param name="thrownType">The expected thrown exception type.</param>
    /// <returns><see langword="true"/> when the guard should be reported.</returns>
    private static bool IsBoundGuard(SyntaxNodeAnalysisContext context, GuardShape shape, INamedTypeSymbol thrownType)
    {
        var model = context.SemanticModel;
        if (model.GetTypeInfo(shape.Creation, context.CancellationToken).Type is not INamedTypeSymbol created
            || !SymbolEqualityComparer.Default.Equals(created, thrownType))
        {
            return false;
        }

        return shape.Kind switch
        {
            GuardKind.NullCheck => model.GetTypeInfo(shape.Value, context.CancellationToken).Type is { IsReferenceType: true } valueType
                && !IsSystemThreadingLock(model.Compilation, valueType),
            GuardKind.Comparison => IsNumericType(model.GetTypeInfo(shape.Value, context.CancellationToken).Type),
            _ => true,
        };
    }

    /// <summary>Returns whether a type is <c>System.Threading.Lock</c>.</summary>
    /// <param name="compilation">The compilation, used to resolve the well-known type.</param>
    /// <param name="type">The checked value's type.</param>
    /// <returns>
    /// <see langword="true"/> for <c>System.Threading.Lock</c>. Suggesting <c>ArgumentNullException.ThrowIfNull</c>
    /// there is wrong: the helper's <c>object?</c> parameter forces a conversion the compiler rejects with CS9216
    /// (a <c>Lock</c> widened to <c>object</c> would silently fall back to monitor-based locking).
    /// </returns>
    private static bool IsSystemThreadingLock(Compilation compilation, ITypeSymbol type)
        => SymbolEqualityComparer.Default.Equals(type, compilation.GetTypeByMetadataName("System.Threading.Lock"));

    /// <summary>Returns whether a type is one of the built-in numeric types the helpers accept.</summary>
    /// <param name="type">The checked value's type.</param>
    /// <returns><see langword="true"/> for the primitive numeric set and decimal.</returns>
    [SuppressMessage(
        "Critical Code Smell",
        "S1541:Methods and properties should not be too complex",
        Justification = "A flat SpecialType list mirrors the helper constraints explicitly instead of relying on enum-value adjacency.")]
    private static bool IsNumericType(ITypeSymbol? type)
        => type?.SpecialType is SpecialType.System_SByte or SpecialType.System_Byte
            or SpecialType.System_Int16 or SpecialType.System_UInt16
            or SpecialType.System_Int32 or SpecialType.System_UInt32
            or SpecialType.System_Int64 or SpecialType.System_UInt64
            or SpecialType.System_Single or SpecialType.System_Double
            or SpecialType.System_Decimal;

    /// <summary>A classified guard clause.</summary>
    /// <param name="Kind">The guard kind.</param>
    /// <param name="HelperName">The throw helper the guard maps onto.</param>
    /// <param name="Value">The checked value — or, for disposal guards, the whole condition.</param>
    /// <param name="Operand">The comparison's other operand, when the helper takes two arguments.</param>
    /// <param name="Creation">The thrown creation, whose type spelling the fix reuses.</param>
    internal readonly record struct GuardShape(
        GuardKind Kind,
        string HelperName,
        ExpressionSyntax Value,
        ExpressionSyntax? Operand,
        ObjectCreationExpressionSyntax Creation)
    {
        /// <summary>The null-check helper name.</summary>
        internal const string NullHelperName = "ThrowIfNull";

        /// <summary>The emptiness helper name.</summary>
        internal const string NullOrEmptyHelperName = "ThrowIfNullOrEmpty";

        /// <summary>The whitespace helper name.</summary>
        internal const string NullOrWhiteSpaceHelperName = "ThrowIfNullOrWhiteSpace";

        /// <summary>The disposal helper name.</summary>
        internal const string DisposedHelperName = "ThrowIf";
    }
}
