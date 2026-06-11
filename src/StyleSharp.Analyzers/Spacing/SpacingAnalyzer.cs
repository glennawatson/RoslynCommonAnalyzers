// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Text;

namespace StyleSharp.Analyzers;

/// <summary>
/// The spacing (SST10xx) analyzer. It walks the token stream once per file and inspects each
/// token's trivia, so every spacing rule shares a single tree walk rather than each one
/// re-traversing. Whitespace is examined by scanning the cached <see cref="SourceText"/>
/// directly, keeping the common (clean) path free of string allocations.
/// </summary>
/// <remarks>
/// Diagnostics: SST1000, SST1001, SST1002, SST1003, SST1004, SST1005, SST1006, SST1007, SST1008, SST1009, SST1010, SST1011, SST1012, SST1013, SST1014, SST1015, SST1016, SST1017,
/// SST1018, SST1019, SST1020, SST1021, SST1022, SST1023, SST1024, SST1025, SST1026, SST1027, SST1028.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class SpacingAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The diagnostic property key naming the spacing fix to apply.</summary>
    internal const string ActionKey = "action";

    /// <summary>The fix action that removes whitespace immediately before the token.</summary>
    internal const string RemoveBefore = "RemoveBefore";

    /// <summary>The fix action that inserts a space immediately after the token.</summary>
    internal const string AddAfter = "AddAfter";

    /// <summary>The fix action that inserts a space immediately before the token.</summary>
    internal const string AddBefore = "AddBefore";

    /// <summary>The fix action that removes whitespace immediately after the token.</summary>
    internal const string RemoveAfter = "RemoveAfter";

    /// <summary>Editorconfig key choosing the collection-expression inner-bracket style ('none' tight, default; 'space' padded). Governs SST1010/SST1011.</summary>
    internal const string CollectionExpressionSpacingKey = "stylesharp.collection_expression_spacing";

    /// <summary>The width of the comment opener, used to find the first character of the comment text.</summary>
    private const int CommentOpenerLength = 2;

    /// <summary>The width of the documentation comment exterior ('///').</summary>
    private const int DocExteriorLength = 3;

    /// <summary>Cached diagnostic properties for fixes that insert a space after the token.</summary>
    private static readonly ImmutableDictionary<string, string?> AddAfterProperties = ImmutableDictionary<string, string?>.Empty.Add(ActionKey, AddAfter);

    /// <summary>Cached diagnostic properties for fixes that insert a space before the token.</summary>
    private static readonly ImmutableDictionary<string, string?> AddBeforeProperties = ImmutableDictionary<string, string?>.Empty.Add(ActionKey, AddBefore);

    /// <summary>Cached diagnostic properties for fixes that remove whitespace after the token.</summary>
    private static readonly ImmutableDictionary<string, string?> RemoveAfterProperties = ImmutableDictionary<string, string?>.Empty.Add(ActionKey, RemoveAfter);

    /// <summary>Cached diagnostic properties for fixes that remove whitespace before the token.</summary>
    private static readonly ImmutableDictionary<string, string?> RemoveBeforeProperties = ImmutableDictionary<string, string?>.Empty.Add(ActionKey, RemoveBefore);

    /// <summary>The whitespace separation between two adjacent tokens.</summary>
    private enum Separation
    {
        /// <summary>The tokens touch with no whitespace.</summary>
        Adjacent,

        /// <summary>The tokens are separated by whitespace on the same line.</summary>
        Space,

        /// <summary>The tokens are separated by a line break.</summary>
        NewLine
    }

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(
        SpacingRules.CommentBeginsWithSpace,
        SpacingRules.DocumentationBeginsWithSpace,
        SpacingRules.PreprocessorKeywordSpacing,
        SpacingRules.MultipleWhitespace,
        SpacingRules.UseSpacesNotTabs,
        SpacingRules.NoTrailingWhitespace,
        SpacingRules.CommaSpacing,
        SpacingRules.SemicolonSpacing,
        SpacingRules.OpeningGenericBracket,
        SpacingRules.ClosingGenericBracket,
        SpacingRules.OpeningAttributeBracket,
        SpacingRules.ClosingAttributeBracket,
        SpacingRules.NullableSpacing,
        SpacingRules.MemberAccessSpacing,
        SpacingRules.ImplicitArraySpacing,
        SpacingRules.OperatorKeywordSpacing,
        SpacingRules.ClosingSquareBracket,
        SpacingRules.IncrementDecrementSpacing,
        SpacingRules.NegativeSignSpacing,
        SpacingRules.PositiveSignSpacing,
        SpacingRules.KeywordSpacing,
        SpacingRules.OperatorSpacing,
        SpacingRules.OpeningParenthesis,
        SpacingRules.ClosingParenthesis,
        SpacingRules.OpeningBrace,
        SpacingRules.ClosingBrace,
        SpacingRules.ColonSpacing,
        SpacingRules.PointerSymbolSpacing,
        SpacingRules.OpeningSquareBracket);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterSyntaxTreeAction(Analyze);
    }

    /// <summary>Walks every token's trivia once and applies the trivia-based spacing rules.</summary>
    /// <param name="context">The syntax tree analysis context.</param>
    private static void Analyze(SyntaxTreeAnalysisContext context)
    {
        var text = context.Tree.GetText(context.CancellationToken);
        var root = context.Tree.GetRoot(context.CancellationToken);
        var collectionPadded = ReadCollectionExpressionPadded(context.Options.AnalyzerConfigOptionsProvider.GetOptions(context.Tree));

        var previous = default(SyntaxToken);
        foreach (var token in root.DescendantTokens())
        {
            ProcessTrivia(context, text, token.LeadingTrivia, isTrailing: false);
            ProcessTrivia(context, text, token.TrailingTrivia, isTrailing: true);
            CheckPair(context, previous, token, collectionPadded);
            previous = token;
        }
    }

    /// <summary>Dispatches every token-pair spacing rule for an adjacent token pair (single separation read).</summary>
    /// <param name="context">The syntax tree analysis context.</param>
    /// <param name="previous">The earlier token.</param>
    /// <param name="current">The later token.</param>
    /// <param name="collectionPadded">Whether collection-expression brackets are padded ('[ 1 ]').</param>
    private static void CheckPair(SyntaxTreeAnalysisContext context, SyntaxToken previous, SyntaxToken current, bool collectionPadded)
    {
        if (previous.IsKind(SyntaxKind.None))
        {
            return;
        }

        var separation = Between(previous, current);
        CheckCommaSemicolon(context, previous, current, separation);
        CheckOperatorKeyword(context, previous, separation);
        CheckKeyword(context, previous, current, separation);
        CheckBinaryOperator(context, previous, current, separation);
        CheckBraces(context, previous, current, separation);
        CheckColon(context, previous, current, separation);

        // Square-bracket spacing runs for every separation (not just Space): a padded collection
        // expression must *add* a space to an otherwise adjacent bracket.
        CheckOpeningBracket(context, previous, current, separation, collectionPadded);
        CheckClosingBracket(context, previous, current, separation, collectionPadded);
        if (separation != Separation.Space)
        {
            return;
        }

        CheckMemberDot(context, previous, current);
        CheckGenericBrackets(context, previous, current);
        CheckAttributeBrackets(context, previous, current);
        CheckNullable(context, current);
        CheckImplicitArray(context, previous, current);
        CheckIncrementDecrement(context, previous, current);
        CheckUnarySign(context, previous, current);
        CheckParentheses(context, previous, current);
        CheckPointerSymbol(context, previous);
    }

    /// <summary>Reports a dereference or address-of symbol followed by a space (SST1023, opt-in, unsafe code only).</summary>
    /// <param name="context">The syntax tree analysis context.</param>
    /// <param name="previous">The earlier token, which is the candidate pointer symbol.</param>
    private static void CheckPointerSymbol(SyntaxTreeAnalysisContext context, SyntaxToken previous)
    {
        if (!IsDereferenceOrAddressOf(previous))
        {
            return;
        }

        Report(context, SpacingRules.PointerSymbolSpacing, previous, RemoveAfter);
    }

    /// <summary>Reports whitespace adjacent to an opening square bracket (SST1010, opt-in).</summary>
    /// <param name="context">The syntax tree analysis context.</param>
    /// <param name="previous">The earlier token.</param>
    /// <param name="current">The later token.</param>
    /// <param name="separation">The whitespace separation between the tokens.</param>
    /// <param name="collectionPadded">Whether collection-expression brackets are padded ('[ 1 ]').</param>
    private static void CheckOpeningBracket(SyntaxTreeAnalysisContext context, SyntaxToken previous, SyntaxToken current, Separation separation, bool collectionPadded)
    {
        CheckSpaceBeforeOpen(context, previous, current, separation);

        // Spacing AFTER '[' (previous is the bracket).
        if (!previous.IsKind(SyntaxKind.OpenBracketToken) || previous.Parent is AttributeListSyntax)
        {
            return;
        }

        if (IsCollectionLikeBracket(previous.Parent))
        {
            CheckCollectionInnerAfterOpen(context, previous, current, separation, collectionPadded);
        }
        else if (separation == Separation.Space)
        {
            Report(context, SpacingRules.OpeningSquareBracket, current, RemoveBefore);
        }
    }

    /// <summary>Reports a space before an element-access / array '[' (SST1010). Collection-expression and list-pattern brackets are exempt.</summary>
    /// <param name="context">The syntax tree analysis context.</param>
    /// <param name="previous">The earlier token.</param>
    /// <param name="current">The later token (the candidate '[').</param>
    /// <param name="separation">The whitespace separation between the tokens.</param>
    private static void CheckSpaceBeforeOpen(SyntaxTreeAnalysisContext context, SyntaxToken previous, SyntaxToken current, Separation separation)
    {
        // A collection-expression / list-pattern '[' opens a new construct after an operator/keyword
        // ('x = [1]', 'x is [1]'), so its leading space belongs to those rules and is always allowed —
        // this is the collection friendliness. A space before '[' after 'new'/'stackalloc' is the
        // implicit-array case owned by SST1026.
        if (separation != Separation.Space
            || !current.IsKind(SyntaxKind.OpenBracketToken)
            || current.Parent is AttributeListSyntax
            || IsCollectionLikeBracket(current.Parent)
            || previous.IsKind(SyntaxKind.NewKeyword)
            || previous.IsKind(SyntaxKind.StackAllocKeyword))
        {
            return;
        }

        Report(context, SpacingRules.OpeningSquareBracket, current, RemoveBefore);
    }

    /// <summary>Applies the configured collection-expression inner-spacing style to the space after '['.</summary>
    /// <param name="context">The syntax tree analysis context.</param>
    /// <param name="open">The opening bracket token.</param>
    /// <param name="current">The token following the bracket.</param>
    /// <param name="separation">The whitespace separation between the tokens.</param>
    /// <param name="collectionPadded">Whether collection-expression brackets are padded ('[ 1 ]').</param>
    private static void CheckCollectionInnerAfterOpen(SyntaxTreeAnalysisContext context, SyntaxToken open, SyntaxToken current, Separation separation, bool collectionPadded)
    {
        // An empty collection ('[]') is handled with the closing bracket so the single inner space is
        // reported once; never force padding into an empty collection.
        if (current.IsKind(SyntaxKind.CloseBracketToken))
        {
            return;
        }

        if (collectionPadded)
        {
            if (separation == Separation.Adjacent)
            {
                Report(context, SpacingRules.OpeningSquareBracket, open, AddAfter);
            }
        }
        else if (separation == Separation.Space)
        {
            Report(context, SpacingRules.OpeningSquareBracket, current, RemoveBefore);
        }
    }

    /// <summary>Returns whether a '[' / ']' opens a collection expression or list pattern (the literal '[...]' forms).</summary>
    /// <param name="parent">The bracket token's parent node.</param>
    /// <returns><see langword="true"/> for a collection expression or list pattern.</returns>
    private static bool IsCollectionLikeBracket(SyntaxNode? parent)
        => parent is CollectionExpressionSyntax or ListPatternSyntax;

    /// <summary>Reports a space inside parentheses — after '(' (SST1008) or before ')' (SST1009).</summary>
    /// <param name="context">The syntax tree analysis context.</param>
    /// <param name="previous">The earlier token.</param>
    /// <param name="current">The later token.</param>
    private static void CheckParentheses(SyntaxTreeAnalysisContext context, SyntaxToken previous, SyntaxToken current)
    {
        if (previous.IsKind(SyntaxKind.OpenParenToken))
        {
            Report(context, SpacingRules.OpeningParenthesis, current, RemoveBefore);
        }
        else if (current.IsKind(SyntaxKind.CloseParenToken))
        {
            Report(context, SpacingRules.ClosingParenthesis, current, RemoveBefore);
        }
    }

    /// <summary>Reports an operator keyword not followed by a space (SST1007).</summary>
    /// <param name="context">The syntax tree analysis context.</param>
    /// <param name="previous">The earlier token.</param>
    /// <param name="separation">The separation to the following token.</param>
    private static void CheckOperatorKeyword(SyntaxTreeAnalysisContext context, SyntaxToken previous, Separation separation)
    {
        if (separation != Separation.Adjacent || !previous.IsKind(SyntaxKind.OperatorKeyword))
        {
            return;
        }

        Report(context, SpacingRules.OperatorKeywordSpacing, previous, AddAfter);
    }

    /// <summary>Reports a control-flow keyword not followed by a space before its parenthesis (SST1000).</summary>
    /// <param name="context">The syntax tree analysis context.</param>
    /// <param name="previous">The earlier token.</param>
    /// <param name="current">The later token.</param>
    /// <param name="separation">The separation between the tokens.</param>
    private static void CheckKeyword(SyntaxTreeAnalysisContext context, SyntaxToken previous, SyntaxToken current, Separation separation)
    {
        if (separation != Separation.Adjacent || !current.IsKind(SyntaxKind.OpenParenToken) || !IsControlKeyword(previous))
        {
            return;
        }

        Report(context, SpacingRules.KeywordSpacing, previous, AddAfter);
    }

    /// <summary>Reports a binary operator missing a space on either side (SST1003).</summary>
    /// <param name="context">The syntax tree analysis context.</param>
    /// <param name="previous">The earlier token.</param>
    /// <param name="current">The later token.</param>
    /// <param name="separation">The separation between the tokens.</param>
    private static void CheckBinaryOperator(SyntaxTreeAnalysisContext context, SyntaxToken previous, SyntaxToken current, Separation separation)
    {
        if (separation != Separation.Adjacent)
        {
            return;
        }

        if (IsBinaryOperator(current))
        {
            Report(context, SpacingRules.OperatorSpacing, current, AddBefore);
        }
        else if (IsBinaryOperator(previous))
        {
            Report(context, SpacingRules.OperatorSpacing, previous, AddAfter);
        }
    }

    /// <summary>Reports a single-line brace block missing inner spacing — after '{' (SST1012) or before '}' (SST1013).</summary>
    /// <param name="context">The syntax tree analysis context.</param>
    /// <param name="previous">The earlier token.</param>
    /// <param name="current">The later token.</param>
    /// <param name="separation">The separation between the tokens.</param>
    private static void CheckBraces(SyntaxTreeAnalysisContext context, SyntaxToken previous, SyntaxToken current, Separation separation)
    {
        if (separation != Separation.Adjacent)
        {
            return;
        }

        if (previous.IsKind(SyntaxKind.OpenBraceToken) && !current.IsKind(SyntaxKind.CloseBraceToken) && previous.Parent is not InterpolationSyntax)
        {
            Report(context, SpacingRules.OpeningBrace, previous, AddAfter);
        }
        else if (current.IsKind(SyntaxKind.CloseBraceToken) && !previous.IsKind(SyntaxKind.OpenBraceToken) && current.Parent is not InterpolationSyntax)
        {
            Report(context, SpacingRules.ClosingBrace, current, AddBefore);
        }
    }

    /// <summary>Reports incorrect spacing around a colon for its context (SST1024).</summary>
    /// <param name="context">The syntax tree analysis context.</param>
    /// <param name="previous">The earlier token.</param>
    /// <param name="current">The later token.</param>
    /// <param name="separation">The separation between the tokens.</param>
    private static void CheckColon(SyntaxTreeAnalysisContext context, SyntaxToken previous, SyntaxToken current, Separation separation)
    {
        if (current.IsKind(SyntaxKind.ColonToken))
        {
            CheckColonBefore(context, current, separation);
        }

        if (!previous.IsKind(SyntaxKind.ColonToken))
        {
            return;
        }

        CheckColonAfter(context, previous, separation);
    }

    /// <summary>Reports the spacing before a colon based on its context.</summary>
    /// <param name="context">The syntax tree analysis context.</param>
    /// <param name="colon">The colon token.</param>
    /// <param name="separation">The separation to the token before it.</param>
    private static void CheckColonBefore(SyntaxTreeAnalysisContext context, SyntaxToken colon, Separation separation)
    {
        switch (separation)
        {
            case Separation.Adjacent when RequiresSpaceBothSides(colon):
                {
                    Report(context, SpacingRules.ColonSpacing, colon, AddBefore);
                    break;
                }

            case Separation.Space when RequiresNoSpaceBefore(colon):
                {
                    Report(context, SpacingRules.ColonSpacing, colon, RemoveBefore);
                    break;
                }
        }
    }

    /// <summary>Reports a colon that should be followed by a space but is not.</summary>
    /// <param name="context">The syntax tree analysis context.</param>
    /// <param name="colon">The colon token.</param>
    /// <param name="separation">The separation to the token after it.</param>
    private static void CheckColonAfter(SyntaxTreeAnalysisContext context, SyntaxToken colon, Separation separation)
    {
        if (separation != Separation.Adjacent || !(RequiresSpaceBothSides(colon) || RequiresNoSpaceBefore(colon)))
        {
            return;
        }

        Report(context, SpacingRules.ColonSpacing, colon, AddAfter);
    }

    /// <summary>Returns whether a colon should have a space on both sides (base list, ctor initializer, ternary, constraint).</summary>
    /// <param name="colon">The colon token.</param>
    /// <returns><see langword="true"/> when the colon needs a space on each side.</returns>
    private static bool RequiresSpaceBothSides(SyntaxToken colon)
        => colon.Parent is BaseListSyntax or ConstructorInitializerSyntax or ConditionalExpressionSyntax or TypeParameterConstraintClauseSyntax;

    /// <summary>Returns whether a colon should have no space before and a space after (label, named argument, attribute target).</summary>
    /// <param name="colon">The colon token.</param>
    /// <returns><see langword="true"/> when the colon needs no space before it.</returns>
    private static bool RequiresNoSpaceBefore(SyntaxToken colon)
        => colon.Parent is NameColonSyntax or SwitchLabelSyntax or LabeledStatementSyntax or AttributeTargetSpecifierSyntax;

    /// <summary>Returns whether the token is a control-flow keyword that takes a parenthesised clause.</summary>
    /// <param name="token">The token.</param>
    /// <returns><see langword="true"/> for if/while/for/foreach/switch/lock/using/fixed/catch.</returns>
    private static bool IsControlKeyword(SyntaxToken token)
        => token.IsKind(SyntaxKind.IfKeyword)
            || token.IsKind(SyntaxKind.WhileKeyword)
            || token.IsKind(SyntaxKind.ForKeyword)
            || token.IsKind(SyntaxKind.ForEachKeyword)
            || token.IsKind(SyntaxKind.SwitchKeyword)
            || token.IsKind(SyntaxKind.LockKeyword)
            || token.IsKind(SyntaxKind.UsingKeyword)
            || token.IsKind(SyntaxKind.FixedKeyword)
            || token.IsKind(SyntaxKind.CatchKeyword);

    /// <summary>Returns whether the token is the operator of a binary or assignment expression.</summary>
    /// <param name="token">The token.</param>
    /// <returns><see langword="true"/> when the token is a binary/assignment operator.</returns>
    private static bool IsBinaryOperator(SyntaxToken token) => token.Parent switch
    {
        BinaryExpressionSyntax binary => binary.OperatorToken == token,
        AssignmentExpressionSyntax assignment => assignment.OperatorToken == token,
        _ => false
    };

    /// <summary>Reports a space between an increment/decrement operator and its operand (SST1020).</summary>
    /// <param name="context">The syntax tree analysis context.</param>
    /// <param name="previous">The earlier token.</param>
    /// <param name="current">The later token.</param>
    private static void CheckIncrementDecrement(SyntaxTreeAnalysisContext context, SyntaxToken previous, SyntaxToken current)
    {
        var postfix = IsIncrementDecrement(current) && current.Parent is PostfixUnaryExpressionSyntax;
        var prefix = IsIncrementDecrement(previous) && previous.Parent is PrefixUnaryExpressionSyntax;
        if (!postfix && !prefix)
        {
            return;
        }

        Report(context, SpacingRules.IncrementDecrementSpacing, current, RemoveBefore);
    }

    /// <summary>Reports a space after a unary <c>-</c> or <c>+</c> sign (SST1021/SST1022).</summary>
    /// <param name="context">The syntax tree analysis context.</param>
    /// <param name="previous">The earlier token.</param>
    /// <param name="current">The later token.</param>
    private static void CheckUnarySign(SyntaxTreeAnalysisContext context, SyntaxToken previous, SyntaxToken current)
    {
        if (previous.Parent is not PrefixUnaryExpressionSyntax)
        {
            return;
        }

        if (previous.IsKind(SyntaxKind.MinusToken))
        {
            Report(context, SpacingRules.NegativeSignSpacing, current, RemoveBefore);
        }
        else if (previous.IsKind(SyntaxKind.PlusToken))
        {
            Report(context, SpacingRules.PositiveSignSpacing, current, RemoveBefore);
        }
    }

    /// <summary>Reports incorrect spacing before a closing square bracket (SST1011), honouring the collection-expression style.</summary>
    /// <param name="context">The syntax tree analysis context.</param>
    /// <param name="previous">The earlier token.</param>
    /// <param name="current">The later token (the candidate ']').</param>
    /// <param name="separation">The whitespace separation between the tokens.</param>
    /// <param name="collectionPadded">Whether collection-expression brackets are padded ('[ 1 ]').</param>
    private static void CheckClosingBracket(SyntaxTreeAnalysisContext context, SyntaxToken previous, SyntaxToken current, Separation separation, bool collectionPadded)
    {
        if (!current.IsKind(SyntaxKind.CloseBracketToken) || current.Parent is AttributeListSyntax)
        {
            return;
        }

        if (IsCollectionLikeBracket(current.Parent))
        {
            CheckCollectionInnerBeforeClose(context, previous, current, separation, collectionPadded);
        }
        else if (separation == Separation.Space)
        {
            // Element-access / array ']' must not be preceded by a space.
            Report(context, SpacingRules.ClosingSquareBracket, current, RemoveBefore);
        }
    }

    /// <summary>Applies the configured collection-expression inner-spacing style to the space before ']'.</summary>
    /// <param name="context">The syntax tree analysis context.</param>
    /// <param name="previous">The token preceding the bracket.</param>
    /// <param name="current">The closing bracket token.</param>
    /// <param name="separation">The whitespace separation between the tokens.</param>
    /// <param name="collectionPadded">Whether collection-expression brackets are padded ('[ 1 ]').</param>
    private static void CheckCollectionInnerBeforeClose(SyntaxTreeAnalysisContext context, SyntaxToken previous, SyntaxToken current, Separation separation, bool collectionPadded)
    {
        // An empty collection ('[]' or '[ ]') is kept tight and never forced to pad.
        if (previous.IsKind(SyntaxKind.OpenBracketToken))
        {
            if (!collectionPadded && separation == Separation.Space)
            {
                Report(context, SpacingRules.ClosingSquareBracket, current, RemoveBefore);
            }

            return;
        }

        if (collectionPadded)
        {
            if (separation == Separation.Adjacent)
            {
                Report(context, SpacingRules.ClosingSquareBracket, current, AddBefore);
            }
        }
        else if (separation == Separation.Space)
        {
            Report(context, SpacingRules.ClosingSquareBracket, current, RemoveBefore);
        }
    }

    /// <summary>Reads the collection-expression inner-spacing style ('none' tight, default; 'space' padded).</summary>
    /// <param name="options">The analyzer config options for the syntax tree.</param>
    /// <returns><see langword="true"/> when collection expressions should be padded ('[ 1, 2 ]').</returns>
    private static bool ReadCollectionExpressionPadded(AnalyzerConfigOptions options)
        => options.TryGetValue(CollectionExpressionSpacingKey, out var value)
            && string.Equals(value, "space", StringComparison.OrdinalIgnoreCase);

    /// <summary>Returns whether the token is an increment or decrement operator.</summary>
    /// <param name="token">The token.</param>
    /// <returns><see langword="true"/> for <c>++</c> or <c>--</c>.</returns>
    private static bool IsIncrementDecrement(SyntaxToken token)
        => token.IsKind(SyntaxKind.PlusPlusToken) || token.IsKind(SyntaxKind.MinusMinusToken);

    /// <summary>Checks comma and semicolon spacing for the pair.</summary>
    /// <param name="context">The syntax tree analysis context.</param>
    /// <param name="previous">The earlier token.</param>
    /// <param name="current">The later token.</param>
    /// <param name="separation">The whitespace separation between the tokens.</param>
    private static void CheckCommaSemicolon(SyntaxTreeAnalysisContext context, SyntaxToken previous, SyntaxToken current, Separation separation)
    {
        if (separation == Separation.Space)
        {
            ReportPrecedingSpace(context, current);
        }

        if (separation != Separation.Adjacent || IsClosing(current))
        {
            return;
        }

        ReportTrailingPunctuation(context, previous, current);
    }

    /// <summary>Reports a space adjacent to a member-access dot (SST1019).</summary>
    /// <param name="context">The syntax tree analysis context.</param>
    /// <param name="previous">The earlier token.</param>
    /// <param name="current">The later token.</param>
    private static void CheckMemberDot(SyntaxTreeAnalysisContext context, SyntaxToken previous, SyntaxToken current)
    {
        if (!IsMemberDot(current) && !IsMemberDot(previous))
        {
            return;
        }

        Report(context, SpacingRules.MemberAccessSpacing, current, RemoveBefore);
    }

    /// <summary>Reports a space inside a generic bracket pair (SST1014/SST1015).</summary>
    /// <param name="context">The syntax tree analysis context.</param>
    /// <param name="previous">The earlier token.</param>
    /// <param name="current">The later token.</param>
    private static void CheckGenericBrackets(SyntaxTreeAnalysisContext context, SyntaxToken previous, SyntaxToken current)
    {
        if (IsGenericOpen(current) || IsGenericOpen(previous))
        {
            Report(context, SpacingRules.OpeningGenericBracket, current, RemoveBefore);
        }
        else if (IsGenericClose(current))
        {
            Report(context, SpacingRules.ClosingGenericBracket, current, RemoveBefore);
        }
    }

    /// <summary>Reports a space inside an attribute bracket pair (SST1016/SST1017).</summary>
    /// <param name="context">The syntax tree analysis context.</param>
    /// <param name="previous">The earlier token.</param>
    /// <param name="current">The later token.</param>
    private static void CheckAttributeBrackets(SyntaxTreeAnalysisContext context, SyntaxToken previous, SyntaxToken current)
    {
        if (IsAttributeBracket(previous, SyntaxKind.OpenBracketToken))
        {
            Report(context, SpacingRules.OpeningAttributeBracket, current, RemoveBefore);
        }
        else if (IsAttributeBracket(current, SyntaxKind.CloseBracketToken))
        {
            Report(context, SpacingRules.ClosingAttributeBracket, current, RemoveBefore);
        }
    }

    /// <summary>Reports a space before a nullable type's question mark (SST1018).</summary>
    /// <param name="context">The syntax tree analysis context.</param>
    /// <param name="current">The later token.</param>
    private static void CheckNullable(SyntaxTreeAnalysisContext context, SyntaxToken current)
    {
        if (!current.IsKind(SyntaxKind.QuestionToken) || current.Parent is not NullableTypeSyntax)
        {
            return;
        }

        Report(context, SpacingRules.NullableSpacing, current, RemoveBefore);
    }

    /// <summary>Reports a space between <c>new</c>/<c>stackalloc</c> and an implicit array bracket (SST1026).</summary>
    /// <param name="context">The syntax tree analysis context.</param>
    /// <param name="previous">The earlier token.</param>
    /// <param name="current">The later token.</param>
    private static void CheckImplicitArray(SyntaxTreeAnalysisContext context, SyntaxToken previous, SyntaxToken current)
    {
        if (!current.IsKind(SyntaxKind.OpenBracketToken) || (!previous.IsKind(SyntaxKind.NewKeyword) && !previous.IsKind(SyntaxKind.StackAllocKeyword)))
        {
            return;
        }

        Report(context, SpacingRules.ImplicitArraySpacing, current, RemoveBefore);
    }

    /// <summary>Returns whether the token is the dot of a member access or qualified name.</summary>
    /// <param name="token">The token.</param>
    /// <returns><see langword="true"/> when the token is a member-access dot.</returns>
    private static bool IsMemberDot(SyntaxToken token)
        => token.IsKind(SyntaxKind.DotToken)
            && token.Parent is MemberAccessExpressionSyntax or QualifiedNameSyntax or MemberBindingExpressionSyntax;

    /// <summary>Returns whether the token opens a generic argument or parameter list.</summary>
    /// <param name="token">The token.</param>
    /// <returns><see langword="true"/> when the token is a generic <c>&lt;</c>.</returns>
    private static bool IsGenericOpen(SyntaxToken token)
        => token.IsKind(SyntaxKind.LessThanToken) && token.Parent is TypeArgumentListSyntax or TypeParameterListSyntax;

    /// <summary>Returns whether the token closes a generic argument or parameter list.</summary>
    /// <param name="token">The token.</param>
    /// <returns><see langword="true"/> when the token is a generic <c>&gt;</c>.</returns>
    private static bool IsGenericClose(SyntaxToken token)
        => token.IsKind(SyntaxKind.GreaterThanToken) && token.Parent is TypeArgumentListSyntax or TypeParameterListSyntax;

    /// <summary>Returns whether the token is the requested bracket of an attribute list.</summary>
    /// <param name="token">The token.</param>
    /// <param name="kind">The bracket kind to match.</param>
    /// <returns><see langword="true"/> when the token is that bracket of an attribute list.</returns>
    private static bool IsAttributeBracket(SyntaxToken token, SyntaxKind kind)
        => token.IsKind(kind) && token.Parent is AttributeListSyntax;

    /// <summary>Reports a comma or semicolon preceded by whitespace.</summary>
    /// <param name="context">The syntax tree analysis context.</param>
    /// <param name="current">The candidate punctuation token.</param>
    private static void ReportPrecedingSpace(SyntaxTreeAnalysisContext context, SyntaxToken current)
    {
        if (current.IsKind(SyntaxKind.CommaToken))
        {
            Report(context, SpacingRules.CommaSpacing, current, RemoveBefore);
        }
        else if (current.IsKind(SyntaxKind.SemicolonToken))
        {
            Report(context, SpacingRules.SemicolonSpacing, current, RemoveBefore);
        }
    }

    /// <summary>Reports a comma or semicolon not followed by a space.</summary>
    /// <param name="context">The syntax tree analysis context.</param>
    /// <param name="previous">The candidate punctuation token.</param>
    /// <param name="current">The token following it.</param>
    private static void ReportTrailingPunctuation(SyntaxTreeAnalysisContext context, SyntaxToken previous, SyntaxToken current)
    {
        if (previous.IsKind(SyntaxKind.CommaToken))
        {
            Report(context, SpacingRules.CommaSpacing, previous, AddAfter);
        }
        else if (previous.IsKind(SyntaxKind.SemicolonToken) && !current.IsKind(SyntaxKind.SemicolonToken))
        {
            Report(context, SpacingRules.SemicolonSpacing, previous, AddAfter);
        }
    }

    /// <summary>Reports a spacing diagnostic on a punctuation token, stashing the fix action.</summary>
    /// <param name="context">The syntax tree analysis context.</param>
    /// <param name="rule">The descriptor to report.</param>
    /// <param name="token">The punctuation token.</param>
    /// <param name="action">The fix action to stash.</param>
    private static void Report(SyntaxTreeAnalysisContext context, DiagnosticDescriptor rule, SyntaxToken token, string action)
    {
        var properties = ActionProperties(action);
        context.ReportDiagnostic(DiagnosticHelper.Create(rule, token.GetLocation(), properties));
    }

    /// <summary>Reports a spacing diagnostic on a text span, stashing the fix action.</summary>
    /// <param name="context">The syntax tree analysis context.</param>
    /// <param name="rule">The descriptor to report.</param>
    /// <param name="span">The span to flag.</param>
    /// <param name="action">The fix action to stash.</param>
    private static void ReportSpan(SyntaxTreeAnalysisContext context, DiagnosticDescriptor rule, TextSpan span, string action)
    {
        var properties = ActionProperties(action);
        context.ReportDiagnostic(DiagnosticHelper.Create(rule, context.Tree, span, properties));
    }

    /// <summary>Returns the cached diagnostic property bag for the requested spacing fix action.</summary>
    /// <param name="action">The fix action to stash in the diagnostic.</param>
    /// <returns>The cached diagnostic properties.</returns>
    private static ImmutableDictionary<string, string?> ActionProperties(string action) => action switch
    {
        AddAfter => AddAfterProperties,
        AddBefore => AddBeforeProperties,
        RemoveAfter => RemoveAfterProperties,
        _ => RemoveBeforeProperties
    };

    /// <summary>Returns the whitespace separation between two adjacent tokens.</summary>
    /// <param name="previous">The earlier token.</param>
    /// <param name="current">The later token.</param>
    /// <returns>The separation kind.</returns>
    private static Separation Between(SyntaxToken previous, SyntaxToken current)
    {
        var sawSpace = false;
        foreach (var trivia in previous.TrailingTrivia)
        {
            if (trivia.IsKind(SyntaxKind.EndOfLineTrivia))
            {
                return Separation.NewLine;
            }

            sawSpace = true;
        }

        foreach (var trivia in current.LeadingTrivia)
        {
            if (trivia.IsKind(SyntaxKind.EndOfLineTrivia))
            {
                return Separation.NewLine;
            }

            sawSpace = true;
        }

        return sawSpace ? Separation.Space : Separation.Adjacent;
    }

    /// <summary>Returns whether the token is a closing bracket, brace, or parenthesis.</summary>
    /// <param name="token">The token.</param>
    /// <returns><see langword="true"/> when the token closes a bracketed construct.</returns>
    private static bool IsClosing(SyntaxToken token)
        => token.IsKind(SyntaxKind.CloseParenToken) || token.IsKind(SyntaxKind.CloseBracketToken) || token.IsKind(SyntaxKind.CloseBraceToken);

    /// <summary>Returns whether the token is a unary dereference ('*') or address-of ('&amp;') operator.</summary>
    /// <param name="token">The token.</param>
    /// <returns><see langword="true"/> when the token is the operator of a pointer-indirection or address-of expression.</returns>
    private static bool IsDereferenceOrAddressOf(SyntaxToken token) =>
        (token.IsKind(SyntaxKind.AsteriskToken) || token.IsKind(SyntaxKind.AmpersandToken))
            && token.Parent is PrefixUnaryExpressionSyntax unary
            && (unary.IsKind(SyntaxKind.PointerIndirectionExpression) || unary.IsKind(SyntaxKind.AddressOfExpression));

    /// <summary>Applies the trivia spacing rules to a single token's leading or trailing trivia.</summary>
    /// <param name="context">The syntax tree analysis context.</param>
    /// <param name="text">The source text.</param>
    /// <param name="list">The trivia list.</param>
    /// <param name="isTrailing">Whether the list is trailing trivia (always mid-line).</param>
    private static void ProcessTrivia(SyntaxTreeAnalysisContext context, SourceText text, SyntaxTriviaList list, bool isTrailing)
    {
        for (var index = 0; index < list.Count; index++)
        {
            var trivia = list[index];
            if (trivia.IsKind(SyntaxKind.WhitespaceTrivia))
            {
                CheckWhitespace(context, text, list, index, isTrailing);
            }
            else if (trivia.IsKind(SyntaxKind.SingleLineCommentTrivia))
            {
                CheckComment(context, text, trivia);
            }
            else if (trivia.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia))
            {
                CheckDocComment(context, text, trivia);
            }
            else if (trivia.IsDirective)
            {
                CheckDirective(context, trivia);
            }
        }
    }

    /// <summary>Reports trailing-whitespace, tab, and multiple-whitespace violations for a whitespace trivia.</summary>
    /// <param name="context">The syntax tree analysis context.</param>
    /// <param name="text">The source text.</param>
    /// <param name="list">The trivia list.</param>
    /// <param name="index">The index of the whitespace trivia.</param>
    /// <param name="isTrailing">Whether the list is trailing trivia.</param>
    private static void CheckWhitespace(SyntaxTreeAnalysisContext context, SourceText text, SyntaxTriviaList list, int index, bool isTrailing)
    {
        var span = list[index].Span;
        var endsLine = (index + 1 < list.Count && list[index + 1].IsKind(SyntaxKind.EndOfLineTrivia)) || span.End == text.Length;
        if (endsLine)
        {
            context.ReportDiagnostic(DiagnosticHelper.Create(SpacingRules.NoTrailingWhitespace, context.Tree, span));
            return;
        }

        if (ContainsTab(text, span))
        {
            context.ReportDiagnostic(DiagnosticHelper.Create(SpacingRules.UseSpacesNotTabs, context.Tree, span));
            return;
        }

        if (!isTrailing || span.Length <= 1)
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(SpacingRules.MultipleWhitespace, context.Tree, span));
    }

    /// <summary>Reports a single-line comment that does not begin with a single space.</summary>
    /// <param name="context">The syntax tree analysis context.</param>
    /// <param name="text">The source text.</param>
    /// <param name="trivia">The comment trivia.</param>
    private static void CheckComment(SyntaxTreeAnalysisContext context, SourceText text, SyntaxTrivia trivia)
    {
        var span = trivia.Span;
        if (span.Length <= CommentOpenerLength)
        {
            return;
        }

        var first = text[span.Start + CommentOpenerLength];
        if (first is ' ' or '/')
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(SpacingRules.CommentBeginsWithSpace, context.Tree, trivia.Span));
    }

    /// <summary>Reports each documentation line whose text abuts the '///' with no separating space.</summary>
    /// <param name="context">The syntax tree analysis context.</param>
    /// <param name="text">The source text.</param>
    /// <param name="trivia">The single-line documentation comment trivia.</param>
    private static void CheckDocComment(SyntaxTreeAnalysisContext context, SourceText text, SyntaxTrivia trivia)
    {
        var span = trivia.FullSpan;
        var end = span.End;
        var pos = span.Start;
        while (pos + DocExteriorLength <= end)
        {
            if (!IsDocExterior(text, span.Start, pos, end))
            {
                pos++;
                continue;
            }

            if (DocTextAbutsExterior(text, pos + DocExteriorLength, end))
            {
                ReportSpan(context, SpacingRules.DocumentationBeginsWithSpace, new(pos, DocExteriorLength), AddAfter);
            }

            pos += DocExteriorLength;
        }
    }

    /// <summary>Returns whether a '///' documentation exterior opens at the given position.</summary>
    /// <param name="text">The source text.</param>
    /// <param name="triviaStart">The start of the documentation trivia.</param>
    /// <param name="pos">The candidate position.</param>
    /// <param name="end">The end of the documentation trivia.</param>
    /// <returns><see langword="true"/> when a line-opening '///' (not '////') starts at the position.</returns>
    private static bool IsDocExterior(SourceText text, int triviaStart, int pos, int end)
    {
        for (var offset = 0; offset < DocExteriorLength; offset++)
        {
            if (text[pos + offset] != '/')
            {
                return false;
            }
        }

        // Only a '///' that opens a line (after a line break or indent) is an exterior marker.
        if (pos > triviaStart && text[pos - 1] is not ('\n' or '\r' or ' ' or '\t'))
        {
            return false;
        }

        // A fourth slash makes this a plain comment marker, not a documentation line.
        var after = pos + DocExteriorLength;
        return after >= end || text[after] != '/';
    }

    /// <summary>Returns whether documentation text immediately abuts the '///' with no separating whitespace.</summary>
    /// <param name="text">The source text.</param>
    /// <param name="after">The position immediately after the '///'.</param>
    /// <param name="end">The end of the documentation trivia.</param>
    /// <returns><see langword="true"/> when a non-whitespace character abuts the exterior.</returns>
    private static bool DocTextAbutsExterior(SourceText text, int after, int end)
        => after < end && text[after] is not (' ' or '\t' or '\r' or '\n');

    /// <summary>Reports a preprocessor keyword that is separated from its '#' by whitespace.</summary>
    /// <param name="context">The syntax tree analysis context.</param>
    /// <param name="trivia">The directive trivia.</param>
    private static void CheckDirective(SyntaxTreeAnalysisContext context, SyntaxTrivia trivia)
    {
        if (trivia.GetStructure() is not DirectiveTriviaSyntax directive)
        {
            return;
        }

        var hash = directive.HashToken;
        var keyword = default(SyntaxToken);
        var seenHash = false;
        foreach (var child in directive.ChildTokens())
        {
            if (!seenHash)
            {
                seenHash = true;
                continue;
            }

            keyword = child;
            break;
        }

        if (keyword.IsKind(SyntaxKind.None) || keyword.SpanStart <= hash.Span.End)
        {
            return;
        }

        Report(context, SpacingRules.PreprocessorKeywordSpacing, keyword, RemoveBefore);
    }

    /// <summary>Returns whether the span contains a tab character.</summary>
    /// <param name="text">The source text.</param>
    /// <param name="span">The span to scan.</param>
    /// <returns><see langword="true"/> when a tab is present.</returns>
    private static bool ContainsTab(SourceText text, TextSpan span)
    {
        for (var position = span.Start; position < span.End; position++)
        {
            if (text[position] == '\t')
            {
                return true;
            }
        }

        return false;
    }
}
