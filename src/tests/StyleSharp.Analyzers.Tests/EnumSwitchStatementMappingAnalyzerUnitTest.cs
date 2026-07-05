// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using VerifyEnumSwitchStatementMapping = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    StyleSharp.Analyzers.Sst2242EnumSwitchStatementMappingAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for <see cref="Sst2242EnumSwitchStatementMappingAnalyzer"/>.</summary>
public class EnumSwitchStatementMappingAnalyzerUnitTest
{
    /// <summary>Verifies an enum switch statement missing a named value is reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task MissingEnumCaseIsReportedAsync()
    {
        var test = new VerifyEnumSwitchStatementMapping.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            TestCode = """
                       public enum Color
                       {
                           Red,
                           Blue
                       }

                       public sealed class C
                       {
                           public int M(Color color)
                           {
                               {|SST2242:switch|} (color)
                               {
                                   case Color.Red:
                                       return 1;
                               }

                               return 0;
                           }
                       }
                       """,
        };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies a switch with a default section is treated as intentional.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task DefaultSectionIsCleanAsync()
    {
        var test = new VerifyEnumSwitchStatementMapping.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            TestCode = """
                       public enum Color
                       {
                           Red,
                           Blue
                       }

                       public sealed class C
                       {
                           public int M(Color color)
                           {
                               switch (color)
                               {
                                   case Color.Red:
                                       return 1;
                                   default:
                                       return 0;
                               }
                           }
                       }
                       """,
        };

        await test.RunAsync(CancellationToken.None);
    }
}
