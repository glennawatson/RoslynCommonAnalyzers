# Configuring StyleSharp

StyleSharp is configured entirely through **`.editorconfig`** — the same file you
already use for `dotnet_diagnostic.*` severities and .NET code style. There is no
separate JSON configuration file.

## Why `.editorconfig` and not a separate config file

StyleSharp deliberately does **not** use a separate JSON configuration file.
Instead it follows the approach the .NET SDK's own **CA analyzers** use:

- Options are read from the compiler-provided `AnalyzerConfigOptionsProvider` —
  StyleSharp never reads the `.editorconfig` from disk itself. The compiler parses
  it and hands the analyzer a key/value view, which keeps analyzers
  file-system-independent and correct under per-directory `.editorconfig`
  cascading.
- Keys follow CA's rule-specific-over-general layering, under a `stylesharp.`
  prefix that mirrors CA's `dotnet_code_quality.` prefix:
  - `stylesharp.<option>` — applies to every rule that reads that option.
  - `stylesharp.<RuleId>.<option>` — overrides the general value for one rule.

This means a single, familiar file controls **severity, code style, and
StyleSharp options** together, and configuration cascades per directory exactly
like everything else in `.editorconfig`.

## Severity

Every rule's severity is set the standard way:

```ini
[*.cs]
dotnet_diagnostic.SST1309.severity = warning   # error | warning | suggestion | silent | none
```

## Recommended preset

[`recommended.editorconfig`](../recommended.editorconfig) at the repository root is
a ready-to-use preset listing every rule grouped by category, with the opt-in
(disabled-by-default) rules commented out so you can switch them on individually.
Copy it in — or merge its `[*.cs]` block into your existing `.editorconfig` — as a
starting point, then tune severities to taste.

## Rule options

Some rules expose options. Current options:

| Option key | Rule | Values | Default |
| --- | --- | --- | --- |
| `stylesharp.tuple_element_naming` | [SST1316](rules/SST1316.md) | `pascal_case`, `camel_case` | `pascal_case` |
| `stylesharp.union_member_naming` | [SST1315](rules/SST1315.md) | `pascal_case`, `camel_case` | `pascal_case` |
| `stylesharp.record_parameter_naming` | [SST1801](rules/SST1801.md) | `pascal_case`, `camel_case` | `pascal_case` |
| `stylesharp.extension_container_preferred_suffix` | [SST1704](rules/SST1704.md) | `Extensions`, `Mixins` | `Extensions` |
| `stylesharp.namespace_root` | [SST1417](rules/SST1417.md) | namespace text | MSBuild `RootNamespace` |
| `stylesharp.instance_member_qualification` | [SST1117](rules/SST1117.md) | `omit_this`, `require_this` | `omit_this` |
| `stylesharp.conditional_operator_placement` | [SST1145](rules/SST1145.md) | `leading`, `trailing` | `leading` |
| `stylesharp.collection_expression_spacing` | [SST1010](rules/SST1010.md) / [SST1011](rules/SST1011.md) | `none`, `space` | `none` |
| `stylesharp.summary_single_line_max_length` | [SST1653](rules/SST1653.md) | positive integer | `100` |
| `stylesharp.max_switch_sections` | [SST1423](rules/SST1423.md) | positive integer | `30` |
| `stylesharp.allowed_hungarian_prefixes` | [SST1305](rules/SST1305.md) | comma-separated prefixes | built-in list only |
| `stylesharp.SST1431.additional_per_owner_types` | [SST1431](rules/SST1431.md) | comma-separated fully-qualified type names | built-in list only |
| `stylesharp.document_exposed_elements` | SST1600 / [SST1601](rules/SST1601.md) / [SST1602](rules/SST1602.md) / SST1654 | `true`, `false` | `true` |
| `stylesharp.document_internal_elements` | SST1600 / [SST1601](rules/SST1601.md) / [SST1602](rules/SST1602.md) / SST1654 | `true`, `false` | `true` |
| `stylesharp.document_private_elements` | SST1600 / [SST1601](rules/SST1601.md) / [SST1602](rules/SST1602.md) / SST1654 | `true`, `false` | `false` |
| `stylesharp.document_private_fields` | SST1600 | `true`, `false` | `false` |
| `stylesharp.document_interfaces` | SST1600 / [SST1601](rules/SST1601.md) | `all`, `exposed`, `none` | `all` |
| `file_header_template` | [SST1633](rules/SST1633.md) | header text (`\n` separates lines, `{fileName}` substituted), or `unset` | `unset` |
| `stylesharp.file_naming_convention` | [SST1402](rules/SST1402.md) / [SST1649](rules/SST1649.md) code fixes | `braces` (`Widget{T}.cs`), `metadata` (`Widget`1.cs`) | `braces` |

Example:

```ini
[*.cs]
# Make tuple element names camelCase project-wide…
stylesharp.tuple_element_naming = camel_case
# …but keep unions PascalCase (rule-specific keys win over general ones).
stylesharp.SST1315.union_member_naming = pascal_case
```

Values are case-insensitive. An unset or unrecognized value falls back to the
default in the table above.

### Instance member qualification

`stylesharp.instance_member_qualification` controls [SST1117](rules/SST1117.md).
The default is `omit_this`, matching the StyleSharp `_field` convention:

```ini
[*.cs]
stylesharp.instance_member_qualification = omit_this
```

Set it to `require_this` when a project wants explicit receiver qualification instead:

```ini
[*.cs]
stylesharp.instance_member_qualification = require_this
```

The setting flips the same rule and code fix rather than enabling a second opposing rule.

### SST1431

`stylesharp.SST1431.additional_per_owner_types` names extra property-system registration
types (beyond the built-in WPF/WinUI/MAUI/Avalonia list) whose static fields and properties
[SST1431](rules/SST1431.md) should leave alone. Give comma- or whitespace-separated
fully-qualified type names; matching is by name (robust to `using` aliases) and includes
subtypes of the listed types. A general `stylesharp.additional_per_owner_types` key is also honored.

```ini
[*.cs]
stylesharp.SST1431.additional_per_owner_types = Custom.Framework.PropertyKey, Other.Lib.RegistryToken
```

### Documentation coverage scope

The `document_*` options control **which declarations the "must be documented"
rules apply to** — SST1600 (elements), SST1601 (partial elements), SST1602 (enum
members), and SST1654 (extension blocks). Each option maps to one accessibility
scope, and the defaults are chosen so a project gets sensible coverage out of the
box:

- `document_exposed_elements` (default `true`) — public and protected elements.
- `document_internal_elements` (default `true`) — internal elements. **On by
  default**, so an internal type or member with no `///` comment is reported.
- `document_private_elements` (default `false`) — private elements **other than
  fields**. Off by default; turn it on to require documentation everywhere except
  private fields.
- `document_private_fields` (default `false`, SST1600 only) — private fields. Off by
  default, as a separate knob from `document_private_elements`, so existing builds are
  unaffected. Turn it on to require a `///` comment on private fields.
  This gate is **independent** of `document_private_elements` — it covers fields and
  nothing else, and need not be combined with it. A private **const** is treated as a
  private field and follows this option. (Non-private fields are governed by
  `document_exposed_elements` / `document_internal_elements`, like any other member,
  with `private protected` treated as private.)
- `document_interfaces` (default `all`) — `all` documents every interface and its
  members regardless of accessibility, `exposed` only non-internal interfaces, and
  `none` never requires interface documentation.

Coverage uses **effective accessibility**: a `public` member of an `internal` class
is treated as internal, so it follows `document_internal_elements`. To require only
the public API surface to be documented, turn `document_internal_elements` off:

```ini
[*.cs]
stylesharp.document_internal_elements = false
```

### Collection expression spacing

`stylesharp.collection_expression_spacing` chooses the inner-bracket style for C#
`[...]` literals — **collection expressions** (`x = [1, 2]`) and **list patterns**
(`x is [1, 2]`). It governs both the opening bracket ([SST1010](rules/SST1010.md))
and the closing bracket ([SST1011](rules/SST1011.md)), keeping the two sides
symmetric.

- `none` (default) — tight `[1, 2]`. Inner padding is removed.
- `space` — padded `[ 1, 2 ]`. Inner padding is required.

```ini
[*.cs]
stylesharp.collection_expression_spacing = space
```

The **outer** space before `[` (after `=`, `return`, `is`, `(`, `,`, etc.) is always
allowed regardless of this option — that is what lets SST1010 be enabled alongside
collection expressions without rewriting `x = [1, 2]` to `x =[1, 2]`. Empty
collections stay `[]` under either setting. Element-access and array brackets
(`items[0]`, `new int[4]`) are never affected by this option and are always tight.

Because SST1010 is opt-in (off by default), enabling the padded style end-to-end
requires turning SST1010 on as well; otherwise only the closing bracket (SST1011, on
by default) is enforced.

## Adding options to new rules

When a rule needs an option, define its keys in `NamingConventions` (general +
rule-specific consts), read them via the compiler's
`AnalyzerConfigOptionsProvider.GetOptions(tree)`, and document the key here and in
the rule's page. Do **not** introduce a separate config file or read
`.editorconfig` directly.
