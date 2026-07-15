// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyReadonlyLock = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.LockTargetAnalyzer,
    StyleSharp.Analyzers.Sst1904ReadonlyLockFieldCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests for <see cref="Sst1904ReadonlyLockFieldCodeFixProvider"/> (SST1904 make the lock field readonly).</summary>
public class Sst1904ReadonlyLockFieldCodeFixUnitTest
{
    /// <summary>A non-readonly private object field locked on.</summary>
    private const string ModifiedFieldSource = """
        public class C
        {
            private object _gate = new();

            public void M()
            {
                lock ({|SST1904:_gate|})
                {
                }
            }
        }
        """;

    /// <summary>The field after the fix.</summary>
    private const string ModifiedFieldFixed = """
        public class C
        {
            private readonly object _gate = new();

            public void M()
            {
                lock (_gate)
                {
                }
            }
        }
        """;

    /// <summary>A field with no modifiers locked on.</summary>
    private const string NoModifierFieldSource = """
        public class C
        {
            object _gate = new();

            public void M()
            {
                lock ({|SST1904:_gate|})
                {
                }
            }
        }
        """;

    /// <summary>The no-modifier field after the fix.</summary>
    private const string NoModifierFieldFixed = """
        public class C
        {
            readonly object _gate = new();

            public void M()
            {
                lock (_gate)
                {
                }
            }
        }
        """;

    /// <summary>Verifies the fix inserts <c>readonly</c> after the access modifier.</summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Test]
    public async Task MakesAModifiedFieldReadonlyAsync()
        => await VerifyReadonlyLock.VerifyCodeFixAsync(ModifiedFieldSource, ModifiedFieldFixed);

    /// <summary>Verifies the fix adds <c>readonly</c> to a field that had no modifiers.</summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Test]
    public async Task MakesANoModifierFieldReadonlyAsync()
        => await VerifyReadonlyLock.VerifyCodeFixAsync(NoModifierFieldSource, NoModifierFieldFixed);
}
