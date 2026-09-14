// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests the syntactic contracts of the shared throw-guard matchers.</summary>
public class ThrowGuardPatternsTests
{
    /// <summary>Checks null operands and constructor parameter names without requiring valid binding.</summary>
    /// <param name="source">The guard statement.</param>
    /// <param name="expected">The matched operand, or null for a rejected guard.</param>
    /// <returns>The assertion task.</returns>
    [Test]
    [Arguments("if (x is null) throw new ArgumentNullException();", "x")]
    [Arguments("if (x == null) { throw new System.ArgumentNullException(nameof(x)); }", "x")]
    [Arguments("if (null == x) throw new global::ArgumentNullException(\"x\");", "x")]
    [Arguments("if (this.x == null) throw new ArgumentNullException(\"x\");", "this.x")]
    [Arguments("if (this.x == null) throw new ArgumentNullException(\"y\");", null)]
    [Arguments("if (x == null) throw new ArgumentNullException;", "x")]
    [Arguments("if (x == null) throw new ArgumentNullException(nameof(y));", null)]
    [Arguments("if (x == null) throw new ArgumentNullException(\"y\");", null)]
    [Arguments("if (x == null) throw new ArgumentNullException(1);", null)]
    [Arguments("if (x == null) throw new ArgumentNullException(GetName());", null)]
    [Arguments("if (x == null) throw new ArgumentNullException(nameof(x), \"message\");", null)]
    [Arguments("if (GetValue() == null) throw new ArgumentNullException(\"x\");", null)]
    [Arguments("if (x is 1) throw new ArgumentNullException();", null)]
    [Arguments("if (x is not null) throw new ArgumentNullException();", null)]
    [Arguments("if (flag) throw new ArgumentNullException();", null)]
    [Arguments("if (x == y) throw new ArgumentNullException();", null)]
    [Arguments("if (x != null) throw new ArgumentNullException();", null)]
    [Arguments("if (x == null) throw new Exception();", null)]
    [Arguments("if (x == null) throw new int();", null)]
    [Arguments("if (x == null) throw error;", null)]
    [Arguments("if (x == null) throw;", null)]
    [Arguments("if (x == null) { Log(); throw new ArgumentNullException(); }", null)]
    [Arguments("if (x == null) throw new ArgumentNullException(); else Log();", null)]
    public async Task NullGuardRequiresMatchingParameterAsync(string source, string? expected)
    {
        var statement = (IfStatementSyntax)SyntaxFactory.ParseStatement(source);
        await Assert.That(ThrowGuardPatterns.TryMatchArgumentNull(statement, out var operand)).IsEqualTo(expected is not null);
        await Assert.That(operand?.ToString()).IsEqualTo(expected);
    }

    /// <summary>Checks recognized string receivers, probes, and rejected near misses.</summary>
    /// <param name="condition">The condition syntax.</param>
    /// <param name="exception">The thrown exception syntax.</param>
    /// <param name="expected">The matched probe name.</param>
    /// <returns>The assertion task.</returns>
    [Test]
    [Arguments("string.IsNullOrEmpty(x)", "ArgumentException()", "IsNullOrEmpty")]
    [Arguments("String.IsNullOrWhiteSpace(x)", "ArgumentNullException()", "IsNullOrWhiteSpace")]
    [Arguments("System.String.IsNullOrEmpty(x)", "System.ArgumentException()", "IsNullOrEmpty")]
    [Arguments("int.IsNullOrEmpty(x)", "ArgumentException()", null)]
    [Arguments("Other.IsNullOrEmpty(x)", "ArgumentException()", null)]
    [Arguments("System.Other.IsNullOrEmpty(x)", "ArgumentException()", null)]
    [Arguments("Get().IsNullOrEmpty(x)", "ArgumentException()", null)]
    [Arguments("string.Equals(x)", "ArgumentException()", null)]
    [Arguments("string.IsNullOrEmpty()", "ArgumentException()", null)]
    [Arguments("string.IsNullOrEmpty(x, y)", "ArgumentException()", null)]
    [Arguments("IsNullOrEmpty(x)", "ArgumentException()", null)]
    [Arguments("flag", "ArgumentException()", null)]
    [Arguments("string.IsNullOrEmpty(x)", "Exception()", null)]
    public async Task StringGuardRecognizesOnlySupportedShapesAsync(string condition, string exception, string? expected)
    {
        var statement = (IfStatementSyntax)SyntaxFactory.ParseStatement($"if ({condition}) throw new {exception};");
        await Assert.That(ThrowGuardPatterns.TryMatchStringGuard(statement, out var method, out var operand)).IsEqualTo(expected is not null);
        await Assert.That(method).IsEqualTo(expected);
        await Assert.That(operand?.ToString()).IsEqualTo(expected is null ? null : "x");
    }

    /// <summary>Checks disposal guards preserve custom messages by refusing them.</summary>
    /// <param name="source">The complete guard.</param>
    /// <param name="expected">Whether the guard matches.</param>
    /// <returns>The assertion task.</returns>
    [Test]
    [Arguments("if (disposed) throw new ObjectDisposedException();", true)]
    [Arguments("if (disposed) throw new ObjectDisposedException;", true)]
    [Arguments("if (disposed) { throw new ObjectDisposedException(nameof(C)); }", true)]
    [Arguments("if (disposed) throw new ObjectDisposedException(\"C\");", false)]
    [Arguments("if (disposed) throw new ObjectDisposedException(nameof(C), \"message\");", false)]
    [Arguments("if (disposed) throw new Exception();", false)]
    [Arguments("if (disposed) Log();", false)]
    [Arguments("if (disposed) throw new ObjectDisposedException(); else Log();", false)]
    public async Task DisposedGuardRequiresStandardArgumentsAsync(string source, bool expected)
    {
        var statement = (IfStatementSyntax)SyntaxFactory.ParseStatement(source);
        await Assert.That(ThrowGuardPatterns.TryMatchObjectDisposed(statement, out var condition)).IsEqualTo(expected);
        await Assert.That(condition?.ToString()).IsEqualTo(expected ? "disposed" : null);
        await Assert.That(ThrowGuardPatterns.TryMatchStringGuard(statement, out _, out _)).IsFalse();
    }

    /// <summary>Checks every comparison orientation and the bound passed to its helper.</summary>
    /// <param name="condition">The comparison.</param>
    /// <param name="expected">The selected helper.</param>
    /// <param name="bound">The selected bound.</param>
    /// <returns>The assertion task.</returns>
    [Test]
    [Arguments("x < 0", "ThrowIfNegative", null)]
    [Arguments("0 > x", "ThrowIfNegative", null)]
    [Arguments("x <= 0", "ThrowIfNegativeOrZero", null)]
    [Arguments("0 >= x", "ThrowIfNegativeOrZero", null)]
    [Arguments("x == 0", "ThrowIfZero", null)]
    [Arguments("0 == x", "ThrowIfZero", null)]
    [Arguments("x > 1", "ThrowIfGreaterThan", "1")]
    [Arguments("1 < x", "ThrowIfGreaterThan", "1")]
    [Arguments("x >= 1", "ThrowIfGreaterThanOrEqual", "1")]
    [Arguments("1 <= x", "ThrowIfGreaterThanOrEqual", "1")]
    [Arguments("x < limit", "ThrowIfLessThan", "limit")]
    [Arguments("x <= 1", "ThrowIfLessThanOrEqual", "1")]
    [Arguments("x == 1", "ThrowIfEqual", "1")]
    [Arguments("1 != x", "ThrowIfNotEqual", "1")]
    [Arguments("x > 0", null, null)]
    [Arguments("x != 0", null, null)]
    [Arguments("x + 1", null, null)]
    [Arguments("x + 0", null, null)]
    [Arguments("x == x", null, null)]
    [Arguments("y == z", null, null)]
    [Arguments("x < \"0\"", "ThrowIfLessThan", "\"0\"")]
    public async Task RangeGuardOrientsComparisonsAsync(string condition, string? expected, string? bound)
    {
        var statement = (IfStatementSyntax)SyntaxFactory.ParseStatement($"if ({condition}) throw new ArgumentOutOfRangeException(nameof(x));");
        await Assert.That(ThrowGuardPatterns.TryMatchRangeGuard(statement, out var match)).IsEqualTo(expected is not null);
        await Assert.That(match.Helper).IsEqualTo(expected);
        await Assert.That(match.Bound?.ToString()).IsEqualTo(bound);
        await Assert.That(match.Value?.ToString()).IsEqualTo(expected is null ? null : "x");
    }

    /// <summary>Checks range guards reject incomplete or differently named constructor arguments.</summary>
    /// <param name="source">The guard statement.</param>
    /// <returns>The assertion task.</returns>
    [Test]
    [Arguments("if (x < 0) throw new ArgumentOutOfRangeException();")]
    [Arguments("if (x < 0) throw new ArgumentOutOfRangeException;")]
    [Arguments("if (x < 0) throw new ArgumentOutOfRangeException { };")]
    [Arguments("if (x < 0) throw new ArgumentOutOfRangeException(\"x\");")]
    [Arguments("if (x < 0) throw new ArgumentOutOfRangeException(nameof(this.x));")]
    [Arguments("if (x < 0) throw new ArgumentOutOfRangeException(nameof(x), \"message\");")]
    [Arguments("if (flag) throw new ArgumentOutOfRangeException(nameof(x));")]
    [Arguments("if (x < 0) Log();")]
    [Arguments("if (x < 0) throw new Exception();")]
    [Arguments("if (x < 0) throw new ArgumentOutOfRangeException(nameof(x)); else Log();")]
    public async Task RangeGuardRejectsUnsupportedBodiesAsync(string source)
    {
        var statement = (IfStatementSyntax)SyntaxFactory.ParseStatement(source);
        await Assert.That(ThrowGuardPatterns.TryMatchRangeGuard(statement, out _)).IsFalse();
    }
}
