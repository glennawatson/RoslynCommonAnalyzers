// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Diagnostic descriptors for the allocation and GC rules (PSH10xx). These target
/// avoidable heap work: delegate and closure allocations, throwaway empty arrays,
/// and objects that burden the garbage collector for no benefit.
/// </summary>
internal static class AllocationRules
{
    /// <summary>PSH1000 — a capture-free anonymous function should be <c>static</c>.</summary>
    public static readonly DiagnosticDescriptor MakeAnonymousFunctionStatic = Create(
        "PSH1000",
        "Anonymous functions without captures should be static",
        "Add 'static' to this anonymous function",
        "Marking a capture-free anonymous function static keeps its delegate cached and stops a later edit from silently capturing state, which would allocate a closure per call.");

    /// <summary>PSH1001 — a zero-length array allocation should reuse the shared empty array.</summary>
    public static readonly DiagnosticDescriptor UseArrayEmpty = Create(
        "PSH1001",
        "Avoid allocating zero-length arrays",
        "Replace this zero-length array allocation with {0}",
        "A zero-length array allocation creates a new object per evaluation; Array.Empty<T>() and the empty collection expression [] (which compiles to it) return one shared array.");

    /// <summary>PSH1002 — a finalizer has an empty body and only slows down garbage collection.</summary>
    public static readonly DiagnosticDescriptor RemoveEmptyFinalizer = Create(
        "PSH1002",
        "Empty finalizers should be removed",
        "Remove this empty finalizer; it only burdens the garbage collector",
        "An empty finalizer does no cleanup yet forces the runtime to track the object on the finalization queue, so it should be removed.");

    /// <summary>PSH1003 — an <c>in</c> parameter of a non-readonly struct type forces defensive copies.</summary>
    public static readonly DiagnosticDescriptor InParameterWithNonReadonlyStruct = Create(
        "PSH1003",
        "'in' parameters should use readonly structs",
        "'{0}' is passed by 'in' reference but '{1}' is not a readonly struct, so member accesses copy it",
        "Passing a non-readonly struct by 'in' reference makes the compiler defensively copy it on member accesses, costing more than passing by value; make the struct readonly or drop 'in'.");

    /// <summary>PSH1004 — a constant inline array argument is reallocated on every call.</summary>
    public static readonly DiagnosticDescriptor HoistConstantArrayArguments = Create(
        "PSH1004",
        "Constant arrays passed as arguments should be hoisted",
        "Hoist this constant array into a static readonly field so it is allocated once",
        "An inline array of constants passed as an argument allocates an identical array on every call; a static readonly field allocates it once and reuses it.");

    /// <summary>PSH1005 — a struct without equality members boxes through <c>ValueType.Equals</c>.</summary>
    public static readonly DiagnosticDescriptor ValueTypeEqualityBoxes = Create(
        "PSH1005",
        "Structs should define equality members to avoid boxing comparisons",
        "Add equality members to '{0}'; the inherited ValueType.Equals boxes and may reflect on every comparison",
        "A struct compared through the inherited ValueType equality boxes both operands and can reflect over fields; implement IEquatable<T> with overrides, or declare a record struct.");

    /// <summary>PSH1006 — a concurrent-dictionary factory captures state instead of using its argument.</summary>
    public static readonly DiagnosticDescriptor ConcurrentDictionaryClosureCapture = Create(
        "PSH1006",
        "ConcurrentDictionary factories should use the lambda argument",
        "Use the factory lambda's own parameter instead of capturing '{0}'",
        "A GetOrAdd or AddOrUpdate factory lambda that captures the key variable allocates a closure on every call; using the lambda's own key parameter lets the delegate be cached.");

    /// <summary>PSH1007 — a large readonly struct parameter is copied when passed by value.</summary>
    public static readonly DiagnosticDescriptor PassLargeReadonlyStructByIn = Create(
        "PSH1007",
        "Pass large readonly structs by 'in' reference",
        "Pass '{0}' by 'in' reference; copying '{1}' (~{2} bytes) per call costs more than the indirection",
        "Passing a large readonly struct by value copies it on every call; 'in' passes a reference. Only structs over a configurable size are reported; well-known cheap types never are.");

    /// <summary>PSH1008 — <c>GC.SuppressFinalize</c> is called for a type that can never have a finalizer.</summary>
    public static readonly DiagnosticDescriptor UselessSuppressFinalize = Create(
        "PSH1008",
        "Remove SuppressFinalize calls for finalizer-free types",
        "'{0}' is sealed and has no finalizer, so this GC.SuppressFinalize call does nothing",
        "GC.SuppressFinalize only matters for objects the GC registered for finalization; on a sealed type with no finalizer the call is pure per-dispose overhead.");

    /// <summary>Creates a Warning-severity Allocations descriptor whose help link points at the rule's docs page.</summary>
    /// <param name="id">The diagnostic id.</param>
    /// <param name="title">The rule title.</param>
    /// <param name="messageFormat">The message format.</param>
    /// <param name="description">The rule description.</param>
    /// <returns>The descriptor.</returns>
    private static DiagnosticDescriptor Create(string id, string title, string messageFormat, string description) =>
        new(
            id,
            title,
            messageFormat,
            "Allocations",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: description,
            helpLinkUri: $"https://github.com/glennawatson/RoslynCommonAnalyzers/blob/main/docs/rules/{id}.md");
}
