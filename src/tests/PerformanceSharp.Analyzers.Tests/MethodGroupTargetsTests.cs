// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using RoslynCommon.Analyzers.Tests;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Tests compilation-wide delegate target discovery and caching.</summary>
public class MethodGroupTargetsTests
{
    /// <summary>Verifies direct, qualified and generic method groups are collected across trees while invocations are ignored.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task CollectsOriginalDefinitionsAcrossTreesAndCachesResultsAsync()
    {
        var compilation = CSharpCompilation.Create(
            "DelegateTargets",
            [
                CSharpSyntaxTree.ParseText("""
                    using System;
                    partial class C
                    {
                        public static void Direct() { }
                        public static void Qualified() { }
                        public static void Generic<T>() { }
                        public static void Called() { }
                        public static void QualifiedCall() { }
                        void Use() { Called(); C.QualifiedCall(); }
                    }
                    """),
                CSharpSyntaxTree.ParseText("""
                    using System;
                    partial class C
                    {
                        Action first = Direct;
                        Action second = C.Qualified;
                        Action third = Generic<int>;
                    }
                    """),
            ],
            RuntimeMetadataReferences.Platform);
        var type = compilation.GetTypeByMetadataName("C")!;
        var targets = new MethodGroupTargets(compilation);

        await Assert.That(targets.Contains(type.GetMembers("Direct")[0], CancellationToken.None)).IsTrue();
        await Assert.That(targets.Contains(type.GetMembers("Qualified")[0], CancellationToken.None)).IsTrue();
        await Assert.That(targets.Contains(type.GetMembers("Generic")[0], CancellationToken.None)).IsTrue();
        await Assert.That(targets.Contains(type.GetMembers("Called")[0], CancellationToken.None)).IsFalse();
        await Assert.That(targets.Contains(type.GetMembers("QualifiedCall")[0], CancellationToken.None)).IsFalse();
        await Assert.That(targets.Contains(type.GetMembers("Direct")[0], new(canceled: true))).IsTrue();
    }

    /// <summary>Verifies type and argument-name positions do not hide a delegate target elsewhere in the compilation.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task DeclarationTypesAndArgumentNamesAreSkippedAsync()
    {
        var compilation = CSharpCompilation.Create(
            "DeclarationNames",
            [CSharpSyntaxTree.ParseText("""
                using System;
                using Alias = System.String;
                namespace Samples;
                [Obsolete]
                class C<T> : Exception where T : Exception
                {
                    C<T> field;
                    C<T> Property => this;
                    C<T> this[C<T> key] => key;
                    event Action Event { add { } remove { } }
                    delegate C<T> Factory();
                    public static C<T> operator +(C<T> left, C<T> right) => left;
                    public static implicit operator C<T>(string value) => new C<T>();
                    C<T> Method(C<T> value)
                    {
                        C<T> Local() => value;
                        try
                        {
                            var type = typeof(C<T>);
                            var missing = default(C<T>);
                            var size = sizeof(T);
                            int.TryParse("1", out var parsed);
                            Method(value: Local());
                        }
                        catch (Exception error) { }
                        Action target = Target;
                        return value;
                    }
                    static void Target() { }
                }
                """)],
            RuntimeMetadataReferences.Platform);
        var type = compilation.GetTypeByMetadataName("Samples.C`1")!;
        var targets = new MethodGroupTargets(compilation);

        await Assert.That(targets.Contains(type.GetMembers("Target")[0], CancellationToken.None)).IsTrue();
        await Assert.That(targets.Contains(type.GetMembers("Method")[0], CancellationToken.None)).IsFalse();
    }

    /// <summary>Verifies a cancelled initial walk does not publish an incomplete cache.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task CancelledWalkCanBeRetriedAsync()
    {
        var compilation = CSharpCompilation.Create("CancelledTargets", [CSharpSyntaxTree.ParseText("class C { void M() {} }")], RuntimeMetadataReferences.Platform);
        var method = compilation.GetTypeByMetadataName("C")!.GetMembers("M")[0];
        var targets = new MethodGroupTargets(compilation);

        await Assert.That(() => targets.Contains(method, new(canceled: true))).Throws<OperationCanceledException>();
        await Assert.That(targets.Contains(method, CancellationToken.None)).IsFalse();
    }
}
