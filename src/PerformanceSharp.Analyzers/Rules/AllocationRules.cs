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

    /// <summary>PSH1009 — a variable-length <c>stackalloc</c> should be bounded by a constant guard.</summary>
    public static readonly DiagnosticDescriptor UnboundedStackalloc = Create(
        "PSH1009",
        "Bound variable-length stackalloc with a constant guard",
        "Guard this stackalloc with a constant length check, falling back to the heap or a pool above it",
        UnboundedStackallocDescription);

    /// <summary>PSH1010 — returning a reference-typed array to the pool should clear it.</summary>
    public static readonly DiagnosticDescriptor ClearPooledReferenceArrays = Create(
        "PSH1010",
        "Clear reference-typed arrays when returning them to the pool",
        "Pass 'clearArray: true' so the pooled array does not keep these '{0}' references alive",
        ClearPooledReferenceArraysDescription);

    /// <summary>PSH1011 — a capturing lambda passed to an API with a state-taking overload should use it.</summary>
    public static readonly DiagnosticDescriptor UseStateOverload = Create(
        "PSH1011",
        "Pass state to callbacks through the state-taking overload",
        "Use the '{0}' overload with a state argument so this lambda does not capture",
        UseStateOverloadDescription);

    /// <summary>PSH1012 — equality through an unconstrained type parameter boxes value types.</summary>
    public static readonly DiagnosticDescriptor UseEqualityComparerDefault = Create(
        "PSH1012",
        "Compare type parameter values with EqualityComparer<T>.Default",
        "Use EqualityComparer<{0}>.Default.Equals for this comparison; Equals through object boxes value types",
        UseEqualityComparerDefaultDescription);

    /// <summary>PSH1013 — constant UTF-8 bytes should be a span property, not a byte array field.</summary>
    public static readonly DiagnosticDescriptor UseUtf8SpanProperty = Create(
        "PSH1013",
        "Expose constant UTF-8 data as a ReadOnlySpan<byte> property",
        "Change '{0}' to a static ReadOnlySpan<byte> property so its bytes stay in the assembly's data section",
        UseUtf8SpanPropertyDescription);

    /// <summary>PSH1014 — a struct whose instance state is immutable should be declared readonly.</summary>
    public static readonly DiagnosticDescriptor MakeStructReadonly = Create(
        "PSH1014",
        "Declare immutable structs as readonly",
        "Add 'readonly' to '{0}'; all of its instance state is already immutable",
        MakeStructReadonlyDescription);

    /// <summary>PSH1015 — a value type cast through object boxes just to unbox.</summary>
    public static readonly DiagnosticDescriptor BoxingRoundTripCast = Create(
        "PSH1015",
        "Avoid casting value types through object",
        "Casting this '{0}' value through object boxes it; cast directly",
        BoxingRoundTripCastDescription);

    /// <summary>The PSH1009 rule description.</summary>
    private const string UnboundedStackallocDescription =
        "A stackalloc whose length comes from data can blow the stack on adversarial or unexpected input; the resilient shape tests the "
        + "length against a constant first and takes a heap or pooled buffer above it, keeping the stack fast path for small sizes.";

    /// <summary>The PSH1010 rule description.</summary>
    private const string ClearPooledReferenceArraysDescription =
        "ArrayPool keeps returned arrays indefinitely; when the elements are reference types (or structs holding references), a non-cleared "
        + "return pins every referenced object graph in memory until the array is rented and overwritten again.";

    /// <summary>The PSH1011 rule description.</summary>
    private const string UseStateOverloadDescription =
        "A lambda that captures locals or 'this' allocates a closure object and a fresh delegate on every call; APIs that accept a "
        + "callback-and-state pair exist so a static lambda can receive the data through the state argument and the delegate can be "
        + "cached. Reported only when the invoked member has a sibling overload that adds a state parameter.";

    /// <summary>The PSH1012 rule description.</summary>
    private const string UseEqualityComparerDefaultDescription =
        "When a type parameter carries no IEquatable constraint, Equals binds to Object.Equals and boxes the argument (and both operands "
        + "for the static overload) on every value-type instantiation; EqualityComparer<T>.Default.Equals compares without boxing and "
        + "the JIT devirtualizes it.";

    /// <summary>The PSH1013 rule description.</summary>
    private const string UseUtf8SpanPropertyDescription =
        "A static readonly byte array built from a UTF-8 literal is a mutable heap object with a startup cost and a field indirection on "
        + "every read; a static ReadOnlySpan<byte> property returning the literal compiles to a direct pointer into the assembly's data "
        + "section. Reported only when every use already reads the field like a span.";

    /// <summary>The PSH1014 rule description.</summary>
    private const string MakeStructReadonlyDescription =
        "When a struct is not declared readonly the compiler defensively copies it before calling its members through 'in' parameters, "
        + "ref readonly locals, and readonly fields, even if every field is already readonly; the readonly modifier proves immutability "
        + "and removes those hidden copies.";

    /// <summary>The PSH1015 rule description.</summary>
    private const string BoxingRoundTripCastDescription =
        "A cast to object followed by an immediate cast back to a value type allocates a box that is thrown away as soon as it is "
        + "unboxed; when a direct conversion between the two value types exists, casting directly converts with no allocation.";

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
