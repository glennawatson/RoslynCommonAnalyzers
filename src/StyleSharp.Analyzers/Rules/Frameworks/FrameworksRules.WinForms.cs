// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The SST2706 descriptor.</summary>
internal static partial class FrameworksRules
{
    /// <summary>SST2706 — a Windows Forms program entry point declares no single-threaded apartment state.</summary>
    public static readonly DiagnosticDescriptor StaThreadEntryPoint = Create(
        "SST2706",
        "Mark the Windows Forms entry point as single-threaded apartment",
        "The Windows Forms entry point '{0}' has neither [STAThread] nor [MTAThread]; add [STAThread] so clipboard, drag-and-drop, and common dialogs work at runtime",
        StaThreadEntryPointDescription);

    /// <summary>The SST2706 rule description.</summary>
    private const string StaThreadEntryPointDescription =
        "Windows Forms leans on COM for a set of everyday UI features — the clipboard, drag-and-drop, the "
        + "common file/color/font dialogs, and some ActiveX and shell integrations. COM single-threaded "
        + "apartment (STA) initialization happens on the thread that runs the program's entry point, and it is "
        + "driven by the apartment attribute on that entry point. When the entry point 'Main' carries neither "
        + "'[System.STAThread]' nor '[System.MTAThread]', the runtime starts the thread in a multithreaded "
        + "apartment, and those COM-backed features fail or misbehave at runtime — a clipboard call throws, a "
        + "file dialog hangs or returns nothing, drag-and-drop silently does not work. The failure is a runtime "
        + "one that a desktop test rarely catches, so the fix is to state the apartment explicitly. Only the "
        + "compilation's actual entry point is checked, and only when 'System.Windows.Forms.Application' is "
        + "referenced, so a class library or a non-UI program is never reported. A 'Main' method that is not the "
        + "entry point, or an entry point that already declares '[STAThread]' or '[MTAThread]', is left alone.";
}
