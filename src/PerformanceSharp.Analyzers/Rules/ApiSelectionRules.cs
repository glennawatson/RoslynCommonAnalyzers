// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Diagnostic descriptors for the API selection rules (PSH14xx). These point at
/// cheaper framework APIs that do the same work with less allocation or setup,
/// and are gated on the replacement API existing in the referenced framework.
/// </summary>
internal static class ApiSelectionRules
{
    /// <summary>PSH1400 — one-shot hashing should use the static <c>HashData</c> methods.</summary>
    public static readonly DiagnosticDescriptor PreferStaticHashData = Create(
        "PSH1400",
        "Use the static HashData method for one-shot hashing",
        "Replace this create-and-compute pattern with {0}.HashData",
        "One-shot hashing through a HashAlgorithm instance allocates and disposes it for nothing; the static HashData methods (.NET 5+) hash in one call and are suggested only where they exist.");

    /// <summary>PSH1401 — attribute types should be sealed for faster reflection lookups.</summary>
    public static readonly DiagnosticDescriptor SealAttributeTypes = Create(
        "PSH1401",
        "Attribute types should be sealed",
        "Seal '{0}' so attribute lookups skip the inheritance search",
        "Reflection-based attribute lookups are cheaper on sealed attribute types because the runtime never has to consider derived attributes.");

    /// <summary>PSH1402 — a compile-time-constant static readonly field should be const.</summary>
    public static readonly DiagnosticDescriptor PreferConstOverStaticReadonly = Create(
        "PSH1402",
        "Use const for compile-time constants",
        "Make '{0}' const; its value is known at compile time",
        "A private or internal static readonly field with a constant value costs a field load on every use; const folds the value into call sites. Public fields are skipped.");

    /// <summary>PSH1403 — fields should not restate their default value.</summary>
    public static readonly DiagnosticDescriptor RemoveRedundantDefaultInitialization = Create(
        "PSH1403",
        "Do not initialize fields to their default value",
        "Remove this initializer; '{0}' already starts at its default value",
        "The runtime zero-initializes fields before any constructor runs, so explicitly assigning the default repeats the work in every constructor for nothing.");

    /// <summary>PSH1404 — the executing assembly is known statically.</summary>
    public static readonly DiagnosticDescriptor PreferTypeofAssembly = Create(
        "PSH1404",
        "Get the assembly from typeof instead of a stack walk",
        "Use typeof({0}).Assembly instead of Assembly.GetExecutingAssembly()",
        "Assembly.GetExecutingAssembly walks the call stack at runtime to discover its caller; typeof(T).Assembly resolves the same assembly statically.");

    /// <summary>PSH1405 — the runtime exposes direct process and thread properties.</summary>
    public static readonly DiagnosticDescriptor UseEnvironmentProperties = Create(
        "PSH1405",
        "Use the direct Environment APIs",
        "Use '{0}' instead of this call chain",
        "Environment.ProcessId, ProcessPath, and CurrentManagedThreadId read runtime state directly; the Process and Thread routes do needless work. Suggested only where the API exists.");

    /// <summary>PSH1406 — Regex can answer bool and count questions without materializing matches.</summary>
    public static readonly DiagnosticDescriptor UseDirectRegexQueries = Create(
        "PSH1406",
        "Ask Regex for the answer directly",
        "Use '{0}' instead of materializing the match",
        "Regex.Match(input).Success and Regex.Matches(input).Count allocate match objects to answer a bool or an int; IsMatch and Count answer directly. Suggested only where the API exists.");

    /// <summary>PSH1407 — key membership is the dictionary's own question.</summary>
    public static readonly DiagnosticDescriptor UseContainsKeyOverKeysContains = Create(
        "PSH1407",
        "Query the dictionary, not its Keys view",
        "Use ContainsKey instead of Keys.Contains",
        "dictionary.Keys.Contains(key) may allocate the keys view and enumerate it; ContainsKey is a single hash probe.");

    /// <summary>PSH1408 — measure elapsed time with timestamps instead of allocating a stopwatch.</summary>
    public static readonly DiagnosticDescriptor UseStopwatchTimestamps = Create(
        "PSH1408",
        "Measure elapsed time with Stopwatch timestamps",
        "Use Stopwatch.GetTimestamp and GetElapsedTime instead of allocating a Stopwatch to read '{0}'",
        UseStopwatchTimestampsDescription);

    /// <summary>PSH1409 — hand-written argument guards should use the framework throw helpers.</summary>
    public static readonly DiagnosticDescriptor UseThrowHelpers = Create(
        "PSH1409",
        "Use the built-in throw helpers for argument guards",
        "Use '{0}' instead of this guard clause",
        UseThrowHelpersDescription);

    /// <summary>PSH1410 — trivial forwarders should ask for aggressive inlining. Opt-in.</summary>
    public static readonly DiagnosticDescriptor InlineTrivialForwarders = CreateOptIn(
        "PSH1410",
        "Mark trivial forwarders for aggressive inlining",
        "Add MethodImplOptions.AggressiveInlining to '{0}'",
        InlineTrivialForwardersDescription);

    /// <summary>PSH1411 — a non-public class that nothing derives from is not sealed.</summary>
    public static readonly DiagnosticDescriptor SealNonDerivedType = Create(
        "PSH1411",
        "Seal types that nothing derives from",
        "Nothing derives from '{0}'; sealing it lets the JIT devirtualize and inline its members",
        SealNonDerivedTypeDescription);

    /// <summary>PSH1412 — a Random instance is allocated where the shared one would do.</summary>
    public static readonly DiagnosticDescriptor UseSharedRandom = Create(
        "PSH1412",
        "Use Random.Shared instead of allocating a Random",
        "Use 'Random.Shared' instead of allocating a '{0}'",
        UseSharedRandomDescription);

    /// <summary>PSH1413 — the Unix epoch is constructed rather than read.</summary>
    public static readonly DiagnosticDescriptor UseUnixEpochField = Create(
        "PSH1413",
        "Read the Unix epoch from the framework",
        "Use '{0}.UnixEpoch' instead of constructing the epoch",
        UseUnixEpochFieldDescription);

    /// <summary>PSH1414 — mark members that do not touch instance state as static.</summary>
    public static readonly DiagnosticDescriptor MarkMembersStatic = Create(
        "PSH1414",
        "Mark members that do not touch instance state as static",
        "'{0}' never uses instance state; making it static removes the hidden 'this' argument",
        MarkMembersStaticDescription);

    /// <summary>PSH1415 — hold the concrete type when the concrete type is what you have.</summary>
    public static readonly DiagnosticDescriptor UseConcreteType = Create(
        "PSH1415",
        "Hold the concrete type when the concrete type is what you have",
        "'{0}' is declared as '{1}' but only ever holds '{2}'; declare the concrete type so its members can be called directly",
        UseConcreteTypeDescription);

    /// <summary>PSH1416 — cache the serializer options instead of building them per call.</summary>
    public static readonly DiagnosticDescriptor CacheSerializerOptions = Create(
        "PSH1416",
        "Cache the serializer options instead of building them per call",
        "'{0}' is constructed on every call; cache it in a static readonly field",
        CacheSerializerOptionsDescription);

    /// <summary>PSH1417 — do not compute an expensive argument for an assertion.</summary>
    public static readonly DiagnosticDescriptor ExpensiveDebugAssertArgument = Create(
        "PSH1417",
        "Do not compute an expensive argument for an assertion",
        "'{0}' is evaluated on every call, including in release builds where the assertion does nothing",
        ExpensiveDebugAssertArgumentDescription);

    /// <summary>The PSH1408 rule description.</summary>
    private const string UseStopwatchTimestampsDescription =
        "A Stopwatch allocated only to read elapsed time can be replaced by capturing Stopwatch.GetTimestamp into a long and asking "
        + "Stopwatch.GetElapsedTime for the difference (.NET 7+) — same precision, no allocation. Suggested only where the API exists.";

    /// <summary>The PSH1409 rule description.</summary>
    private const string UseThrowHelpersDescription =
        "A hand-written check-and-throw inlines exception construction into every caller and enlarges methods past JIT inlining budgets; "
        + "the framework throw helpers keep the guard to a single call and move the throw out of line. Each helper is suggested only "
        + "where it exists, and its standard message replaces any hand-written one.";

    /// <summary>The PSH1410 rule description.</summary>
    private const string InlineTrivialForwardersDescription =
        "An expression-bodied method that only forwards to another member can still be skipped by the JIT's IL-size inlining heuristics, "
        + "leaving a call frame around a one-line body; MethodImplOptions.AggressiveInlining makes the intent explicit. Blanket inlining "
        + "attributes are an opinionated convention, so the rule is opt-in.";

    /// <summary>The PSH1411 rule description.</summary>
    private const string SealNonDerivedTypeDescription =
        "A call on an unsealed class must go through a virtual dispatch unless the JIT can prove no override exists. Sealing a class it "
        + "cannot be told about states that up front: virtual and interface calls on it devirtualize, inline, and stop blocking the "
        + "optimizations downstream of them. Only 'private', 'file' and 'internal' classes are reported, because those are the ones whose "
        + "whole set of derived types the compilation can see — a 'public' class may be derived from outside the assembly, so sealing it is "
        + "a breaking change. Set 'performancesharp.PSH1411.include_public = true' in a project that is not a library to report those too. "
        + "A class that is already sealed, static, abstract, or a record, and one that any type in the compilation derives from, is never "
        + "reported.";

    /// <summary>The UseSharedRandom rule description.</summary>
    private const string UseSharedRandomDescription =
        "'Random.Shared' is a thread-safe instance the runtime already made, so taking it costs nothing. Allocating your own is worse than "
        + "redundant: a 'new Random()' created in a tight loop or per request can be seeded from the same clock tick as the last one and "
        + "hand back the identical sequence, and a single instance shared across threads without a lock is not safe. A Random constructed "
        + "with an explicit seed is deliberate — a reproducible sequence — and is never reported.";

    /// <summary>The UseUnixEpochField rule description.</summary>
    private const string UseUnixEpochFieldDescription =
        "'new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)' is the Unix epoch spelled out, and spelling it out invites getting it wrong — "
        + "the overload without a 'DateTimeKind' produces an Unspecified epoch that silently shifts by the local offset the moment it is "
        + "compared or converted. 'DateTime.UnixEpoch' and 'DateTimeOffset.UnixEpoch' are the value, already UTC, computed at compile time.";

    /// <summary>The PSH1414 rule description.</summary>
    private const string MarkMembersStaticDescription =
        "An instance method that never reads 'this' still takes it — a hidden argument passed at every call, and a receiver the JIT must "
        + "prove non-null before it can dispatch. Static says what the method actually is, and lets the call go direct.";

    /// <summary>The PSH1415 rule description.</summary>
    private const string UseConcreteTypeDescription =
        "A local or field typed as an interface dispatches through it — every call is virtual, and none of it can be inlined. When the code "
        + "assigns exactly one concrete type and never needs the abstraction, declaring the concrete type turns those calls direct and lets "
        + "the JIT see through them.";

    /// <summary>The PSH1416 rule description.</summary>
    private const string CacheSerializerOptionsDescription =
        "The serializer caches its per-type metadata against the options instance it was handed. A fresh options object on every call throws "
        + "that cache away and re-derives the whole contract — reflection, converter lookup and all — for a type it has already seen. This is "
        + "one of the most expensive accidental costs in a serialization path.";

    /// <summary>The PSH1417 rule description.</summary>
    private const string ExpensiveDebugAssertArgumentDescription =
        "The call to 'Debug.Assert' is compiled away in release builds — but the arguments are evaluated before the call, so the work that "
        + "produced them is not. An assertion whose message interpolates state, or whose condition calls something costly, pays that cost in "
        + "production for a check that no longer runs.";

    /// <summary>Creates a Warning-severity ApiSelection descriptor whose help link points at the rule's docs page.</summary>
    /// <param name="id">The diagnostic id.</param>
    /// <param name="title">The rule title.</param>
    /// <param name="messageFormat">The message format.</param>
    /// <param name="description">The rule description.</param>
    /// <returns>The descriptor.</returns>
    private static DiagnosticDescriptor Create(string id, string title, string messageFormat, string description) =>
        DescriptorFactory.Create(id, title, messageFormat, "ApiSelection", description);

    /// <summary>Creates an ApiSelection descriptor that is disabled by default (opt-in via .editorconfig).</summary>
    /// <param name="id">The diagnostic id.</param>
    /// <param name="title">The rule title.</param>
    /// <param name="messageFormat">The message format.</param>
    /// <param name="description">The rule description.</param>
    /// <returns>The descriptor.</returns>
    private static DiagnosticDescriptor CreateOptIn(string id, string title, string messageFormat, string description) =>
        DescriptorFactory.CreateOptIn(id, title, messageFormat, "ApiSelection", description);
}
