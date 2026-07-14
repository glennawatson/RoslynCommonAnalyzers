// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyEnumStorage = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<StyleSharp.Analyzers.Sst2313EnumStorageAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST2313 (enums should use an allowed storage type).</summary>
public class EnumStorageAnalyzerUnitTest
{
    /// <summary>Verifies every storage type outside the default allowed list is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task StorageOutsideTheAllowedListIsReportedAsync()
        => await VerifyEnumStorage.VerifyAnalyzerAsync(
            """
            public enum {|SST2313:Tiny|} : byte
            {
                Low,
            }

            public enum {|SST2313:Signed|} : sbyte
            {
                Low,
            }

            public enum {|SST2313:Small|} : short
            {
                Low,
            }

            public enum {|SST2313:SmallUnsigned|} : ushort
            {
                Low,
            }

            public enum {|SST2313:Unsigned|} : uint
            {
                Low,
            }

            public enum {|SST2313:Wide|} : long
            {
                Low,
            }

            public enum {|SST2313:WideUnsigned|} : ulong
            {
                Low,
            }
            """);

    /// <summary>Verifies an enum stored as an int is clean, whether or not it says so.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task IntStorageIsCleanAsync()
        => await VerifyEnumStorage.VerifyAnalyzerAsync(
            """
            public enum Implicit
            {
                Low,
            }

            public enum Explicit : int
            {
                Low,
            }
            """);

    /// <summary>Verifies an enum is reported wherever it is declared, and whatever it is marked with.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task StorageIsCheckedRegardlessOfDeclarationSiteAsync()
        => await VerifyEnumStorage.VerifyAnalyzerAsync(
            """
            using System;

            [Flags]
            public enum {|SST2313:Options|} : byte
            {
                None = 0,
                First = 1,
            }

            internal enum {|SST2313:Internal|} : byte
            {
                Low,
            }

            public class Outer
            {
                private enum {|SST2313:Nested|} : byte
                {
                    Low,
                }

                public int Read() => (int)Nested.Low;
            }
            """);

    /// <summary>Verifies a rule-specific allowed list lets a deliberate packing choice through.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConfiguredStorageIsAllowedAsync()
    {
        var test = new VerifyEnumStorage.Test
        {
            TestCode = """
                       public enum Packed : byte
                       {
                           Low,
                       }

                       public enum {|SST2313:Wide|} : long
                       {
                           Low,
                       }
                       """,
        };

        test.TestState.AnalyzerConfigFiles.Add(
            ("/.editorconfig", """
            root = true
            [*.cs]
            stylesharp.SST2313.allowed_enum_storage = int, byte

            """));

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies the project-wide key is honoured when no rule-specific one is set.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task GeneralKeyIsHonouredAsync()
    {
        var test = new VerifyEnumStorage.Test
        {
            TestCode = """
                       public enum Packed : byte
                       {
                           Low,
                       }
                       """,
        };

        test.TestState.AnalyzerConfigFiles.Add(
            ("/.editorconfig", """
            root = true
            [*.cs]
            stylesharp.allowed_enum_storage = int, byte

            """));

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies the rule-specific key wins over the project-wide one.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task RuleSpecificKeyOverridesGeneralKeyAsync()
    {
        var test = new VerifyEnumStorage.Test
        {
            TestCode = """
                       public enum {|SST2313:Packed|} : byte
                       {
                           Low,
                       }
                       """,
        };

        test.TestState.AnalyzerConfigFiles.Add(
            ("/.editorconfig", """
            root = true
            [*.cs]
            stylesharp.allowed_enum_storage = int, byte
            stylesharp.SST2313.allowed_enum_storage = int

            """));

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies the allowed list accepts CLR names as readily as C# keywords.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ClrNamesAreAcceptedAsync()
    {
        var test = new VerifyEnumStorage.Test
        {
            TestCode = """
                       public enum Wide : long
                       {
                           Low,
                       }
                       """,
        };

        test.TestState.AnalyzerConfigFiles.Add(
            ("/.editorconfig", """
            root = true
            [*.cs]
            stylesharp.SST2313.allowed_enum_storage = Int32, Int64

            """));

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies an empty list falls back to the default rather than reporting every enum.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EmptyListFallsBackToTheDefaultAsync()
    {
        var test = new VerifyEnumStorage.Test
        {
            TestCode = """
                       public enum Implicit
                       {
                           Low,
                       }

                       public enum {|SST2313:Packed|} : byte
                       {
                           Low,
                       }
                       """,
        };

        test.TestState.AnalyzerConfigFiles.Add(
            ("/.editorconfig", """
            root = true
            [*.cs]
            stylesharp.SST2313.allowed_enum_storage =

            """));

        await test.RunAsync(CancellationToken.None);
    }
}
