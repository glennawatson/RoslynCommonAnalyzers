// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports two methods declared in the same type whose bodies are identical token for token (SST2318). That
/// is almost always a copy-paste where the second method was meant to differ and never got changed, so it
/// silently does the first method's work.
/// </summary>
/// <remarks>
/// <para>
/// Off by default. Identical bodies are sometimes legitimate, so the rule ships disabled and only reports the
/// duplicates most likely to be a mistake: two ordinary methods whose bodies are <em>non-trivial</em>. A body
/// is trivial — and ignored — when it is a block of fewer than two statements, or an expression body that is
/// just a literal, a name or member access, a <c>throw</c>, <c>default</c>, or <c>null</c>. A one-line
/// forwarding method or a shared <c>throw new NotImplementedException();</c> is expected to repeat and is left
/// alone.
/// </para>
/// <para>
/// Only methods that take the same parameter types are compared. The rule's remedy is to have one method call
/// the other, which is impossible when the second cannot supply the first's arguments, and matching tokens do
/// not mean matching work once the names in them bind to different types — two event handlers that both read
/// <c>e.PropertyName</c> off a different <c>EventArgs</c> are not a copy-paste. Parameter <em>names</em> are
/// deliberately not part of the comparison, so two methods that differ only in what they call an argument
/// still match.
/// </para>
/// <para>
/// Bodies are compared by their normalized token stream, so whitespace and comments do not matter and only the
/// second of a matching pair is reported. Methods with no body at all — <c>abstract</c>, <c>extern</c>, and
/// unimplemented <c>partial</c> declarations — never qualify. The clean path is a single count of eligible
/// methods; the token key is built only when a type has at least two of them.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2318DuplicateMemberBodyAnalyzer : DiagnosticAnalyzer
{
    /// <summary>Separates adjacent tokens in a body key so two short tokens cannot spell one longer one.</summary>
    private const char TokenSeparator = (char)1;

    /// <summary>Separates one parameter from the next, and the signature from the body, in a body key.</summary>
    private const char SectionSeparator = (char)2;

    /// <summary>The number of matching methods a type needs before a duplicate body is possible.</summary>
    private const int MinimumDuplicateCandidates = 2;

    /// <summary>The number of statements a block body needs before it counts as non-trivial.</summary>
    private const int MinimumBlockStatements = 2;

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(DesignRules.DuplicateMemberBody);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(
            Analyze,
            SyntaxKind.ClassDeclaration,
            SyntaxKind.StructDeclaration,
            SyntaxKind.RecordDeclaration,
            SyntaxKind.RecordStructDeclaration,
            SyntaxKind.InterfaceDeclaration);
    }

    /// <summary>Reports each method whose body repeats an earlier method's body in the same type.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var type = (TypeDeclarationSyntax)context.Node;
        var members = type.Members;

        // Cheap prepass: a type needs at least two methods with a non-trivial body before any token key or
        // tracking dictionary is worth allocating. Most types never clear this bar.
        if (CountEligibleMethods(members) < MinimumDuplicateCandidates)
        {
            return;
        }

        var bodiesByKey = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var i = 0; i < members.Count; i++)
        {
            if (members[i] is not MethodDeclarationSyntax method || !HasNonTrivialBody(method))
            {
                continue;
            }

            var key = BuildBodyKey(method);
            var name = method.Identifier.Text;
            if (bodiesByKey.TryGetValue(key, out var firstName))
            {
                context.ReportDiagnostic(DiagnosticHelper.Create(
                    DesignRules.DuplicateMemberBody,
                    method.Identifier.GetLocation(),
                    name,
                    firstName));
            }
            else
            {
                bodiesByKey[key] = name;
            }
        }
    }

    /// <summary>Counts, up to two, the methods in a type that carry a non-trivial body.</summary>
    /// <param name="members">The type's members.</param>
    /// <returns>The number of eligible methods, capped at two.</returns>
    private static int CountEligibleMethods(SyntaxList<MemberDeclarationSyntax> members)
    {
        var eligible = 0;
        for (var i = 0; i < members.Count; i++)
        {
            if (members[i] is MethodDeclarationSyntax method && HasNonTrivialBody(method))
            {
                eligible++;
                if (eligible >= MinimumDuplicateCandidates)
                {
                    return eligible;
                }
            }
        }

        return eligible;
    }

    /// <summary>Returns whether a method has a body substantial enough to be worth comparing.</summary>
    /// <param name="method">The method to test.</param>
    /// <returns><see langword="true"/> when the method has a non-trivial block or expression body.</returns>
    private static bool HasNonTrivialBody(MethodDeclarationSyntax method)
    {
        if (method.Body is { } block)
        {
            return block.Statements.Count >= MinimumBlockStatements;
        }

        return method.ExpressionBody is { } arrow && !IsTrivialExpression(arrow.Expression);
    }

    /// <summary>Returns whether an expression-bodied member says too little to be a meaningful duplicate.</summary>
    /// <param name="expression">The expression body's expression.</param>
    /// <returns><see langword="true"/> for a literal, a name, a member access, a throw, or a default.</returns>
    private static bool IsTrivialExpression(ExpressionSyntax expression)
        => expression is LiteralExpressionSyntax
            or IdentifierNameSyntax
            or MemberAccessExpressionSyntax
            or ThrowExpressionSyntax
            or DefaultExpressionSyntax;

    /// <summary>Builds a whitespace-insensitive key from a method's parameter types and body token stream.</summary>
    /// <param name="method">The method whose body is keyed.</param>
    /// <returns>The normalized token key.</returns>
    /// <remarks>
    /// Only the tokens are read, so trivia — whitespace and comments — is ignored, and a separator between
    /// tokens keeps adjacent names from colliding with a single longer one. The parameter types lead the key so
    /// that two bodies only match when one method could actually call the other.
    /// </remarks>
    private static string BuildBodyKey(MethodDeclarationSyntax method)
    {
        var builder = new StringBuilder();
        AppendParameterTypes(method.ParameterList, builder);
        builder.Append(SectionSeparator);

        SyntaxNode body = method.Body is { } block ? block : method.ExpressionBody!.Expression;
        foreach (var token in body.DescendantTokens())
        {
            builder.Append(token.ValueText).Append(TokenSeparator);
        }

        return builder.ToString();
    }

    /// <summary>Appends each parameter's modifiers and type to a body key.</summary>
    /// <param name="parameters">The method's parameter list.</param>
    /// <param name="builder">The key being built.</param>
    /// <remarks>
    /// Parameter names are left out on purpose: two methods that differ only in what they call an argument can
    /// still delegate to one another, so they stay comparable. Modifiers are included because an <c>out</c> or
    /// <c>ref</c> parameter changes what a caller can hand over.
    /// </remarks>
    private static void AppendParameterTypes(ParameterListSyntax parameters, StringBuilder builder)
    {
        var list = parameters.Parameters;
        for (var i = 0; i < list.Count; i++)
        {
            var parameter = list[i];
            var modifiers = parameter.Modifiers;
            for (var m = 0; m < modifiers.Count; m++)
            {
                builder.Append(modifiers[m].ValueText).Append(TokenSeparator);
            }

            if (parameter.Type is { } type)
            {
                foreach (var token in type.DescendantTokens())
                {
                    builder.Append(token.ValueText).Append(TokenSeparator);
                }
            }

            builder.Append(SectionSeparator);
        }
    }
}
