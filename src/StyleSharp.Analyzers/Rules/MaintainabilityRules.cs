// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Single source of truth for the maintainability (SST14xx) diagnostic descriptors.
/// </summary>
internal static class MaintainabilityRules
{
    /// <summary>SST1400 — an element does not declare an access modifier.</summary>
    public static readonly DiagnosticDescriptor AccessModifierDeclared = Create(
        "SST1400",
        "Access modifier should be declared",
        "'{0}' should declare an explicit access modifier",
        "Types and members declare their accessibility explicitly rather than relying on the implicit default.");

    /// <summary>SST1401 — a non-private, non-constant field is exposed.</summary>
    public static readonly DiagnosticDescriptor FieldsPrivate = Create(
        "SST1401",
        "Fields should be private",
        "Field '{0}' should be private; expose it through a property instead",
        "Fields are an implementation detail and should be private (constants may be any accessibility).");

    /// <summary>SST1402 — a file declares more than one top-level type.</summary>
    public static readonly DiagnosticDescriptor SingleType = Create(
        "SST1402",
        "File should contain a single type",
        "'{0}' should be moved to its own file; a file should declare a single top-level type",
        "Each file declares a single top-level type so types are easy to locate.");

    /// <summary>SST1403 — a file declares more than one namespace.</summary>
    public static readonly DiagnosticDescriptor SingleNamespace = Create(
        "SST1403",
        "File should contain a single namespace",
        "Namespace '{0}' should be the only namespace in the file",
        "Each file declares a single namespace.");

    /// <summary>SST1404 — a code-analysis suppression has no justification.</summary>
    public static readonly DiagnosticDescriptor SuppressionJustified = Create(
        "SST1404",
        "Code analysis suppression should have justification",
        "The suppression should set a non-empty 'Justification'",
        "Every [SuppressMessage] explains why the rule is suppressed.");

    /// <summary>SST1405 — a <c>Debug.Assert</c> call provides no message.</summary>
    public static readonly DiagnosticDescriptor AssertMessage = Create(
        "SST1405",
        "Debug.Assert should provide message text",
        "The Debug.Assert call should provide a message describing the assertion",
        "Debug.Assert calls describe the failed assumption for whoever hits them.");

    /// <summary>SST1406 — a <c>Debug.Fail</c> call provides no message.</summary>
    public static readonly DiagnosticDescriptor FailMessage = Create(
        "SST1406",
        "Debug.Fail should provide message text",
        "The Debug.Fail call should provide a message describing the failure",
        "Debug.Fail calls describe the failure for whoever hits them.");

    /// <summary>SST1407 — mixed-precedence arithmetic is not parenthesized.</summary>
    public static readonly DiagnosticDescriptor ArithmeticPrecedence = Create(
        "SST1407",
        "Arithmetic expressions should declare precedence",
        "Add parentheses to make the arithmetic precedence explicit",
        "Mixed arithmetic and shift operators are parenthesized so the intended precedence is clear.");

    /// <summary>SST1408 — mixed conditional operators are not parenthesized.</summary>
    public static readonly DiagnosticDescriptor ConditionalPrecedence = Create(
        "SST1408",
        "Conditional expressions should declare precedence",
        "Add parentheses to make the conditional precedence explicit",
        "Expressions mixing '&&' and '||' are parenthesized so the intended precedence is clear.");

    /// <summary>SST1410 — an anonymous method has an empty parameter list.</summary>
    public static readonly DiagnosticDescriptor RemoveDelegateParentheses = Create(
        "SST1410",
        "Remove delegate parenthesis when possible",
        "Remove the empty parameter list from the anonymous method",
        "An anonymous method with no parameters omits the empty parameter list.");

    /// <summary>SST1411 — an attribute uses an empty argument list.</summary>
    public static readonly DiagnosticDescriptor RemoveAttributeParentheses = Create(
        "SST1411",
        "Attribute constructor should not use unnecessary parenthesis",
        "Remove the empty argument list from the attribute",
        "An attribute with no arguments omits the empty parentheses.");

    /// <summary>SST1413 — a multi-line initializer omits the trailing comma.</summary>
    public static readonly DiagnosticDescriptor TrailingComma = Create(
        "SST1413",
        "Use a trailing comma in multi-line initializers",
        "Add a trailing comma after the last element",
        "The last element of a multi-line initializer or enum is followed by a trailing comma so reordering is clean.");

    /// <summary>SST1412 — files should be stored as UTF-8 with a byte order mark (opt-in; mutually exclusive with SST1450).</summary>
    public static readonly DiagnosticDescriptor Utf8WithBom = CreateOptIn(
        "SST1412",
        "Store files as UTF-8 with a byte order mark",
        "This file should be saved as UTF-8 with a byte order mark",
        "Source files are stored as UTF-8 with a byte order mark. Off by default — enable either this or SST1450, not both.");

    /// <summary>SST1450 — files should be stored as UTF-8 without a byte order mark (StyleSharp original; opt-in; mutually exclusive with SST1412).</summary>
    public static readonly DiagnosticDescriptor Utf8WithoutBom = CreateOptIn(
        "SST1450",
        "Store files as UTF-8 without a byte order mark",
        "This file should be saved as UTF-8 without a byte order mark",
        "Source files are stored as UTF-8 without a byte order mark. Off by default — enable either this or SST1412, not both.");

    /// <summary>SST1414 — a tuple type in a member signature has an unnamed element.</summary>
    public static readonly DiagnosticDescriptor TupleSignatureElementNames = Create(
        "SST1414",
        "Tuple types in signatures should have element names",
        "Name the elements of this tuple type",
        "A tuple type that appears in a member signature names each element so callers do not depend on positional 'ItemN' access.");

    /// <summary>SST1416 — a public member is declared in a type that is not externally visible (StyleSharp original; opt-in).</summary>
    public static readonly DiagnosticDescriptor NoPublicOnInternalType = CreateOptIn(
        "SST1416",
        "Do not declare public members in a non-public type",
        "Member '{0}' is public but its type is not externally visible; declare it internal",
        "A public member of a type that is not externally visible is misleading — its effective accessibility is limited by the type. Off by default — public-on-internal is a common habit.");

    /// <summary>SST1418 — a binary expression is an operand of <c>??</c> without parentheses (StyleSharp original; extends SST1407/SST1408).</summary>
    public static readonly DiagnosticDescriptor NullCoalescingPrecedence = Create(
        "SST1418",
        "Declare precedence when mixing the null-coalescing operator",
        "Parenthesize this expression to make its precedence with '??' explicit",
        "A binary expression used as an operand of the '??' operator is parenthesized so the precedence is explicit.");

    /// <summary>SST1417 — a namespace does not match the file's folder structure (StyleSharp original; opt-in).</summary>
    public static readonly DiagnosticDescriptor NamespaceMatchesFolder = CreateOptIn(
        "SST1417",
        "Namespace should match the folder structure",
        "Namespace '{0}' does not match the folder structure; expected '{1}'",
        "A type's namespace mirrors the file's folder path under the project root. Off by default — set 'stylesharp.namespace_root' in .editorconfig to override the root namespace.");

    /// <summary>SST1415 — an argument-exception constructor uses a string literal where <c>nameof</c> would track renames (StyleSharp original).</summary>
    public static readonly DiagnosticDescriptor UseNameofForParameter = Create(
        "SST1415",
        "Use nameof for parameter references",
        "Use 'nameof({0})' instead of the string literal \"{0}\"",
        "A string literal that names a parameter in an argument-exception constructor is written with 'nameof' so it follows renames.");

    /// <summary>SST1421 — a property has a setter but no getter.</summary>
    public static readonly DiagnosticDescriptor NoWriteOnlyProperty = Create(
        "SST1421",
        "Write-only properties should not be used",
        "Add a getter or replace this write-only property with a method",
        "A property exposes a readable value; write-only operations are represented by methods.");

    /// <summary>SST1423 — a switch statement exceeds the configured section count.</summary>
    public static readonly DiagnosticDescriptor TooManySwitchSections = Create(
        "SST1423",
        "Switch statements should not have too many sections",
        "Reduce this switch statement from {0} sections to at most {1}",
        "Large switch statements are split into smaller abstractions. The maximum section count is configurable and defaults to 30.");

    /// <summary>SST1419 — a modifier has no effect in its declaration context.</summary>
    public static readonly DiagnosticDescriptor NoRedundantModifier = Create(
        "SST1419",
        "Remove redundant modifiers",
        "Remove the redundant '{0}' modifier",
        "Modifiers are omitted when the declaration context already guarantees the same behavior.");

    /// <summary>SST1420 — a property trivially wraps a private backing field.</summary>
    public static readonly DiagnosticDescriptor PreferAutoProperty = Create(
        "SST1420",
        "Trivial properties should be auto-implemented",
        "Convert this property to an auto-property and remove its backing field",
        "A property whose accessors only read and write a private single-use field is auto-implemented.");

    /// <summary>SST1422 — a private field acts only as method-local temporary storage (opt-in).</summary>
    public static readonly DiagnosticDescriptor PrivateFieldUsedAsLocal = CreateOptIn(
        "SST1422",
        "Private fields used only as locals should be local variables",
        "Move field '{0}' into the method that uses it",
        "A field that is reset before every use and referenced by one method does not represent object state. Off by default because conversion is a refactoring.");

    /// <summary>SST1424 — a private field is never assigned outside construction (opt-in).</summary>
    public static readonly DiagnosticDescriptor FieldShouldBeReadonly = CreateOptIn(
        "SST1424",
        "Fields that are never reassigned should be readonly",
        "Make field '{0}' readonly",
        "A private instance field assigned only by its initializer or constructors is declared readonly. Off by default because readonly conversion can be a source-compatibility decision.");

    /// <summary>SST1425 — a class/struct primary-constructor parameter is reassigned after capture.</summary>
    public static readonly DiagnosticDescriptor NoReassignedPrimaryConstructorParameter = Create(
        "SST1425",
        "Do not reassign captured primary-constructor parameters",
        "Do not reassign the captured primary-constructor parameter '{0}'",
        "Reassigning a class or struct primary-constructor parameter mutates its hidden captured field, which is surprising. Use a separate local or explicit field instead.");

    /// <summary>SST1426 — a <c>#pragma warning disable</c> silences an analyzer warning that a scoped <c>[SuppressMessage]</c> should handle (StyleSharp original).</summary>
    public static readonly DiagnosticDescriptor PreferSuppressMessageOverPragma = Create(
        "SST1426",
        "Use [SuppressMessage] instead of #pragma warning disable",
        "Suppress '{0}' with a scoped [SuppressMessage] attribute instead of '#pragma warning disable'",
        "Analyzer warnings use a scoped [SuppressMessage] attribute, not a #pragma warning disable directive. Compiler (CS) warnings are exempt because only a pragma can disable them.");

    /// <summary>SST1427 — a <c>protected</c> member of a sealed type has no effect, since the type cannot be derived.</summary>
    public static readonly DiagnosticDescriptor NoProtectedInSealed = Create(
        "SST1427",
        "Protected members of sealed types should not be used",
        "Make this member private; 'protected' has no effect in a sealed type",
        "A sealed type cannot be derived from, so a 'protected' member is reachable only as if it were private. Mark it private (or drop 'protected' from 'protected internal').");

    /// <summary>SST1428 — an abstract type exposes a <c>public</c> constructor that only derived types can call.</summary>
    public static readonly DiagnosticDescriptor NoPublicConstructorOnAbstractType = Create(
        "SST1428",
        "Abstract types should not declare public constructors",
        "Make this constructor 'protected'; only a derived type can call it",
        "An abstract type cannot be instantiated directly, so a 'public' constructor is misleading. A 'protected' (or private) constructor states that only derived types call it.");

    /// <summary>SST1429 — a <c>catch</c> clause swallows the base <see cref="Exception"/> with an empty body.</summary>
    public static readonly DiagnosticDescriptor NoEmptyCatchOfBaseException = Create(
        "SST1429",
        "Empty catch clauses should not swallow the base exception",
        "Handle, rethrow, or narrow this catch; an empty catch of the base exception hides failures",
        "A 'catch (Exception) { }' (or bare 'catch { }') with an empty body silently discards every error, including ones the code is not prepared for. Handle it, rethrow, or catch a narrower type.");

    /// <summary>SST1430 — <c>throw ex;</c> in a catch resets the captured stack trace instead of preserving it.</summary>
    public static readonly DiagnosticDescriptor PreserveStackTraceOnRethrow = Create(
        "SST1430",
        "Rethrow with 'throw;' to preserve the stack trace",
        "Use 'throw;' instead of 'throw {0};' so the original stack trace is kept",
        "Re-throwing the caught exception with 'throw ex;' overwrites its stack trace with the rethrow location. A bare 'throw;' keeps the original trace intact.");

    /// <summary>SST1431 — a <c>static</c> member of a generic type never mentions the type's type parameters.</summary>
    public static readonly DiagnosticDescriptor StaticMemberShouldUseTypeParameter = Create(
        "SST1431",
        "Static members of a generic type should use a type parameter",
        "Reference a type parameter, or move '{0}' off the generic type",
        "A static member whose signature ignores its generic type's type parameters forces callers to pick an arbitrary type argument, and usually belongs on a non-generic type.");

    /// <summary>SST1432 — a class declares only static members yet is not marked <c>static</c> (opt-in).</summary>
    public static readonly DiagnosticDescriptor MakeClassStatic = CreateOptIn(
        "SST1432",
        "Classes with only static members should be static",
        "Mark '{0}' as 'static'; it declares only static members",
        "A class whose members are all static can be marked 'static' to forbid instantiation. Off by default, since marking a published, instantiable type 'static' is a breaking change.");

    /// <summary>SST1433 — a public parameterless constructor with an empty body restates the compiler default.</summary>
    public static readonly DiagnosticDescriptor NoRedundantConstructor = Create(
        "SST1433",
        "Redundant constructors should be removed",
        "Remove this redundant constructor; the compiler supplies an equivalent default constructor",
        "A type's only constructor being a public, parameterless, empty constructor restates the default constructor the compiler would emit anyway, so it can be removed.");

    /// <summary>SST1435 — a namespace declaration contains no members.</summary>
    public static readonly DiagnosticDescriptor NoEmptyNamespace = Create(
        "SST1435",
        "Empty namespace declarations should be removed",
        "Remove this empty namespace declaration",
        "A namespace declaration with no members serves no purpose and can be deleted.");

    /// <summary>SST1436 — a class, struct, or record declares no members (opt-in).</summary>
    public static readonly DiagnosticDescriptor NoEmptyType = CreateOptIn(
        "SST1436",
        "Empty types should not be declared",
        "Add members to '{0}' or remove it; an empty type is rarely intentional",
        "A class, struct, or record with no members is usually an oversight. Off by default, because empty types are sometimes used as markers, DTOs, or extension points.");

    /// <summary>SST1437 — an interface declares no members (opt-in).</summary>
    public static readonly DiagnosticDescriptor NoEmptyInterface = CreateOptIn(
        "SST1437",
        "Empty interfaces should not be declared",
        "Add members to '{0}' or remove it; an empty interface adds no contract",
        "An interface with no members defines no contract and is often better expressed with an attribute. Off by default, because marker interfaces are an accepted pattern.");

    /// <summary>SST1438 — a method has an empty body (opt-in).</summary>
    public static readonly DiagnosticDescriptor NoEmptyMethod = CreateOptIn(
        "SST1438",
        "Methods should not be empty",
        "Give '{0}' a body or document why it is intentionally empty",
        "An empty method body usually signals unfinished work. Off by default, because empty overrides, virtual hooks, and interface implementations are legitimately empty.");

    /// <summary>SST1439 — a loop or guard statement has an empty embedded block.</summary>
    public static readonly DiagnosticDescriptor NoEmptyNestedBlock = Create(
        "SST1439",
        "Nested code blocks should not be left empty",
        "Fill or remove this empty block; an empty loop or guard body is usually a mistake",
        "An empty block as the body of a loop ('while', 'for', 'foreach', 'do') or guard ('if', 'lock', 'fixed', 'using') usually means missing code or a stray semicolon.");

    /// <summary>SST1440 — a private member has no reads or calls in its declaring type.</summary>
    public static readonly DiagnosticDescriptor RemoveUnusedPrivateMember = Create(
        "SST1440",
        "Remove private members with no local use",
        "Remove private member '{0}' because it has no local use",
        "A private member that is never read, called, or otherwise referenced in its declaring type adds dead surface area and can be removed.");

    /// <summary>SST1441 — a private field is assigned but never read.</summary>
    public static readonly DiagnosticDescriptor RemoveUnreadPrivateField = Create(
        "SST1441",
        "Remove private fields that are never read",
        "Remove private field '{0}' or its dead writes because the value is never read",
        "A private field that receives values but is never read does not represent observable state. Remove the field or the dead writes that feed it.");

    /// <summary>SST1442 - a function's branching complexity exceeds the configured maximum.</summary>
    public static readonly DiagnosticDescriptor CyclomaticComplexity = Create(
        "SST1442",
        "Functions should keep branching complexity low",
        "Reduce the branching complexity of this {2} from {1} to at most {0}",
        "Functions keep their direct decision count below the configured threshold. Wide switch dispatch counts once so table-like code is not penalized.");

    /// <summary>SST1443 - a function's nested-flow complexity exceeds the configured maximum.</summary>
    public static readonly DiagnosticDescriptor CognitiveComplexity = Create(
        "SST1443",
        "Functions should keep nested flow easy to follow",
        "Reduce the nested-flow complexity of this {0} from {1} to at most {2}",
        "Functions keep nesting-heavy control flow below the configured threshold. Flat dispatch is cheaper than nested branching.");

    /// <summary>SST1444 - a loop always exits or continues before a second iteration can run.</summary>
    public static readonly DiagnosticDescriptor SingleIterationLoop = Create(
        "SST1444",
        "Loops should do more than one iteration",
        "Replace this loop jump or restructure the loop so another iteration can run",
        "A loop whose body unconditionally jumps before another iteration can run is usually clearer as straight-line code or a conditional.");

    /// <summary>SST1445 - a using directive imports a namespace, type, or alias the file never uses.</summary>
    public static readonly DiagnosticDescriptor UnnecessaryUsingDirective = Create(
        "SST1445",
        "Remove unnecessary using directives",
        "The using directive for '{0}' is unnecessary",
        "A using directive nothing in the file resolves through is dead weight: it slows reading, invites name collisions, and hides which dependencies the file really has.");

    /// <summary>SST1446 - a class sits deeper in an inheritance chain than the configured maximum.</summary>
    public static readonly DiagnosticDescriptor InheritanceDepth = Create(
        "SST1446",
        "Inheritance chains should stay shallow",
        "'{0}' is {1} levels deep in its inheritance chain, which exceeds the maximum of {2}",
        "Every inheritance level adds state and behavior a reader must load to understand the leaf type; deep chains are rigid and hard to change. Prefer composition or flatter hierarchies.");

    /// <summary>SST1447 - an Equals or GetHashCode override delegates to object's reference-based implementation.</summary>
    public static readonly DiagnosticDescriptor BaseObjectEqualityDelegation = Create(
        "SST1447",
        "Equality overrides should not delegate to object",
        "'{0}' calls the object implementation of '{1}', which compares by reference identity instead of the value semantics this override implies",
        "Calling base.Equals or base.GetHashCode when the base class is object silently reintroduces reference semantics inside an override whose purpose is value semantics.");

    /// <summary>SST1448 - an argument is passed explicitly to a caller-info parameter.</summary>
    public static readonly DiagnosticDescriptor CallerInfoArgument = Create(
        "SST1448",
        "Let the compiler supply caller-info arguments",
        "Remove this argument so the compiler supplies the caller's {0}",
        "Caller-info parameters exist so the compiler injects the calling member, file, or line; passing a value explicitly defeats that and usually reports the wrong call site.");

    /// <summary>SST1449 - code writes directly to the console.</summary>
    public static readonly DiagnosticDescriptor NoConsoleOutput = Create(
        "SST1449",
        "Avoid writing directly to the console",
        "Replace this '{0}' call with the application's logging abstraction",
        "Direct console writes bypass log levels, sinks, and redirection, and turn into noise or lost output when no console is attached. Route diagnostics through a logger.");

    /// <summary>SST1451 - a DateTime is created without specifying its kind.</summary>
    public static readonly DiagnosticDescriptor DateTimeKindRequired = Create(
        "SST1451",
        "Specify a DateTimeKind when creating a DateTime",
        "Specify a DateTimeKind so consumers know whether this DateTime is UTC or local",
        "A DateTime constructed without a kind is DateTimeKind.Unspecified and conversions silently guess; stating the kind (or using DateTimeOffset) makes the timeline explicit.");

    /// <summary>SST1452 - a declared generic type parameter is never used.</summary>
    public static readonly DiagnosticDescriptor UnusedTypeParameter = Create(
        "SST1452",
        "Remove unused type parameters",
        "The type parameter '{0}' is never used; remove it",
        "A type parameter nothing references forces every caller to supply a meaningless type argument and usually marks a refactoring leftover.");

    /// <summary>SST1453 - code appears after a statement that unconditionally leaves the block.</summary>
    public static readonly DiagnosticDescriptor NoUnreachableCode = Create(
        "SST1453",
        "Unreachable code should be removed",
        "Remove this unreachable statement",
        "Statements after an unconditional return, throw, break, or continue in the same block never execute and should be removed.");

    /// <summary>SST1454 - a composite format string contains a placeholder that cannot be satisfied.</summary>
    public static readonly DiagnosticDescriptor ValidCompositeFormatString = Create(
        "SST1454",
        "Composite format strings should match their arguments",
        "Fix this composite format string; one of its placeholders cannot be satisfied",
        "A composite format string is checked against the supplied arguments so invalid or out-of-range placeholders are caught at compile time.");

    /// <summary>SST1455 - an unsafe modifier is present where the declaration contains no unsafe construct.</summary>
    public static readonly DiagnosticDescriptor NoUnnecessaryUnsafeModifier = Create(
        "SST1455",
        "Remove unnecessary unsafe modifiers",
        "Remove the unnecessary 'unsafe' modifier",
        "An unsafe modifier is used only where the declaration actually contains pointer, function-pointer, fixed, or sizeof syntax that requires unsafe context.");

    /// <summary>SST1456 - a readonly field stores a mutable source-defined struct.</summary>
    public static readonly DiagnosticDescriptor NoReadonlyMutableStructField = Create(
        "SST1456",
        "Mutable struct fields should not be readonly",
        "Remove 'readonly' from field '{0}', or make the struct readonly",
        "A readonly field of a mutable struct still allows defensive-copy surprises on member access. Make the struct immutable or keep the field writable so the mutability is explicit.");

    /// <summary>SST1457 - a global suppression target cannot be resolved to a declaration in the compilation.</summary>
    public static readonly DiagnosticDescriptor ValidGlobalSuppressionTarget = Create(
        "SST1457",
        "Global suppressions should point at real declarations",
        "Fix or remove this global suppression target; '{0}' does not resolve in this compilation",
        "A global SuppressMessage target should resolve to the declaration it suppresses so stale suppression entries are removed instead of silently accumulating.");

    /// <summary>SST1458 - a global suppression target uses the old tilde-prefixed target spelling.</summary>
    public static readonly DiagnosticDescriptor UseDeclarationIdSuppressionTarget = Create(
        "SST1458",
        "Global suppression targets should use declaration ids directly",
        "Remove the legacy '~' prefix from this global suppression target",
        "Global SuppressMessage targets use declaration ids directly, which keeps the string consumable by Roslyn's documentation-id resolver without an extra legacy marker.");

    /// <summary>SST1459 - parentheses wrap an expression in a context where grouping has no effect.</summary>
    public static readonly DiagnosticDescriptor RemoveUnnecessaryParentheses = Create(
        "SST1459",
        "Remove grouping parentheses that do not group anything",
        "Remove these parentheses; this expression is already isolated by its containing syntax",
        "Parentheses around a standalone return value, argument, initializer, or assignment value add visual noise when no operator precedence is being clarified.");

    /// <summary>SST1460 - a struct instance member can be readonly because its body does not mutate state.</summary>
    public static readonly DiagnosticDescriptor MakeStructMemberReadonly = Create(
        "SST1460",
        "Mark non-mutating struct members readonly",
        "Mark '{0}' as readonly so it can be called without a defensive copy",
        "A struct member that only reads state is marked readonly so calls through readonly receivers avoid defensive-copy overhead and communicate the member's non-mutating contract.");

    /// <summary>SST1461 - a private parameter is never read by its declaration body.</summary>
    public static readonly DiagnosticDescriptor RemoveUnusedPrivateParameter = Create(
        "SST1461",
        "Remove private parameters that are never read",
        "Remove parameter '{0}' because this private declaration never reads it",
        "A private or local-function parameter that is not read by its body adds dead API surface inside the type and usually marks an unfinished refactor.");

    /// <summary>SST1462 - a suppression targets a diagnostic that is disabled in the active analyzer config scope.</summary>
    public static readonly DiagnosticDescriptor RemoveDisabledDiagnosticSuppression = Create(
        "SST1462",
        "Remove suppressions for diagnostics that are already disabled",
        "Remove this suppression because diagnostic '{0}' is disabled in this analyzer config scope",
        "A SuppressMessage attribute for a diagnostic configured to 'none' or 'silent' cannot suppress a live report and should not stay in source as stale policy documentation.");

    /// <summary>SST1463 - a string literal names an in-scope symbol in a name-shaped argument.</summary>
    public static readonly DiagnosticDescriptor UseNameofForSymbolName = Create(
        "SST1463",
        "Use nameof for symbol-name strings",
        "Use 'nameof({0})' so this symbol-name string follows renames",
        "A string literal passed to a name-shaped parameter is written with nameof when it matches an in-scope symbol, keeping rename operations correct without reflection or runtime lookup.");

    /// <summary>SST1464 — an else clause follows a branch that never falls through.</summary>
    public static readonly DiagnosticDescriptor UnwrapElseAfterJump = Create(
        "SST1464",
        "Unwrap else after a branch that does not fall through",
        "Unwrap this 'else'; the preceding branch always jumps",
        "When the if branch ends in a return, throw, break, or continue, the else wrapper only adds nesting; moving its statements after the if reads straighter.");

    /// <summary>SST1465 — an else block contains only an if statement.</summary>
    public static readonly DiagnosticDescriptor CollapseElseIntoElseIf = Create(
        "SST1465",
        "Collapse an else block that only wraps an if",
        "Write this as 'else if'",
        "An else block whose only statement is an if statement flattens into an 'else if' chain, removing a level of nesting without changing the branch order.");

    /// <summary>SST1466 — a case label shares a switch section with the default label.</summary>
    public static readonly DiagnosticDescriptor RemoveCaseBesideDefault = Create(
        "SST1466",
        "Remove case labels that share a section with default",
        "Remove this case label; the default label already routes here",
        "A case label in the same switch section as the default label selects the body the default already provides; it stays only when a 'goto case' statement targets it.");

    /// <summary>SST1467 — a loop drives an enumerator by hand instead of using foreach.</summary>
    public static readonly DiagnosticDescriptor UseForeachOverManualEnumerator = Create(
        "SST1467",
        "Enumerate with foreach instead of driving the enumerator by hand",
        "Replace this manual enumerator loop with 'foreach'",
        "A while loop over GetEnumerator, MoveNext, and Current restates what foreach compiles to, without the guaranteed disposal and scoping that foreach provides.");

    /// <summary>SST1468 — boolean logic uses a non-short-circuiting operator.</summary>
    public static readonly DiagnosticDescriptor UseShortCircuitOperator = Create(
        "SST1468",
        "Boolean logic should short-circuit",
        "Use '{0}' so the right operand is evaluated only when it can matter",
        UseShortCircuitOperatorDescription);

    /// <summary>SST1469 — a non-nullable value type is compared to null.</summary>
    public static readonly DiagnosticDescriptor ValueTypeNullComparison = Create(
        "SST1469",
        "Do not compare a value type to null",
        "'{0}' is a non-nullable value type; this comparison is always {1}",
        "A non-nullable value type can never be null, so the comparison folds to a constant and usually points at a misread type or a missing '?'.");

    /// <summary>SST1470 — a trailing catch clause only rethrows.</summary>
    public static readonly DiagnosticDescriptor RemoveRethrowOnlyCatch = Create(
        "SST1470",
        "Remove a catch clause that only rethrows",
        "Remove this catch clause; it only rethrows the exception",
        "A final catch clause whose body is exactly 'throw;' with no filter changes nothing about how the exception propagates and hides the clauses that do work.");

    /// <summary>SST1471 — a numeric literal in an expression carries unexplained meaning.</summary>
    public static readonly DiagnosticDescriptor MagicNumber = Create(
        "SST1471",
        "Magic numbers should be named constants",
        "Replace the magic number '{0}' with a named constant",
        MagicNumberDescription);

    /// <summary>The SST1468 rule description.</summary>
    private const string UseShortCircuitOperatorDescription =
        "The bitwise '&' and '|' operators on booleans always evaluate both operands; the conditional forms stop as soon as the answer "
        + "is known. Reported only when the right operand has no side effects to skip.";

    /// <summary>The SST1471 rule description.</summary>
    private const string MagicNumberDescription =
        "A numeric literal buried in an expression states a value without stating its meaning. A literal is excluded when it already sits "
        + "at a declaration that names it, when it is -1, 0 or 1, when it is a bit pattern or shift distance, when it guards a Count or "
        + "Length, or when its position already carries the meaning. Configure the allowed values with "
        + "'stylesharp.SST1471.magic_number_allowed_values'.";

    /// <summary>Creates a Warning-severity Maintainability descriptor whose help link points at the rule's docs page.</summary>
    /// <param name="id">The diagnostic id.</param>
    /// <param name="title">The rule title.</param>
    /// <param name="messageFormat">The message format.</param>
    /// <param name="description">The rule description.</param>
    /// <returns>The descriptor.</returns>
    private static DiagnosticDescriptor Create(string id, string title, string messageFormat, string description) =>
        DescriptorFactory.Create(id, title, messageFormat, "Maintainability", description);

    /// <summary>Creates a Maintainability descriptor that is disabled by default (opt-in via .editorconfig).</summary>
    /// <param name="id">The diagnostic id.</param>
    /// <param name="title">The rule title.</param>
    /// <param name="messageFormat">The message format.</param>
    /// <param name="description">The rule description.</param>
    /// <returns>The descriptor.</returns>
    private static DiagnosticDescriptor CreateOptIn(string id, string title, string messageFormat, string description) =>
        DescriptorFactory.CreateOptIn(id, title, messageFormat, "Maintainability", description);
}
