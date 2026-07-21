# Rule Index

This page is the full categorized rule catalog for the packages published from
this repository: [`StyleSharp.Analyzers`](#stylesharp-rule-index) (`SST####`),
[`PerformanceSharp.Analyzers`](#performancesharp-rule-index) (`PSH####`), and
[`SecuritySharp.Analyzers`](#securitysharp-rule-index) (`SES####`).

- Repository overview and installation: [`../README.md`](../README.md)
- Configuration reference: [`CONFIGURATION.md`](CONFIGURATION.md)
- Performance guidance: [`PERFORMANCE.md`](PERFORMANCE.md)
- Recommended presets: [`../recommended.editorconfig`](../recommended.editorconfig) (StyleSharp), [`../recommended-performancesharp.editorconfig`](../recommended-performancesharp.editorconfig) (PerformanceSharp), [`../recommended-securitysharp.editorconfig`](../recommended-securitysharp.editorconfig) (SecuritySharp)

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
| [PSH1008](rules/PSH1008.md) | GC.SuppressFinalize does nothing for sealed finalizer-free types. Code fix removes it. |
| [PSH1009](rules/PSH1009.md) | Bound variable-length `stackalloc` with a constant guard. |
| [PSH1010](rules/PSH1010.md) | Clear reference-typed arrays when returning them to the pool. Code fix adds `clearArray: true`. |
| [PSH1011](rules/PSH1011.md) | Pass state to callbacks through the state-taking overload. |
| [PSH1012](rules/PSH1012.md) | Compare type parameter values with `EqualityComparer<T>.Default`. Code fix rewrites the call. |
| [PSH1013](rules/PSH1013.md) | Expose constant UTF-8 data as a `ReadOnlySpan<byte>` property. Code fix converts the field. |
| [PSH1014](rules/PSH1014.md) | Declare immutable structs as `readonly`. Code fix adds the modifier. |
| [PSH1015](rules/PSH1015.md) | Avoid casting value types through `object`. Code fix casts directly. |
| [PSH1016](rules/PSH1016.md) | Test enum flags with bitwise operators instead of `Enum.HasFlag`. |
| [PSH1017](rules/PSH1017.md) | A property allocates a copy of a collection on every read. |
| [PSH1018](rules/PSH1018.md) | A hand-written array is passed to a `params` parameter. Code fix passes the elements directly. |
| [PSH1019](rules/PSH1019.md) | The range indexer on an array allocates a copy where a view would do. Code fix slices in place with `AsSpan` or `AsMemory`. |
| [PSH1020](rules/PSH1020.md) | A multidimensional array is chosen where a jagged array would index on the CLR's fast path. |
| [PSH1021](rules/PSH1021.md) | An explicit `GC.Collect` or `GC.WaitForPendingFinalizers` call forces collection the runtime tunes itself. |
| [PSH1022](rules/PSH1022.md) | A parameterless `new EventArgs()` allocates where the shared `EventArgs.Empty` singleton would serve. Code fix uses the singleton. |

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
| [PSH1112](rules/PSH1112.md) | Seed the collection through its constructor. Code fix emits `[.. source]` or a seeded constructor. |
| [PSH1113](rules/PSH1113.md) | Sort naturally with `Order()`/`OrderDescending()` instead of an identity selector. |
| [PSH1114](rules/PSH1114.md) | Freeze static lookup collections that are never mutated. Opt-in. |
| [PSH1115](rules/PSH1115.md) | Insert-if-absent should probe the dictionary once. Code fix uses `TryAdd`. |
| [PSH1116](rules/PSH1116.md) | Probe string-keyed collections with a span through `GetAlternateLookup`. |
| [PSH1117](rules/PSH1117.md) | Ask the collection whether it is empty via `IsEmpty`. Code fix rewrites the comparison. |
| [PSH1118](rules/PSH1118.md) | Take the extreme element without sorting. |
| [PSH1119](rules/PSH1119.md) | Check for elements without counting them all. |
| [PSH1120](rules/PSH1120.md) | Do not materialize a sequence just to enumerate it. |
| [PSH1122](rules/PSH1122.md) | Read a sorted set's extreme through its `Min`/`Max` property, not the LINQ extension. Code fix uses the property. |
| [PSH1124](rules/PSH1124.md) | Read a linked list's end through its `First`/`Last` property, not the LINQ extension. Code fix reads the node's `Value`. |
| [PSH1125](rules/PSH1125.md) | A lazy sequence is enumerated more than once, so whatever produced it runs again. |
| [PSH1126](rules/PSH1126.md) | An async query counts the whole result set to learn whether it has any rows. Code fix rewrites the comparison to `AnyAsync()`. |
| [PSH1127](rules/PSH1127.md) | An array is filled with its default value one element at a time. Code fix rewrites the call to `Array.Clear`. |

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
| [PSH1208](rules/PSH1208.md) | Encode constant strings with u8 literals. Code fix rewrites the call. |
| [PSH1209](rules/PSH1209.md) | Build transformed strings with `string.Create`. |
| [PSH1210](rules/PSH1210.md) | Compare UTF-8 bytes without decoding them. Code fix uses `SequenceEqual`. |
| [PSH1211](rules/PSH1211.md) | Pass values directly instead of `ToString` results. Code fix drops the call. |
| [PSH1212](rules/PSH1212.md) | Slice with `AsSpan` when the call accepts a span. Code fix renames the call. |
| [PSH1213](rules/PSH1213.md) | Probe repeated character sets through `SearchValues`. |
| [PSH1214](rules/PSH1214.md) | Append the parts, not a concatenated whole. |
| [PSH1215](rules/PSH1215.md) | Concatenate when there is no separator. |
| [PSH1216](rules/PSH1216.md) | Ask for equality, not ordering. |
| [PSH1217](rules/PSH1217.md) | Do not copy a sequence to an array just to read it. Code fix drops the copy. |
| [PSH1218](rules/PSH1218.md) | Slice with `AsSpan` instead of allocating a substring to search it. Code fix rewrites the slice. |
| [PSH1219](rules/PSH1219.md) | Ask whether a string is blank without trimming it. Code fix uses `string.IsNullOrWhiteSpace`. |
| [PSH1220](rules/PSH1220.md) | A length argument spells out the run that reaches the end anyway. Code fix drops the length argument. |
| [PSH1221](rules/PSH1221.md) | An `IndexOf` result compared to `0` scans the whole string to answer a question about position zero. Code fix rewrites it to `StartsWith`. |
| [PSH1222](rules/PSH1222.md) | A concatenation materializes its slices before copying them again into the result. Code fix concatenates the spans. |
| [PSH1223](rules/PSH1223.md) | A reused composite format string is re-parsed on every call. Code fix hoists it into a `static readonly CompositeFormat` field. |
| [PSH1224](rules/PSH1224.md) | Bytes are converted to hex by building the string twice. Code fix rewrites the pair to `Convert.ToHexString`. |
| [PSH1225](rules/PSH1225.md) | Bytes are decoded through a throwaway `char[]`. Code fix rewrites the pair to `Encoding.GetString`. |
| [PSH1226](rules/PSH1226.md) | A string's `ToCharArray()` result is only iterated, allocating a throwaway `char[]`; iterate the string directly. Code fix drops the copy. |
| [PSH1227](rules/PSH1227.md) | A cheaper equivalent exists — `string.CompareOrdinal` over `Compare(…, Ordinal)`, `Debug.Fail` over `Debug.Assert(false, …)`. Info. Code fix rewrites the call. |

## Concurrency (PerformanceSharp)

| Rule | Description |
| --- | --- |
| [PSH1300](rules/PSH1300.md) | A dedicated object lock field should be a `System.Threading.Lock`. |
| [PSH1301](rules/PSH1301.md) | Do not wrap a single task in `WhenAll` or `WaitAll`. |
| [PSH1302](rules/PSH1302.md) | TaskCompletionSource should run continuations asynchronously. Code fix supplies the flag. |
| [PSH1303](rules/PSH1303.md) | Do not block an async method with `Thread.Sleep`. Code fix awaits `Task.Delay`. |
| [PSH1304](rules/PSH1304.md) | Use `PeriodicTimer` instead of pacing a loop with `Task.Delay`. |
| [PSH1305](rules/PSH1305.md) | Enumerate a `ConcurrentDictionary` directly instead of a Keys/Values snapshot. Code fix deconstructs the pair. |
| [PSH1306](rules/PSH1306.md) | Guard one-time execution with an interlocked latch. Opt-in. |
| [PSH1307](rules/PSH1307.md) | Access interlocked fields with `Volatile`. Code fix wraps the access. |
| [PSH1308](rules/PSH1308.md) | Return the completed task instead of `Task.FromResult`. Code fix rewrites the call. |
| [PSH1309](rules/PSH1309.md) | Register cancellation callbacks without flowing the execution context. Opt-in. |
| [PSH1310](rules/PSH1310.md) | An `IAsyncDisposable` is disposed by a synchronous `using` inside an async method. Code fix inserts the `await`. |
| [PSH1311](rules/PSH1311.md) | An `async` method whose body is one tail-position await builds a state machine only to forward a task. Code fix drops `async` and returns the task. |
| [PSH1312](rules/PSH1312.md) | A `null` is returned where the declared return type is `Task` or `Task<T>`. Code fix returns a completed task. |
| [PSH1313](rules/PSH1313.md) | An `async` method calls a synchronous method that has a fitting async overload. Code fix awaits the async overload. |
| [PSH1314](rules/PSH1314.md) | A stream is read or written through the array-based `ReadAsync`/`WriteAsync` overloads. Code fix rewrites the call via `AsMemory`. |
| [PSH1315](rules/PSH1315.md) | A thread is parked on a task that is not provably complete — `Result`, `Wait()`, `GetAwaiter().GetResult()` on a `Task` or `ValueTask`. The guarded fast path and an awaiter's own `GetResult` are silent. Code fix awaits, where an `await` compiles. |
| [PSH1316](rules/PSH1316.md) | A `ValueTask` is consumed more than once - awaited across loop iterations, or through a copy - so a later consume reads a recycled pooled token. Code fix hoists the producer into the loop. |

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
| [PSH1408](rules/PSH1408.md) | Measure elapsed time with `Stopwatch.GetTimestamp`/`GetElapsedTime` instead of allocating a Stopwatch. |
| [PSH1409](rules/PSH1409.md) | Use the built-in throw helpers for argument guards. Code fix rewrites the guard, honoring helper aliases. |
| [PSH1410](rules/PSH1410.md) | Mark trivial forwarders for aggressive inlining. Opt-in. |
| [PSH1411](rules/PSH1411.md) | Seal non-public types nothing derives from so the JIT can devirtualize. Code fix adds `sealed`. |
| [PSH1412](rules/PSH1412.md) | Use `Random.Shared` instead of allocating a `Random`. Code fix takes the shared instance. |
| [PSH1413](rules/PSH1413.md) | Read the Unix epoch from `DateTime.UnixEpoch`, not a hand-built date. Code fix uses the field. |
| [PSH1414](rules/PSH1414.md) | A private or internal member never touches instance state, so it still pays for a receiver it does not use; test, benchmark, and serialization-callback members (and members of test-fixture types) are left alone because a framework needs them on an instance. Code fix for a private member adds `static` and repairs `this.`-qualified call sites. |
| [PSH1415](rules/PSH1415.md) | A local or private field is typed as an interface where only one concrete type is ever assigned. Code fix changes the declared type. |
| [PSH1416](rules/PSH1416.md) | A fresh `JsonSerializerOptions` per call throws away the serializer's per-type metadata cache. |
| [PSH1417](rules/PSH1417.md) | An expensive argument is computed for a `Debug.Assert` that release builds compile away. |
| [PSH1418](rules/PSH1418.md) | A shareable client (`HttpClient` or an Azure SDK service client) is constructed for a single call, so its pooled connections and caches die with it and every call pays the setup cost again. |
| [PSH1419](rules/PSH1419.md) | A call to the TimeZoneConverter package where the built-in `TimeZoneInfo` now resolves IANA and Windows ids cross-platform (.NET 6+). Code fix rewrites `GetTimeZoneInfo` to `TimeZoneInfo.FindSystemTimeZoneById`. |
| [PSH1420](rules/PSH1420.md) | A shareable client held in an instance field of an Azure Functions worker class is rebuilt on every invocation, leaking sockets and connections; share a static/singleton client or inject `IHttpClientFactory`. |

## AspNetCore

| Rule | Description |
| --- | --- |
| [PSH1500](rules/PSH1500.md) | A route handler returns `Results.*`; `TypedResults.*` avoids boxing the result and gives the endpoint its response metadata. Code fix rewrites the call. |
| [PSH1501](rules/PSH1501.md) | Middleware is registered in the legacy `Use(next => context => ...)` nested-delegate form, which allocates a per-request closure; the two-parameter `Use((context, next) => ...)` overload does not. |
| [PSH1502](rules/PSH1502.md) | A route handler returns a deferred `IEnumerable<T>` (an `IQueryable<T>` or an un-materialized LINQ query), so the response serializer enumerates it synchronously on the request thread. |
| [PSH1503](rules/PSH1503.md) | The legacy response-caching middleware only honors HTTP cache-control headers; output caching (.NET 7+) caches on the server under keys you control and can be invalidated. Info. |
| [PSH1505](rules/PSH1505.md) | A class implements an MVC exception filter (`IExceptionFilter`/`IAsyncExceptionFilter`); centralized error handling belongs in an `IExceptionHandler` the pipeline runs once. Info. |
| [PSH1506](rules/PSH1506.md) | The HTTP request or response body is read or written synchronously (`ReadToEnd`, `Body.Read`, `Body.Write`), which blocks a thread on Kestrel and buffers the whole payload; use the async overload. Code fix awaits it when the method is already async. |

## Blazor

| Rule | Description |
| --- | --- |
| [PSH1600](rules/PSH1600.md) | A delegate captured per iteration inside a component render loop reallocates on every render (measured ~128 B per row per render) and churns the diff; hoist it to a cached delegate or a precomputed per-item model. |
| [PSH1601](rules/PSH1601.md) | A JavaScript-interop call is issued once per loop iteration; on Interactive Server each is a separate SignalR round-trip. Batch into a single call over the collection. |
| [PSH1602](rules/PSH1602.md) | `StateHasChanged` is called unconditionally in `OnAfterRender`/`OnAfterRenderAsync`, scheduling another render every time — a runaway loop. Guard it with `firstRender` or a state flag. |
| [PSH1603](rules/PSH1603.md) | A non-delegate allocation is used as a component-parameter value inside a render loop, allocating per item and forcing the child to re-render each pass. Sibling of PSH1600. |

# StyleSharp Rule Index

Style, layout, naming, documentation, and readability rules. Ids are grouped by
the hundreds digit: `SST10xx` spacing, `SST11xx` readability, `SST12xx` ordering,
`SST13xx` naming, `SST14xx` maintainability, `SST15xx` layout, `SST16xx`
documentation, `SST17xx` extensions, `SST18xx` records, `SST19xx` concurrency,
`SST20xx` modernization, `SST21xx` collection expressions, `SST22xx` modern
syntax, `SST23xx` design — the shape of a type's public surface — `SST24xx`
correctness — code that compiles and runs but does not do what it says — `SST25xx`
testing, `SST26xx` logging, and `SST27xx` frameworks (marker-gated Blazor,
ASP.NET Core, and Windows Forms defects).

Perf-motivated rules that previously lived here (SST1434, SST1900, SST2229,
SST2230, SST2233) moved to PerformanceSharp as PSH1002, PSH1300, PSH1101,
PSH1102, and PSH1100.

## Concurrency

| Rule | Description |
| --- | --- |
| [SST1901](rules/SST1901.md) | A lock targets a field or property reachable from outside the declaring type. |
| [SST1902](rules/SST1902.md) | A lock targets `this`, a `Type`, or a string. Opt-in. |
| [SST1903](rules/SST1903.md) | A lock targets an object that is fresh on every call - inline `new`, or a local `new` that never escapes the method. |
| [SST1904](rules/SST1904.md) | A lock targets a non-readonly field, which a later assignment can swap out from under a caller. Code fix makes it readonly. |
| [SST1905](rules/SST1905.md) | An `async void` method, lambda, or local function that is not a genuine event handler. Code fix returns `Task`. |

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
| [SST1658](rules/SST1658.md) | Documentation prose repeats a word ("the the"). Code fix removes the repeat. |
| [SST1659](rules/SST1659.md) | A comment has no text at all. Code fix removes it. |
| [SST1660](rules/SST1660.md) | The `<param>` tags are not in parameter order. Code fix reorders them. Info. |
| [SST1661](rules/SST1661.md) | A snippet uses `<c>`/`<code>` mismatched to single- vs multi-line content. Code fix swaps the tag. Info. |
| [SST1662](rules/SST1662.md) | A thrown exception type has no `<exception>` documentation. Code fix adds the skeleton. Opt-in. |
| [SST1663](rules/SST1663.md) | A `//` comment before a public member reads like a summary; use `///`. Code fix converts it. Opt-in. |
| [SST1664](rules/SST1664.md) | A summary separates paragraphs with blank lines instead of `<para>`. Code fix wraps them. Opt-in. |

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
| [SST1708](rules/SST1708.md) | An extension method never uses its `this` receiver, so it need not be an extension. |
| [SST1709](rules/SST1709.md) | A method in a `*Extensions` class whose first parameter lacks `this`. Code fix converts it to an extension block. Opt-in. |

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
| [SST1521](rules/SST1521.md) | A line is longer than the configured maximum, which defaults to 120 characters. |
| [SST1522](rules/SST1522.md) | A file has more code lines than the configured maximum, which defaults to 500. |
| [SST1523](rules/SST1523.md) | A member has more code lines than the configured maximum, which defaults to 60. |
| [SST1524](rules/SST1524.md) | A switch section has more code lines than the configured maximum, which defaults to 20. |
| [SST1525](rules/SST1525.md) | A multi-statement `switch` section has no braces; the braces-on policy extends to switch sections. Code fix wraps it. |
| [SST1526](rules/SST1526.md) | A wrapped binary expression places the operator inconsistently. Configurable (`before`/`after`, default before). Opt-in. |
| [SST1527](rules/SST1527.md) | The `=>` of an expression-bodied member wraps inconsistently. Configurable. Opt-in. |
| [SST1528](rules/SST1528.md) | The `=` of a wrapped initializer wraps inconsistently. Configurable. Opt-in. |
| [SST1529](rules/SST1529.md) | A wrapped `?.`/`.` call chain places the break inconsistently. Configurable. Opt-in. |
| [SST1530](rules/SST1530.md) | A newline sits between a type declaration and its base list. Code fix pulls the base list onto the declaration line. Opt-in. |
| [SST1531](rules/SST1531.md) | A short object initializer is split across lines. Code fix collapses it when it fits. Opt-in. |
| [SST1532](rules/SST1532.md) | A file mixes line endings. Configurable (`lf`/`crlf`, default lf). Opt-in. |
| [SST1533](rules/SST1533.md) | A source file contains no code. Opt-in. |

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
| [SST1445](rules/SST1445.md) | A using directive is unnecessary. Code fix removes it. |
| [SST1446](rules/SST1446.md) | An inheritance chain is deeper than the configured maximum. Configurable depth and external counting. |
| [SST1447](rules/SST1447.md) | An equality override delegates to object's reference semantics. |
| [SST1448](rules/SST1448.md) | An argument is passed explicitly to a caller-info parameter. Code fix removes it. |
| [SST1449](rules/SST1449.md) | Code writes directly to the console. |
| [SST1450](rules/SST1450.md) | Files should be stored as UTF-8 without a byte order mark. Opt-in. |
| [SST1451](rules/SST1451.md) | A DateTime is created without a DateTimeKind. |
| [SST1452](rules/SST1452.md) | A generic type parameter is never used. |
| [SST1453](rules/SST1453.md) | A statement follows an unconditional exit and cannot run. |
| [SST1454](rules/SST1454.md) | A composite format string contains a placeholder that no argument can satisfy. |
| [SST1455](rules/SST1455.md) | A declaration is marked `unsafe` but contains no unsafe syntax. |
| [SST1456](rules/SST1456.md) | A readonly field stores a mutable source-defined struct. |
| [SST1457](rules/SST1457.md) | A global suppression target does not resolve to a declaration in the compilation. |
| [SST1458](rules/SST1458.md) | A global suppression target uses a legacy tilde-prefixed target string. |
| [SST1459](rules/SST1459.md) | Parentheses wrap a standalone expression in a context where grouping has no effect. |
| [SST1460](rules/SST1460.md) | A struct instance member can be marked `readonly` because it does not mutate state. |
| [SST1461](rules/SST1461.md) | A private or local-function parameter is never read. |
| [SST1462](rules/SST1462.md) | A suppression targets a diagnostic that is disabled in the active analyzer config scope. |
| [SST1463](rules/SST1463.md) | A symbol-name string literal can use `nameof`. |
| [SST1464](rules/SST1464.md) | Unwrap an `else` that follows a branch which does not fall through. |
| [SST1465](rules/SST1465.md) | Collapse an `else` block that only wraps an `if`. |
| [SST1466](rules/SST1466.md) | Remove case labels that share a section with `default`. |
| [SST1467](rules/SST1467.md) | Enumerate with `foreach` instead of driving the enumerator by hand. |
| [SST1468](rules/SST1468.md) | Boolean logic should short-circuit. |
| [SST1469](rules/SST1469.md) | Do not compare a value type to null. |
| [SST1470](rules/SST1470.md) | Remove a catch clause that only rethrows. |
| [SST1471](rules/SST1471.md) | Magic numbers should be named constants. |
| [SST1472](rules/SST1472.md) | Signatures should not declare too many parameters. |
| [SST1473](rules/SST1473.md) | Floating-point values should not be compared for exact equality. Code fix rewrites NaN tests as `IsNaN`. |
| [SST1474](rules/SST1474.md) | Identical expressions appear on both sides of an operator. |
| [SST1475](rules/SST1475.md) | A condition repeats one already tested — in a chain or `switch` (its branch cannot run), or in the `if` immediately before it. |
| [SST1476](rules/SST1476.md) | Every branch of a conditional has the same body, so the condition decides nothing. |
| [SST1477](rules/SST1477.md) | An integer division is widened to a floating-point type after it has already truncated. Code fix casts an operand. |
| [SST1478](rules/SST1478.md) | A shift count is zero, negative, or at least the operand's width. |
| [SST1479](rules/SST1479.md) | A count or length is compared against a value it can never take. Code fix folds the constant. |
| [SST1480](rules/SST1480.md) | An exception is constructed and then discarded. Code fix adds the `throw`. |
| [SST1481](rules/SST1481.md) | A bitwise operation has a constant operand that makes it pointless. Code fix removes the operation. |
| [SST1482](rules/SST1482.md) | `GetHashCode` reads mutable state, which loses the object in any hash-based collection. |
| [SST1483](rules/SST1483.md) | A constructor calls an overridable member, so a derived override sees a half-built object. |
| [SST1484](rules/SST1484.md) | A declaration shadows a field or property of an enclosing scope. |
| [SST1485](rules/SST1485.md) | A member callers cannot defend against — `Equals`, `Dispose`, an operator — throws. |
| [SST1486](rules/SST1486.md) | The same string literal is repeated instead of being named once. |
| [SST1487](rules/SST1487.md) | A collection element is assigned twice with nothing reading it in between. |
| [SST1488](rules/SST1488.md) | An exception type does not declare the standard constructors. Code fix adds them, documented. |
| [SST1489](rules/SST1489.md) | An exception type carries formatter-based serialization members the target framework has obsoleted. Code fix removes them. |
| [SST1490](rules/SST1490.md) | A base list names an interface the rest of the list already implies. Code fix removes the entry. |
| [SST1491](rules/SST1491.md) | A modifier restates the declaration's default. Code fix removes the modifier. |
| [SST1492](rules/SST1492.md) | A value is tested against what it is then assigned, so the guard decides nothing. Code fix keeps the assignment. |
| [SST1493](rules/SST1493.md) | A method's whole body is a constant. Code fix exposes it as a get-only property. |
| [SST1494](rules/SST1494.md) | A trailing argument repeats the parameter's default. Code fix drops it, and the ones after it. |
| [SST1495](rules/SST1495.md) | `==` compares references on a type that overrides `Equals`, so the two disagree. Code fix calls `object.Equals`. |
| [SST1496](rules/SST1496.md) | An abstract type declares nothing abstract, so it asks nothing of its derived types. Code fix makes it concrete. |
| [SST1497](rules/SST1497.md) | A local is declared and never read. Code fix removes the variable and keeps what computing it did. |
| [SST1498](rules/SST1498.md) | Only a nested type uses a private member, so it is declared further out than it needs to be. Code fix moves a static method into the nested type. |
| [SST1499](rules/SST1499.md) | A static field visible outside its type can still be changed — it is global mutable state. Code fix adds `readonly` when that is all it takes. |

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
| [SST2008](rules/SST2008.md) | A negated pattern test should use an `is not` pattern. |
| [SST2009](rules/SST2009.md) | A catch block tests a condition and rethrows on the losing branch, which is an exception filter written by hand. Code fix moves the condition into a `when` clause. |
| [SST2010](rules/SST2010.md) | A type reads the machine clock directly instead of through a `TimeProvider`. Opt-in. |
| [SST2011](rules/SST2011.md) | An instant is recorded from the local clock. Code fix rewrites `.Now` to `.UtcNow`. |
| [SST2012](rules/SST2012.md) | A GUID is constructed with the parameterless constructor. Code fix uses `Guid.Empty`. |
| [SST2013](rules/SST2013.md) | An `if` whose entire body is another `if`, with no `else` on either. Code fix merges the conditions. |
| [SST2014](rules/SST2014.md) | A `goto` jumps to a label. |
| [SST2015](rules/SST2015.md) | A `++` or `--` is buried inside a larger expression, so its side effect happens in the middle of something else. |
| [SST2016](rules/SST2016.md) | A `DateTime` is the type of an externally visible field, property, parameter or return type, so the offset is lost at the boundary. |
| [SST2017](rules/SST2017.md) | A `.Date` or `.TimeOfDay` read proves the value is only a date, or only a time of day: use `DateOnly` / `TimeOnly`. |
| [SST2018](rules/SST2018.md) | A null check sits beside an `is` type pattern that already excludes null. Code fix removes the null check. |

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
| [SST2234](rules/SST2234.md) | `Nullable<T>` should use the `T?` shorthand. Code fix rewrites it. |
| [SST2235](rules/SST2235.md) | A capture-free local function can be declared `static`. |
| [SST2236](rules/SST2236.md) | A tail-position using block can use a using declaration. |
| [SST2237](rules/SST2237.md) | A single block-scoped namespace can use file-scoped syntax. |
| [SST2238](rules/SST2238.md) | A nested property pattern can use extended property-pattern syntax. |
| [SST2239](rules/SST2239.md) | A lambda that only forwards to one method can use a method group. |
| [SST2240](rules/SST2240.md) | A delegate null check followed by invocation can use conditional invocation. |
| [SST2241](rules/SST2241.md) | A constructor that only stores its parameters can use primary-constructor storage. Code fix moves the parameters and member initializers. |
| [SST2242](rules/SST2242.md) | An enum switch statement mapping should name every enum value or include a catch-all. |
| [SST2243](rules/SST2243.md) | A verbatim string literal is full of doubled-quote escapes, or spans lines. Code fix rewrites it as a raw string literal. |
| [SST2244](rules/SST2244.md) | A numeric literal's suffix is lower case. Code fix upper-cases the suffix, leaving the digits alone. |
| [SST2245](rules/SST2245.md) | A `for` loop with only a condition should be a `while` loop. Code fix rewrites it. |
| [SST2246](rules/SST2246.md) | A chain of `?:` expressions that tests one value against constants can be a switch expression. Code fix rewrites it. |
| [SST2247](rules/SST2247.md) | Consecutive locals that copy one tuple- or `Deconstruct`-able value's members in order should be a deconstruction. Code fix folds them into `var (a, b) = source;`. |
| [SST2248](rules/SST2248.md) | Two comparisons of the same value against constants can fold into one `is`-pattern. Code fix rewrites them. |
| [SST2249](rules/SST2249.md) | A `string.Format` call with a literal format, or a concatenation of literals with values, reads more clearly as an interpolated string. Code fix rewrites it; a call passing an explicit format provider is left alone so its culture is not dropped. |
| [SST2250](rules/SST2250.md) | A bare local declared without a value and assigned once by the next straight-line statement can be joined into an initialized declaration. Code fix joins them. |
| [SST2251](rules/SST2251.md) | A method call names type arguments that inference would supply. Code fix removes them. |
| [SST2252](rules/SST2252.md) | A `switch` statement nested inside another `switch` statement's section; lift it into a method, a `switch` expression, or a lookup. |
| [SST2254](rules/SST2254.md) | A target-typed `new()` is written where an explicit type reads more clearly; the code fix restores `new TypeName(...)`. Opt-in — the counterpart to SST2202's target-typed direction, so a team enables at most one. |
| [SST2255](rules/SST2255.md) | A hand-written null-or-empty string test. Code fix uses `string.IsNullOrEmpty`. |
| [SST2256](rules/SST2256.md) | An extension method called in static form. Code fix rewrites to instance form. Info. |
| [SST2257](rules/SST2257.md) | A lambda block body that is a single `return`. Code fix uses an expression body. Info. |
| [SST2258](rules/SST2258.md) | A redundant explicit delegate wrapper (`new EventHandler(M)`). Code fix drops it. Info. |
| [SST2259](rules/SST2259.md) | A stray `;` after a type declaration. Code fix removes it. Info. |
| [SST2260](rules/SST2260.md) | An `as` cast to a type the operand already has. Code fix removes it. Info. |
| [SST2261](rules/SST2261.md) | `(x && !y) || (!x && y)` reimplements exclusive-or. Code fix uses `^` when the operands are side-effect-free. Info. |
| [SST2262](rules/SST2262.md) | A raw string literal whose content needs no raw syntax. Code fix demotes it. Info. |
| [SST2263](rules/SST2263.md) | An infinite loop whose body re-derives its stop condition. Code fix hoists the condition into the header. Info. |
| [SST2264](rules/SST2264.md) | A numeric literal cast to an enum. Code fix names the member. |
| [SST2265](rules/SST2265.md) | Consecutive fluent calls on one receiver can fold into a chain. Opt-in. |
| [SST2266](rules/SST2266.md) | A local read exactly once can be inlined into that use. Opt-in. |
| [SST2267](rules/SST2267.md) | Infinite loops written in mixed `while(true)`/`for(;;)` styles. Configurable. Opt-in. |
| [SST2268](rules/SST2268.md) | Inconsistent `()` on object creation with an initializer. Configurable. Opt-in. |
| [SST2269](rules/SST2269.md) | Inconsistent parentheses around a conditional's condition. Configurable. Opt-in. |
| [SST2270](rules/SST2270.md) | Inconsistent explicit-vs-implicit array-creation type. Configurable. Opt-in. |
| [SST2271](rules/SST2271.md) | `var`-vs-explicit local type per the configured preference. Configurable. Opt-in. |
| [SST2272](rules/SST2272.md) | `[Flags]` member values written as mixed decimals and shifts. Configurable. Opt-in. |

## Design

The shape of a type's public surface: interface contracts, operator and event
conventions, and what a member exposes.

| Rule | Description |
| --- | --- |
| [SST2300](rules/SST2300.md) | A class implements `IDisposable` but builds only half of the disposal pattern. Code fix adds the two mechanical clauses. |
| [SST2301](rules/SST2301.md) | A class implements `IEquatable<T>` for itself and can still be derived from. Code fix seals the type. |
| [SST2302](rules/SST2302.md) | A type overloads an operator without the rest of the set that operator belongs to. |
| [SST2303](rules/SST2303.md) | An enum is marked `[Flags]` but its members are not distinct bit values. |
| [SST2304](rules/SST2304.md) | An event's delegate does not have the standard `void (object sender, TEventArgs e)` shape. |
| [SST2305](rules/SST2305.md) | A property whose type is a mutable collection declares a caller-visible setter. Code fix removes the setter. |
| [SST2306](rules/SST2306.md) | A member whose declared return type is a collection hands back `null`. Code fix returns the empty collection. |
| [SST2307](rules/SST2307.md) | A generic method's type parameter appears in no parameter, so no caller can infer it and every call site names it. |
| [SST2308](rules/SST2308.md) | An `[Obsolete]` attribute carries no message, or one that is empty or only whitespace. |
| [SST2309](rules/SST2309.md) | An externally visible member declares an optional parameter, so every caller that omits it compiles the default into itself. |
| [SST2310](rules/SST2310.md) | Deprecated code is still here. A standing reminder to remove it once its last caller is gone. |
| [SST2311](rules/SST2311.md) | A visible `const` is copied into every assembly that reads it, so changing its value never reaches an already-compiled caller. |
| [SST2312](rules/SST2312.md) | A type is declared outside any namespace. |
| [SST2313](rules/SST2313.md) | An enum is stored as a type the project does not allow. Configurable; defaults to `int`. |
| [SST2314](rules/SST2314.md) | An `[Obsolete]` explains itself but carries no `DiagnosticId`, so every caller gets the same CS0618. .NET 5+ only. |
| [SST2315](rules/SST2315.md) | A type creates and keeps a disposable but is not `IDisposable` - a static factory field, an auto-property `new`, or a collection of disposables. Code fix implements it. |
| [SST2316](rules/SST2316.md) | A type declares a public `Dispose`/`DisposeAsync` but not the matching interface, so owners that dispose through the interface never call it. `ref struct` exempt. Code fix adds the interface. |
| [SST2317](rules/SST2317.md) | A disposable owns a raw native handle with no finalizer, so it leaks when `Dispose` is not called. The message promotes a `SafeHandle`. |
| [SST2318](rules/SST2318.md) | Two methods in one type have token-identical, non-trivial bodies, usually a copy-paste that was meant to differ. Off by default. |
| [SST2319](rules/SST2319.md) | An optional parameter's default can never bind because a same-named overload already takes exactly its required prefix. |
| [SST2320](rules/SST2320.md) | An interface inherits the same member from two unrelated base interfaces, so every consumer that accesses it gets an ambiguity error. |
| [SST2321](rules/SST2321.md) | A class library calls `Environment.Exit` or `Environment.FailFast`, ending the whole host process instead of throwing. |
| [SST2322](rules/SST2322.md) | A non-private instance `readonly` field holds a mutable collection, so any caller can still add, remove, or clear its items; `readonly` freezes the reference, not the contents. |
| [SST2323](rules/SST2323.md) | A non-static abstract class that extends only `object` and declares nothing but public abstract members is a stateless contract better written as an interface. |
| [SST2324](rules/SST2324.md) | A member is declared more accessible than its containing type, so the wider modifier is dead — the container caps its reach. |
| [SST2325](rules/SST2325.md) | An async method checks an argument after its first await, so the guard does not throw at the call site but later, when the returned task is awaited. |
| [SST2326](rules/SST2326.md) | An interface-typed value is narrowed to a concrete class that implements it — via a cast, `as`, or `is` test — coupling the code to one implementation. Info. |
| [SST2327](rules/SST2327.md) | A type inspects its own runtime type against a specific class (`this is Derived`, `this as Derived`, or `this.GetType() == typeof(Derived)`) instead of dispatching through a virtual member. |
| [SST2328](rules/SST2328.md) | A visible instance field or property hands out a raw native pointer (`IntPtr`/`UIntPtr`/`nint`/`nuint`), letting callers read, write, free, or corrupt the native memory the type owns. Keep it private behind a `SafeHandle`. |
| [SST2329](rules/SST2329.md) | A `[Flags]` enum declares no zero-valued member. Code fix adds `None = 0`. |
| [SST2330](rules/SST2330.md) | A `[Flags]` member is a numeric literal equal to a combination of others (`All = 7`). Code fix writes `A | B | C`. Info. |
| [SST2331](rules/SST2331.md) | An enum leaves member values implicit, so their numbers depend on declaration order. Opt-in. |
| [SST2332](rules/SST2332.md) | An auto-property's `private set` is only written during construction; make it get-only. |
| [SST2333](rules/SST2333.md) | A generic comparison/equality contract is implemented without its non-generic counterpart. Opt-in. |
| [SST2334](rules/SST2334.md) | A publicly visible type has no `[DebuggerDisplay]`. Opt-in. |
| [SST2335](rules/SST2335.md) | Parts of a partial type disagree on the `static` modifier. Opt-in. |

## Correctness

Code that compiles and runs but does not do what it says.

| Rule | Description |
| --- | --- |
| [SST2400](rules/SST2400.md) | Two arguments name each other's parameters, so they have been transposed. Code fix puts them back in the parameters' order. |
| [SST2401](rules/SST2401.md) | A `catch` targets `NullReferenceException`, by naming it or by reaching it through a filter. |
| [SST2402](rules/SST2402.md) | An instance constructor assigns a static field of its own type, so the last instance built wins. |
| [SST2403](rules/SST2403.md) | `this` escapes a constructor — passed as an argument, stored somewhere that outlives the object, or captured in a closure handed to somebody else. |
| [SST2404](rules/SST2404.md) | An iterator's argument guard does not run until the first `MoveNext`. Code fix splits the validating method from the private iterator. |
| [SST2405](rules/SST2405.md) | A `[DebuggerDisplay]` string names a member the type neither declares nor inherits. |
| [SST2406](rules/SST2406.md) | A `while` or `for` condition reads only variables that nothing in the loop ever writes. |
| [SST2407](rules/SST2407.md) | A field-like event is declared, and nothing in the compilation ever raises it. |
| [SST2408](rules/SST2408.md) | A local `StringBuilder` is appended to, and its contents are never read. |
| [SST2409](rules/SST2409.md) | A `throw` constructs `Exception`, `SystemException`, or `ApplicationException`, which callers cannot catch selectively. |
| [SST2410](rules/SST2410.md) | A local is handed a newly created `IDisposable` and never disposes it, and the value never leaves the method. |
| [SST2411](rules/SST2411.md) | A `for` loop declares and tests a counter it never advances, so the loop runs forever or not at all. |
| [SST2412](rules/SST2412.md) | A `for` loop steps its counter away from the side of its bound. Code fix flips the comparison. |
| [SST2413](rules/SST2413.md) | A `for` loop's condition is already false at the counter's constant starting value, so its body never runs. |
| [SST2414](rules/SST2414.md) | Two branches of one conditional share an implementation, so one was probably meant to differ. Code fix merges duplicated switch sections. |
| [SST2415](rules/SST2415.md) | A non-short-circuiting `&`/`|` runs work the left operand was meant to guard. Code fix switches to `&&`/`||`. |
| [SST2416](rules/SST2416.md) | A remainder test against a non-zero value misses every negative on a signed type. Code fix promotes `IsOddInteger`, or `% 2 != 0`. |
| [SST2417](rules/SST2417.md) | An assignment is spaced like a transposed operator (`x =+ 1`). Code fix offers `x += 1` or `x = +1`. |
| [SST2418](rules/SST2418.md) | The result of an immutable value's method is discarded, so the call does nothing. |
| [SST2419](rules/SST2419.md) | A set or list operation is applied to the collection itself. |
| [SST2420](rules/SST2420.md) | An index-of result tested with `> 0` treats a match at the first position as not found. Code fix uses `Contains`, or `>= 0`. |
| [SST2421](rules/SST2421.md) | A write through a `readonly` field of an unconstrained type parameter lands on a copy and is lost. |
| [SST2422](rules/SST2422.md) | A property's getter reads a different field than its setter writes. Code fix points the getter at the setter's field. |
| [SST2423](rules/SST2423.md) | A value owned by a `using` is returned out of the `using` scope, so the caller receives an already-disposed object. Code fix transfers ownership. |
| [SST2424](rules/SST2424.md) | An override declares a different parameter default than the base, so the same call means different things through the base and derived types. |
| [SST2425](rules/SST2425.md) | An override forwards to the base but drops one of its own optional arguments, so the base substitutes its default and the caller's value is lost. |
| [SST2426](rules/SST2426.md) | An override's `params` modifier disagrees with the base and is ignored, so it only misleads readers. Code fix matches the base. |
| [SST2427](rules/SST2427.md) | A derived overload takes a base type of a same-named base overload's parameter, so calls through the derived type never reach the base overload. |
| [SST2428](rules/SST2428.md) | A static field initializer reads a static field declared later, so it sees that field's default and keeps it. |
| [SST2429](rules/SST2429.md) | A `set`, `init`, `add`, or `remove` accessor never reads `value`, so the assignment or subscription is discarded. |
| [SST2430](rules/SST2430.md) | A serialization callback's signature does not match the shape the serializer invokes, so it never runs. |
| [SST2431](rules/SST2431.md) | An overridden `ToString` can return null, which breaks interpolation, concatenation, and debugger display. Code fix returns `string.Empty`. |
| [SST2432](rules/SST2432.md) | `GetType()` is called on a value that is already a `Type`, returning the reflection object's runtime type. Code fix removes the call. |
| [SST2433](rules/SST2433.md) | A caller-info parameter is followed by an ordinary parameter, so a positional argument lands in the wrong one, or it has no default. |
| [SST2434](rules/SST2434.md) | A reference-type array is widened to an array of its base type, making every element write a runtime-checked store that can throw. |
| [SST2435](rules/SST2435.md) | A base class's value-equality `Equals` is used as an early-out fast path, so a derived override skips comparing its own fields. |
| [SST2436](rules/SST2436.md) | An instance event is raised with a null sender or null args, so every subscriber that reads them throws. Code fix passes `this` or `EventArgs.Empty`. |
| [SST2437](rules/SST2437.md) | A generic type is nested inside its own base's type arguments, which expands without end and throws `TypeLoadException` at load. |
| [SST2438](rules/SST2438.md) | A catch logs at error or critical level but never passes the caught exception, so the stack trace is lost. Code fix passes it. Level floor configurable. |
| [SST2439](rules/SST2439.md) | An exception is passed as a log message value instead of the exception argument. Code fix hoists it into the exception argument. |
| [SST2440](rules/SST2440.md) | Two log values named after the template placeholders sit in each other's slots. Code fix swaps them back. |
| [SST2441](rules/SST2441.md) | A message-template placeholder is empty, whitespace, or not a property name, so its value is dropped from the payload. |
| [SST2442](rules/SST2442.md) | A message template names the same placeholder twice, so one value silently overwrites the other in a structured sink. |
| [SST2443](rules/SST2443.md) | A typed logger's category is a type other than the one that logs, so its level filters and sink routes do nothing. Code fix rewrites the category. |
| [SST2444](rules/SST2444.md) | A constant regular-expression pattern does not parse, so it throws on first use. Refactoring converts a valid literal to a source-generated `[GeneratedRegex]` method. |
| [SST2445](rules/SST2445.md) | A custom date/time format uses an unquoted `/` or `:` with a culture-sensitive provider, so the separators change with the culture. Code fixes quote the separators or switch to the invariant culture. |
| [SST2446](rules/SST2446.md) | A stream read's returned byte count is awaited and discarded through a configured awaiter or a local, so a short read passes unnoticed. Code fix rewrites to `ReadExactlyAsync` where it exists. |
| [SST2448](rules/SST2448.md) | A combined or opaque delegate is removed with `-`/`-=`, which strips handlers only as one contiguous run, so the order they were combined in silently decides the result. |
| [SST2449](rules/SST2449.md) | An event or delegate handler added as a lambda or anonymous method is removed with `-=`, which never matches it, so the subscription is never removed. |
| [SST2450](rules/SST2450.md) | A `Debug.Assert` condition performs a side effect, so a release build compiles the call out and the work never runs. |
| [SST2451](rules/SST2451.md) | Every constructor of a non-static, non-abstract class is private, yet no member ever creates an instance, so the type can never exist. |
| [SST2452](rules/SST2452.md) | A method marked `[Pure]` returns `void`, a bare `Task`, or a bare `ValueTask`, so it has no observable result — the attribute is wrong or the method is dead. Code fix removes the attribute. |
| [SST2456](rules/SST2456.md) | A field-like event declared `override`, or `new` hiding an inherited event, gets its own backing delegate field, so handlers added through one type are invisible to raises through the other. |
| [SST2457](rules/SST2457.md) | An integer sequence `Sum` is wrapped in `unchecked`, which does not stop it throwing on overflow. |
| [SST2458](rules/SST2458.md) | A bitwise operator is applied to an enum not declared `[Flags]`, producing a value with no defined meaning. |
| [SST2459](rules/SST2459.md) | `[Optional]` on a `ref` or `out` parameter advertises an optionality no C# caller can use, while reflection reads `IsOptional` as true. Code fix removes the attribute. |
| [SST2460](rules/SST2460.md) | `[DefaultValue]` on a method or record parameter is inert: it does not make the parameter optional and no call site reads it. Code fix swaps it for the interop `[DefaultParameterValue]`. |
| [SST2462](rules/SST2462.md) | A member declared with `new` is less accessible than the inherited member it hides, so a base-typed reference still binds to the more accessible member and the reduced accessibility has no effect. |
| [SST2463](rules/SST2463.md) | A derived type's instance field differs from an inherited accessible field only by case, so an unqualified reference to either name compiles and silently uses the wrong storage. |
| [SST2464](rules/SST2464.md) | A mutable class (a settable field or property) declares a value-equality `operator ==`, so a mutated instance's hash no longer matches the bucket it was stored in and it is lost as a dictionary or hash-set key. |
| [SST2465](rules/SST2465.md) | A for loop's body reassigns the counter or the local its condition tests, so the loop runs a different number of times than its header states. |
| [SST2467](rules/SST2467.md) | A type declares a `params` overload and a same-arity overload whose last parameter is more specific than the array's element type, so a single argument of that type silently binds to the specific overload instead of the params one. |
| [SST2468](rules/SST2468.md) | A classic partial method is declared but never implemented, so the compiler silently removes the declaration and every call to it. |
| [SST2470](rules/SST2470.md) | Two string literals concatenate with no space between them, fusing a SQL keyword into the adjacent token so the query changes at runtime. Code fix adds a space to a regular right literal. |
| [SST2472](rules/SST2472.md) | A type is exported for a contract (`[Export(typeof(IFoo))]`) it neither implements nor inherits, so the container cannot supply it for that contract. |
| [SST2473](rules/SST2473.md) | A `new` expression constructs a type that is itself a shared export part, bypassing the container and its single-instance guarantee. |
| [SST2474](rules/SST2474.md) | A part-creation-policy attribute is applied to a type with no `[Export]`, so it governs nothing. |
| [SST2475](rules/SST2475.md) | An entity's primary key is typed `DateTime` or `DateTimeOffset`, so keys collide within a tick, are not stable identifiers, cluster the table by insertion time, and round-trip imprecisely across providers. |
| [SST2479](rules/SST2479.md) | A for/while/do loop variable captured by a lambda, anonymous method, or local function that is stored beyond the iteration reads its final value on every deferred call. |
| [SST2481](rules/SST2481.md) | A `GetHashCode` override folds the base object identity hash into a value hash, so two value-equal instances hash differently and are lost in any hash-based collection. |
| [SST2484](rules/SST2484.md) | A raw handle read through `SafeHandle.DangerousGetHandle()` is not reference-counted, so a concurrent dispose or finalize can recycle the value and it is used after free. |
| [SST2485](rules/SST2485.md) | A member throws `new NotImplementedException`, a stub that compiles but crashes at runtime on any path that reaches it. `NotSupportedException` is left alone. |
| [SST2486](rules/SST2486.md) | An assembly is loaded through `Assembly.LoadFrom`, `LoadFile`, or `LoadWithPartialName` instead of `Assembly.Load` with a full display name; a code fix swaps `LoadWithPartialName` to `Assembly.Load`. |
| [SST2487](rules/SST2487.md) | A `[ConstructorArgument]` names no parameter of any constructor of its declaring type, so a markup extension cannot round-trip the property back to a constructor argument. |
| [SST2488](rules/SST2488.md) | A catch logs the caught exception and then rethrows it with a bare `throw;`, so the same failure is recorded here and again where it is finally handled. |
| [SST2489](rules/SST2489.md) | A relational comparison an integer operand's type already decides — an unsigned value `>= 0` (always true) or `< 0` (always false), or a value at its type's min/max edge such as `b <= 255` for a `byte`. |
| [SST2490](rules/SST2490.md) | Two adjacent `try` statements in the same block repeat the same catch/finally handling, so the pair can collapse into one `try` wrapping both bodies. |
| [SST2491](rules/SST2491.md) | A non-`async` method returns an awaitable from inside `using`/`try-finally`/`lock`, so the resource is torn down before the task completes. Code fix makes it `async`. |
| [SST2492](rules/SST2492.md) | A null-guard throws on a parameter the signature declares may be null. |
| [SST2493](rules/SST2493.md) | `== null`/`!= null` on an unconstrained generic `T`. Code fix uses `is null`/`is not null`. |
| [SST2494](rules/SST2494.md) | A `??` whose left operand is a constant null, so the right is always taken. Code fix folds it. |
| [SST2495](rules/SST2495.md) | A `[Flags]` combination includes an operand whose bits another already covers. Code fix removes it. |
| [SST2496](rules/SST2496.md) | An explicit `Dispose`/`Close` on a resource an enclosing `using` already disposes. Code fix removes it. Info. |

## Testing

| Rule | Description |
| --- | --- |
| [SST2500](rules/SST2500.md) | A test method carrying a test attribute contains no assertion and no expected-exception check, so it always passes without verifying anything. Reported only when every call in the body resolves to a non-verifying platform (BCL) API (or there are none); any user or third-party call keeps it silent. |
| [SST2501](rules/SST2501.md) | An equality or identity assertion compares an expression with itself, so a positive assertion always passes and a negated one always fails, verifying nothing. Covers xUnit, NUnit (classic and `Assert.That`), and MSTest. |
| [SST2502](rules/SST2502.md) | An equality assertion is passed a constant as its actual argument and a computed value as its expected, so a failure reports them the wrong way round. Code fix swaps the two arguments. |
| [SST2503](rules/SST2503.md) | An equality assertion compares a value against a boolean literal (`Assert.Equal(true, x)` / `Assert.AreEqual(true, x)`), obscuring intent and giving a worse failure message. Code fix rewrites it to the framework's boolean assertion. |
| [SST2504](rules/SST2504.md) | A concrete class marked as a test fixture (MSTest test-class or NUnit test-fixture) declares no test method of its own and inherits none, so the runner loads it but never runs anything. |
| [SST2505](rules/SST2505.md) | A test method declares parameters but no data source, so the runner cannot supply arguments and the test silently never runs. |
| [SST2506](rules/SST2506.md) | A test method calls `Thread.Sleep`, spending a fixed real-time delay on every run that slows the suite and races the wall clock, a classic flaky-test source. |
| [SST2507](rules/SST2507.md) | A test method declares its expected failure with an expected-exception attribute instead of asserting the specific operation, so any statement in the whole method throwing that type passes the test. |
| [SST2508](rules/SST2508.md) | A fluent assertion names its subject with a bare `Should()` statement but chains no check, so it compiles, runs, and passes while verifying nothing. Gated on FluentAssertions/AwesomeAssertions. |
| [SST2509](rules/SST2509.md) | A method carrying a test attribute has a signature the runner cannot execute — non-public, a parameterless generic, or a return type other than `void`/`Task`/`ValueTask` — so it is discovered and then silently skipped. |

## Logging

Legacy tracing in place of structured logging.

| Rule | Description |
| --- | --- |
| [SST2600](rules/SST2600.md) | Application output is written through `Trace.Write`/`WriteLine`/`WriteIf`/`WriteLineIf` when a structured logger (`ILogger`) is available, so the message loses its level, category, and named state. Reported only when `ILogger` resolves; `Debug.*` is excluded. |
| [SST2601](rules/SST2601.md) | An `ILogger`/`ILogger<T>` field or property is named against the logger convention (`_logger`/`_log` for a private instance one, `Logger` otherwise). Configurable via `stylesharp.SST2601.fieldname`. |

## Frameworks

Framework-specific defects (Blazor, ASP.NET Core MVC, Windows Forms) that compile
cleanly but misbehave at runtime. Each rule is gated on the relevant framework
type, so a project that does not use the framework pays nothing.

| Rule | Description |
| --- | --- |
| [SST2700](rules/SST2700.md) | An MVC route template contains a backslash; route segments are separated by `/`, so the route is unreachable. Code fix replaces `\` with `/`. |
| [SST2701](rules/SST2701.md) | A `[JSInvokable]` method is not public, so JavaScript interop cannot call it. Code fix makes it public. |
| [SST2702](rules/SST2702.md) | A `[SupplyParameterFromQuery]` property has a type the framework cannot bind from the query string, which throws at runtime. |
| [SST2703](rules/SST2703.md) | A routable component's route constraint (`{id:int}`) disagrees with the matching `[Parameter]` CLR type, so the route silently fails to match. |
| [SST2704](rules/SST2704.md) | A public action on an `[ApiController]` declares no HTTP-verb attribute, so it answers every verb and can make routing ambiguous. |
| [SST2705](rules/SST2705.md) | A bound model member is a non-nullable value type with no required marker, so a request that omits it binds the default with no error. Opt-in. |
| [SST2706](rules/SST2706.md) | A Windows Forms entry point carries neither `[STAThread]` nor `[MTAThread]`; without STA, clipboard, drag-and-drop, and common dialogs misbehave. Code fix adds `[STAThread]`. |
| [SST2707](rules/SST2707.md) | A fire-and-forget `Task.Run` in a controller captures the request's `HttpContext`, which is disposed when the request ends, so the background work throws `ObjectDisposedException`. Opt-in. |
| [SST2708](rules/SST2708.md) | A component subscribes to an event in a lifecycle method but never unsubscribes, so the event source keeps the component alive — a per-session leak on a Server circuit. |
| [SST2709](rules/SST2709.md) | `StateHasChanged` is called while the component is being disposed, which the renderer no longer supports and throws. |
| [SST2710](rules/SST2710.md) | `StateHasChanged` is called directly from a timer callback, off the renderer's dispatcher; marshal it with `InvokeAsync(StateHasChanged)`. |
| [SST2711](rules/SST2711.md) | A synchronous component lifecycle method is overridden as `async void`, which the framework never awaits; override the `…Async` twin returning `Task`. Code fix rewrites the signature. |
| [SST2712](rules/SST2712.md) | An `[Inject]`/`[CascadingParameter]` property has no setter, so the framework's reflection-based binding leaves it null. Code fix adds a setter. |
| [SST2713](rules/SST2713.md) | A `DotNetObjectReference.Create(this)` is passed inline and never stored, so nothing can dispose it and it leaks on the JavaScript side. |

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
| [SST1319](rules/SST1319.md) | An enumeration's type name holds an underscore or an all-capitals acronym. SST1300 owns its first character. |
| [SST1320](rules/SST1320.md) | A method parameter's name is identical to its containing method's name. |
| [SST1321](rules/SST1321.md) | A method whose name ends in `Async` returns nothing awaitable — the inverse of SST1317. Code fix (rename) drops the suffix. |

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
| [SST1218](rules/SST1218.md) | Other members separate a method's overloads. Code fix moves the overload back beside its family. |
| [SST1219](rules/SST1219.md) | A `switch` statement's `default` section is not last. Code fix moves it to the end. |
| [SST1220](rules/SST1220.md) | An all-named argument list is in a different order than the parameters. Code fix reorders it to declaration order. Info. |
| [SST1221](rules/SST1221.md) | `where` constraint clauses are not ordered to match the type-parameter list. Code fix reorders them. Info. |

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
| [SST1119](rules/SST1119.md) | A numeric literal's digit separators group its digits irregularly. Code fix regroups them evenly. |
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
| [SST1138](rules/SST1138.md) | A free-standing block declares nothing and only nests its statements. Code fix splices them into the enclosing block. |
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

The unique-line list family uses `SST1150`-`SST1171`. Older `SST0001`-`SST0022`
pages are retained only as historical aliases.

| Rule | Description |
| --- | --- |
| [SST1150](rules/SST1150.md) | Require each constructor parameter on a unique line. |
| [SST1151](rules/SST1151.md) | Require each method parameter on a unique line. |
| [SST1152](rules/SST1152.md) | Require each delegate parameter on a unique line. |
| [SST1153](rules/SST1153.md) | Require each indexer parameter on a unique line. |
| [SST1154](rules/SST1154.md) | Require each invocation argument on a unique line. |
| [SST1155](rules/SST1155.md) | Require each object creation argument on a unique line. |
| [SST1156](rules/SST1156.md) | Require each element access argument on a unique line. |
| [SST1157](rules/SST1157.md) | Require each attribute argument on a unique line. |
| [SST1158](rules/SST1158.md) | Require each anonymous method parameter on a unique line. |
| [SST1159](rules/SST1159.md) | Require each parenthesized lambda parameter on a unique line. |
| [SST1160](rules/SST1160.md) | Require each record primary-constructor parameter on a unique line. |
| [SST1161](rules/SST1161.md) | Require each class primary-constructor parameter on a unique line. |
| [SST1162](rules/SST1162.md) | Require each struct primary-constructor parameter on a unique line. |
| [SST1163](rules/SST1163.md) | Require each target-typed object creation argument on a unique line. |
| [SST1164](rules/SST1164.md) | Require each constructor initializer argument on a unique line. |
| [SST1165](rules/SST1165.md) | Require each primary-constructor base argument on a unique line. |
| [SST1166](rules/SST1166.md) | Require each local-function parameter on a unique line. |
| [SST1167](rules/SST1167.md) | Require each operator parameter on a unique line. |
| [SST1168](rules/SST1168.md) | Require each conversion-operator parameter on a unique line. |
| [SST1169](rules/SST1169.md) | Require each type parameter on a unique line. |
| [SST1170](rules/SST1170.md) | Require each type argument on a unique line. |
| [SST1171](rules/SST1171.md) | Require each function-pointer parameter on a unique line. |
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
| [SST1804](rules/SST1804.md) | A positional record has an empty `{ }` body where `;` would do. Code fix rewrites it. Info. |

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

# SecuritySharp Rule Index

Rules whose primary motivation is the runtime security of the analyzed code. Ids
are grouped by the hundreds digit: `SES10xx` cryptography, `SES11xx` transport,
`SES12xx` secrets, `SES13xx` injection, `SES14xx` serialization, `SES15xx` web
hardening, `SES16xx` AI input trust boundaries.

## Cryptography

| Rule | Description |
| --- | --- |
| [SES1001](rules/SES1001.md) | AEAD encryption (`AesGcm`/`AesCcm`/`ChaCha20Poly1305`) uses a constant or reused nonce, which is catastrophic under a fixed key. |
| [SES1002](rules/SES1002.md) | Password-based key derivation (`Rfc2898DeriveBytes`/`Pbkdf2`) is given a constant or predictable salt, letting an attacker precompute rainbow tables and defeating per-secret salting. |
| [SES1003](rules/SES1003.md) | A `Rfc2898DeriveBytes.Pbkdf2` one-shot derives a key with a constant iteration count below the configured floor (default 100000), leaving offline password cracking cheap. |
| [SES1004](rules/SES1004.md) | A secret (token, key, password, nonce, salt, session id, OTP, reset token) is minted from `Guid.NewGuid()`; a GUID is an identifier, not a cryptographically strong secret. |
| [SES1005](rules/SES1005.md) | A secret (HMAC, signature, tag, token, or hash) is compared with a non-constant-time equality (`==`, `.Equals`, `SequenceEqual`), leaking it a byte at a time through timing. Code fix rewrites a byte-buffer comparison to `CryptographicOperations.FixedTimeEquals`. |
| [SES1006](rules/SES1006.md) | A Data Protection key ring is persisted to an explicit repository (`PersistKeysToFileSystem`/`DbContext`/`AzureBlobStorage`/`StackExchangeRedis`/`Registry`) with no `ProtectKeysWith...` call in the same chain, so the keys are stored unencrypted at rest. |
| [SES1007](rules/SES1007.md) | A type derives from an abstract cryptographic primitive base (`HashAlgorithm`/`KeyedHashAlgorithm`/`HMAC`/`SymmetricAlgorithm`/`AsymmetricAlgorithm`/`DeriveBytes`) and implements the algorithm by hand; use a vetted platform implementation. Subclassing a concrete algorithm to configure it is not reported. |
| [SES1008](rules/SES1008.md) | An XML signature is verified with the no-key `SignedXml.CheckSignature()` overload, which trusts the key embedded in the document's `KeyInfo`, so an attacker can re-sign tampered XML with their own key and still pass; pass a known key or certificate instead. |
| [SES1009](rules/SES1009.md) | A password is hashed with a fast general-purpose hash (`MD5`/`SHA-1`/`SHA-256`/`SHA-384`/`SHA-512`) via `HashData`/`ComputeHash` instead of a slow, salted password KDF; a fast hash is cheap to brute-force even when salted. |

## Transport

| Rule | Description |
| --- | --- |
| [SES1102](rules/SES1102.md) | A read of `HttpClientHandler.DangerousAcceptAnyServerCertificateValidator` disables TLS server-certificate validation, so the client trusts any certificate and the connection is open to man-in-the-middle attacks. |
| [SES1104](rules/SES1104.md) | X509 certificate-chain validation is deliberately weakened: `RevocationMode` set to `NoCheck`, or `VerificationFlags` set to a value naming `AllowUnknownCertificateAuthority` or `AllFlags` (alone or OR-combined), so revoked or untrusted certificates are accepted. |
| [SES1105](rules/SES1105.md) | Bearer/OpenID Connect metadata is fetched over plain HTTP because `RequireHttpsMetadata` is set to false outside a development-environment guard, exposing token validation to a network attacker. |
| [SES1106](rules/SES1106.md) | An `HttpClient` request targets a cleartext `http://` URL literal (a string overload, a `new Uri(...)` argument, or a `BaseAddress` assignment); non-loopback hosts only. |
| [SES1107](rules/SES1107.md) | A SQL connection weakens transport security: `TrustServerCertificate=true`, `Encrypt=false`, or `Encrypt=Optional` in a literal connection string or a `SqlConnectionStringBuilder`, bypassing server-certificate validation or transport encryption. |
| [SES1108](rules/SES1108.md) | A custom `HttpClientHandler.ServerCertificateCustomValidationCallback` always returns `true` (an expression/block lambda, an anonymous method, or a method group to a source method of that shape), disabling TLS server authentication so the client trusts any certificate. |

## Secrets

| Rule | Description |
| --- | --- |
| [SES1201](rules/SES1201.md) | A string literal hard-codes a recognizable credential (API key, token, private key, or connection-string password), which is committed to source and must be treated as leaked. |
| [SES1202](rules/SES1202.md) | A non-empty string literal is hard-coded where a credential is expected (a credential-named parameter or a credential-type constructor), even when its text is not a recognizable secret pattern. |
| [SES1203](rules/SES1203.md) | A database connection-string literal names a user but supplies an empty or missing password, a zero-strength credential that lets anyone who can reach the server authenticate as that account. |

## Injection

| Rule | Description |
| --- | --- |
| [SES1301](rules/SES1301.md) | A process command line is composed from a non-constant interpolated or concatenated string via `ProcessStartInfo.Arguments` (assignment or object initializer) or `Process.Start(fileName, arguments)`; use `ArgumentList` so each argument is escaped. |
| [SES1302](rules/SES1302.md) | A `ProcessStartInfo` with `UseShellExecute = true` names a non-constant `FileName` (from the initializer or the constructor argument), so the OS shell resolves a data-derived program: a command-injection and unexpected-program risk. |
| [SES1303](rules/SES1303.md) | A regular-expression pattern is built from non-constant data, letting an attacker inject regex metacharacters (alternation, catastrophic backtracking, capture rewriting); reports the pattern argument of the `Regex` constructor and the static `Regex.IsMatch`/`Match`/`Matches`/`Replace`/`Split` overloads. |
| [SES1304](rules/SES1304.md) | An archive entry name (`ZipArchiveEntry.FullName` / `TarEntry.Name`) is joined via `Path.Combine` or `+` straight into a file-writing sink with no inline containment check, letting a crafted `../` or absolute entry escape the target directory (zip slip / path traversal). |
| [SES1305](rules/SES1305.md) | An uploaded file name (`IFormFile.FileName`) is used to build a storage path -- a `Path.Combine` argument, a `+` path concatenation, or a file-creating call (`File.Create`/`OpenWrite`/`WriteAllBytes`/`Copy`, `new FileStream`) -- enabling path traversal; sanitize with `Path.GetFileName` or use a server-generated name. |
| [SES1306](rules/SES1306.md) | Non-constant C# source is compiled and executed via the scripting API (`CSharpScript.EvaluateAsync`/`RunAsync`/`Create`), which is arbitrary code execution; the code channel must be a constant, trusted template rather than runtime data. |
| [SES1307](rules/SES1307.md) | `Path.GetTempFileName()` creates a predictable, world-readable temporary file open to a time-of-check/time-of-use race and a 65535-file limit (CWE-377); use `Path.GetRandomFileName()` for an unpredictable name, or `Directory.CreateTempSubdirectory()` (.NET 7+) for an isolated directory. |
| [SES1308](rules/SES1308.md) | A file or directory is created group- or world-writable (a `UnixFileMode` including `GroupWrite`/`OtherWrite`, CWE-732), letting other local users tamper with it. |
| [SES1309](rules/SES1309.md) | An XSLT stylesheet is loaded via `XslCompiledTransform.Load` with `XsltSettings` that enable embedded script (`EnableScript = true`, a constant `enableScript` constructor argument, or `XsltSettings.TrustedXslt`), letting a stylesheet run arbitrary code in the host process (CWE-95). |
| [SES1310](rules/SES1310.md) | A `DirectoryEntry` binds to the directory without proving identity — `AuthenticationTypes.Anonymous`, or an `LDAP://` path bound with an explicitly empty/`null` username and password (CWE-287). |

## Serialization

| Rule | Description |
| --- | --- |
| [SES1401](rules/SES1401.md) | A type resolved from non-constant data via `Type.GetType` is passed inline to `Activator.CreateInstance` or a `Deserialize(Type, ...)` call, letting untrusted input choose which type is instantiated. |
| [SES1402](rules/SES1402.md) | An assembly is loaded from raw bytes (`Assembly.Load(byte[])` / `AssemblyLoadContext.LoadFromStream`) or from a non-constant `LoadFrom`/`LoadFile`/`UnsafeLoadFrom` path, running unverifiable code with full process trust. |
| [SES1403](rules/SES1403.md) | A constant `System.Text.Json` `MaxDepth` (on `JsonSerializerOptions`/`JsonReaderOptions`/`JsonDocumentOptions`) is raised above a configurable ceiling (default 64), re-opening the deep-nesting stack-exhaustion denial-of-service that the default limit guards against. |
| [SES1404](rules/SES1404.md) | A type is instantiated by name through the string overloads of `Activator.CreateInstance`/`Activator.CreateInstanceFrom` from a non-constant `typeName`, letting untrusted input choose which type is constructed (CWE-470). |
| [SES1405](rules/SES1405.md) | MessagePack typeless deserialization (`MessagePackSerializer.Typeless`, or a serializer built on `TypelessObjectResolver`/`TypelessContractlessStandardResolver`) reconstructs whatever .NET type the payload names, letting untrusted input instantiate arbitrary types (CWE-502). |
| [SES1406](rules/SES1406.md) | Reflection reaches non-public members by passing `BindingFlags.NonPublic` to `Type.GetMethod`/`GetField`/`GetMembers`/etc., bypassing the accessibility the type declares (CWE-470). Opt-in — reflecting over private members is routine in tests, serializers, and DI, so it ships disabled. |

## WebHardening

| Rule | Description |
| --- | --- |
| [SES1501](rules/SES1501.md) | A single CORS policy calls both `AllowAnyOrigin()` and `AllowCredentials()` on `CorsPolicyBuilder`; a wildcard origin combined with credentials is rejected by browsers and throws when the policy is applied. |
| [SES1502](rules/SES1502.md) | A CORS origin predicate passed to `CorsPolicyBuilder.SetIsOriginAllowed` unconditionally returns true (`_ => true`), allowing every origin -- equivalent to `AllowAnyOrigin` and dangerous with credentials. |
| [SES1503](rules/SES1503.md) | JWT signature verification is turned off on `TokenValidationParameters` because `RequireSignedTokens` or `ValidateIssuerSigningKey` is set to false, so a forged or unsigned token passes validation. |
| [SES1504](rules/SES1504.md) | A cookie initializer (`CookieOptions`/`CookieBuilder`) sets `SameSite=None` without securing the cookie in the same initializer (`Secure = true`, or a non-`None` `SecurePolicy`), so the browser drops it or it travels over plain HTTP. |
| [SES1505](rules/SES1505.md) | The request body size limit is removed -- `[DisableRequestSizeLimit]` on a controller or action, or `MaxRequestBodySize` set to null on `KestrelServerLimits`/`IHttpMaxRequestBodySizeFeature` -- letting a client stream an unbounded upload and exhaust server memory or disk. |
| [SES1506](rules/SES1506.md) | The developer exception page (`UseDeveloperExceptionPage`) is enabled without a development-environment guard, so in production it renders full exception detail and stack traces to the client. |
| [SES1507](rules/SES1507.md) | A single method or type declaration carries both `[AllowAnonymous]` and `[Authorize]`; the anonymous marker wins at runtime, so the co-located `[Authorize]` is dead and the endpoint is unauthenticated. |
| [SES1508](rules/SES1508.md) | A validation/verification method (`bool`/`Task<bool>` named `Validate`/`Verify`/`Authenticate`/`Authorize`/`Check`/`IsValid`/`IsAuthentic`/`Ensure`) fails open: a `catch` swallows a broad or security-relevant exception and returns success. |
| [SES1509](rules/SES1509.md) | A constant, backtracking-prone regular expression (an unbounded quantifier over a group that itself repeats or alternates, as in `(a+)+` or `(a|aa)+`) is compiled or run with no match timeout and without `RegexOptions.NonBacktracking`, so a crafted input can force catastrophic backtracking and hang the thread (ReDoS, CWE-1333). |
| [SES1510](rules/SES1510.md) | A controller (`ControllerBase`) redirects to a non-constant URL via `Redirect`/`RedirectPermanent`/`RedirectPreserveMethod`/`RedirectPermanentPreserveMethod`; an attacker-controlled target is an open redirect (CWE-601) to a phishing site — validate the URL is local (e.g. `LocalRedirect`). |
| [SES1511](rules/SES1511.md) | The forwarded-headers trust boundary is removed — `.Clear()` on `KnownProxies`/`KnownNetworks`/`KnownIPNetworks`, or `ForwardLimit` set to null — so untrusted proxies can spoof the client IP, host, and scheme via `X-Forwarded-*` headers (CWE-348). |
| [SES1512](rules/SES1512.md) | Sensitive framework diagnostics — EF Core `EnableSensitiveDataLogging()`, or `IdentityModelEventSource.ShowPII`/`LogCompleteSecurityArtifact = true` — are enabled without a development-environment guard, so parameter values, PII, and full tokens land in production logs (CWE-215/532). |
| [SES1513](rules/SES1513.md) | An `IAuthorizationService.AuthorizeAsync` call discards its `AuthorizationResult` (a bare await or `_ =`), so nothing reads `Succeeded` and the guarded operation runs whether or not authorization passed (CWE-863). |
| [SES1514](rules/SES1514.md) | OpenID Connect protocol protections are disabled — `UsePkce`, `RequireState`, `RequireStateValidation`, or `RequireNonce` set to false — weakening the authorization-code flow against CSRF and replay (CWE-352/294). |
| [SES1515](rules/SES1515.md) | A `Content-Security-Policy` value carries `'unsafe-inline'`, `'unsafe-eval'`, or a bare `*` source on a `default-src`/`script-src`/`style-src`/`object-src`/`base-uri` directive, re-permitting injected inline scripts and defeating the header's XSS protection (CWE-1021/79). |

## Ai

| Rule | Description |
| --- | --- |
| [SES1601](rules/SES1601.md) | An LLM system-role message (`Microsoft.Extensions.AI` `ChatMessage(ChatRole.System, ...)`, or Semantic Kernel `ChatHistory.AddSystemMessage`/`AddMessage(AuthorRole.System, ...)`/`ChatMessageContent(AuthorRole.System, ...)`) is given non-constant content; runtime or user data in the instruction channel is a prompt-injection risk. |
| [SES1602](rules/SES1602.md) | AI model output (`ChatResponse`/`ChatMessage` `.Text`) flows inline into a dangerous sink (a process start, a scripting call, a raw SQL command, or a `File` path); executing or evaluating model output is a prompt-injection-to-code-execution path. |
| [SES1603](rules/SES1603.md) | A model-facing tool declared read-only (`ReadOnly = true`) or non-destructive (`Destructive = false`) via `[McpServerTool]` calls a state-changing API in its body (a file delete or overwrite, a directory delete, a process start, an ADO.NET non-query, an EF bulk mutation, or `SaveChanges`), so a host may auto-invoke it and cause irreversible damage. |
| [SES1604](rules/SES1604.md) | A Semantic Kernel prompt template disables the default encoding of substituted input by setting `AllowDangerouslySetContent = true` on `PromptTemplateConfig`/`InputVariable`/a template factory, re-opening prompt injection through template variables. |
| [SES1605](rules/SES1605.md) | Sensitive AI telemetry capture is enabled (`EnableSensitiveData = true`) on a `Microsoft.Extensions.AI` OpenTelemetry instrumentation client, shipping raw prompts and model responses -- which routinely carry secrets and PII -- verbatim to the telemetry backend. |
| [SES1606](rules/SES1606.md) | A string literal targets a model-weights file (`.onnx`, `.gguf`, `.safetensors`, `.pt`, `.pth`, `.ckpt`) over a cleartext `http://` URL, letting a network attacker swap in a tampered or backdoored model; non-loopback hosts only, and the `HttpClient`-sink case is left to SES1106. |

## Blazor

Component-authoring security defects specific to Blazor / Razor Components, each
gated on the relevant framework type so a non-Blazor project pays nothing.

| Rule | Description |
| --- | --- |
| [SES1701](rules/SES1701.md) | Raw HTML is rendered from a non-constant value (`MarkupString`/`AddMarkupContent`), bypassing automatic encoding — an XSS risk. Sanitizer allow-list via `securitysharp.SES1701.sanitizers`. |
| [SES1702](rules/SES1702.md) | A JavaScript-interop call targets a script-evaluation primitive (`eval`, `Function`, `document.write`), turning interop into a script-injection channel. |
| [SES1703](rules/SES1703.md) | `[Authorize]` on a non-routable component enforces nothing — authorization runs as a routing concern. Exempt types via `securitysharp.SES1703.exempt_types`. |
| [SES1704](rules/SES1704.md) | `IHttpContextAccessor` or a cascading `HttpContext` is used in an interactively-rendered component, where it is null or frozen at circuit start. |
| [SES1705](rules/SES1705.md) | `NavigationManager.NavigateTo` is called with a target that is not a verified relative URL — an open-redirect risk. Validator allow-list via `securitysharp.SES1705.validators`. |
| [SES1706](rules/SES1706.md) | An uploaded file is read with an unbounded or client-chosen size limit, letting an attacker fill server memory. Threshold via `securitysharp.SES1706.max_bytes`. |
| [SES1707](rules/SES1707.md) | A secret-shaped literal appears in code reachable as WebAssembly, which downloads to the browser in full — guaranteed disclosure. |
| [SES1708](rules/SES1708.md) | `CircuitOptions.DetailedErrors` is enabled, shipping server exception detail to every connected client. |
| [SES1709](rules/SES1709.md) | `SerializeAllClaims` serializes every claim into client-readable WebAssembly authentication state, exposing internal ids, tokens, and PII. |
| [SES1710](rules/SES1710.md) | Antiforgery validation is disabled on a form (`[RequireAntiforgeryToken(required: false)]`), removing CSRF protection. |
