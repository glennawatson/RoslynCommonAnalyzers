// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Diagnostic descriptors for the modernization rules (SST20xx). These point hand-written
/// code at a clearer modern equivalent — a runtime throw-helper in place of an argument
/// guard, or a pattern-matching form in place of an <c>as</c>/<c>is</c> idiom. Throw-helper
/// rules are gated on the helper existing in the referenced framework, so they light up only
/// where the replacement compiles.
/// </summary>
internal static class ModernizationRules
{
    /// <summary>SST2000 — a null check + throw should use <c>ArgumentNullException.ThrowIfNull</c>.</summary>
    public static readonly DiagnosticDescriptor UseThrowIfNull = Create(
        "SST2000",
        "Use ArgumentNullException.ThrowIfNull",
        "Replace the null check with 'ArgumentNullException.ThrowIfNull({0})'",
        "A null check that throws ArgumentNullException is replaced by the ArgumentNullException.ThrowIfNull guard helper (.NET 6+).");

    /// <summary>SST2001 — an empty-string check + throw should use <c>ArgumentException.ThrowIfNullOrEmpty</c> (opt-in).</summary>
    public static readonly DiagnosticDescriptor UseThrowIfNullOrEmpty = CreateOptIn(
        "SST2001",
        "Use ArgumentException.ThrowIfNullOrEmpty",
        "Replace the check with 'ArgumentException.ThrowIfNullOrEmpty({0})'",
        "A string.IsNullOrEmpty check that throws is replaced by ArgumentException.ThrowIfNullOrEmpty (.NET 7+). Off by default — it can change the thrown message and exception type.");

    /// <summary>SST2002 — a whitespace check + throw should use <c>ArgumentException.ThrowIfNullOrWhiteSpace</c> (opt-in).</summary>
    public static readonly DiagnosticDescriptor UseThrowIfNullOrWhiteSpace = CreateOptIn(
        "SST2002",
        "Use ArgumentException.ThrowIfNullOrWhiteSpace",
        "Replace the check with 'ArgumentException.ThrowIfNullOrWhiteSpace({0})'",
        "A string.IsNullOrWhiteSpace check that throws is replaced by ArgumentException.ThrowIfNullOrWhiteSpace (.NET 8+). Off by default — it can change the thrown message and exception type.");

    /// <summary>SST2003 — a disposed check should use <c>ObjectDisposedException.ThrowIf</c>.</summary>
    public static readonly DiagnosticDescriptor UseObjectDisposedThrowIf = Create(
        "SST2003",
        "Use ObjectDisposedException.ThrowIf",
        "Replace the disposed check with 'ObjectDisposedException.ThrowIf({0}, this)'",
        "A standard disposed check is replaced by ObjectDisposedException.ThrowIf (.NET 8+).");

    /// <summary>SST2004 — a range check should use an <c>ArgumentOutOfRangeException.ThrowIf...</c> helper.</summary>
    public static readonly DiagnosticDescriptor UseArgumentOutOfRangeThrowIf = Create(
        "SST2004",
        "Use ArgumentOutOfRangeException range helpers",
        "Replace the range check with 'ArgumentOutOfRangeException.{0}'",
        "A simple range check is replaced by the matching ArgumentOutOfRangeException.ThrowIf helper (.NET 8+).");

    /// <summary>SST2005 — an <c>as</c> cast compared to <c>null</c> should use the <c>is</c> type pattern.</summary>
    public static readonly DiagnosticDescriptor UseIsPatternOverAsNullCheck = Create(
        "SST2005",
        "Use the 'is' type pattern instead of comparing an 'as' cast to null",
        "Use '{0}' instead of comparing an 'as' cast to null",
        "Casting with 'as' and then comparing to null ('x as T != null') restates a type test that 'x is T' (or 'x is not T') expresses directly, without the throwaway local.");

    /// <summary>SST2006 — a negated type test (<c>!(x is T)</c>) should use the <c>is not</c> pattern.</summary>
    public static readonly DiagnosticDescriptor UseNegatedIsPattern = Create(
        "SST2006",
        "Use the 'is not' pattern instead of negating an 'is' check",
        "Use 'is not' instead of negating the 'is' check",
        "A type test negated with '!' reads more directly as the 'is not' pattern: '!(x is T)' becomes 'x is not T'.");

    /// <summary>SST2007 — an <c>is</c> check followed by a cast should use a declaration pattern.</summary>
    public static readonly DiagnosticDescriptor UseDeclarationPatternOverIsCheckAndCast = Create(
        "SST2007",
        "Declare the checked type in the pattern",
        "Declare '{0}' in the 'is' pattern instead of casting after the check",
        "An 'is' type check followed by a local cast of the same value reads more directly as a declaration pattern: 'x is T t'.");

    /// <summary>SST2008 — a negated pattern test should use an <c>is not</c> pattern.</summary>
    public static readonly DiagnosticDescriptor UseIsNotPattern = Create(
        "SST2008",
        "Use an is-not pattern instead of negating a pattern",
        "Use 'is not' instead of negating this pattern test",
        "A pattern test negated with '!' reads more directly as 'is not', and avoids wrapping the whole pattern expression in an extra grouping expression.");

    /// <summary>SST2009 — a catch block opens with a condition that decides whether to rethrow.</summary>
    public static readonly DiagnosticDescriptor UseExceptionFilter = Create(
        "SST2009",
        "Filter exceptions with a when clause",
        "Move this condition into a 'when' filter",
        UseExceptionFilterDescription);

    /// <summary>SST2010 — the clock is read from a static the caller cannot replace.</summary>
    public static readonly DiagnosticDescriptor UseTimeProvider = CreateOptIn(
        "SST2010",
        "Read the clock through a TimeProvider",
        "'{0}' reads the machine clock directly; take a TimeProvider so the caller can supply the time",
        UseTimeProviderDescription);

    /// <summary>SST2011 — an instant is recorded in local time.</summary>
    public static readonly DiagnosticDescriptor RecordInstantsInUtc = Create(
        "SST2011",
        "Record instants in UTC",
        "'{0}' records an instant in local time; use the UTC clock",
        RecordInstantsInUtcDescription);

    /// <summary>SST2012 — the empty GUID is constructed rather than named.</summary>
    public static readonly DiagnosticDescriptor UseGuidEmpty = Create(
        "SST2012",
        "Use Guid.Empty for the empty GUID",
        "Use 'Guid.Empty' instead of 'new Guid()'",
        UseGuidEmptyDescription);

    /// <summary>SST2013 — an if that only wraps another if can state one condition.</summary>
    public static readonly DiagnosticDescriptor MergeNestedIf = Create(
        "SST2013",
        "Merge an if that only wraps another if",
        "This 'if' only wraps another; combine the conditions with '&&'",
        MergeNestedIfDescription);

    /// <summary>SST2014 — control jumps to a label.</summary>
    public static readonly DiagnosticDescriptor AvoidGoto = Create(
        "SST2014",
        "Avoid goto",
        "'goto' makes control flow hard to follow; use a loop, a method, or a flag",
        AvoidGotoDescription);

    /// <summary>SST2015 — an increment is buried inside a larger expression.</summary>
    public static readonly DiagnosticDescriptor IsolateIncrement = Create(
        "SST2015",
        "Do not bury an increment inside a larger expression",
        "'{0}' both reads and writes '{1}' inside a larger expression; give it its own statement",
        IsolateIncrementDescription);

    /// <summary>The SST2009 rule description.</summary>
    private const string UseExceptionFilterDescription =
        "A catch block that immediately tests a condition and rethrows on the losing branch is an exception filter written by hand; "
        + "'catch ... when' skips the handler without unwinding the stack for exceptions it was never going to keep.";

    /// <summary>The UseTimeProvider rule description.</summary>
    private const string UseTimeProviderDescription =
        "A type that calls 'DateTime.Now' reaches out of itself for a value nobody can control, which means the behavior that depends on it "
        + "cannot be tested without waiting for real time to pass — or without changing the machine's clock. 'TimeProvider' is the seam: the "
        + "production caller passes 'TimeProvider.System' and the test passes a fake. Off by default, because introducing the seam is a "
        + "design change and not every type wants it; turn it on where time is part of the logic.";

    /// <summary>The RecordInstantsInUtc rule description.</summary>
    private const string RecordInstantsInUtcDescription =
        "Local time is a presentation format, not a point in time. An instant stored as local time is ambiguous twice a year — the hour "
        + "that repeats when the clocks go back genuinely maps to two different moments — and it means something different again when the "
        + "process moves to another machine. Record UTC and convert to local only where a human reads it.";

    /// <summary>The UseGuidEmpty rule description.</summary>
    private const string UseGuidEmptyDescription =
        "'new Guid()' does not make a new GUID — it makes the all-zero one, which is the opposite of what the code appears to say. The "
        + "reader who wanted a fresh identifier has to know that 'Guid.NewGuid' is the one that generates, and that this one does not. "
        + "'Guid.Empty' says what the value is.";

    /// <summary>The MergeNestedIf rule description.</summary>
    private const string MergeNestedIfDescription =
        "Two nested ifs with nothing between them and no else on either are one condition written across two levels of indentation. "
        + "Combining them with '&&' says the same thing, and gives the reader back the nesting level for something that earns it.";

    /// <summary>The AvoidGoto rule description.</summary>
    private const string AvoidGotoDescription =
        "A 'goto' means the reader can no longer work out how control reached a line by looking at the lines above it. The structured forms "
        + "— break, continue, return, an extracted method — say where control goes in the same breath as why. The one place 'goto' still "
        + "earns its keep is jumping between switch sections, which the language has no other word for, and that use is not reported.";

    /// <summary>The IsolateIncrement rule description.</summary>
    private const string IsolateIncrementDescription =
        "'array[i++] = Compute(i)' has a value that depends on which side the compiler evaluates first, and readers reliably guess wrong. "
        + "The increment is doing two jobs — producing a value and advancing a variable — and only one of them is visible where it is "
        + "written. Put it on its own line; the cost is a line and the gain is that the code means one thing.";

    /// <summary>Creates a Warning-severity Modernization descriptor whose help link points at the rule's docs page.</summary>
    /// <param name="id">The diagnostic id.</param>
    /// <param name="title">The rule title.</param>
    /// <param name="messageFormat">The message format.</param>
    /// <param name="description">The rule description.</param>
    /// <returns>The descriptor.</returns>
    private static DiagnosticDescriptor Create(string id, string title, string messageFormat, string description) =>
        DescriptorFactory.Create(id, title, messageFormat, "Modernization", description);

    /// <summary>Creates a Modernization descriptor that is disabled by default (opt-in via .editorconfig).</summary>
    /// <param name="id">The diagnostic id.</param>
    /// <param name="title">The rule title.</param>
    /// <param name="messageFormat">The message format.</param>
    /// <param name="description">The rule description.</param>
    /// <returns>The descriptor.</returns>
    private static DiagnosticDescriptor CreateOptIn(string id, string title, string messageFormat, string description) =>
        DescriptorFactory.CreateOptIn(id, title, messageFormat, "Modernization", description);
}
