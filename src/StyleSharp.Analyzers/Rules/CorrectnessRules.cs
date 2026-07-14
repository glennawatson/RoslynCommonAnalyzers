// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Diagnostic descriptors for the correctness rules (SST24xx). These report code that compiles and
/// runs but does not do what it appears to: arguments handed over in the wrong order, a guard that
/// runs too late to guard anything, a reference to a member that is not there.
/// </summary>
internal static class CorrectnessRules
{
    /// <summary>SST2400 — arguments are passed in an order the parameter names contradict.</summary>
    public static readonly DiagnosticDescriptor SwappedArguments = Create(
        "SST2400",
        "Arguments should be passed in the parameter's order",
        "'{0}' is passed as '{1}'; the names suggest these arguments are swapped",
        SwappedArgumentsDescription);

    /// <summary>SST2401 — a null-dereference failure is caught rather than prevented.</summary>
    public static readonly DiagnosticDescriptor CatchNullReference = Create(
        "SST2401",
        "Do not catch NullReferenceException",
        "Catching '{0}' hides a bug rather than handling a failure",
        CatchNullReferenceDescription);

    /// <summary>SST2402 — a constructor writes to state shared by every instance.</summary>
    public static readonly DiagnosticDescriptor StaticFieldWrittenInConstructor = Create(
        "SST2402",
        "Constructors should not write to static fields",
        "'{0}' is static; assigning it in an instance constructor lets every new instance overwrite it for all the others",
        StaticFieldWrittenInConstructorDescription);

    /// <summary>SST2403 — a half-built instance escapes its own constructor.</summary>
    public static readonly DiagnosticDescriptor ThisEscapesConstructor = Create(
        "SST2403",
        "Do not let 'this' escape from a constructor",
        "'this' escapes '{0}' before construction finishes; the receiver can observe a half-built object",
        ThisEscapesConstructorDescription);

    /// <summary>SST2404 — an iterator's argument checks do not run until it is enumerated.</summary>
    public static readonly DiagnosticDescriptor IteratorValidatesTooLate = Create(
        "SST2404",
        "Validate an iterator's arguments before it starts yielding",
        "'{0}' validates its arguments inside an iterator, so the check does not run until the caller enumerates",
        IteratorValidatesTooLateDescription);

    /// <summary>SST2405 — a debugger display string names a member that does not exist.</summary>
    public static readonly DiagnosticDescriptor DebuggerDisplayNamesMissingMember = Create(
        "SST2405",
        "DebuggerDisplay should reference members that exist",
        "'{0}' names '{1}', which '{2}' does not declare",
        DebuggerDisplayNamesMissingMemberDescription);

    /// <summary>SST2406 — a for loop's stop condition can never change.</summary>
    public static readonly DiagnosticDescriptor InvariantLoopCondition = Create(
        "SST2406",
        "A loop's stop condition should be able to change",
        "Nothing in this loop can change '{0}', so it runs forever or not at all",
        InvariantLoopConditionDescription);

    /// <summary>SST2407 — an event is declared but nothing raises it.</summary>
    public static readonly DiagnosticDescriptor EventNeverRaised = Create(
        "SST2407",
        "Declared events should be raised",
        "Nothing raises '{0}'; subscribers will wait forever",
        EventNeverRaisedDescription);

    /// <summary>SST2408 — a StringBuilder is filled and never read.</summary>
    public static readonly DiagnosticDescriptor StringBuilderNeverRead = Create(
        "SST2408",
        "A StringBuilder that is filled should be read",
        "'{0}' is appended to but its contents are never used",
        StringBuilderNeverReadDescription);

    /// <summary>SST2409 — a general exception type is thrown.</summary>
    public static readonly DiagnosticDescriptor ThrowsGeneralException = Create(
        "SST2409",
        "Do not throw a general exception type",
        "Throwing '{0}' gives callers nothing to catch selectively",
        ThrowsGeneralExceptionDescription);

    /// <summary>SST2410 — a disposable is created into a local and never disposed.</summary>
    public static readonly DiagnosticDescriptor DisposableNeverDisposed = Create(
        "SST2410",
        "A created disposable should be disposed",
        "'{0}' is never disposed",
        DisposableNeverDisposedDescription);

    /// <summary>The SwappedArguments rule description.</summary>
    private const string SwappedArgumentsDescription =
        "When the arguments at a call site have the same names as the parameters but in a different order, the call compiles and does the "
        + "wrong thing — the types line up, so nothing stops it. This is the failure a long parameter list of same-typed values invites, "
        + "and it is invisible in review. Only a genuine transposition is reported: the names must match parameters the call actually has, "
        + "in a different position.";

    /// <summary>The CatchNullReference rule description.</summary>
    private const string CatchNullReferenceDescription =
        "A NullReferenceException is not a condition to recover from; it is the runtime reporting that the code dereferenced something it "
        + "never checked. Catching it converts a crash with a stack trace into silent, arbitrary behavior — and it catches every future "
        + "instance of the same mistake, anywhere inside the try block. Guard the value instead.";

    /// <summary>The StaticFieldWrittenInConstructor rule description.</summary>
    private const string StaticFieldWrittenInConstructorDescription =
        "An instance constructor runs once per object, but a static field exists once per type. Assigning one from the other means the "
        + "last object constructed silently redefines the field for every object that already exists — and in a threaded program, which one "
        + "wins is a race. If the value belongs to the type, set it in a static constructor or an initializer.";

    /// <summary>The ThisEscapesConstructor rule description.</summary>
    private const string ThisEscapesConstructorDescription =
        "Handing 'this' to anything before the constructor returns publishes an object whose fields are not all set — including 'readonly' "
        + "ones, which the receiver may read as null. Worse, if the receiver stores it somewhere another thread can see, that thread can "
        + "use the object while it is still being built. Publish the instance after construction, from a factory or an initialization step.";

    /// <summary>The IteratorValidatesTooLate rule description.</summary>
    private const string IteratorValidatesTooLateDescription =
        "A method containing 'yield' does not run when it is called — the body starts only on the first MoveNext. So the argument guard at "
        + "the top of it does not fire at the call site, where the caller's stack still says who passed the bad value, but later, from "
        + "inside whatever foreach eventually consumed the sequence. Split the method: a normal method that validates and then returns the "
        + "iterator from a private local function.";

    /// <summary>The DebuggerDisplayNamesMissingMember rule description.</summary>
    private const string DebuggerDisplayNamesMissingMemberDescription =
        "The expression in a [DebuggerDisplay] is resolved by the debugger, not the compiler, so a typo or a renamed member survives the "
        + "build and shows up as an error string in the watch window — at exactly the moment someone is trying to debug something else.";

    /// <summary>The InvariantLoopCondition rule description.</summary>
    private const string InvariantLoopConditionDescription =
        "A loop whose condition reads only values the body never touches has already decided its answer before it starts. Either it never "
        + "runs, or it never stops — and the variable that was supposed to advance is the one that was forgotten.";

    /// <summary>The EventNeverRaised rule description.</summary>
    private const string EventNeverRaisedDescription =
        "An event nobody raises is a promise the type never keeps. Callers subscribe, the handler never runs, and there is nothing to see "
        + "at the point of failure — the bug is the absence of code. Raise it, or remove it.";

    /// <summary>The StringBuilderNeverRead rule description.</summary>
    private const string StringBuilderNeverReadDescription =
        "Every Append in the method did real work and then threw it away — the 'ToString' that was supposed to collect it is missing. The "
        + "code looks like it builds a string and does not, and nothing at runtime says so.";

    /// <summary>The ThrowsGeneralException rule description.</summary>
    private const string ThrowsGeneralExceptionDescription =
        "'Exception', 'SystemException' and 'ApplicationException' say only that something went wrong. A caller who wants to handle one "
        + "failure has to catch all of them, including the ones it has no idea about — so the code that handles a missing file also "
        + "swallows the bug three frames down. Throw the type that names the failure.";

    /// <summary>The DisposableNeverDisposed rule description.</summary>
    private const string DisposableNeverDisposedDescription =
        "The local holds the only reference to something the method built and owns — a handle, a socket, a timer — and the method "
        + "returns without releasing it. Nothing fails at the point of the leak; the cost arrives later, as a file that stays locked or "
        + "a connection that is never given back. This is an ownership check, not a dataflow one: it reports only a local that is "
        + "created with 'new', used where it stands, and dropped. The moment the value is handed anywhere else — returned, stored, "
        + "passed to a method or a constructor, added to a collection, captured — the rule says nothing, because whoever received it "
        + "may be the one that disposes it.";

    /// <summary>Creates a Warning-severity Correctness descriptor whose help link points at the rule's docs page.</summary>
    /// <param name="id">The diagnostic id.</param>
    /// <param name="title">The rule title.</param>
    /// <param name="messageFormat">The message format.</param>
    /// <param name="description">The rule description.</param>
    /// <returns>The descriptor.</returns>
    private static DiagnosticDescriptor Create(string id, string title, string messageFormat, string description) =>
        DescriptorFactory.Create(id, title, messageFormat, "Correctness", description);
}
