// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis.Testing;

using VerifyEnumSwitchStatementMapping = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    StyleSharp.Analyzers.Sst2242EnumSwitchStatementMappingAnalyzer>;
using VerifyEnumSwitchStatementMappingFix = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst2242EnumSwitchStatementMappingAnalyzer,
    StyleSharp.Analyzers.EnumSwitchCoverageCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for <see cref="Sst2242EnumSwitchStatementMappingAnalyzer"/>.</summary>
public class EnumSwitchStatementMappingAnalyzerUnitTest
{
    /// <summary>Verifies values named by an <c>or</c> pattern count as covered.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>
    /// Stacked case labels are commonly rewritten as one <c>or</c> pattern. Reading only plain labels made
    /// the rule re-report both values, the fix wrote them back as duplicates, and the merge and the pattern
    /// rewrite turned that into a loop no run could finish.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ValuesNamedByAnOrPatternAreCoveredAsync() =>
        VerifyEnumSwitchStatementMapping.VerifyAnalyzerAsync(
            """
            public enum Level
            {
                Low,
                Medium,
                High
            }

            public sealed class C
            {
                public void M(Level level)
                {
                    switch (level)
                    {
                        case Level.Low or Level.Medium:
                            System.Console.WriteLine("a");
                            break;
                        case Level.High:
                            System.Console.WriteLine("b");
                            break;
                    }
                }
            }
            """);

    /// <summary>Verifies a guarded label does not count as covering its value.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>The guard decides whether the section runs, so the value is not handled outright.</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task GuardedLabelDoesNotCoverItsValueAsync() =>
        VerifyEnumSwitchStatementMapping.VerifyAnalyzerAsync(
            """
            public enum Level
            {
                Low,
                High
            }

            public sealed class C
            {
                public void M(Level level, bool ready)
                {
                    {|SST2242:switch|} (level)
                    {
                        case Level.Low when ready:
                            System.Console.WriteLine("a");
                            break;
                        case Level.High:
                            System.Console.WriteLine("b");
                            break;
                    }
                }
            }
            """);

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

    /// <summary>Verifies a switch over an enum this assembly does not declare gains a catch-all instead.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>Naming every value of a framework enum is hundreds of dead sections, not a fix.</remarks>
    [Test]
    public async Task ForeignEnumGainsACatchAllAsync()
    {
        var test = new VerifyEnumSwitchStatementMappingFix.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            TestCode = """
                       public sealed class C
                       {
                           public int M(System.DayOfWeek day)
                           {
                               {|SST2242:switch|} (day)
                               {
                                   case System.DayOfWeek.Monday:
                                       return 1;
                               }

                               return 0;
                           }
                       }
                       """,
            FixedCode = """
                        public sealed class C
                        {
                            public int M(System.DayOfWeek day)
                            {
                                switch (day)
                                {
                                    case System.DayOfWeek.Monday:
                                        return 1;
                                    default:
                                        break;
                                }

                                return 0;
                            }
                        }
                        """,
        };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies the fix writes a section for each enum value the switch omits.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task MissingEnumCaseIsAddedAsync()
    {
        var test = new VerifyEnumSwitchStatementMappingFix.Test
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
            FixedCode = """
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
                                    case Color.Blue:
                                        break;
                                }

                                return 0;
                            }
                        }
                        """,
        };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies a value named only under a guard gains a catch-all rather than a second label.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>
    /// The guarded section handles the value when its guard holds, so an unguarded label for the same
    /// value reads as a duplicate of it, and the values the switch never names still go unhandled.
    /// </remarks>
    [Test]
    public async Task ValueNamedOnlyUnderAGuardGainsACatchAllAsync()
    {
        var test = new VerifyEnumSwitchStatementMappingFix.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            TestCode = """
                       public enum Step
                       {
                           None,
                           Adopt,
                           Push
                       }

                       public sealed class C
                       {
                           public string M(Step step, bool accepted)
                           {
                               {|SST2242:switch|} (step)
                               {
                                   case Step.Adopt when accepted:
                                       return "adopted";
                                   case Step.Push:
                                       return "pushed";
                               }

                               return "none";
                           }
                       }
                       """,
            FixedCode = """
                        public enum Step
                        {
                            None,
                            Adopt,
                            Push
                        }

                        public sealed class C
                        {
                            public string M(Step step, bool accepted)
                            {
                                switch (step)
                                {
                                    case Step.Adopt when accepted:
                                        return "adopted";
                                    case Step.Push:
                                        return "pushed";
                                    default:
                                        break;
                                }

                                return "none";
                            }
                        }
                        """,
        };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies the added label is written the way the file already names the value.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>
    /// One file compiled into two projects can sit in a different namespace in each, so a label rooted at
    /// the namespace of the compilation the fix ran in does not bind in the other.
    /// </remarks>
    [Test]
    public async Task AddedLabelUsesTheNameTheFileWritesAsync()
    {
        var test = new VerifyEnumSwitchStatementMappingFix.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            TestCode = """
                       namespace Widgets;

                       public enum Step
                       {
                           None,
                           Adopt
                       }

                       public sealed class C
                       {
                           public int M(Step step)
                           {
                               {|SST2242:switch|} (step)
                               {
                                   case Step.Adopt:
                                       return 1;
                               }

                               return 0;
                           }
                       }
                       """,
            FixedCode = """
                        namespace Widgets;

                        public enum Step
                        {
                            None,
                            Adopt
                        }

                        public sealed class C
                        {
                            public int M(Step step)
                            {
                                switch (step)
                                {
                                    case Step.Adopt:
                                        return 1;
                                    case Step.None:
                                        break;
                                }

                                return 0;
                            }
                        }
                        """,
        };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies several omitted values are stacked onto one section.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>A section per value gives the switch several bodies that do the same nothing.</remarks>
    [Test]
    public async Task SeveralMissingValuesShareOneSectionAsync()
    {
        var test = new VerifyEnumSwitchStatementMappingFix.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            TestCode = """
                       public enum Color
                       {
                           Red,
                           Blue,
                           Green
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
            FixedCode = """
                        public enum Color
                        {
                            Red,
                            Blue,
                            Green
                        }

                        public sealed class C
                        {
                            public int M(Color color)
                            {
                                switch (color)
                                {
                                    case Color.Red:
                                        return 1;
                                    case Color.Blue:
                                    case Color.Green:
                                        break;
                                }

                                return 0;
                            }
                        }
                        """,
        };

        await test.RunAsync(CancellationToken.None);
    }
}
