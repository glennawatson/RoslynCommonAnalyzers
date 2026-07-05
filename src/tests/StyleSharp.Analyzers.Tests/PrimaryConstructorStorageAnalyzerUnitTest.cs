// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using VerifyPrimaryConstructorStorage = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    StyleSharp.Analyzers.Sst2241PrimaryConstructorStorageAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for <see cref="Sst2241PrimaryConstructorStorageAnalyzer"/>.</summary>
public class PrimaryConstructorStorageAnalyzerUnitTest
{
    /// <summary>Verifies a constructor that only stores its parameters is reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task StorageOnlyConstructorIsReportedAsync()
    {
        var test = new VerifyPrimaryConstructorStorage.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            TestCode = """
                       public sealed class C
                       {
                           private readonly int _value;

                           public {|SST2241:C|}(int value)
                           {
                               _value = value;
                           }
                       }
                       """,
        };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies constructors with extra work are clean.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task ConstructorWithExtraWorkIsCleanAsync()
    {
        var test = new VerifyPrimaryConstructorStorage.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            TestCode = """
                       public sealed class C
                       {
                           private readonly int _value;

                           public C(int value)
                           {
                               _value = value;
                               System.Console.WriteLine(value);
                           }
                       }
                       """,
        };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies a storage-only constructor is clean when the type has another constructor.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task StorageOnlyConstructorOnMultiConstructorTypeIsCleanAsync()
    {
        var test = new VerifyPrimaryConstructorStorage.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            TestCode = """
                       #nullable enable

                       public sealed class Bag
                       {
                           private readonly object? _a;
                           private readonly object? _b;

                           public Bag()
                           {
                           }

                           public Bag(object a, object b)
                           {
                               _a = a;
                               _b = b;
                           }
                       }
                       """,
        };

        await test.RunAsync(CancellationToken.None);
    }
}
