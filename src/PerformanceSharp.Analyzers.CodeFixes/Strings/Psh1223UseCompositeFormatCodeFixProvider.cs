// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Hoists a reported constant format string into a <c>static readonly CompositeFormat</c> field and
/// points the call at it (PSH1223). The parsing that used to happen on every call now happens once,
/// when the type is initialized.
/// </summary>
/// <remarks>
/// <para>
/// <b>The field goes first, and that is not cosmetic.</b> Static field initializers run in
/// declaration order, so a <c>string.Format</c> call sitting in another static field's initializer
/// would read a null <c>CompositeFormat</c> if the new field were appended after it. Inserting at the
/// top of the type means the parsed format is always ready before anything in that type can use it.
/// </para>
/// <para>
/// <b>The format is written out as a literal.</b> The analyzer only reports a format it could read as
/// a compile-time constant, so the value is known and re-emitting it is exact. Reusing the original
/// expression instead would work for a literal and for a <c>const</c> field, and would quietly fail
/// for a <c>const</c> local, which is not in scope at field level — so the literal is used unless the
/// expression is one of the two that provably still binds there.
/// </para>
/// <para>
/// <b>The current culture is named, not left implicit.</b> Every <c>CompositeFormat</c> overload takes
/// an <see cref="IFormatProvider"/>, and <c>string.Format(format, args)</c> was already using the
/// current culture — so the rewrite says so. Passing a bare <see langword="null"/> instead would be
/// ambiguous against <c>Format(string, object, object)</c>, and the analyzer's speculative bind would
/// have refused it.
/// </para>
/// </remarks>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Psh1223UseCompositeFormatCodeFixProvider))]
[Shared]
public sealed class Psh1223UseCompositeFormatCodeFixProvider : CodeFixProvider
{
    /// <summary>The suffix given to the hoisted field.</summary>
    private const string FieldSuffix = "Format";

    /// <summary>The first numeric suffix tried when the plain field name is already taken.</summary>
    private const int FirstNumberedSuffix = 2;

    /// <summary>The fully qualified parsed-format type, used when the simple name does not resolve.</summary>
    private const string QualifiedCompositeFormat = "global::System.Text.CompositeFormat";

    /// <summary>The simple current-culture spelling, used when it resolves.</summary>
    private const string SimpleCurrentCulture = "CultureInfo.CurrentCulture";

    /// <summary>The fully qualified current-culture spelling, used when the simple name does not resolve.</summary>
    private const string QualifiedCurrentCulture = "global::System.Globalization.CultureInfo.CurrentCulture";

    /// <summary>The globalization type whose simple spelling the rewrite prefers when it is in scope.</summary>
    private const string CultureInfoTypeName = "CultureInfo";

    /// <summary>The globalization namespace holding the culture type.</summary>
    private const string GlobalizationNamespace = "System.Globalization";

    /// <summary>The namespace holding the parsed-format type.</summary>
    private const string TextNamespace = "System.Text";

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(StringRules.UseCompositeFormat.Id);

    /// <inheritdoc/>
    public override FixAllProvider? GetFixAllProvider() => null;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
        => ReplaceNodeCodeFix.RegisterAsync(
            context,
            "Hoist the format into a CompositeFormat field",
            nameof(Psh1223UseCompositeFormatCodeFixProvider),
            TryRewrite);

    /// <summary>Resolves the reported format call and builds the type carrying the hoisted field.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="model">The semantic model for the document.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The type declaration to swap, or <see langword="null"/> when the hoist is not safe here.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, SemanticModel model, Diagnostic diagnostic)
    {
        if (root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true)
                ?.FirstAncestorOrSelf<InvocationExpressionSyntax>() is not { } invocation
            || !Psh1223UseCompositeFormatAnalyzer.IsFormatShape(invocation)
            || FindHoistTarget(invocation) is not { } owner)
        {
            return null;
        }

        var formatIndex = Psh1223UseCompositeFormatAnalyzer.GetHoistableFormatIndex(model, invocation, CancellationToken.None);
        var culture = ResolvesCultureInfo(model, invocation.SpanStart) ? SimpleCurrentCulture : QualifiedCurrentCulture;
        if (formatIndex < 0 || !Psh1223UseCompositeFormatAnalyzer.RewriteBindsToCompositeFormat(model, invocation, formatIndex, culture))
        {
            return null;
        }

        var fieldName = CreateFieldName(owner, invocation);
        var provider = Psh1223UseCompositeFormatAnalyzer.GetProvider(invocation, formatIndex, culture);
        var field = SyntaxFactory.IdentifierName(fieldName);
        var rewritten = Psh1223UseCompositeFormatAnalyzer.BuildCompositeFormatCall(invocation, formatIndex, provider, field);

        var declaration = BuildFieldDeclaration(model, owner, invocation, formatIndex, fieldName);
        var updated = owner.ReplaceNode(invocation, rewritten.WithTriviaFrom(invocation));
        return new NodeReplacement(owner, updated.WithMembers(updated.Members.Insert(0, declaration)));
    }

    /// <summary>Finds the type the field can be hoisted into.</summary>
    /// <param name="invocation">The reported <c>Format</c> invocation.</param>
    /// <returns>The owning type, or <see langword="null"/> when there is nowhere safe to put a field.</returns>
    /// <remarks>
    /// An interface is excluded even though C# would allow a static field in one: a format string is
    /// implementation detail, and an interface is not where it belongs. A type with no block body — a
    /// positional record written with a semicolon — has no member list to insert into, and cannot
    /// contain the call in the first place.
    /// </remarks>
    private static TypeDeclarationSyntax? FindHoistTarget(InvocationExpressionSyntax invocation)
    {
        var owner = invocation.FirstAncestorOrSelf<TypeDeclarationSyntax>();
        return owner is ClassDeclarationSyntax or StructDeclarationSyntax or RecordDeclarationSyntax
            && !owner.OpenBraceToken.IsKind(SyntaxKind.None)
            ? owner
            : null;
    }

    /// <summary>Builds the <c>private static readonly CompositeFormat X = CompositeFormat.Parse("...");</c> field.</summary>
    /// <param name="model">The semantic model for the document.</param>
    /// <param name="owner">The type receiving the field.</param>
    /// <param name="invocation">The reported <c>Format</c> invocation.</param>
    /// <param name="formatIndex">The format argument's index.</param>
    /// <param name="fieldName">The name chosen for the field.</param>
    /// <returns>The field declaration, indented and spaced for the type it joins.</returns>
    private static MemberDeclarationSyntax BuildFieldDeclaration(
        SemanticModel model,
        TypeDeclarationSyntax owner,
        InvocationExpressionSyntax invocation,
        int formatIndex,
        string fieldName)
    {
        var position = invocation.SpanStart;
        var typeName = ResolvesCompositeFormat(model, position)
            ? Psh1223UseCompositeFormatAnalyzer.CompositeFormatTypeName
            : QualifiedCompositeFormat;
        var formatArgument = invocation.ArgumentList.Arguments[formatIndex].Expression;
        var source = GetFormatSource(model, formatArgument);
        var text = $"private static readonly {typeName} {fieldName} = {typeName}.Parse({source});";

        var lineBreak = LineEndingHelper.GetLineBreak(owner);
        var indentation = GetMemberIndentation(owner);
        return SyntaxFactory.ParseMemberDeclaration(text)!
            .WithLeadingTrivia(SyntaxFactory.Whitespace(indentation))
            .WithTrailingTrivia(lineBreak, lineBreak);
    }

    /// <summary>Returns the source text of the format the hoisted field should parse.</summary>
    /// <param name="model">The semantic model for the document.</param>
    /// <param name="formatArgument">The reported format argument.</param>
    /// <returns>The expression text to parse in the field initializer.</returns>
    /// <remarks>
    /// A literal is reused verbatim so a raw or verbatim string keeps its spelling, and a
    /// <c>const</c> field is reused because a compile-time constant binds just as well at field level.
    /// Anything else — in practice a <c>const</c> local — is out of scope up there, so its value is
    /// written out as a literal instead. The value is a compile-time constant either way, so this is
    /// exact.
    /// </remarks>
    private static string GetFormatSource(SemanticModel model, ExpressionSyntax formatArgument)
    {
        if (formatArgument is LiteralExpressionSyntax
            || model.GetSymbolInfo(formatArgument).Symbol is IFieldSymbol { IsConst: true })
        {
            return formatArgument.WithoutTrivia().ToString();
        }

        var constant = model.GetConstantValue(formatArgument);
        return SyntaxFactory.Literal((string)constant.Value!).ToFullString();
    }

    /// <summary>Chooses a field name that does not collide with anything the type already declares.</summary>
    /// <param name="owner">The type receiving the field.</param>
    /// <param name="invocation">The reported <c>Format</c> invocation.</param>
    /// <returns>The field name.</returns>
    private static string CreateFieldName(TypeDeclarationSyntax owner, InvocationExpressionSyntax invocation)
    {
        var existing = CollectMemberNames(owner);
        var baseName = GetEnclosingMemberName(invocation) + FieldSuffix;
        if (!existing.Contains(baseName))
        {
            return baseName;
        }

        // Only one existing name can collide with each candidate, so among this many candidates at
        // least one is always free and the loop always returns from inside.
        var limit = existing.Count + FirstNumberedSuffix;
        for (var suffix = FirstNumberedSuffix; suffix <= limit; suffix++)
        {
            var candidate = baseName + suffix.ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (!existing.Contains(candidate))
            {
                return candidate;
            }
        }

        return baseName + limit.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>Returns the name of the member the call sits in, to base the field name on.</summary>
    /// <param name="invocation">The reported <c>Format</c> invocation.</param>
    /// <returns>The enclosing member's name, or an empty string when it has none.</returns>
    private static string GetEnclosingMemberName(InvocationExpressionSyntax invocation)
        => invocation.FirstAncestorOrSelf<MemberDeclarationSyntax>() switch
        {
            MethodDeclarationSyntax method => method.Identifier.ValueText,
            PropertyDeclarationSyntax property => property.Identifier.ValueText,
            ConstructorDeclarationSyntax constructor => constructor.Identifier.ValueText,
            _ => string.Empty,
        };

    /// <summary>Collects every name the type already declares, so the new field does not shadow one.</summary>
    /// <param name="owner">The type receiving the field.</param>
    /// <returns>The declared member names.</returns>
    private static HashSet<string> CollectMemberNames(TypeDeclarationSyntax owner)
    {
        var names = new HashSet<string>(StringComparer.Ordinal) { owner.Identifier.ValueText };
        foreach (var member in owner.Members)
        {
            switch (member)
            {
                case FieldDeclarationSyntax field:
                {
                    foreach (var variable in field.Declaration.Variables)
                    {
                        names.Add(variable.Identifier.ValueText);
                    }

                    break;
                }

                case MethodDeclarationSyntax method:
                {
                    names.Add(method.Identifier.ValueText);
                    break;
                }

                case PropertyDeclarationSyntax property:
                {
                    names.Add(property.Identifier.ValueText);
                    break;
                }

                case EventDeclarationSyntax declaredEvent:
                {
                    names.Add(declaredEvent.Identifier.ValueText);
                    break;
                }

                default:
                {
                    continue;
                }
            }
        }

        return names;
    }

    /// <summary>Returns the indentation for a member of a type: the type's own indent plus one level.</summary>
    /// <param name="owner">The type receiving the field.</param>
    /// <returns>The member indentation whitespace.</returns>
    private static string GetMemberIndentation(TypeDeclarationSyntax owner)
    {
        var leading = owner.GetLeadingTrivia();
        var typeIndent = leading.Count > 0 && leading[leading.Count - 1].IsKind(SyntaxKind.WhitespaceTrivia)
            ? leading[leading.Count - 1].ToString()
            : string.Empty;
        return typeIndent + "    ";
    }

    /// <summary>Returns whether the culture type resolves by its simple name at a position.</summary>
    /// <param name="model">The semantic model for the document.</param>
    /// <param name="position">The lookup position.</param>
    /// <returns><see langword="true"/> when the unqualified spelling binds.</returns>
    private static bool ResolvesCultureInfo(SemanticModel model, int position)
        => ResolvesIn(model, position, CultureInfoTypeName, GlobalizationNamespace);

    /// <summary>Returns whether the parsed-format type resolves by its simple name at a position.</summary>
    /// <param name="model">The semantic model for the document.</param>
    /// <param name="position">The lookup position.</param>
    /// <returns><see langword="true"/> when the unqualified spelling binds.</returns>
    private static bool ResolvesCompositeFormat(SemanticModel model, int position)
        => ResolvesIn(model, position, Psh1223UseCompositeFormatAnalyzer.CompositeFormatTypeName, TextNamespace);

    /// <summary>Returns whether a simple type name resolves to the expected namespace at a position.</summary>
    /// <param name="model">The semantic model for the document.</param>
    /// <param name="position">The lookup position.</param>
    /// <param name="name">The simple type name.</param>
    /// <param name="containingNamespace">The namespace the name must resolve into.</param>
    /// <returns><see langword="true"/> when the unqualified spelling binds.</returns>
    private static bool ResolvesIn(SemanticModel model, int position, string name, string containingNamespace)
    {
        foreach (var candidate in model.LookupNamespacesAndTypes(position, name: name))
        {
            if (candidate is INamedTypeSymbol named && named.ContainingNamespace.ToDisplayString() == containingNamespace)
            {
                return true;
            }
        }

        return false;
    }
}
