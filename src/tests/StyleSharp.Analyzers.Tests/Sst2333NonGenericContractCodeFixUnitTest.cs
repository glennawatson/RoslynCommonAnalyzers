// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.Composition.Hosting;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Editing;
using VerifyContract = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst2333NonGenericContractAnalyzer,
    StyleSharp.Analyzers.Sst2333NonGenericContractCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests for <see cref="Sst2333NonGenericContractCodeFixProvider"/> (SST2333 add the non-generic member).</summary>
public class Sst2333NonGenericContractCodeFixUnitTest
{
    /// <summary>A type implementing only the generic comparable contract.</summary>
    private const string ComparableSource = """
        using System;

        public class {|SST2333:Money|} : IComparable<Money>
        {
            public int CompareTo(Money other) => 0;
        }
        """;

    /// <summary>The type after the fix adds the non-generic comparable contract.</summary>
    private const string ComparableFixed = """
        using System;

        public class Money : IComparable<Money>, IComparable
        {
            public int CompareTo(Money other) => 0;

            int IComparable.CompareTo(object obj) => obj is Money other ? ((IComparable<Money>)this).CompareTo(other) : throw new InvalidCastException();
        }
        """;

    /// <summary>A type implementing only the generic comparer contract.</summary>
    private const string ComparerSource = """
        using System.Collections.Generic;

        public class {|SST2333:IntComparer|} : IComparer<int>
        {
            public int Compare(int x, int y) => 0;
        }
        """;

    /// <summary>The type after the fix adds the non-generic comparer contract.</summary>
    private const string ComparerFixed = """
        using System.Collections.Generic;

        public class IntComparer : IComparer<int>, System.Collections.IComparer
        {
            public int Compare(int x, int y) => 0;

            int System.Collections.IComparer.Compare(object x, object y) => ((IComparer<int>)this).Compare((int)x, (int)y);
        }
        """;

    /// <summary>A type implementing only the generic equality-comparer contract.</summary>
    private const string EqualityComparerSource = """
        using System.Collections.Generic;

        public class {|SST2333:IntEquality|} : IEqualityComparer<int>
        {
            public bool Equals(int x, int y) => true;

            public int GetHashCode(int obj) => 0;
        }
        """;

    /// <summary>The type after the fix adds the non-generic equality-comparer contract.</summary>
    private const string EqualityComparerFixed = """
        using System.Collections.Generic;

        public class IntEquality : IEqualityComparer<int>, System.Collections.IEqualityComparer
        {
            public bool Equals(int x, int y) => true;

            public int GetHashCode(int obj) => 0;

            bool System.Collections.IEqualityComparer.Equals(object x, object y) => ((IEqualityComparer<int>)this).Equals((int)x, (int)y);
            int System.Collections.IEqualityComparer.GetHashCode(object obj) => ((IEqualityComparer<int>)this).GetHashCode((int)obj);
        }
        """;

    /// <summary>A type implementing only the generic equatable contract.</summary>
    private const string EquatableSource = """
        using System;

        public class {|SST2333:Money|} : IEquatable<Money>
        {
            public bool Equals(Money other) => true;

            public override int GetHashCode() => 0;
        }
        """;

    /// <summary>The type after the fix adds an <c>object.Equals</c> override.</summary>
    private const string EquatableFixed = """
        using System;

        public class Money : IEquatable<Money>
        {
            public bool Equals(Money other) => true;

            public override int GetHashCode() => 0;

            public override bool Equals(object obj) => obj is Money other && ((IEquatable<Money>)this).Equals(other);
        }
        """;

    /// <summary>Verifies the fix adds <c>IComparable</c> forwarding to <c>IComparable&lt;T&gt;</c>.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task AddsNonGenericComparableAsync() =>
        VerifyContract.VerifyCodeFixAsync(ComparableSource, ComparableFixed);

    /// <summary>Verifies the fix adds <c>IComparer</c> forwarding to <c>IComparer&lt;T&gt;</c>.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task AddsNonGenericComparerAsync() =>
        VerifyContract.VerifyCodeFixAsync(ComparerSource, ComparerFixed);

    /// <summary>Verifies the fix adds <c>IEqualityComparer</c> forwarding to <c>IEqualityComparer&lt;T&gt;</c>.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task AddsNonGenericEqualityComparerAsync() =>
        VerifyContract.VerifyCodeFixAsync(EqualityComparerSource, EqualityComparerFixed);

    /// <summary>Verifies the fix adds an <c>object.Equals</c> override forwarding to <c>IEquatable&lt;T&gt;</c>.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task AddsObjectEqualsOverrideAsync() =>
        VerifyContract.VerifyCodeFixAsync(EquatableSource, EquatableFixed);

    /// <summary>Verifies stale locations, directive boundaries, and missing contract metadata prevent both edit paths.</summary>
    /// <param name="source">The document remaining when the fix is requested.</param>
    /// <param name="contract">The requested contract, including invalid and absent values.</param>
    /// <param name="hasContract">Whether the diagnostic carries the contract property.</param>
    /// <param name="argument">The generic argument stored on the diagnostic.</param>
    /// <param name="hasArgument">Whether the diagnostic carries the argument property.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("enum C { }", "IComparable", true, "global::C", true)]
    [Arguments("class C {\n#region Members\n#endregion\n}", "IComparable", true, "global::C", true)]
    [Arguments("class C { }", null, false, "global::C", true)]
    [Arguments("class C { }", null, true, "global::C", true)]
    [Arguments("class C { }", "Unknown", true, "global::C", true)]
    [Arguments("class C { }", "IComparable", true, null, false)]
    [Arguments("class C { }", "IComparable", true, null, true)]
    public async Task InapplicableContractDoesNotRegisterOrEditAsync(string source, string? contract, bool hasContract, string? argument, bool hasArgument)
    {
        using var workspace = new AdhocWorkspace();
        var document = workspace.AddProject(nameof(Test), LanguageNames.CSharp).AddDocument("Test.cs", source);
        var root = (await document.GetSyntaxRootAsync())!;
        var identifier = root.DescendantTokens().Single(static token => token.ValueText == "C");
        var properties = ImmutableDictionary<string, string?>.Empty;
        if (hasContract)
        {
            properties = properties.Add(Sst2333NonGenericContractAnalyzer.ContractKey, contract);
        }

        if (hasArgument)
        {
            properties = properties.Add(Sst2333NonGenericContractAnalyzer.TypeArgumentKey, argument);
        }

        var diagnostic = Diagnostic.Create(DesignRules.MissingNonGenericContract, identifier.GetLocation(), properties);
        using var container = new ContainerConfiguration().WithPart<Sst2333NonGenericContractCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        var actions = new List<CodeAction>();
        await provider.RegisterCodeFixesAsync(new(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        await Assert.That(actions).IsEmpty();
        var editor = await DocumentEditor.CreateAsync(document);
        ((IBatchFixableCodeFix)provider).RegisterBatchEdits(editor, diagnostic);
        await Assert.That(editor.GetChangedRoot().ToFullString()).IsEqualTo(source);
    }
}
