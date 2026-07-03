# Rule Index

This page is the full categorized rule catalog for both packages published from
this repository: [`StyleSharp.Analyzers`](#stylesharp-rule-index) (`SST####`) and
[`PerformanceSharp.Analyzers`](#performancesharp-rule-index) (`PSH####`).

- Repository overview and installation: [`../README.md`](../README.md)
- Configuration reference: [`CONFIGURATION.md`](CONFIGURATION.md)
- Performance guidance: [`PERFORMANCE.md`](PERFORMANCE.md)
- Recommended presets: [`../recommended.editorconfig`](../recommended.editorconfig) (StyleSharp), [`../recommended-performancesharp.editorconfig`](../recommended-performancesharp.editorconfig) (PerformanceSharp)

Unless noted otherwise, rules are enabled by default at `Warning` severity. Rules marked `opt-in` are disabled by default and are commented out in the presets.

# PerformanceSharp Rule Index

Rules whose primary motivation is the runtime performance of the analyzed code.
Ids are grouped by the hundreds digit: `PSH10xx` allocations & GC, `PSH11xx`
collections & enumeration, `PSH12xx` strings & text, `PSH13xx` concurrency &
async, `PSH14xx` API selection.

## Allocations

| Rule | Description |
| --- | --- |
| [PSH1000](rules/PSH1000.md) | Anonymous functions without captures should be static. |
| [PSH1001](rules/PSH1001.md) | Avoid allocating zero-length arrays (`[]` on C# 12+, else `Array.Empty<T>()`). |
| [PSH1002](rules/PSH1002.md) | Empty finalizers should be removed. |
| [PSH1003](rules/PSH1003.md) | `in` parameters should use readonly structs. |
| [PSH1004](rules/PSH1004.md) | Constant arrays passed as arguments should be hoisted. |
| [PSH1005](rules/PSH1005.md) | Structs should define equality members to avoid boxing comparisons. |
| [PSH1006](rules/PSH1006.md) | `ConcurrentDictionary` factories should use the lambda argument. |
| [PSH1007](rules/PSH1007.md) | Pass large readonly structs by `in` reference. Configurable size threshold and exclusions. |

## Collections

| Rule | Description |
| --- | --- |
| [PSH1100](rules/PSH1100.md) | Hot-path code should avoid `System.Linq.Enumerable` calls. Opt-in. |
| [PSH1101](rules/PSH1101.md) | A LINQ `Where` predicate can move into the terminal call. |
| [PSH1102](rules/PSH1102.md) | A LINQ type check followed by `Cast<T>` can use one typed filter. |
| [PSH1103](rules/PSH1103.md) | Prefer the collection's own count over enumerating. |
| [PSH1104](rules/PSH1104.md) | Use `TryGetValue` instead of `ContainsKey` followed by an indexer read. |
| [PSH1105](rules/PSH1105.md) | Avoid double lookups on dictionaries and sets. |
| [PSH1106](rules/PSH1106.md) | Index collections directly instead of using LINQ element access. |
| [PSH1107](rules/PSH1107.md) | Filter sequences before sorting them. |
| [PSH1108](rules/PSH1108.md) | Chain secondary sorts with `ThenBy`. |
| [PSH1109](rules/PSH1109.md) | Merge consecutive `Where` calls. |
| [PSH1110](rules/PSH1110.md) | Use the collection's own predicate methods over LINQ. |
| [PSH1111](rules/PSH1111.md) | Use `Contains` for membership tests. |

## Strings

| Rule | Description |
| --- | --- |
| [PSH1200](rules/PSH1200.md) | Compare strings without allocating case-converted copies. |
| [PSH1201](rules/PSH1201.md) | Use the char overload for single-character strings. |
| [PSH1202](rules/PSH1202.md) | Append characters as char, not single-character strings. |
| [PSH1203](rules/PSH1203.md) | Let `StringBuilder` do the formatting work. |
| [PSH1204](rules/PSH1204.md) | Test for empty strings by length. |
| [PSH1205](rules/PSH1205.md) | Remove interpolation that does no work. |
| [PSH1206](rules/PSH1206.md) | Do not build strings by concatenation in loops. |
| [PSH1207](rules/PSH1207.md) | Specify `StringComparison` for culture-sensitive string operations. |

## Concurrency (PerformanceSharp)

| Rule | Description |
| --- | --- |
| [PSH1300](rules/PSH1300.md) | A dedicated object lock field should be a `System.Threading.Lock`. |
| [PSH1301](rules/PSH1301.md) | Do not wrap a single task in `WhenAll` or `WaitAll`. |

## ApiSelection

| Rule | Description |
| --- | --- |
| [PSH1400](rules/PSH1400.md) | Use the static `HashData` method for one-shot hashing. |
| [PSH1401](rules/PSH1401.md) | Attribute types should be sealed. |
| [PSH1402](rules/PSH1402.md) | Use `const` for compile-time constants. |
| [PSH1403](rules/PSH1403.md) | Do not initialize fields to their default value. |
| [PSH1404](rules/PSH1404.md) | Get the assembly from `typeof` instead of a stack walk. |
| [PSH1405](rules/PSH1405.md) | Use the direct `Environment` APIs. |
| [PSH1406](rules/PSH1406.md) | Ask `Regex` for the answer directly. |
| [PSH1407](rules/PSH1407.md) | Query the dictionary, not its `Keys` view. |

# StyleSharp Rule Index

Style, layout, naming, documentation, and readability rules. Perf-motivated rules
that previously lived here (SST1434, SST1900, SST2229, SST2230, SST2233) moved to
PerformanceSharp as PSH1002, PSH1300, PSH1101, PSH1102, and PSH1100.

## Concurrency

| Rule | Description |
| --- | --- |
| [SST1901](rules/SST1901.md) | A lock targets a field or property reachable from outside the declaring type. |
| [SST1902](rules/SST1902.md) | A lock targets `this`, a `Type`, or a string. Opt-in. |
| [SST1903](rules/SST1903.md) | A lock targets a newly created object. |

## Documentation

| Rule | Description |
| --- | --- |
| [SST1600](rules/SST1600.md) | Externally visible members should be documented. |
| [SST1601](rules/SST1601.md) | Partial elements should be documented. |
| [SST1602](rules/SST1602.md) | Enumeration members should be documented. |
| [SST1604](rules/SST1604.md) | Element documentation should contain a summary. |
| [SST1605](rules/SST1605.md) | Partial element documentation should have a summary. Opt-in. |
| [SST1606](rules/SST1606.md) | The summary should have text. |
| [SST1607](rules/SST1607.md) | Partial element summary should have text. Opt-in. |
| [SST1608](rules/SST1608.md) | Documentation should not use the default placeholder summary. |
| [SST1609](rules/SST1609.md) | Property documentation should have a value element. Opt-in. |
| [SST1610](rules/SST1610.md) | Property value documentation should have text. Opt-in. |
| [SST1611](rules/SST1611.md) | Parameters should be documented. |
| [SST1612](rules/SST1612.md) | Parameter documentation should match the parameters. |
| [SST1613](rules/SST1613.md) | Parameter documentation should declare a name. |
| [SST1614](rules/SST1614.md) | Parameter documentation should have text. |
| [SST1615](rules/SST1615.md) | The return value should be documented. |
| [SST1616](rules/SST1616.md) | Return value documentation should have text. |
| [SST1617](rules/SST1617.md) | A void return value should not be documented. |
| [SST1618](rules/SST1618.md) | Generic type parameters should be documented. |
| [SST1619](rules/SST1619.md) | Generic type parameters of a partial type should be documented. Opt-in. |
| [SST1620](rules/SST1620.md) | Type parameter documentation should match the type parameters. |
| [SST1621](rules/SST1621.md) | Type parameter documentation should declare a name. |
| [SST1622](rules/SST1622.md) | Type parameter documentation should have text. |
| [SST1623](rules/SST1623.md) | Property summaries should describe their accessors. |
| [SST1624](rules/SST1624.md) | A property summary mentions a restricted setter. Opt-in. |
| [SST1625](rules/SST1625.md) | Element documentation should not be copy-pasted. |
| [SST1626](rules/SST1626.md) | A documentation-style comment is used where it does not document an element. |
| [SST1627](rules/SST1627.md) | A documentation section contains no text. Opt-in. |
| [SST1628](rules/SST1628.md) | Documentation text should begin with a capital letter. Opt-in. |
| [SST1629](rules/SST1629.md) | Documentation text should end with a period. |
| [SST1630](rules/SST1630.md) | Documentation text should contain whitespace between words. Opt-in. |
| [SST1631](rules/SST1631.md) | Documentation text should be mostly letters. Opt-in. |
| [SST1632](rules/SST1632.md) | Documentation text should meet a minimum length. Opt-in. |
| [SST1633](rules/SST1633.md) | Files should begin with the configured header. |
| [SST1642](rules/SST1642.md) | Constructor summaries should begin with the standard text. |
| [SST1643](rules/SST1643.md) | Destructor summaries should begin with the standard text. |
| [SST1644](rules/SST1644.md) | A documentation comment contains an interior blank line. Opt-in. |
| [SST1648](rules/SST1648.md) | `inheritdoc` should only be used on inheriting elements. |
| [SST1649](rules/SST1649.md) | The file name should match the first type name. |
| [SST1651](rules/SST1651.md) | Placeholder documentation elements should be removed. |
| [SST1653](rules/SST1653.md) | Keep short documentation summaries on a single line. |
| [SST1654](rules/SST1654.md) | Extension blocks should be documented with a summary. |
| [SST1655](rules/SST1655.md) | Extension block parameters should be documented. |
| [SST1656](rules/SST1656.md) | Extension block type parameters should be documented. |
| [SST1657](rules/SST1657.md) | Extension block documentation should reference a real parameter or type parameter. |

## Extensions

| Rule | Description |
| --- | --- |
| [SST1700](rules/SST1700.md) | An extension block declares no members. |
| [SST1701](rules/SST1701.md) | Two extension blocks in a type share the same receiver type. |
| [SST1702](rules/SST1702.md) | Extension blocks in a type are separated by other members. |
| [SST1703](rules/SST1703.md) | A classic `this`-parameter extension method is used where an extension block could be. Opt-in. |
| [SST1704](rules/SST1704.md) | A class declaring extension blocks is not named with an `Extensions` suffix. |
| [SST1705](rules/SST1705.md) | A class mixes classic extension methods with extension blocks. |
| [SST1706](rules/SST1706.md) | An extension block targets a broad receiver type such as `object` or `dynamic`. |
| [SST1707](rules/SST1707.md) | Extension blocks in a type are not ordered by receiver type. Opt-in. |

## Layout

| Rule | Description |
| --- | --- |
| [SST1500](rules/SST1500.md) | A brace in a multi-line construct shares its line with other code. |
| [SST1501](rules/SST1501.md) | A statement block is collapsed onto a single line. |
| [SST1502](rules/SST1502.md) | An element body is collapsed onto a single line. |
| [SST1503](rules/SST1503.md) | A control-flow statement omits the braces around its child statement. |
| [SST1504](rules/SST1504.md) | The accessors of a property or event mix single-line and multi-line forms. |
| [SST1505](rules/SST1505.md) | An opening brace is followed by a blank line. |
| [SST1506](rules/SST1506.md) | An element documentation header is followed by a blank line. |
| [SST1507](rules/SST1507.md) | Two or more blank lines appear in a row. |
| [SST1508](rules/SST1508.md) | A closing brace is preceded by a blank line. |
| [SST1509](rules/SST1509.md) | An opening brace is preceded by a blank line. |
| [SST1510](rules/SST1510.md) | A chained block such as `else`, `catch`, or `finally` is preceded by a blank line. |
| [SST1511](rules/SST1511.md) | The `while` footer of a do/while loop is preceded by a blank line. |
| [SST1512](rules/SST1512.md) | A single-line comment is followed by a blank line. |
| [SST1513](rules/SST1513.md) | A closing brace is not followed by a blank line. |
| [SST1514](rules/SST1514.md) | An element documentation header is not preceded by a blank line. |
| [SST1515](rules/SST1515.md) | A single-line comment is not preceded by a blank line. |
| [SST1516](rules/SST1516.md) | Adjacent members or namespace elements are not separated by a blank line. |
| [SST1517](rules/SST1517.md) | The file begins with one or more blank lines. |
| [SST1518](rules/SST1518.md) | The file does not end with exactly one newline. |
| [SST1519](rules/SST1519.md) | A multi-line child statement of a control-flow keyword omits its braces. |
| [SST1520](rules/SST1520.md) | The clauses of an if/else chain use braces inconsistently. |

## Maintainability

| Rule | Description |
| --- | --- |
| [SST1400](rules/SST1400.md) | An element does not declare an access modifier. |
| [SST1401](rules/SST1401.md) | A non-private, non-constant field is exposed. |
| [SST1402](rules/SST1402.md) | A file declares more than one top-level type. |
| [SST1403](rules/SST1403.md) | A file declares more than one namespace. |
| [SST1404](rules/SST1404.md) | A code-analysis suppression has no justification. |
| [SST1405](rules/SST1405.md) | A `Debug.Assert` call provides no message. |
| [SST1406](rules/SST1406.md) | A `Debug.Fail` call provides no message. |
| [SST1407](rules/SST1407.md) | Mixed-precedence arithmetic is not parenthesized. |
| [SST1408](rules/SST1408.md) | Mixed conditional operators are not parenthesized. |
| [SST1410](rules/SST1410.md) | An anonymous method has an empty parameter list. |
| [SST1411](rules/SST1411.md) | An attribute uses an empty argument list. |
| [SST1412](rules/SST1412.md) | Files should be stored as UTF-8 with a byte order mark. Opt-in. |
| [SST1413](rules/SST1413.md) | A multi-line initializer omits the trailing comma. |
| [SST1414](rules/SST1414.md) | A tuple type in a member signature has an unnamed element. |
| [SST1415](rules/SST1415.md) | An argument-exception constructor uses a string literal where `nameof` would track renames. |
| [SST1416](rules/SST1416.md) | A public member is declared in a type that is not externally visible. Opt-in. |
| [SST1417](rules/SST1417.md) | A namespace does not match the file's folder structure. Opt-in. |
| [SST1418](rules/SST1418.md) | A binary expression is an operand of `??` without parentheses. |
| [SST1419](rules/SST1419.md) | A modifier has no effect in its declaration context. |
| [SST1420](rules/SST1420.md) | A property trivially wraps a private backing field. |
| [SST1421](rules/SST1421.md) | A property has a setter but no getter. |
| [SST1422](rules/SST1422.md) | A private field acts only as method-local temporary storage. Opt-in. |
| [SST1423](rules/SST1423.md) | A switch statement exceeds the configured section count. |
| [SST1424](rules/SST1424.md) | A private field is never assigned outside construction. Opt-in. |
| [SST1425](rules/SST1425.md) | A captured primary-constructor parameter is reassigned. |
| [SST1426](rules/SST1426.md) | A `#pragma warning disable` silences an analyzer warning that a scoped `[SuppressMessage]` should handle. |
| [SST1427](rules/SST1427.md) | A `protected` member of a sealed type has no effect, since the type cannot be derived. |
| [SST1428](rules/SST1428.md) | An abstract type declares a `public` constructor that only derived types can call. |
| [SST1429](rules/SST1429.md) | An empty `catch` of the base exception (or a bare `catch`) silently swallows every error. |
| [SST1430](rules/SST1430.md) | `throw ex;` re-throws the caught exception and discards its original stack trace. |
| [SST1431](rules/SST1431.md) | A static member of a generic type ignores the type's type parameters. |
| [SST1432](rules/SST1432.md) | A class declares only static members and could be marked `static`. Opt-in. |
| [SST1433](rules/SST1433.md) | A type's only constructor is a public, parameterless, empty default. |
| [SST1435](rules/SST1435.md) | A namespace declaration has no members. |
| [SST1436](rules/SST1436.md) | A class, struct, or record has no members. Opt-in. |
| [SST1437](rules/SST1437.md) | An interface has no members. Opt-in. |
| [SST1438](rules/SST1438.md) | A method has an empty body. Opt-in. |
| [SST1439](rules/SST1439.md) | A loop or guard statement has an empty embedded block. |
| [SST1440](rules/SST1440.md) | A private member has no local use. |
| [SST1441](rules/SST1441.md) | A private field is assigned but never read. |
| [SST1442](rules/SST1442.md) | A function has too many direct branch points. |
| [SST1443](rules/SST1443.md) | A function has too much nested control flow. |
| [SST1444](rules/SST1444.md) | A loop cannot naturally reach a second iteration. |
| [SST1450](rules/SST1450.md) | Files should be stored as UTF-8 without a byte order mark. Opt-in. |

## Modernization

| Rule | Description |
| --- | --- |
| [SST2000](rules/SST2000.md) | A null check plus throw should use `ArgumentNullException.ThrowIfNull`. |
| [SST2001](rules/SST2001.md) | An empty-string check plus throw should use `ArgumentException.ThrowIfNullOrEmpty`. Opt-in. |
| [SST2002](rules/SST2002.md) | A whitespace check plus throw should use `ArgumentException.ThrowIfNullOrWhiteSpace`. Opt-in. |
| [SST2003](rules/SST2003.md) | A disposed check should use `ObjectDisposedException.ThrowIf`. |
| [SST2004](rules/SST2004.md) | A range check should use an `ArgumentOutOfRangeException.ThrowIf...` helper. |
| [SST2005](rules/SST2005.md) | An `as` cast compared to `null` (`x as T != null`) should use the `is` type pattern. |
| [SST2006](rules/SST2006.md) | A negated type test (`!(x is T)`) should use the `is not` pattern. |
| [SST2007](rules/SST2007.md) | An `is` check followed by a cast local should use a declaration pattern. |

## Collection Expressions

| Rule | Description |
| --- | --- |
| [SST2100](rules/SST2100.md) | An empty collection creation can use `[]`. |
| [SST2101](rules/SST2101.md) | An explicit collection creation can use `[...]`. Opt-in. |
| [SST2102](rules/SST2102.md) | A span-targeted stackalloc initializer can use a collection expression. |
| [SST2103](rules/SST2103.md) | A collection-builder factory call can keep the elements at the target site. |
| [SST2104](rules/SST2104.md) | A short builder-local sequence can be returned as a collection expression. |
| [SST2105](rules/SST2105.md) | A literal array materialized with LINQ can use the target collection expression directly. |

## Modern Syntax

| Rule | Description |
| --- | --- |
| [SST2200](rules/SST2200.md) | A single-use backing field can use the C# 14 `field` keyword. Opt-in. |
| [SST2201](rules/SST2201.md) | A return-only switch statement can use a switch expression. |
| [SST2202](rules/SST2202.md) | An object creation repeats an explicit target type. |
| [SST2203](rules/SST2203.md) | An array or string index can use from-end indexing. |
| [SST2204](rules/SST2204.md) | A string slice can use range syntax. |
| [SST2205](rules/SST2205.md) | An enum switch statement omits named enum values. |
| [SST2206](rules/SST2206.md) | An enum switch expression omits named enum values. |
| [SST2207](rules/SST2207.md) | A null guard and return can keep the throw in the returned expression. |
| [SST2208](rules/SST2208.md) | An out variable can be declared at the call site. |
| [SST2209](rules/SST2209.md) | A null-forgiving operator has no local effect. |
| [SST2210](rules/SST2210.md) | A nullable directive repeats the current file-local state. |
| [SST2211](rules/SST2211.md) | A nullable restore directive has no file-local state to restore. |
| [SST2212](rules/SST2212.md) | Literal UTF-8 byte data can use a `u8` string literal. |
| [SST2213](rules/SST2213.md) | A typed pattern has an unnecessary discard designation. |
| [SST2214](rules/SST2214.md) | A tuple temporary only feeds copied element locals. |
| [SST2215](rules/SST2215.md) | A temporary local swaps two locals. |
| [SST2216](rules/SST2216.md) | A tuple element name repeats the inferred name. |
| [SST2217](rules/SST2217.md) | A manual hash-code expression can use `System.HashCode.Combine`. |
| [SST2218](rules/SST2218.md) | Lambda parameter types can be omitted when the target already supplies them. |
| [SST2219](rules/SST2219.md) | A single-expression property accessor can use an expression body. |
| [SST2220](rules/SST2220.md) | An interpolation hole can carry the value and literal format directly. |
| [SST2221](rules/SST2221.md) | An ignored expression value is assigned to the discard. Opt-in. |
| [SST2222](rules/SST2222.md) | A local value is overwritten before it is read. |
| [SST2223](rules/SST2223.md) | A null fallback assignment can use `??=`. |
| [SST2224](rules/SST2224.md) | An anonymous object can become a tuple literal for local value bundles. Opt-in. |
| [SST2225](rules/SST2225.md) | A `foreach` loop hides an explicit element cast. |
| [SST2226](rules/SST2226.md) | A cast hides an inner explicit conversion. |
| [SST2227](rules/SST2227.md) | A post-assignment null fallback can be folded into the assigned expression. |
| [SST2228](rules/SST2228.md) | A delegate local used only as a call target can be a local function. |
| [SST2231](rules/SST2231.md) | A broad `object` pattern can use a direct null pattern. |
| [SST2232](rules/SST2232.md) | `nameof` does not need concrete generic type arguments. |

## Naming

| Rule | Description |
| --- | --- |
| [SST1300](rules/SST1300.md) | Types and members should be PascalCase. |
| [SST1302](rules/SST1302.md) | Interface names should begin with `I`. |
| [SST1303](rules/SST1303.md) | Const names should be PascalCase. |
| [SST1304](rules/SST1304.md) | Non-private readonly fields should be PascalCase. |
| [SST1305](rules/SST1305.md) | Names should not use Hungarian notation. Opt-in. |
| [SST1306](rules/SST1306.md) | Field names should begin with a lower-case letter. Opt-in. |
| [SST1307](rules/SST1307.md) | Accessible fields should be PascalCase. |
| [SST1308](rules/SST1308.md) | Field names should not be prefixed with `m_` or `s_`. Opt-in. |
| [SST1309](rules/SST1309.md) | Private fields should be `_camelCase`. |
| [SST1310](rules/SST1310.md) | Field names should not contain underscores. Opt-in. |
| [SST1311](rules/SST1311.md) | Static readonly fields should be PascalCase. |
| [SST1312](rules/SST1312.md) | Local variables should be camelCase. |
| [SST1313](rules/SST1313.md) | Parameters should be camelCase. |
| [SST1314](rules/SST1314.md) | Type parameters should begin with `T`. |
| [SST1315](rules/SST1315.md) | Union member names should use the configured casing. |
| [SST1316](rules/SST1316.md) | Tuple element names should use the configured casing. |
| [SST1317](rules/SST1317.md) | Task-returning method names should end with `Async`. |
| [SST1318](rules/SST1318.md) | Overriding or implementing parameter names should match the base member. |

## Ordering

| Rule | Description |
| --- | --- |
| [SST1200](rules/SST1200.md) | Using directives should be placed outside the namespace. |
| [SST1201](rules/SST1201.md) | Members should be ordered by kind. |
| [SST1202](rules/SST1202.md) | Members should be ordered by accessibility. |
| [SST1203](rules/SST1203.md) | Constants should appear before fields. |
| [SST1204](rules/SST1204.md) | Static members should appear before instance members. |
| [SST1205](rules/SST1205.md) | Partial elements should declare an access modifier. |
| [SST1206](rules/SST1206.md) | Declaration keywords should follow the standard order. |
| [SST1207](rules/SST1207.md) | Place `protected` before `internal`. |
| [SST1208](rules/SST1208.md) | `System` using directives should appear before other usings. |
| [SST1209](rules/SST1209.md) | Using alias directives should appear after other usings. |
| [SST1210](rules/SST1210.md) | Regular using directives should be ordered alphabetically. |
| [SST1211](rules/SST1211.md) | Using alias directives should be ordered alphabetically by alias. |
| [SST1212](rules/SST1212.md) | Property accessors should be ordered with `get` first. |
| [SST1213](rules/SST1213.md) | Event accessors should be ordered with `add` first. |
| [SST1214](rules/SST1214.md) | Static readonly fields should appear before static non-readonly fields. |
| [SST1215](rules/SST1215.md) | Instance readonly fields should appear before instance non-readonly fields. |
| [SST1216](rules/SST1216.md) | Using static directives should be placed after regular usings and before aliases. |
| [SST1217](rules/SST1217.md) | Using static directives should be ordered alphabetically. |

## Readability

| Rule | Description |
| --- | --- |
| [SST1100](rules/SST1100.md) | A `base.` prefix is used where the type does not override the member. |
| [SST1102](rules/SST1102.md) | A query clause is separated from the previous clause by a blank line. |
| [SST1103](rules/SST1103.md) | Query clauses mix single-line and multi-line layout. |
| [SST1104](rules/SST1104.md) | A query clause shares the last line of a multi-line previous clause. |
| [SST1105](rules/SST1105.md) | A multi-line query clause does not begin on its own line. |
| [SST1106](rules/SST1106.md) | A statement is empty. |
| [SST1107](rules/SST1107.md) | More than one statement shares a line. |
| [SST1110](rules/SST1110.md) | An opening parenthesis or bracket does not sit on the line of the preceding code. |
| [SST1111](rules/SST1111.md) | A closing parenthesis or bracket does not sit on the last parameter's line. |
| [SST1112](rules/SST1112.md) | An empty parameter list's closing parenthesis is on a different line. |
| [SST1113](rules/SST1113.md) | A comma does not sit on the previous parameter's line. |
| [SST1114](rules/SST1114.md) | A blank line separates the declaration from its parameter list. |
| [SST1115](rules/SST1115.md) | A blank line separates a parameter from the preceding comma. |
| [SST1116](rules/SST1116.md) | A qualified name has a shorter spelling that binds to the same symbol. |
| [SST1117](rules/SST1117.md) | Instance member access does not match the configured `this.` qualification style. |
| [SST1118](rules/SST1118.md) | A parameter or argument spans multiple lines. Opt-in. |
| [SST1120](rules/SST1120.md) | A comment contains no text. |
| [SST1121](rules/SST1121.md) | A framework type name is used instead of its built-in alias. Opt-in. |
| [SST1122](rules/SST1122.md) | An empty string literal is used instead of `string.Empty`. |
| [SST1123](rules/SST1123.md) | A `#region` is placed inside a code element body. |
| [SST1124](rules/SST1124.md) | A `#region` directive is used. |
| [SST1125](rules/SST1125.md) | A `Nullable<T>` type is written in long form instead of the `T?` shorthand. |
| [SST1127](rules/SST1127.md) | A generic type constraint shares a line with the declaration or another constraint. |
| [SST1128](rules/SST1128.md) | A constructor initializer shares a line with the constructor signature. |
| [SST1129](rules/SST1129.md) | A value type is created with a parameterless constructor call instead of `default`. |
| [SST1130](rules/SST1130.md) | An anonymous delegate is used where a lambda expression is clearer. |
| [SST1131](rules/SST1131.md) | A comparison places the constant on the left. |
| [SST1132](rules/SST1132.md) | Several fields are declared in a single statement. |
| [SST1133](rules/SST1133.md) | Several attributes share one bracket list. |
| [SST1134](rules/SST1134.md) | An attribute shares a line with another attribute or the element. |
| [SST1135](rules/SST1135.md) | A using directive names a namespace or type that is not fully qualified. |
| [SST1136](rules/SST1136.md) | Several enum members share a line. |
| [SST1137](rules/SST1137.md) | Sibling elements are indented differently from one another. |
| [SST1139](rules/SST1139.md) | A numeric literal is cast where a literal suffix would express the type. |
| [SST1140](rules/SST1140.md) | A wrapped conditional operator does not start an indented continuation line. |
| [SST1141](rules/SST1141.md) | An explicit `ValueTuple<...>` is used where tuple syntax would do. |
| [SST1142](rules/SST1142.md) | A tuple element is accessed by `ItemN` where it has a name. |
| [SST1143](rules/SST1143.md) | A boolean expression is compared to a `true` or `false` literal. |
| [SST1144](rules/SST1144.md) | Stacked case labels could be combined into one `or` pattern. Opt-in. |
| [SST1145](rules/SST1145.md) | A wrapped conditional expression places `?` or `:` inconsistently. |
| [SST1146](rules/SST1146.md) | An `if` statement follows a closing brace on the same line. |
| [SST1147](rules/SST1147.md) | A conditional expression is nested inside another conditional expression. Opt-in. |
| [SST1148](rules/SST1148.md) | A regular comment appears to contain C# code. Opt-in. |
| [SST1149](rules/SST1149.md) | A null check uses `== null` or `!= null` instead of `is null` or `is not null`. |

The unique-line list family was renumbered to `SST1150`-`SST1171`. The detailed docs pages still live under the earlier `SST0001`-`SST0022` filenames, so the links below point to those existing pages.

| Rule | Description |
| --- | --- |
| [SST1150](rules/SST0001.md) | Require each constructor parameter on a unique line. |
| [SST1151](rules/SST0002.md) | Require each method parameter on a unique line. |
| [SST1152](rules/SST0003.md) | Require each delegate parameter on a unique line. |
| [SST1153](rules/SST0004.md) | Require each indexer parameter on a unique line. |
| [SST1154](rules/SST0005.md) | Require each invocation argument on a unique line. |
| [SST1155](rules/SST0006.md) | Require each object creation argument on a unique line. |
| [SST1156](rules/SST0007.md) | Require each element access argument on a unique line. |
| [SST1157](rules/SST0008.md) | Require each attribute argument on a unique line. |
| [SST1158](rules/SST0009.md) | Require each anonymous method parameter on a unique line. |
| [SST1159](rules/SST0010.md) | Require each parenthesized lambda parameter on a unique line. |
| [SST1160](rules/SST0011.md) | Require each record primary-constructor parameter on a unique line. |
| [SST1161](rules/SST0012.md) | Require each class primary-constructor parameter on a unique line. |
| [SST1162](rules/SST0013.md) | Require each struct primary-constructor parameter on a unique line. |
| [SST1163](rules/SST0014.md) | Require each target-typed object creation argument on a unique line. |
| [SST1164](rules/SST0015.md) | Require each constructor initializer argument on a unique line. |
| [SST1165](rules/SST0016.md) | Require each primary-constructor base argument on a unique line. |
| [SST1166](rules/SST0017.md) | Require each local-function parameter on a unique line. |
| [SST1167](rules/SST0018.md) | Require each operator parameter on a unique line. |
| [SST1168](rules/SST0019.md) | Require each conversion-operator parameter on a unique line. |
| [SST1169](rules/SST0020.md) | Require each type parameter on a unique line. |
| [SST1170](rules/SST0021.md) | Require each type argument on a unique line. |
| [SST1171](rules/SST0022.md) | Require each function-pointer parameter on a unique line. |
| [SST1172](rules/SST1172.md) | A comparison is negated with `!` instead of using the opposite operator. |
| [SST1173](rules/SST1173.md) | An anonymous-type member restates a name that would be inferred. |
| [SST1174](rules/SST1174.md) | A `return;` at the tail of a void member, or a `continue;` at the tail of a loop, has no effect. |
| [SST1175](rules/SST1175.md) | A cast targets the type the operand already has. |
| [SST1176](rules/SST1176.md) | A field, event, or auto-property is initialized to its type's default value. Opt-in. |
| [SST1177](rules/SST1177.md) | A base list restates a compiler-implied type (`object` base, `int` enum). |
| [SST1178](rules/SST1178.md) | A parameterless `: base()` constructor call restates the compiler default. |
| [SST1179](rules/SST1179.md) | A `default:` switch section whose only statement is `break;` adds nothing. |
| [SST1180](rules/SST1180.md) | An `else` clause has an empty body. |
| [SST1181](rules/SST1181.md) | An `override` only forwards to the same base member. |
| [SST1182](rules/SST1182.md) | A conditional expression just yields the boolean literals (`c ? true : false`). |
| [SST1183](rules/SST1183.md) | An interpolated string has no interpolations. |
| [SST1184](rules/SST1184.md) | A verbatim string literal needs no verbatim quoting. |
| [SST1185](rules/SST1185.md) | An assignment recomputes its target (`x = x + y`) instead of using a compound operator. |
| [SST1186](rules/SST1186.md) | A literal sits on the left of an equality comparison (`0 == count`). |
| [SST1187](rules/SST1187.md) | An assignment is chained as the value of another assignment (`a = b = c`). |
| [SST1188](rules/SST1188.md) | A `default(T)` is written where the bare `default` literal suffices. |
| [SST1189](rules/SST1189.md) | An assignment copies a value onto itself (`x = x`). |
| [SST1190](rules/SST1190.md) | A prefix-negation operator is applied twice (`!!x`, `~~x`). |
| [SST1191](rules/SST1191.md) | A long base-10 integer literal has no digit separators. Opt-in. |
| [SST1192](rules/SST1192.md) | A string literal embeds a raw control character. Opt-in. |
| [SST1193](rules/SST1193.md) | An object is created and then immediately assigned member values. |
| [SST1194](rules/SST1194.md) | A collection is created and then immediately populated with `Add`. |
| [SST1195](rules/SST1195.md) | A null fallback conditional expression can use `??`. |
| [SST1196](rules/SST1196.md) | A null-guarded member access can use `?.`. |
| [SST1197](rules/SST1197.md) | Adjacent return statements can use a conditional expression. |
| [SST1198](rules/SST1198.md) | Matching if/else assignments can use a conditional expression. |
| [SST1199](rules/SST1199.md) | `typeof(T).Name` can use `nameof(T)`. |

## Records

| Rule | Description |
| --- | --- |
| [SST1800](rules/SST1800.md) | A record class is neither sealed nor abstract. Opt-in. |
| [SST1801](rules/SST1801.md) | A positional record parameter does not match the configured casing. |
| [SST1802](rules/SST1802.md) | A record declares a settable instance property instead of an init-only property. |
| [SST1803](rules/SST1803.md) | A record struct is not declared readonly. |

## Spacing

| Rule | Description |
| --- | --- |
| [SST1000](rules/SST1000.md) | A control-flow keyword is not followed by a space. |
| [SST1001](rules/SST1001.md) | A comma is spaced incorrectly. |
| [SST1002](rules/SST1002.md) | A semicolon is spaced incorrectly. |
| [SST1003](rules/SST1003.md) | A binary operator is not surrounded by spaces. |
| [SST1004](rules/SST1004.md) | A documentation comment line does not begin with a single space after `///`. |
| [SST1005](rules/SST1005.md) | A single-line comment does not begin with a single space. |
| [SST1006](rules/SST1006.md) | A preprocessor keyword is preceded by a space after `#`. |
| [SST1007](rules/SST1007.md) | An operator keyword is not followed by a space. |
| [SST1008](rules/SST1008.md) | An opening parenthesis is followed by a space. |
| [SST1009](rules/SST1009.md) | A closing parenthesis is preceded by a space. |
| [SST1010](rules/SST1010.md) | An opening square bracket has adjacent whitespace. Opt-in. |
| [SST1011](rules/SST1011.md) | A closing square bracket is preceded by a space. |
| [SST1012](rules/SST1012.md) | An opening brace is not followed by a space on a single line. |
| [SST1013](rules/SST1013.md) | A closing brace is not preceded by a space on a single line. |
| [SST1014](rules/SST1014.md) | An opening generic bracket is preceded or followed by a space. |
| [SST1015](rules/SST1015.md) | A closing generic bracket is preceded by a space. |
| [SST1016](rules/SST1016.md) | An opening attribute bracket is followed by a space. |
| [SST1017](rules/SST1017.md) | A closing attribute bracket is preceded by a space. |
| [SST1018](rules/SST1018.md) | A nullable type symbol is preceded by a space. |
| [SST1019](rules/SST1019.md) | A member access symbol is surrounded by spaces. |
| [SST1020](rules/SST1020.md) | An increment or decrement symbol is separated from its operand by a space. |
| [SST1021](rules/SST1021.md) | A unary negative sign is followed by a space. |
| [SST1022](rules/SST1022.md) | A unary positive sign is followed by a space. |
| [SST1023](rules/SST1023.md) | A dereference or address-of symbol is followed by a space. Opt-in. |
| [SST1024](rules/SST1024.md) | A colon is spaced incorrectly for its context. |
| [SST1025](rules/SST1025.md) | Two or more whitespace characters appear in a row within a line. |
| [SST1026](rules/SST1026.md) | A space follows `new` or `stackalloc` in an implicit array creation. |
| [SST1027](rules/SST1027.md) | A tab character is used where the project standardizes on spaces. |
| [SST1028](rules/SST1028.md) | A line ends with trailing whitespace. |
