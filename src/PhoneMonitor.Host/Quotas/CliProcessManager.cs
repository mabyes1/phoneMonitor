using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace PhoneMonitor.Host.Quotas
{
    /// <summary>
    /// Terminates running CLI/desktop processes before an account switch.
    /// Codex only reads a single ~/.codex/auth.json; a running session's token
    /// refresh will clobber a freshly-swapped account, so the old process MUST be
    /// killed before switching (confirmed behavior, not just UX polish).
    /// </summary>
    internal static class CliProcessManager
    {
        // Process names (without .exe) to terminate per provider family.
        // codex has two flavors that share ~/.codex/auth.json: the npm/native binary
        // and the OpenAI desktop app — both run as "codex". "chatgpt" is the desktop
        // app which also refreshes the same credential.
        internal static readonly string[] AgyProcessNames = { "agy" };
        internal static readonly string[] CodexProcessNames = { "codex", "chatgpt", "ChatGPT" };

        /// <summary>
        /// Kills every running process matching any of the given names (case-insensitive,
        /// no extension) along with its child process tree. Returns the number killed.
        /// Never throws: a process that exits or is inaccessible mid-scan is skipped.
        /// </summary>
        internal static int KillByNames(IEnumerable<string> processNames)
        {
            if (processNames == null)
            {
                return 0;
            }

            var distinct = processNames
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => name.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var killed = 0;
            foreach (var name in distinct)
            {
                Process[] matches;
                try
                {
                    matches = Process.GetProcessesByName(name);
                }
                catch (InvalidOperationException)
                {
                    continue;
                }

                foreach (var process in matches)
                {
                    try
                    {
                        process.Kill(entireProcessTree: true);
                        process.WaitForExit(3000);
                        killed++;
                    }
                    catch (Exception ex) when (
                        ex is InvalidOperationException ||   // already exited
                        ex is System.ComponentModel.Win32Exception || // access denied / cannot terminate
                        ex is NotSupportedException)
                    {
                    }
                    finally
                    {
                        process.Dispose();
                    }
                }
            }

            return killed;
        }
    }
}
