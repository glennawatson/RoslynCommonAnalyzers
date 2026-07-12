// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using Microsoft.CodeAnalysis.Text;

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a string literal written out three or more times in one file (SST1486). The number of copies and
/// the shortest literal that counts are configured with
/// <c>stylesharp.SST1486.duplicate_string_threshold</c> and
/// <c>stylesharp.SST1486.minimum_string_length</c>.
/// </summary>
/// <remarks>
/// <para>
/// This is the string counterpart of SST1471. A repeated literal cannot be changed in one place, and a typo
/// in one copy of it compiles cleanly and fails at runtime. The report lands on the <em>first</em> copy —
/// the one whose value should become a named constant — and states how many copies exist, rather than
/// squiggling every copy and saying the same thing N times.
/// </para>
/// <para>
/// Counting duplicates is a per-file aggregation, so the rule runs as a single syntax-tree action rather
/// than as a per-node action that would have to share mutable state across callbacks. The file is walked
/// twice. The first walk is allocation-free: it counts the literals that could be reported and folds their
/// hashes into a 64-bucket filter. If fewer literals survive the exclusions than the threshold needs, or if
/// no two of them ever landed in the same bucket, nothing in the file can repeat and the analyzer returns
/// without allocating anything at all. Only a file that passes that filter pays for the second walk and the
/// dictionary it fills. The filter has false positives — sixty-four buckets collide quickly once a file has
/// a few dozen distinct literals — but no false negatives, so it is sound: it can cost a wasted dictionary,
/// never a missed diagnostic.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst1486DuplicatedStringLiteralAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The mask that maps a literal's hash onto one of the filter's 64 buckets.</summary>
    private const int BucketMask = 63;

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(MaintainabilityRules.DuplicatedStringLiteral);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxTreeAction(Analyze);
    }

    /// <summary>Surveys one file's string literals, then tallies them only when a repeat is possible.</summary>
    /// <param name="context">The syntax-tree analysis context.</param>
    private static void Analyze(SyntaxTreeAnalysisContext context)
    {
        var tree = context.Tree;
        var options = DuplicateStringOptions.Read(context.Options.AnalyzerConfigOptionsProvider.GetOptions(tree));
        var root = tree.GetRoot(context.CancellationToken);

        var survey = new LiteralScan(options.MinimumLength);
        DescendantTraversalHelper.VisitDescendantTokens(root, ref survey, static (in SyntaxToken token, ref LiteralScan state) => state.Observe(in token));
        if (!survey.CouldRepeat(options.Threshold))
        {
            return;
        }

        var tally = new LiteralScan(options.MinimumLength, survey.Counted);
        DescendantTraversalHelper.VisitDescendantTokens(root, ref tally, static (in SyntaxToken token, ref LiteralScan state) => state.Observe(in token));
        Report(context, tally.Occurrences, options.Threshold);
    }

    /// <summary>Reports the first copy of every literal that reaches the threshold.</summary>
    /// <param name="context">The syntax-tree analysis context.</param>
    /// <param name="occurrences">The tallied literals.</param>
    /// <param name="threshold">The number of copies at which a literal is reported.</param>
    private static void Report(SyntaxTreeAnalysisContext context, Dictionary<string, Occurrence> occurrences, int threshold)
    {
        foreach (var entry in occurrences)
        {
            var occurrence = entry.Value;
            if (occurrence.Count < threshold)
            {
                continue;
            }

            context.ReportDiagnostic(DiagnosticHelper.Create(
                MaintainabilityRules.DuplicatedStringLiteral,
                context.Tree,
                occurrence.FirstSpan,
                entry.Key,
                occurrence.Count.ToString(CultureInfo.InvariantCulture)));
        }
    }

    /// <summary>One literal's tally: where its first copy sits, and how many copies there are.</summary>
    /// <param name="FirstSpan">The span of the first copy, which is the one reported.</param>
    /// <param name="Count">The number of copies seen so far.</param>
    private readonly record struct Occurrence(TextSpan FirstSpan, int Count);

    /// <summary>
    /// The per-file scan state. Constructed without a dictionary it surveys — counting the countable literals
    /// and folding their hashes into a bucket filter, allocating nothing. Constructed with one it tallies.
    /// </summary>
    private struct LiteralScan : IEquatable<LiteralScan>
    {
        /// <summary>The shortest literal that counts.</summary>
        private readonly int _minimumLength;

        /// <summary>The tally, or <see langword="null"/> while surveying.</summary>
        private readonly Dictionary<string, Occurrence>? _occurrences;

        /// <summary>The buckets any countable literal has landed in.</summary>
        private ulong _seen;

        /// <summary>The buckets two or more literals have landed in.</summary>
        private ulong _collided;

        /// <summary>The number of countable literals seen.</summary>
        private int _counted;

        /// <summary>Initializes a new instance of the <see cref="LiteralScan"/> struct for the surveying pass.</summary>
        /// <param name="minimumLength">The shortest literal that counts.</param>
        public LiteralScan(int minimumLength)
        {
            _minimumLength = minimumLength;
            _occurrences = null;
            _seen = 0;
            _collided = 0;
            _counted = 0;
        }

        /// <summary>Initializes a new instance of the <see cref="LiteralScan"/> struct for the tallying pass.</summary>
        /// <param name="minimumLength">The shortest literal that counts.</param>
        /// <param name="capacity">The number of countable literals the survey found.</param>
        public LiteralScan(int minimumLength, int capacity)
        {
            _minimumLength = minimumLength;
            _occurrences = new Dictionary<string, Occurrence>(capacity, StringComparer.Ordinal);
            _seen = 0;
            _collided = 0;
            _counted = 0;
        }

        /// <summary>Gets the number of countable literals seen.</summary>
        public readonly int Counted => _counted;

        /// <summary>Gets the tally, which is only present after a tallying pass.</summary>
        public readonly Dictionary<string, Occurrence> Occurrences => _occurrences!;

        /// <summary>Returns whether the surveyed file can possibly hold a repeated literal.</summary>
        /// <param name="threshold">The number of copies at which a literal is reported.</param>
        /// <returns><see langword="false"/> only when no literal in the file can reach the threshold.</returns>
        /// <remarks>
        /// Two copies of one value always land in the same bucket, so an empty collision mask proves every
        /// countable literal is distinct. A collision does not prove the converse, which is why it only buys the
        /// tallying pass rather than a diagnostic.
        /// </remarks>
        public readonly bool CouldRepeat(int threshold) => _counted >= threshold && _collided != 0;

        /// <summary>Observes one token and returns whether the walk should continue.</summary>
        /// <param name="token">The token.</param>
        /// <returns><see langword="true"/> always: every token in the file must be seen.</returns>
        public bool Observe(in SyntaxToken token)
        {
            if (!TryGetCountedValue(in token, _minimumLength, out var value))
            {
                return true;
            }

            _counted++;
            if (_occurrences is null)
            {
                var bucket = 1UL << (value.GetHashCode() & BucketMask);
                _collided |= _seen & bucket;
                _seen |= bucket;
                return true;
            }

            _occurrences[value] = _occurrences.TryGetValue(value, out var occurrence)
                ? new Occurrence(occurrence.FirstSpan, occurrence.Count + 1)
                : new Occurrence(token.Span, 1);

            return true;
        }

        /// <summary>Returns whether two scan states are equivalent.</summary>
        /// <param name="other">The other state.</param>
        /// <returns><see langword="true"/> when the tracked state is equal.</returns>
        public readonly bool Equals(LiteralScan other)
            => _counted == other._counted
                && _seen == other._seen
                && _collided == other._collided
                && ReferenceEquals(_occurrences, other._occurrences);

        /// <inheritdoc/>
        public override readonly bool Equals(object? obj) => obj is LiteralScan other && Equals(other);

        /// <inheritdoc/>
        public override readonly int GetHashCode() => unchecked((_counted * 397) ^ (int)_seen);

        /// <summary>Reads the value of a string literal token that is worth counting.</summary>
        /// <param name="token">The token being visited.</param>
        /// <param name="minimumLength">The shortest literal that counts.</param>
        /// <param name="value">The literal's value, without its quotes or escapes.</param>
        /// <returns><see langword="true"/> when the token is a countable string literal.</returns>
        /// <remarks>
        /// The token kind is tested first, so the overwhelming majority of a file's tokens are rejected on an
        /// integer comparison and never touch the ancestor walk. An interpolated string's text segments carry a
        /// different token kind entirely and so are never counted — they are part of a template, not a value that
        /// can be hoisted — while a string literal written inside an interpolation hole is an ordinary literal and
        /// is counted like any other.
        /// </remarks>
        private static bool TryGetCountedValue(in SyntaxToken token, int minimumLength, out string value)
        {
            value = string.Empty;
            if (!IsStringLiteralKind(token.Kind()) || token.Parent is not LiteralExpressionSyntax literal)
            {
                return false;
            }

            // A literal that is empty or nothing but whitespace names nothing, whatever the minimum length says.
            var text = token.ValueText;
            if (text.Length < minimumLength || string.IsNullOrWhiteSpace(text) || IsExemptPosition(literal))
            {
                return false;
            }

            value = text;
            return true;
        }

        /// <summary>Returns whether a token kind is one of the string-literal kinds.</summary>
        /// <param name="kind">The token kind.</param>
        /// <returns><see langword="true"/> for a quoted, verbatim, raw or UTF-8 string literal token.</returns>
        /// <remarks>
        /// The value is compared, not the spelling, so <c>"a\tb"</c> and its verbatim or raw equivalents are the
        /// same literal — which is exactly the point: three spellings of one value are still one value with three
        /// homes.
        /// </remarks>
        private static bool IsStringLiteralKind(SyntaxKind kind) => kind is SyntaxKind.StringLiteralToken
            or SyntaxKind.SingleLineRawStringLiteralToken
            or SyntaxKind.MultiLineRawStringLiteralToken
            or SyntaxKind.Utf8StringLiteralToken
            or SyntaxKind.Utf8SingleLineRawStringLiteralToken
            or SyntaxKind.Utf8MultiLineRawStringLiteralToken;

        /// <summary>Returns whether the literal's position already gives it a name or a structure.</summary>
        /// <param name="literal">The string literal expression.</param>
        /// <returns><see langword="true"/> when a copy here is idiomatic rather than a duplicate.</returns>
        /// <remarks>
        /// The walk stops at the first enclosing statement, member or lambda, so it is bounded by expression depth
        /// rather than by file depth. It does not stop at an invocation, because
        /// <c>private const string Key = Normalize("value");</c> must still reach the declaration that names it —
        /// but a <c>nameof</c> invocation ends the walk, because its operand is a name and never a value the file
        /// repeats.
        /// </remarks>
        private static bool IsExemptPosition(LiteralExpressionSyntax literal)
        {
            SyntaxNode previous = literal;
            for (var current = literal.Parent; current is not null; current = current.Parent)
            {
                switch (current)
                {
                    // `[Obsolete("use X instead")]` on twenty members is idiomatic, not twenty duplicates, and
                    // `case "read":` is a value the switch already names structurally.
                    case AttributeSyntax:
                    case SwitchLabelSyntax:
                        return true;
                    case SwitchExpressionArmSyntax arm when ReferenceEquals(previous, arm.Pattern):
                        return true;
                    case InvocationExpressionSyntax invocation when IsNameOfInvocation(invocation):
                        return true;

                    // The declaration *is* the named constant this rule asks for; naming it again is circular.
                    case FieldDeclarationSyntax field:
                        return IsNamedConstantField(field.Modifiers);
                    case LocalDeclarationStatementSyntax local:
                        return local.IsConst;

                    // A lambda does not inherit the name of the field it is assigned to, so the walk ends here.
                    case AnonymousFunctionExpressionSyntax:
                    case StatementSyntax:
                    case MemberDeclarationSyntax:
                        return false;
                }

                previous = current;
            }

            return false;
        }

        /// <summary>Returns whether an invocation is a <c>nameof</c> operator.</summary>
        /// <param name="invocation">The invocation.</param>
        /// <returns><see langword="true"/> for <c>nameof(...)</c>.</returns>
        private static bool IsNameOfInvocation(InvocationExpressionSyntax invocation)
            => invocation.Expression is IdentifierNameSyntax { Identifier.ValueText: "nameof" };

        /// <summary>Returns whether a field declaration is the named home of a constant value.</summary>
        /// <param name="modifiers">The field's modifiers.</param>
        /// <returns><see langword="true"/> for a <c>const</c> or a <c>static readonly</c> field.</returns>
        private static bool IsNamedConstantField(SyntaxTokenList modifiers)
            => ModifierListHelper.Contains(modifiers, SyntaxKind.ConstKeyword)
                || (ModifierListHelper.Contains(modifiers, SyntaxKind.StaticKeyword)
                    && ModifierListHelper.Contains(modifiers, SyntaxKind.ReadOnlyKeyword));
    }
}
