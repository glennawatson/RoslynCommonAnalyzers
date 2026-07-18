// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a run of consecutive local declarations that each copy one member of the same value
/// into its own local — <c>var a = pair.Item1; var b = pair.Item2;</c>, or
/// <c>var x = point.X; var y = point.Y;</c> when <c>point</c> exposes a matching
/// <c>Deconstruct(out …)</c> — where a single deconstruction says the same thing (SST2247).
/// </summary>
/// <remarks>
/// The value being read stays; only the member copies fold into <c>var (a, b) = pair;</c>. The rule
/// fires only when the source is a side-effect-free local or parameter, the reads cover every
/// position of a tuple (or a matching <c>Deconstruct</c> arity) in order, and the value is
/// deconstructible with that arity. A value declared in the immediately preceding statement is left
/// to the tuple-temporary rule so the two never both fire. The clean path is pure syntax and never
/// touches the semantic model.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2247MemberCopyDeconstructionAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The numeric C# 7 language-version value, the first with deconstruction declarations.</summary>
    private const int CSharp7 = 7;

    /// <summary>The smallest run of member copies worth folding into a deconstruction.</summary>
    private const int MinimumMemberCopies = 2;

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(ModernSyntaxRules.DeconstructMemberCopies);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(AnalyzeLocalDeclaration, SyntaxKind.LocalDeclarationStatement);
    }

    /// <summary>Resolves the deconstruction candidate that starts at a local declaration.</summary>
    /// <param name="first">The candidate first member-copy statement.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="cancellationToken">A token that cancels analysis.</param>
    /// <param name="candidate">The resolved candidate.</param>
    /// <returns><see langword="true"/> when the statement starts a foldable member-copy run.</returns>
    internal static bool TryGetCandidate(
        LocalDeclarationStatementSyntax first,
        SemanticModel model,
        CancellationToken cancellationToken,
        out MemberCopyDeconstruction candidate)
    {
        candidate = default;
        if (!TryGetMemberCopyRun(first, out var block, out var startIndex, out var count, out var sourceName))
        {
            return false;
        }

        var members = new MemberCopyInfo[count];
        var names = new string[count];
        for (var i = 0; i < count; i++)
        {
            if (!TryReadMemberCopy(block.Statements[startIndex + i], out members[i]))
            {
                return false;
            }

            names[i] = members[i].LocalName;
        }

        if (!TryValidateSource(members[0].SourceIdentifier, block, startIndex, members, model, cancellationToken))
        {
            return false;
        }

        var last = (LocalDeclarationStatementSyntax)block.Statements[startIndex + count - 1];
        candidate = new MemberCopyDeconstruction(first, last, block, startIndex, count, sourceName, names);
        return true;
    }

    /// <summary>Reports a foldable member-copy run at its first statement.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void AnalyzeLocalDeclaration(SyntaxNodeAnalysisContext context)
    {
        var first = (LocalDeclarationStatementSyntax)context.Node;
        if (!TryGetCandidate(first, context.SemanticModel, context.CancellationToken, out var candidate))
        {
            return;
        }

        context.ReportDiagnostic(
            DiagnosticHelper.Create(ModernSyntaxRules.DeconstructMemberCopies, candidate.FirstStatement.GetLocation(), candidate.SourceName));
    }

    /// <summary>Finds the block, start index and length of a member-copy run using syntax only.</summary>
    /// <param name="first">The candidate first statement.</param>
    /// <param name="block">The containing block.</param>
    /// <param name="startIndex">The run's first-statement index.</param>
    /// <param name="count">The run length.</param>
    /// <param name="sourceName">The shared source identifier name.</param>
    /// <returns><see langword="true"/> when a two-or-more member-copy run starts at <paramref name="first"/>.</returns>
    private static bool TryGetMemberCopyRun(
        LocalDeclarationStatementSyntax first,
        out BlockSyntax block,
        out int startIndex,
        out int count,
        out string sourceName)
    {
        block = null!;
        startIndex = -1;
        count = 0;
        sourceName = string.Empty;
        if (first.SyntaxTree.Options is not CSharpParseOptions { LanguageVersion: var version } || (int)version < CSharp7
            || !TryReadMemberCopy(first, out var firstCopy)
            || first.Parent is not BlockSyntax parent
            || !TryGetStatementIndex(parent, first, out var index)
            || IsContinuationOfRun(parent, index, firstCopy.SourceName))
        {
            return false;
        }

        var length = MeasureRun(parent, index, firstCopy.SourceName);
        if (length < MinimumMemberCopies)
        {
            return false;
        }

        block = parent;
        startIndex = index;
        count = length;
        sourceName = firstCopy.SourceName;
        return true;
    }

    /// <summary>Reads a <c>var name = source.member;</c> statement.</summary>
    /// <param name="statement">The candidate statement.</param>
    /// <param name="info">The parsed member-copy shape.</param>
    /// <returns><see langword="true"/> when the statement copies one member of one identifier into one <c>var</c> local.</returns>
    private static bool TryReadMemberCopy(StatementSyntax statement, out MemberCopyInfo info)
    {
        info = default;
        if (statement is not LocalDeclarationStatementSyntax local
            || local.UsingKeyword.IsKind(SyntaxKind.UsingKeyword)
            || local.Modifiers.Count != 0
            || local.Declaration.Type is not IdentifierNameSyntax { Identifier.ValueText: "var" }
            || local.Declaration.Variables.Count != 1)
        {
            return false;
        }

        var variable = local.Declaration.Variables[0];
        if (variable.Identifier.ValueText.Length == 0
            || !TryReadSourceMemberAccess(variable, out var source, out var memberAccess))
        {
            return false;
        }

        info = new MemberCopyInfo(variable.Identifier.ValueText, source, source.Identifier.ValueText, memberAccess);
        return true;
    }

    /// <summary>Reads the <c>source.member</c> initializer of a variable declarator.</summary>
    /// <param name="variable">The variable declarator.</param>
    /// <param name="source">The source identifier expression.</param>
    /// <param name="memberAccess">The member access being copied.</param>
    /// <returns><see langword="true"/> when the initializer is a simple member access on an identifier.</returns>
    private static bool TryReadSourceMemberAccess(
        VariableDeclaratorSyntax variable,
        out IdentifierNameSyntax source,
        out MemberAccessExpressionSyntax memberAccess)
    {
        source = null!;
        memberAccess = null!;
        if (variable.Initializer?.Value is not MemberAccessExpressionSyntax access
            || !access.IsKind(SyntaxKind.SimpleMemberAccessExpression)
            || access.Expression is not IdentifierNameSyntax identifier
            || access.Name is not IdentifierNameSyntax)
        {
            return false;
        }

        source = identifier;
        memberAccess = access;
        return true;
    }

    /// <summary>Returns whether the statement before a run reads the same source, making this a continuation.</summary>
    /// <param name="block">The containing block.</param>
    /// <param name="startIndex">The candidate first-statement index.</param>
    /// <param name="sourceName">The shared source identifier name.</param>
    /// <returns><see langword="true"/> when an earlier statement already starts the run.</returns>
    private static bool IsContinuationOfRun(BlockSyntax block, int startIndex, string sourceName)
        => startIndex > 0
            && TryReadMemberCopy(block.Statements[startIndex - 1], out var previous)
            && previous.SourceName == sourceName;

    /// <summary>Counts the consecutive member copies of one source starting at an index.</summary>
    /// <param name="block">The containing block.</param>
    /// <param name="startIndex">The first-statement index.</param>
    /// <param name="sourceName">The shared source identifier name.</param>
    /// <returns>The run length.</returns>
    private static int MeasureRun(BlockSyntax block, int startIndex, string sourceName)
    {
        var count = 1;
        for (var i = startIndex + 1; i < block.Statements.Count; i++)
        {
            if (!TryReadMemberCopy(block.Statements[i], out var info) || info.SourceName != sourceName)
            {
                break;
            }

            count++;
        }

        return count;
    }

    /// <summary>Validates that the source is a deconstructible local or parameter whose members are read in positional order.</summary>
    /// <param name="sourceIdentifier">The source identifier expression.</param>
    /// <param name="block">The containing block.</param>
    /// <param name="startIndex">The first-statement index.</param>
    /// <param name="members">The member copies in read order.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="cancellationToken">A token that cancels analysis.</param>
    /// <returns><see langword="true"/> when the run can become a deconstruction without changing behaviour.</returns>
    private static bool TryValidateSource(
        IdentifierNameSyntax sourceIdentifier,
        BlockSyntax block,
        int startIndex,
        MemberCopyInfo[] members,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        var sourceSymbol = model.GetSymbolInfo(sourceIdentifier, cancellationToken).Symbol;
        if (sourceSymbol is not (ILocalSymbol or IParameterSymbol)
            || (startIndex > 0 && IsDeclaredInStatement(sourceSymbol, block.Statements[startIndex - 1], cancellationToken))
            || model.GetTypeInfo(sourceIdentifier, cancellationToken).Type is not { } sourceType)
        {
            return false;
        }

        return MatchesTupleElements(sourceType, members, model, cancellationToken)
            || MatchesDeconstructParameters(sourceType, members, model, cancellationToken);
    }

    /// <summary>Returns whether the source's members are its tuple elements read in positional order.</summary>
    /// <param name="sourceType">The source type.</param>
    /// <param name="members">The member copies in read order.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="cancellationToken">A token that cancels analysis.</param>
    /// <returns><see langword="true"/> for a full, in-order tuple element read.</returns>
    private static bool MatchesTupleElements(
        ITypeSymbol sourceType,
        MemberCopyInfo[] members,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        if (sourceType is not INamedTypeSymbol { IsTupleType: true } tuple || tuple.TupleElements.Length != members.Length)
        {
            return false;
        }

        for (var i = 0; i < members.Length; i++)
        {
            if (model.GetSymbolInfo(members[i].MemberAccess.Name, cancellationToken).Symbol is not IFieldSymbol field)
            {
                return false;
            }

            var element = tuple.TupleElements[i];
            if (!SymbolEqualityComparer.Default.Equals(
                    field.CorrespondingTupleField ?? field,
                    element.CorrespondingTupleField ?? element))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Returns whether the source's members map to a single matching <c>Deconstruct</c> in positional order.</summary>
    /// <param name="sourceType">The source type.</param>
    /// <param name="members">The member copies in read order.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="cancellationToken">A token that cancels analysis.</param>
    /// <returns><see langword="true"/> when each member name matches the same-position <c>Deconstruct</c> out parameter.</returns>
    private static bool MatchesDeconstructParameters(
        ITypeSymbol sourceType,
        MemberCopyInfo[] members,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        if (FindDeconstruct(sourceType, members.Length) is not { } deconstruct)
        {
            return false;
        }

        for (var i = 0; i < members.Length; i++)
        {
            if (model.GetSymbolInfo(members[i].MemberAccess, cancellationToken).Symbol is not { } member
                || !string.Equals(member.Name, deconstruct.Parameters[i].Name, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Finds the single public instance <c>Deconstruct</c> with the given all-out arity.</summary>
    /// <param name="type">The source type.</param>
    /// <param name="arity">The required parameter count.</param>
    /// <returns>The matching method, or <see langword="null"/> when none or several match.</returns>
    private static IMethodSymbol? FindDeconstruct(ITypeSymbol type, int arity)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            var members = current.GetMembers("Deconstruct");
            IMethodSymbol? found = null;
            var matches = 0;
            for (var i = 0; i < members.Length; i++)
            {
                if (IsDeconstructMethod(members[i], arity))
                {
                    found = (IMethodSymbol)members[i];
                    matches++;
                }
            }

            if (matches == 1)
            {
                return found;
            }

            if (matches > 1)
            {
                return null;
            }
        }

        return null;
    }

    /// <summary>Returns whether a symbol is a public instance <c>Deconstruct</c> with an all-out parameter list of the given arity.</summary>
    /// <param name="symbol">The candidate symbol.</param>
    /// <param name="arity">The required parameter count.</param>
    /// <returns><see langword="true"/> when the symbol is a usable <c>Deconstruct</c>.</returns>
    private static bool IsDeconstructMethod(ISymbol symbol, int arity)
        => symbol is IMethodSymbol { IsStatic: false, DeclaredAccessibility: Accessibility.Public, ReturnsVoid: true } method
            && method.Parameters.Length == arity
            && AllParametersAreOut(method);

    /// <summary>Returns whether every parameter of a method is an <c>out</c> parameter.</summary>
    /// <param name="method">The method to inspect.</param>
    /// <returns><see langword="true"/> when the parameter list is all <c>out</c>.</returns>
    private static bool AllParametersAreOut(IMethodSymbol method)
    {
        for (var i = 0; i < method.Parameters.Length; i++)
        {
            if (method.Parameters[i].RefKind != RefKind.Out)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Returns whether a symbol is declared by the given local-declaration statement.</summary>
    /// <param name="symbol">The source symbol.</param>
    /// <param name="statement">The candidate declaring statement.</param>
    /// <param name="cancellationToken">A token that cancels analysis.</param>
    /// <returns><see langword="true"/> when the symbol's declaration is that statement.</returns>
    private static bool IsDeclaredInStatement(ISymbol symbol, StatementSyntax statement, CancellationToken cancellationToken)
    {
        var references = symbol.DeclaringSyntaxReferences;
        for (var i = 0; i < references.Length; i++)
        {
            if (references[i].GetSyntax(cancellationToken).FirstAncestorOrSelf<LocalDeclarationStatementSyntax>() is { } declaration
                && declaration.Span == statement.Span)
            {
                return true;
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

    /// <summary>A resolved run of member copies that can fold into one deconstruction.</summary>
    /// <param name="FirstStatement">The run's first statement.</param>
    /// <param name="LastStatement">The run's last statement.</param>
    /// <param name="Block">The containing block.</param>
    /// <param name="StartIndex">The first-statement index in the block.</param>
    /// <param name="Count">The run length.</param>
    /// <param name="SourceName">The shared source identifier name.</param>
    /// <param name="Names">The declared local names in read order.</param>
    internal readonly record struct MemberCopyDeconstruction(
        LocalDeclarationStatementSyntax FirstStatement,
        LocalDeclarationStatementSyntax LastStatement,
        BlockSyntax Block,
        int StartIndex,
        int Count,
        string SourceName,
        string[] Names);

    /// <summary>One parsed <c>var name = source.member;</c> statement.</summary>
    /// <param name="LocalName">The declared local name.</param>
    /// <param name="SourceIdentifier">The source identifier expression.</param>
    /// <param name="SourceName">The source identifier name.</param>
    /// <param name="MemberAccess">The member access being copied.</param>
    private readonly record struct MemberCopyInfo(
        string LocalName,
        IdentifierNameSyntax SourceIdentifier,
        string SourceName,
        MemberAccessExpressionSyntax MemberAccess);
}
