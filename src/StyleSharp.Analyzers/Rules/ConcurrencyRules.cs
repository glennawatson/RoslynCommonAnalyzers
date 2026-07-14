// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Diagnostic descriptors for the concurrency rules (SST19xx). These cover locking
/// conventions around what a <c>lock</c> statement may safely target. The
/// perf-motivated lock-type rule moved to PerformanceSharp.Analyzers as PSH1300.
/// </summary>
internal static class ConcurrencyRules
{
    /// <summary>SST1901 — a <c>lock</c> targets a field or property reachable from outside the declaring type.</summary>
    public static readonly DiagnosticDescriptor DoNotLockOnAccessibleMember = Create(
        "SST1901",
        "Do not lock on a publicly accessible object",
        "Do not lock on '{0}', which is accessible beyond the declaring type",
        "Locking on a field or property reachable from outside the type lets unrelated code take the same lock and deadlock; lock on a private, dedicated object instead.");

    /// <summary>SST1902 — a <c>lock</c> targets <c>this</c>, a <c>Type</c>, or a string (opt-in).</summary>
    public static readonly DiagnosticDescriptor DoNotLockOnWeakIdentity = CreateOptIn(
        "SST1902",
        "Do not lock on 'this', a Type, or a string",
        "Do not lock on this, a Type, or a string; lock on a private, dedicated object instead",
        "Locking on 'this', a System.Type, or a string exposes the lock to unrelated code (strings may be interned, Types are shared), risking deadlocks. Off by default.");

    /// <summary>SST1903 — a <c>lock</c> expression creates a fresh object on the spot.</summary>
    public static readonly DiagnosticDescriptor DoNotLockOnNewlyCreatedObject = Create(
        "SST1903",
        "Do not lock on a newly-created object",
        "Do not lock on a newly-created object; store a dedicated lock object instead",
        "Locking on a newly-created object never coordinates with other callers because every evaluation creates a fresh instance; store a dedicated lock object in a field instead.");

    /// <summary>Creates a Warning-severity Concurrency descriptor whose help link points at the rule's docs page.</summary>
    /// <param name="id">The diagnostic id.</param>
    /// <param name="title">The rule title.</param>
    /// <param name="messageFormat">The message format.</param>
    /// <param name="description">The rule description.</param>
    /// <returns>The descriptor.</returns>
    private static DiagnosticDescriptor Create(string id, string title, string messageFormat, string description) =>
        DescriptorFactory.Create(id, title, messageFormat, "Concurrency", description);

    /// <summary>Creates a Concurrency descriptor that is disabled by default (opt-in via .editorconfig).</summary>
    /// <param name="id">The diagnostic id.</param>
    /// <param name="title">The rule title.</param>
    /// <param name="messageFormat">The message format.</param>
    /// <param name="description">The rule description.</param>
    /// <returns>The descriptor.</returns>
    private static DiagnosticDescriptor CreateOptIn(string id, string title, string messageFormat, string description) =>
        DescriptorFactory.CreateOptIn(id, title, messageFormat, "Concurrency", description);
}
