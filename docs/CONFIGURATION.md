# Configuring StyleSharp

StyleSharp is configured entirely through **`.editorconfig`** — the same file you
already use for `dotnet_diagnostic.*` severities and .NET code style. There is no
separate JSON configuration file.

## Why `.editorconfig` and not `stylecop.json`

StyleCop.Analyzers configures rule behaviour through a separate `stylecop.json`
file. StyleSharp deliberately does **not** do that. Instead it follows the
approach the .NET SDK's own **CA analyzers** use:

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

## Rule options

Some rules expose options. Current options:

| Option key | Rule | Values | Default |
| --- | --- | --- | --- |
| `stylesharp.tuple_element_naming` | [SST1316](rules/SST1316.md) | `pascal_case`, `camel_case` | `pascal_case` |
| `stylesharp.union_member_naming` | [SST1315](rules/SST1315.md) | `pascal_case`, `camel_case` | `pascal_case` |
| `stylesharp.record_parameter_naming` | [SST1801](rules/SST1801.md) | `pascal_case`, `camel_case` | `pascal_case` |
| `stylesharp.summary_single_line_max_length` | [SST1653](rules/SST1653.md) | positive integer | `100` |
| `file_header_template` | [SST1633](rules/SST1633.md) | header text (`\n` separates lines, `{fileName}` substituted), or `unset` | `unset` |

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

## Adding options to new rules

When a rule needs an option, define its keys in `NamingConventions` (general +
rule-specific consts), read them via the compiler's
`AnalyzerConfigOptionsProvider.GetOptions(tree)`, and document the key here and in
the rule's page. Do **not** introduce a separate config file or read
`.editorconfig` directly.
