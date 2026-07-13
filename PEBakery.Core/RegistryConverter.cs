/*
    Copyright (C) 2026-present Jonathan Holmgren (Homes32)
    Licensed under GPL 3.0
 
    PEBakery is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program.  If not, see <http://www.gnu.org/licenses/>.

    Additional permission under GNU GPL version 3 section 7

    If you modify this program, or any covered work, by linking
    or combining it with external libraries, containing parts
    covered by the terms of various license, the licensors of
    this program grant you additional permission to convey the
    resulting work. An external library is a library which is
    not derived from or based on this program. 
*/

using PEBakery.Helper;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

#region Info
// Converts between Windows .reg files and PEBakery RegWrite/RegWriteEx/RegDelete script syntax, in both directions. 
// Used by UtilityWindow.xaml.cs.
//
//   - String escaping (quotes, commas, '%', '#') is handled by the PEBakery.Core.StringEscaper.
//   - StringEscaper.QuoteEscape(string str, bool fullEscape = false, bool escapePercent = false)
//     always escapes '#' to '#$h' before applying any other escape, escapes quote/tab always,
//     escapes ',' and ' ' too when fullEscape is true, escapes '%' -> "#$p" when escapePercent is true,
//     then wraps the result in quotes if it still contains a literal ',' or ' '. It does NOT
//     escape embedded newlines - a raw '\r'/'\n' passes straight through, which would corrupt
//     PEBakery script syntax for a multi-line .reg value, so EscapeToken/UnescapeToken  in this
//     file layer PEBakery's own "#$x" line-break escape on top of QuoteEscape/QuoteUnescape themselves.
//     Called here with fullEscape=false, matching how the String Escaper tab itself calls
//     it (StringEscaper.QuoteEscape(str, false, flag)) -- a comma/space in an argument
//     ends up quote-wrapped rather than escaped in place, which is fine since PEBakery's
//     argument splitting is quote-aware.
//   - Because the character immediately after '#' in the escaped output is
//     always '$' (from "#$h"), the escaped text can never contain the "#1".."#9"/
//     "#a"/"#c"/"#r" pattern, so the risk of the script parser misreading valid registry strings
//     as legacy section parms can't actually occur in this converter's output, no matter what the compat option is set to.
//   - StringEscaper.QuoteUnescape(string str, bool escapePercent = false) strips one layer
//     of surrounding quotes (Trim('"')) and reverses the #$-style escapes in one call.
//   - RegHiveLoad/RegHiveUnload lines are recognized (script -> .reg direction) only so
//     that key paths written against an offline-loaded hive (e.g. "Tmp_System\...") can be
//     flagged. The real root hive (SYSTEM/SOFTWARE/NTUSER.DAT/etc.) the mounted hive
//     corresponds to is not recoverable from the script alone, so those entries are
//     output with a warning comment rather than guessing at the root.
//   - REGEDIT4 files will always be read using ANSI encoding, using the running sytems codepage. A warning will be flagged
//     so the user knows to double-check the codepage of the source system in the output is garbled.
//   - Regedit 5.00 files auto-detect encoding and always output as UTF-16 LE
//   - All other files auto-detect encoding and output as UTF-8 (No BOM)
//   - Registry commands inside conditional statement one-liners (If,Foo,Equals,Bar,RegWrite...) are skipped with a warning.
//   - Registry commands inside conditional statement blocks (If/Else) are extracted and converted, but commented out with a warning.
//   - Dynamic keys such as HKCC keys are rejected with a warning. HKU only accepts paths pointing to ".Default".
//     HKCR re-maps to "HKLM\Software\Classes". "HKLM\System\CurrentControlSet" remaps to "HKLM\System\ControlSet001"
//   - Any existing file comments are discarded during conversion.
//   - There is no way for the parser to understand if a windows environment variable in a registry string is from PEBakery 
//     or if its truly an environment var. (eg. %SystemRoot%, %WinDir%). For saftey, all will be treated like an unresolved
//     PEBakery variable and commented out in the results and a warning issued.
#endregion

namespace PEBakery.Core
{
    #region Public Options

    public sealed class RegConvertOptions
    {
        /// <summary>Passed through to StringEscaper as escapePercent, both escaping and unescaping.</summary>
        public bool EscapePercent { get; set; } = true;

        /// <summary>
        /// .reg -> script only. Rewrites every KeyPath and forces HiveShort to "HKLM" to
        /// reflect the entry landing under an offline-loaded hive, matching the
        /// RegHiveLoad variable-naming convention. The rewrite differs by source hive:
        /// HKLM paths already embed the hive file's name as their first segment, so that
        /// segment is renamed ("SYSTEM\..." -> "Tmp_SYSTEM\..."); other hives (HKCU, etc.)
        /// have no such segment, so one is synthesized ahead of the untouched path
        /// ("Software\Foo" -> "Tmp_Default\Software\Foo" for HKCU). See ApplyHivePrefix
        /// for the exact mapping. Sanitized via SanitizeHivePrefix before use; leave empty
        /// to disable both the path rewrite and the hive coercion.
        /// </summary>
        public string HivePrefix { get; set; } = "Tmp_";

        /// <summary>
        /// .reg -> script only, and only applied when HivePrefix rewriting is active.
        /// "CurrentControlSet" is a symlink the running kernel maintains at runtime,
        /// pointing at whichever ControlSetNNN is actually active (per
        /// HKLM\SYSTEM\Select\Current); it does not exist inside an offline-loaded SYSTEM
        /// hive file, which only contains the real numbered control sets. So any
        /// "System\CurrentControlSet\..." path is rewritten to use this control set name
        /// instead (default "ControlSet001", the common case for a freshly-installed
        /// system) before the HivePrefix rewrite runs. A warning is emitted for every
        /// entry this rewrite touches, since the actual active control set can only be
        /// confirmed by checking SYSTEM\Select\Current in the source hive.
        /// </summary>
        public string ControlSetOverride { get; set; } = "ControlSet001";
    }

    public sealed class ConversionResult
    {
        public string Output { get; set; } = string.Empty;
        public List<string> Warnings { get; } = new List<string>();

        /// <summary>
        /// The line-comment prefix for whichever format this result's Output is in;
        /// "//" for PEBakery script (from ConvertRegToScript), ";" for .reg (from
        /// ConvertScriptToReg). Set by the conversion method itself so callers formatting
        /// warnings (e.g. FormatWithWarnings) can't accidentally pass the wrong prefix for
        /// the direction they're calling.
        /// </summary>
        public string CommentPrefix { get; set; } = string.Empty;
    }
    #endregion

    #region Shared data model

    internal enum RegValueKind
    {
        None,          // REG_NONE            (0x0) - byte payload, usually (but not always) empty
        StringVal,     // REG_SZ              (0x1)
        ExpandString,  // REG_EXPAND_SZ       (0x2)
        Binary,        // REG_BINARY          (0x3)
        Dword,         // REG_DWORD           (0x4)
        MultiString,   // REG_MULTI_SZ        (0x7)
        Qword,         // REG_QWORD           (0x11)
        Other,         // any nonstandard / arbitrary hex(N) type
        DeleteKey,     // RegDelete with no ValueName / .reg's [-HIVE\Path]
        DeleteValue,   // RegDelete with a ValueName / .reg's "Name"=- (or @=-)
        CreateKey      // RegWrite with only HKey,ValueType,KeyPath (no ValueName/Value) - script -> .reg
                       // direction only. Creates the key itself with no value written, mirroring .reg's
                       // bare "[HIVE\Path]" section header with no value lines beneath it.
    }

    internal sealed class RegEntry
    {
        public string HiveShort = "HKLM";       // HKLM, HKCU, HKCR, HKU, HKCC
        public string KeyPath = string.Empty;   // subkey path, no leading/trailing backslash, hive not included
        public string ValueName = string.Empty; // "" == default ("@") value
        public int TypeId;                      // numeric registry type (may be nonstandard); unused for deletes
        public RegValueKind Kind;

        public string StringValue = string.Empty;               // StringVal / ExpandString
        public ulong NumericValue;                              // Dword / Qword
        public List<string> MultiStrings = new List<string>();  // MultiString
        public List<string> ByteTokens = new List<string>();    // None / Binary / Other - raw 2-hex-digit tokens

        public string? LoadedHiveNote; // set only by FlagLoadedHiveAmbiguity; describes the KEY, not any one value

        /// <summary>
        /// True if FlagPebakeryVariables found an unresolved "%Variable%" token anywhere in
        /// this entry's KeyPath, ValueName, StringValue, or MultiStrings (script -> .reg
        /// direction only). The literal, unexpanded text is still written into UnresolvedVarNote and
        /// into the .reg output for visibility, but since it isn't valid literal data, every
        /// physical line for this entry must also be commented out in the generated .reg file.
        /// </summary>
        public bool HasUnresolvedVariable;

        /// <summary>
        /// Set by FlagPebakeryVariables when HasUnresolvedVariable is true. Deliberately kept
        /// separate from LoadedHiveNote (rather than appended onto it) so the two stay
        /// independently printable: LoadedHiveNote describes the KEY as a whole (identical for
        /// every value sharing that KeyPath, e.g. loaded-hive ambiguity) and is safe to print
        /// once at the section header regardless of any individual entry's variable status.
        /// UnresolvedVarNote is per-entry, different values under the same key can
        /// reference different unresolved %Variables% so it always prints per-value.
        /// </summary>
        public string? UnresolvedVarNote;

        /// <summary>
        /// Raw "condition text (line N)" for a compound one-liner (e.g.
        /// "If,EXISTFILE,Foo (line 2)"), set by TagEntryWithSingleLineConditional.
        /// Null if this entry's RegWrite/RegWriteEx/RegDelete was a bare line, not a
        /// one-liner. Combined with EnclosingBlockDesc by the BlockNote property
        /// into a single readable sentence, rather than each being independently
        /// formatted and concatenated.
        /// </summary>
        internal string? SingleLineConditionalDesc;

        /// <summary>
        /// Raw description of the innermost enclosing If/Else Begin..End block (e.g.
        /// "Else (line 108, is paired with If at line 105)"), set by
        /// TagEntryWithBlockInfo and possibly extended afterward with a retroactive
        /// "- mutually exclusive with Else branch at line N" suffix once a later
        /// sibling Else is found. May also carry its own "[nested N levels deep]"
        /// suffix describing Begin..End block depth (see TagEntryWithBlockInfo) -
        /// a meaning of "nested" unrelated to whether a single-line one-liner is
        /// also present, which is why BlockNote below uses "Combined conditional",
        /// not "Nested conditional", for that separate case. Null if this entry
        /// doesn't sit inside any Begin..End block.
        /// </summary>
        internal string? EnclosingBlockDesc;

        /// <summary>
        /// Set (script -> .reg direction only) when this entry was extracted from
        /// inside a conditional, either a compound one-liner, an enclosing Begin..End
        /// block, or both at once. When both apply, they're combined into a single
        /// sentence ("Combined conditional: X inside Y") rather than two independently
        /// tagged clauses glued together, which would read as two stacked
        /// "[WARNING]" tags on one line. Null means the line was not inside any
        /// conditional. GenerateRegFile adds the single "[WARNING] " prefix once, at
        /// print time. See BlockFrame / TagEntryWithBlockInfo /
        /// TagEntryWithSingleLineConditional in ConvertScriptToReg.
        /// </summary>
        public string? BlockNote =>
            SingleLineConditionalDesc != null && EnclosingBlockDesc != null
                ? $"Combined conditional: {SingleLineConditionalDesc} inside {EnclosingBlockDesc}"
                : SingleLineConditionalDesc != null
                    ? $"Single-line conditional: {SingleLineConditionalDesc}"
                    : EnclosingBlockDesc;

        /// <summary>
        /// True if this entry's enclosing If/Else block was paired with a sibling
        /// Else/If branch that was ALSO extracted meaning the two sets of entries
        /// are mutually exclusive at runtime (only one branch ever executes), even
        /// though both get outputted (commented out) into the same .reg output.
        /// </summary>
        public bool BlockMutuallyExclusive;

        /// <summary>
        /// Name of the PEBakery script [Section] this entry's RegWrite/RegWriteEx/RegDelete
        /// line was found in, e.g. "Process" or "RegConfig" (script -> .reg direction only).
        /// Tracked purely by scanning top-to-bottom for [SectionName] headers as encountered
        /// in the source text. This does NOT resolve Run/RunEx call order, so it reflects
        /// the entry's literal position in the file, not execution order, which we won't determine.
        /// Null if no section header preceded the entry.
        /// </summary>
        public string? ScriptSection;
    }

    /// <summary>
    /// Tracks one currently-open (or since-closed, if still referenced for If/Else
    /// pairing) If/Else Begin..End block while scanning a script top-to-bottom in
    /// ConvertScriptToReg. Kind is "If" or "Else". Entries collects every RegEntry
    /// extracted while this frame was anywhere on the open-block stack (including
    /// entries from nested blocks inside it), so that if an Else is later found to
    /// pair with a closed If, the If's already-extracted entries can be retroactively
    /// flagged as BlockMutuallyExclusive.
    /// </summary>
    internal sealed class BlockFrame
    {
        public int Id;
        public string Kind = string.Empty; // "If" or "Else"
        public string HeaderText = string.Empty; // the raw line text that opened this block
        public int LineNumber;
        public BlockFrame? PairedIfFrame;   // set on an Else frame once paired with a preceding If
        public BlockFrame? PairedElseFrame; // set on an If frame once paired with a following Else
        public List<RegEntry> Entries = new List<RegEntry>();
    }

    #endregion

    public static partial class RegistryConverter
    {
        #region Static RegEx
        [GeneratedRegex("[^A-Za-z0-9_]")]
        private static partial Regex RegHivePrefixRegex();

        [GeneratedRegex(@"\bReg(Write(Ex)?|Delete)\b", RegexOptions.IgnoreCase, "en-US")]
        private static partial Regex RegInsideConditionalRegex();

        [GeneratedRegex(@"^\[(-?)([A-Za-z_]+)\\?(.*)\]$")]
        private static partial Regex SectionRegex();

        [GeneratedRegex(@"^(""(?:[^""\\]|\\.)*""|@)=(.*)$", RegexOptions.Singleline)]
        private static partial Regex ValueLineRegex();

        [GeneratedRegex(@"%[A-Za-z0-9_]+%")]
        private static partial Regex PebakeryVariableRegex();
        #endregion

        #region Hive/Type lookup

        /// <summary>
        /// Validates a hive token (accepts either short "HKLM" or long "HKEY_LOCAL_MACHINE"
        /// form, from either a .reg section header or a script's HKey argument) via
        /// RegistryHelper.ParseStringToRegHive, and returns it normalized to the canonical
        /// short form ("HKLM") that RegEntry.HiveShort uses internally.
        /// </summary>
        private static bool TryNormalizeHive(string token, [NotNullWhen(true)] out string? hiveShort)
        {
            RegistryHive? hive = RegistryHelper.ParseStringToRegHive(token);
            if (hive == null)
            {
                hiveShort = null;
                return false;
            }
            hiveShort = RegistryHelper.RegHiveToString(hive.Value);
            return hiveShort != null;
        }
        /// <summary>
        /// Expands a canonical short hive form ("HKLM") back to the long "HKEY_*" form a
        /// .reg file's [section header] needs.
        /// Falls back to the short form itself if somehow unrecognized.
        /// </summary>
        private static string HiveShortToFull(string hiveShort)
        {
            RegistryHive? hive = RegistryHelper.ParseStringToRegHive(hiveShort);
            if (hive == null)
                return hiveShort;
            return RegistryHelper.RegHiveToFullString(hive.Value) ?? hiveShort;
        }

        private static readonly Dictionary<RegistryValueKind, string> ValueKindToFriendlyName = new Dictionary<RegistryValueKind, string>
        {
            [RegistryValueKind.None] = "REG_NONE",
            [RegistryValueKind.String] = "REG_SZ",
            [RegistryValueKind.ExpandString] = "REG_EXPAND_SZ",
            [RegistryValueKind.Binary] = "REG_BINARY",
            [RegistryValueKind.DWord] = "REG_DWORD",
            [RegistryValueKind.MultiString] = "REG_MULTI_SZ",
            [RegistryValueKind.QWord] = "REG_QWORD",
        };

        /// <summary>Returns True (and the mapped internal Kind) if RegistryHelper recognizes typeId as one of the seven standard registry types.</summary>
        private static bool TryGetKindFromTypeId(int typeId, out RegValueKind kind)
        {
            RegistryValueKind? rvk = RegistryHelper.WBIntToValudKind(unchecked((uint)typeId));
            if (rvk == null)
            {
                kind = RegValueKind.Other;
                return false;
            }
            switch (rvk.Value)
            {
                case RegistryValueKind.None: kind = RegValueKind.None; return true;
                case RegistryValueKind.String: kind = RegValueKind.StringVal; return true;
                case RegistryValueKind.ExpandString: kind = RegValueKind.ExpandString; return true;
                case RegistryValueKind.Binary: kind = RegValueKind.Binary; return true;
                case RegistryValueKind.DWord: kind = RegValueKind.Dword; return true;
                case RegistryValueKind.MultiString: kind = RegValueKind.MultiString; return true;
                case RegistryValueKind.QWord: kind = RegValueKind.Qword; return true;
                default: kind = RegValueKind.Other; return false;
            }
        }

        private static RegValueKind KindFromTypeId(int typeId)
        {
            TryGetKindFromTypeId(typeId, out RegValueKind kind);
            return kind;
        }

        /// <summary>
        /// Formats a ValueType argument for generated script: the friendly "REG_XXX" name
        /// when RegistryHelper recognizes the type, otherwise a hex fallback (e.g."0x200000").
        /// </summary>
        private static string TypeIdToScriptArg(int typeId)
        {
            RegistryValueKind? rvk = RegistryHelper.WBIntToValudKind(unchecked((uint)typeId));
            if (rvk != null && ValueKindToFriendlyName.TryGetValue(rvk.Value, out string? friendly))
                return friendly;
            return "0x" + typeId.ToString("x");
        }

        /// <summary>
        /// Parses a script ValueType argument: a friendly "REG_XXX" name (via
        /// RegistryHelper.ParseValueKind + ValueKindToWBInt) or a "0x.."-prefixed hex value
        /// for nonstandard/arbitrary types (RegWriteEx).
        /// </summary>
        private static int ParseTypeToken(string tok)
        {
            tok = StripOuterQuotes(tok);

            RegistryValueKind? rvk = RegistryHelper.ParseValueKind(tok);
            if (rvk != null)
            {
                uint? wbInt = RegistryHelper.ValueKindToWBInt(rvk.Value);
                if (wbInt != null)
                    return unchecked((int)wbInt.Value);
            }

            if (tok.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                return Convert.ToInt32(tok.Substring(2), 16);

            return Convert.ToInt32(tok); // last resort: bare decimal
        }

        #endregion

        #region PEBakery escaping

        private static string EscapeToken(string raw, RegConvertOptions opt)
        {
            if (raw == null) raw = string.Empty;
            string escaped = StringEscaper.QuoteEscape(raw, false, opt.EscapePercent);
            // StringEscaper.QuoteEscape does NOT translate an embedded raw newline into
            // PEBakery's own line-break escape ("#$x") . It leaves literal '\r'/'\n' 
            // characters untouched. A multi-line .reg value (see MergeUnterminatedQuotedValues,
            // which folds a raw embedded line break into RegEntry.StringValue)
            // would otherwise emit a real newline into the middle of  a RegWrite line,
            // splitting one script command across multiple physical lines and producing invalid
            // PEBakery script syntax. Normalize every newline variant to "#$x" as the final step,
            // after QuoteEscape has already finished its own '#$h' escaping, so this literal
            // "#$x" isn't itself re-escaped into "#$h$x".
            return escaped.Replace("\r\n", "#$x").Replace("\r", "#$x").Replace("\n", "#$x");
        }

        /// <summary>
        /// Guarantees an EscapeToken result is wrapped in double quotes. QuoteEscape only
        /// adds its own surrounding quotes when the escaped text still contains a literal
        /// ',' or ' ' (see file header), so a KeyPath/ValueName/string Value with none of
        /// those (e.g. a plain "Software\Foo" or "DeviceId") would otherwise come out
        /// completely bare. Script output reads much more predictably, and is far less
        /// fragile against future edits to a value that happens to gain a space, if these
        /// three always carry explicit quotes. Does not double-wrap a token
        /// QuoteEscape already quoted for its own reasons.
        /// </summary>
        private static string ForceQuote(string escapedToken)
        {
            if (escapedToken.Length >= 2 && escapedToken[0] == '"' && escapedToken[^1] == '"')
                return escapedToken;
            return "\"" + escapedToken + "\"";
        }

        private static string UnescapeToken(string? token, RegConvertOptions opt)
        {
            if (token == null) return string.Empty;
            string unescaped = StringEscaper.QuoteUnescape(token, opt.EscapePercent);
            // Mirror image of EscapeToken's newline handling: reverse PEBakery's "#$x"
            // line-break escape back into a real newline character, so a script value
            // produced by EscapeToken's multi-line handling round-trips back into a .reg
            // file's original raw embedded-newline quirk (script -> .reg direction)
            // instead of surviving as the literal 3-character text "#$x". Applied after
            // QuoteUnescape so it operates on the same fully-unescaped text EscapeToken
            // started from.
            return unescaped.Replace("#$x", "\n");
        }

        /// <summary>
        /// Strip one layer of surrounding quotes from tokens that are never escaped (HKey,
        /// ValueType, numeric Dword/Qword, raw hex byte tokens). These can still be
        /// quoted by a script author for readability even though they hold no special chars.
        /// </summary>
        private static string StripOuterQuotes(string tok)
        {
            tok = tok.Trim();
            if (tok.Length >= 2 && tok.StartsWith("\"") && tok.EndsWith("\""))
                return tok.Substring(1, tok.Length - 2);
            return tok;
        }

        /// <summary>
        /// Parses a RegWrite DWORD/QWORD "script token" into its 32/64-bit value. This is
        /// purely about correctly decoding PEBakery's own number-literal syntax on the way
        /// in, it has no bearing on the .reg "output" format (that's handled separately by
        /// GenerateRegFile, which always outputs unsigned hex, e.g. "dword:XXXXXXXX", since
        /// that's the only DWORD/QWORD notation the .reg format itself supports.
        ///
        /// A script token like "-1332477852" is valid PEBakery syntax with a well-defined
        /// 32-bit value, so it needs to be decoded correctly rather than rejected. Since a
        /// DWORD/QWORD is just a fixed-width bit pattern with no inherent sign, "-1332477852"
        /// and "0xB0940064" denote the exact same bits, so ParseInt32/ParseInt64 (signed,
        /// also accepts a "0x..." hex literal) is tried first, falling back to
        /// ParseUInt32/ParseUInt64 for tokens too large to fit as signed. A token valid
        /// under both interpretations (e.g. "100") comes out identical either way; only
        /// tokens outside the signed range are affected by the fallback order.
        /// </summary>
        private static ulong ParseRegNumericToken(string token, RegValueKind kind)
        {
            if (kind == RegValueKind.Dword)
            {
                if (NumberHelper.ParseInt32(token, out int i32))
                    return unchecked((uint)i32);
                if (NumberHelper.ParseUInt32(token, out uint u32))
                    return u32;
                throw new FormatException(
                    $"'{token}' is not a valid DWORD (must fit as either a signed or unsigned 32-bit integer)");
            }
            else // Qword
            {
                if (NumberHelper.ParseInt64(token, out long i64))
                    return unchecked((ulong)i64);
                if (NumberHelper.ParseUInt64(token, out ulong u64))
                    return u64;
                throw new FormatException(
                    $"'{token}' is not a valid QWORD (must fit as either a signed or unsigned 64-bit integer)");
            }
        }

        #endregion

        #region Hive-prefix path rewriting (.reg -> script only)

        /// <summary>
        /// Keeps only [A-Za-z0-9_] so the prefix can't inject a backslash, comma, quote, or
        /// otherwise break the generated script's syntax. Returns "" (meaning "no rewrite")
        /// if nothing safe remains.
        /// </summary>
        public static string SanitizeHivePrefix(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return string.Empty;
            return RegHivePrefixRegex().Replace(raw.Trim(), "");
        }

        /// <summary>
        /// Maps a non-HKLM, non-HKU hive to the mount-name segment its offline-loaded hive
        /// file is conventionally given: HKCU resolves to NTUSER.DAT for the default
        /// profile ("Default"), and HKCR resolves to the Classes subtree of the SOFTWARE
        /// hive ("Software\Classes", NOT plain "Software", since HKCR paths have no
        /// "Classes" segment of their own to preserve). HKLM needs no entry here: its .reg
        /// paths already embed the hive file's own name as their first segment (SYSTEM\...,
        /// SOFTWARE\...), so that segment itself gets renamed rather than a new one being
        /// synthesized. HKU is handled separately (see RewriteHkuEntry) because, unlike
        /// HKCU/HKCR, its .reg paths already carry a per-user first segment (a SID, or
        /// ".DEFAULT") that must be inspected rather than blindly discarded.
        /// </summary>
        private static readonly Dictionary<string, string> NonHklmHiveMountNames =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["HKCU"] = "Default",
                ["HKCR"] = "Software\\Classes",
            };

        /// <summary>
        /// Rewrites the "CurrentControlSet" alias to a concrete control set name for HKLM
        /// paths shaped like "System\CurrentControlSet\...". Offline-loaded hives have no
        /// such alias, it's a live-kernel symlink, not a real registry key so a script
        /// that still says CurrentControlSet would silently create a bogus new top-level
        /// key under the offline hive instead of touching the real control set.
        /// Returns the path unchanged if it isn't an HKLM SYSTEM\CurrentControlSet path.
        /// </summary>
        private static string RewriteCurrentControlSetAlias(string hiveShort, string path, string controlSetName)
        {
            if (!hiveShort.Equals("HKLM", StringComparison.OrdinalIgnoreCase))
                return path;

            string[] segs = path.Split('\\');
            if (segs.Length < 2)
                return path;
            if (!segs[0].Equals("System", StringComparison.OrdinalIgnoreCase))
                return path;
            if (!segs[1].Equals("CurrentControlSet", StringComparison.OrdinalIgnoreCase))
                return path;

            segs[1] = controlSetName;
            return string.Join("\\", segs);
        }

        /// <summary>
        /// Rewrites a KeyPath to reflect where it lands once its hive is offline-loaded
        /// under HKLM, which differs by source hive:
        ///   - HKLM: the hive file name is already the path's first segment (a .reg path
        ///     under HKEY_LOCAL_MACHINE mirrors the real hive files: SYSTEM, SOFTWARE, ...),
        ///     so that existing segment itself is renamed:
        ///       "SYSTEM\ControlSet001\..."  ->  "Tmp_SYSTEM\ControlSet001\..."
        ///   - HKCU / HKCR: the .reg path has no hive-file segment to rename. HKCU's
        ///     "Software\Foo" is relative to NTUSER.DAT directly, with no "NTUSER" segment
        ///     present so a new segment naming the mounted hive is inserted ahead of the
        ///     untouched original path:
        ///       "Software\Foo"  ->  "Tmp_Default\Software\Foo"
        /// HKU is NOT handled here, see TryRewriteHkuKeyPath because its .reg paths
        /// already carry their own per-user first segment that must be validated rather
        /// than blindly overwritten. Note: for HKLM SYSTEM paths, RewriteCurrentControlSetAlias
        /// should be applied to the path before it reaches this function.
        /// </summary>
        private static string ApplyHivePrefix(string hiveShort, string path, string prefix)
        {
            if (prefix.Length == 0)
                return path;

            if (hiveShort.Equals("HKLM", StringComparison.OrdinalIgnoreCase))
            {
                if (path.Length == 0)
                    return path;
                int idx = path.IndexOf('\\');
                return idx < 0
                    ? prefix + path
                    : prefix + path.Substring(0, idx) + path.Substring(idx);
            }

            string mountName = NonHklmHiveMountNames.TryGetValue(hiveShort, out string? n) ? n : hiveShort;
            return path.Length == 0
                ? prefix + mountName
                : prefix + mountName + "\\" + path;
        }

        /// <summary>
        /// Rewrites an HKU entry's KeyPath for the offline-mounted default-user hive.
        ///
        /// Unlike HKCU/HKCR, an HKU .reg path already carries a per-user identifier as its
        /// own first segment, either ".DEFAULT" (the default user profile template, i.e.
        /// the NTUSER.DAT that WinPE setup offline-mounts) or a specific user's SID (a
        /// live/loaded hive with no fixed offline mount point). Only ".DEFAULT" can be
        /// mapped; any other first segment is left unrewritten and must be skipped by the
        /// caller, since silently rewriting an arbitrary SID's hive to the same
        /// Tmp_Default mount as the default profile would corrupt unrelated user data.
        /// </summary>
        /// <returns>
        /// true if <paramref name="newPath"/> was populated and the entry should be kept;
        /// false if the entry targets a non-.DEFAULT user and should be skipped.
        /// </returns>
        private static bool TryRewriteHkuKeyPath(string path, string prefix, out string newPath)
        {
            int idx = path.IndexOf('\\');
            string first = idx < 0 ? path : path.Substring(0, idx);
            string rest = idx < 0 ? string.Empty : path.Substring(idx + 1);

            if (!first.Equals(".DEFAULT", StringComparison.OrdinalIgnoreCase))
            {
                newPath = string.Empty;
                return false;
            }

            newPath = rest.Length == 0 ? prefix + "Default" : prefix + "Default" + "\\" + rest;
            return true;
        }

        #endregion

        #region .reg parsing (.reg  ->  PEBakery script)

        /// <summary>
        /// Merges '\' continuation lines into single logical lines and also returns the
        /// 1-based source line number each logical line *started* on (i.e. the first physical
        /// line of a continuation run), so callers can report accurate line numbers in warnings even after merging.
        /// </summary>
        private static List<(string Text, int LineNumber)> MergeContinuationLines(string text)
        {
            string[] rawLines = text.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');
            List<(string Text, int LineNumber)> merged = new List<(string, int)>();
            StringBuilder? current = null;
            int startLine = 0;

            for (int i = 0; i < rawLines.Length; i++)
            {
                string raw = rawLines[i];
                int lineNo = i + 1;
                string line = current == null ? raw : raw.TrimStart();
                if (current == null)
                {
                    current = new StringBuilder(line);
                    startLine = lineNo;
                }
                else
                {
                    current.Append(line);
                }

                string trimmedEnd = current.ToString().TrimEnd();
                if (trimmedEnd.EndsWith("\\") && !trimmedEnd.EndsWith("\\\\"))
                {
                    current = new StringBuilder(trimmedEnd.Substring(0, trimmedEnd.Length - 1).TrimEnd());
                    continue; // keep accumulating
                }

                merged.Add((current.ToString(), startLine));
                current = null;
            }
            if (current != null)
                merged.Add((current.ToString(), startLine));

            return merged;
        }

        /// <summary>
        /// True if data is a value's complete data portion. Non-string kinds (dword:,
        /// hex:, hex(n):) are always considered complete here since MergeContinuationLines
        /// already resolves their only legitimate continuation form (trailing '\'). A
        /// quoted string is complete only if its closing quote is unescaped and sits at
        /// the very end of data. Works correctly even when data contains embedded '\n'
        /// characters, since it's a plain character scan rather than a line/regex-based
        /// check.
        /// </summary>
        private static bool IsCompleteQuotedData(string data)
        {
            if (!data.StartsWith("\""))
                return true;
            if (data.Length < 2)
                return false; // lone opening quote
            for (int i = 1; i < data.Length; i++)
            {
                if (data[i] == '\\' && i + 1 < data.Length)
                {
                    i++;
                    continue;
                }
                if (data[i] == '"')
                    return i == data.Length - 1;
            }
            return false; // no unescaped closing quote found
        }

        /// <summary>
        /// If line has the shape "Name"=data or @=data, returns the data portion (whatever
        /// follows the first unescaped '='). A plain character scan rather than
        /// ValueLineRegex so it stays correct when line spans multiple embedded '\n'
        /// characters (ValueLineRegex's '.' doesn't match across those without
        /// RegexOptions.Singleline).
        /// </summary>
        private static bool TryParseNameEqualsData(string line, out string data)
        {
            data = string.Empty;
            if (line.Length == 0)
                return false;

            int idx;
            if (line[0] == '@')
            {
                idx = 1;
            }
            else if (line[0] == '"')
            {
                int i = 1;
                bool found = false;
                for (; i < line.Length; i++)
                {
                    if (line[i] == '\\' && i + 1 < line.Length) { i++; continue; }
                    if (line[i] == '"') { found = true; break; }
                }
                if (!found)
                    return false; // the NAME token itself is unterminated, not this quirk
                idx = i + 1;
            }
            else
            {
                return false;
            }

            if (idx >= line.Length || line[idx] != '=')
                return false;

            data = line.Substring(idx + 1);
            return true;
        }

        /// <summary>
        /// Recovers from a real-world .reg quirk: a quoted string value containing a raw,
        /// unescaped literal newline (rather than a properly escaped one since .reg's grammar
        /// has no legitimate multi-line string continuation, unlike hex:'s trailing-'\'
        /// continuation already handled by MergeContinuationLines). Seen from apps that
        /// export values like:
        ///   "DeviceId"="<Data>...</Data>
        ///
        ///   "
        /// Detects a value line whose data starts an unterminated quoted string and folds
        /// subsequent physical lines into it (preserving the embedded newline verbatim)
        /// until the quote actually closes. That embedded '\n' survives into
        /// RegEntry.StringValue and is converted to PEBakery's own "#$x" line-break escape
        /// by EscapeToken when the script is generated (StringEscaper.QuoteEscape alone
        /// does not do this).
        /// </summary>
        private static List<(string Text, int LineNumber)> MergeUnterminatedQuotedValues(
            List<(string Text, int LineNumber)> lines, List<string> warnings)
        {
            List<(string, int)> outLines = new List<(string, int)>(lines.Count);
            int i = 0;
            while (i < lines.Count)
            {
                string text = lines[i].Text;
                int lineNo = lines[i].LineNumber;
                string trimmed = text.Trim();

                if (TryParseNameEqualsData(trimmed, out string data) && !IsCompleteQuotedData(data))
                {
                    StringBuilder combined = new StringBuilder(text);
                    int j = i + 1;
                    bool closed = false;
                    while (j < lines.Count)
                    {
                        combined.Append('\n').Append(lines[j].Text);
                        j++;
                        string candTrimmed = combined.ToString().Trim();
                        if (TryParseNameEqualsData(candTrimmed, out string candData) && IsCompleteQuotedData(candData))
                        {
                            closed = true;
                            break;
                        }
                    }

                    if (closed)
                    {
                        outLines.Add((combined.ToString(), lineNo));
                    }
                    else
                    {
                        warnings.Add($"Line {lineNo}: Quoted string value's closing quote was never found (reached end of file while merging embedded-newline continuation). ({text})");
                    }
                    i = j;
                    continue;
                }

                outLines.Add((text, lineNo));
                i++;
            }
            return outLines;
        }

        private static string UnescapeRegString(string quoted)
        {
            // quoted excludes the surrounding double quotes already; .reg escaping is
            // always \\ and \" regardless of PEBakery conventions, so this stays independent  of StringEscaper.
            StringBuilder sb = new StringBuilder(quoted.Length);
            for (int i = 0; i < quoted.Length; i++)
            {
                if (quoted[i] == '\\' && i + 1 < quoted.Length)
                {
                    sb.Append(quoted[i + 1]);
                    i++;
                }
                else
                {
                    sb.Append(quoted[i]);
                }
            }
            return sb.ToString();
        }

        private static List<string> SplitHexTokens(string data)
        {
            return data.Split(',')
                        .Select(t => t.Trim())
                        .Where(t => t.Length > 0)
                        .ToList();
        }

        private static byte[] TokensToBytes(List<string> tokens)
        {
            byte[] bytes = new byte[tokens.Count];
            for (int i = 0; i < tokens.Count; i++)
                bytes[i] = Convert.ToByte(tokens[i], 16);
            return bytes;
        }

        /// <summary>
        /// Decodes a hex(n) byte payload (a single string value: hex(1)=REG_SZ hex form,
        /// hex(2)=REG_EXPAND_SZ) using the given text encoding, stripping one trailing NULL
        /// terminator sized to that encoding's char width (2 bytes for UTF-16, 1 byte for
        /// single-byte ANSI/ASCII) if present.
        /// </summary>
        /// <param name="stringEncoding">
        /// Encoding.Unicode for modern "Windows Registry Editor Version 5.00" files, where
        /// hex(n) string payloads are always UTF-16LE. Legacy REGEDIT4 files instead encode
        /// these payloads as single-byte ANSI text (the exporting machine's active codepage,
        /// approximated here by <see cref="EncodingHelper.DefaultAnsi"/>) decoding a
        /// REGEDIT4 payload as UTF-16 instead corrupts every string value in the file, since
        /// each pair of single ANSI bytes gets reinterpreted as one wide character.
        /// </param>
        private static string DecodeRegString(byte[] bytes, Encoding stringEncoding)
        {
            int nulWidth = stringEncoding.GetByteCount("\0");
            int len = bytes.Length;
            if (nulWidth <= len && bytes.Skip(len - nulWidth).All(b => b == 0))
                len -= nulWidth;
            return stringEncoding.GetString(bytes, 0, len);
        }

        /// <summary>
        /// Decodes a hex(7) REG_MULTI_SZ byte payload (a NULL-separated run of strings, itself
        /// terminated by a final extra NULL) using the given text encoding. See
        /// <see cref="DecodeRegString"/> for why this must match the file's actual format
        /// (UTF-16 for modern .reg, single-byte ANSI for legacy REGEDIT4) rather than always
        /// assuming UTF-16.
        /// </summary>
        private static List<string> DecodeRegMultiString(byte[] bytes, Encoding stringEncoding)
        {
            string full = stringEncoding.GetString(bytes);
            // Split on NULL, drop trailing empty entries created by the double-NULL terminator.
            List<string> parts = full.Split('\0').ToList();
            while (parts.Count > 0 && parts[parts.Count - 1].Length == 0)
                parts.RemoveAt(parts.Count - 1);
            return parts;
        }

        public static ConversionResult ConvertRegToScript(string regText, RegConvertOptions? opt = null)
        {
            opt ??= new RegConvertOptions();
            ConversionResult result = new ConversionResult { CommentPrefix = "//" };
            List<(string Text, int LineNumber)> lines = MergeContinuationLines(regText);
            lines = MergeUnterminatedQuotedValues(lines, result.Warnings);

            // Detect which .reg header this file uses. This matters beyond just skipping the
            // header line: legacy REGEDIT4 files encode hex(1)/hex(2)/hex(7) string payloads
            // as single-byte ANSI text, while modern "Windows Registry Editor Version 5.00"
            // files always encode them as UTF-16LE decoding one format's bytes with the
            // other's encoding corrupts every string value in the file. The header is the
            // only reliable signal for which one a given file actually is.
            // Skipped since a hand-edited file may have either preceding the header.
            bool isLegacyRegedit4 = lines
                .Select(l => l.Text.Trim())
                .FirstOrDefault(t => t.Length > 0 && !t.StartsWith(";"))
                ?.Equals("REGEDIT4", StringComparison.OrdinalIgnoreCase) == true;
            Encoding stringPayloadEncoding = isLegacyRegedit4 ? EncodingHelper.DefaultAnsi : Encoding.Unicode;
            if (isLegacyRegedit4)
            {
                result.Warnings.Add($"Detected a legacy REGEDIT4 header: hex(1)/hex(2)/hex(7) string values were decoded using " +
                    $"this machine's active ANSI codepage ({stringPayloadEncoding.WebName}), since REGEDIT4 has no way to " +
                    "declare its own codepage. If the file was exported on a machine with a different codepage, affected " +
                    "string values may come out with garbled. If this happens the file should be verified manually.");
            }

            string? currentHiveShort = null;
            string? currentKeyPath = null;
            List<RegEntry> entries = new List<RegEntry>();

            foreach ((string rawLine, int lineNo) in lines)
            {
                string line = rawLine.Trim();
                if (line.Length == 0) continue;
                if (line.StartsWith(";")) continue; // .reg comment
                if (line.Equals("Windows Registry Editor Version 5.00", StringComparison.OrdinalIgnoreCase)) continue;
                if (line.Equals("REGEDIT4", StringComparison.OrdinalIgnoreCase)) continue;

                Match secM = SectionRegex().Match(line);
                if (secM.Success)
                {
                    bool isDelete = secM.Groups[1].Value == "-";
                    string hivePart = secM.Groups[2].Value;
                    string rest = secM.Groups[3].Value;

                    if (!TryNormalizeHive(hivePart, out currentHiveShort))
                    {
                        result.Warnings.Add($"Line {lineNo}: Unrecognized hive '{hivePart}' in section header; skipping section.");
                        currentHiveShort = null;
                        currentKeyPath = null;
                        continue;
                    }
                    currentKeyPath = rest;

                    if (isDelete)
                    {
                        // [-HIVE\Path] whole-key deletion, maps directly to RegDelete with no ValueName.
                        entries.Add(new RegEntry
                        {
                            HiveShort = currentHiveShort,
                            KeyPath = currentKeyPath,
                            ValueName = "",
                            Kind = RegValueKind.DeleteKey
                        });
                    }
                    continue;
                }

                if (currentHiveShort == null || currentKeyPath == null)
                {
                    result.Warnings.Add($"Line {lineNo}: Unrecognized data before section header, skipped: {line}");
                    continue;
                }

                Match valM = ValueLineRegex().Match(line);
                if (!valM.Success)
                {
                    result.Warnings.Add($"Line {lineNo}: Could not parse line, skipped: {line}");
                    continue;
                }

                string nameToken = valM.Groups[1].Value;
                string valueName = nameToken == "@" ? "" : UnescapeRegString(nameToken.Substring(1, nameToken.Length - 2));
                string data = valM.Groups[2].Value.Trim();

                RegEntry entry = new RegEntry
                {
                    HiveShort = currentHiveShort,
                    KeyPath = currentKeyPath,
                    ValueName = valueName
                };

                try
                {
                    if (data == "-")
                    {
                        // "Name"=- (or @=-) single-value deletion, maps to RegDelete with ValueName.
                        entry.Kind = RegValueKind.DeleteValue;
                    }
                    else if (data.StartsWith("\""))
                    {
                        string inner = data.Substring(1, data.Length - 2);
                        entry.TypeId = 0x1;
                        entry.Kind = RegValueKind.StringVal;
                        entry.StringValue = UnescapeRegString(inner);
                    }
                    else if (data.StartsWith("dword:", StringComparison.OrdinalIgnoreCase))
                    {
                        entry.TypeId = 0x4;
                        entry.Kind = RegValueKind.Dword;
                        entry.NumericValue = Convert.ToUInt32(data.Substring(6).Trim(), 16);
                    }
                    else if (data.StartsWith("hex(", StringComparison.OrdinalIgnoreCase))
                    {
                        int close = data.IndexOf(')');
                        int colon = data.IndexOf(':', close);
                        int typeId = Convert.ToInt32(data.Substring(4, close - 4), 16);
                        List<string> tokens = SplitHexTokens(data.Substring(colon + 1));
                        FillEntryFromTypedBytes(entry, typeId, tokens, stringPayloadEncoding);
                    }
                    else if (data.StartsWith("hex:", StringComparison.OrdinalIgnoreCase))
                    {
                        List<string> tokens = SplitHexTokens(data.Substring(4));
                        FillEntryFromTypedBytes(entry, 0x3, tokens, stringPayloadEncoding);
                    }
                    else
                    {
                        result.Warnings.Add($"Line {lineNo}: Unrecognized value data, skipped: {line}");
                        continue;
                    }
                }
                catch (Exception ex)
                {
                    result.Warnings.Add($"Line {lineNo}: Failed to parse value '{(valueName.Length == 0 ? "@" : valueName)}' under {currentKeyPath}: {ex.Message}");
                    continue;
                }

                entries.Add(entry);
            }

            // Hive-prefix path rewrite (.reg -> script direction only).
            //
            // An offline-loaded hive is always mounted under HKLM (reg.exe / RegHiveLoad
            // have no ability to mount elsewhere), regardless of which hive the source
            // .reg file's paths referenced (HKCU, HKU, etc. e.g. a NTUSER.DAT export is
            // still HKCU in the .reg but becomes HKLM\Tmp_X\... once loaded offline). So
            // rewriting KeyPath without also forcing HiveShort to HKLM leaves the original
            // hive in place, producing bogus paths like HKCU\Tmp_Default\... instead of
            // HKLM\Tmp_Default\....
            string hivePrefix = SanitizeHivePrefix(opt.HivePrefix);
            if (hivePrefix.Length > 0)
            {
                List<RegEntry> rewritten = new List<RegEntry>(entries.Count);
                bool controlSetWarningEmitted = false; // <-- must be HERE, outside the foreach below

                foreach (RegEntry e in entries)
                {
                    if (e.HiveShort.Equals("HKU", StringComparison.OrdinalIgnoreCase))
                    {
                        if (TryRewriteHkuKeyPath(e.KeyPath, hivePrefix, out string hkuPath))
                        {
                            e.KeyPath = hkuPath;
                            e.HiveShort = "HKLM";
                            rewritten.Add(e);
                        }
                        else
                        {
                            result.Warnings.Add($"Skipped entry under 'HKU\\{e.KeyPath}': only HKU\\.DEFAULT maps to the offline-mounted default-user hive; other HKU keys are per-SID user hives with no fixed offline mount point.");
                        }
                        continue;
                    }

                    if (e.HiveShort.Equals("HKCC", StringComparison.OrdinalIgnoreCase))
                    {
                        result.Warnings.Add($"Skipped entry under 'HKEY_CURRENT_CONFIG\\{e.KeyPath}': HKCC is a live-kernel view composed from the current hardware profile under HKLM\\SYSTEM\\CurrentControlSet\\Hardware Profiles\\Current; it has no fixed offline hive or mount point to write to safely.");
                        continue;
                    }

                    string origPath = e.KeyPath;
                    string aliasFixed = RewriteCurrentControlSetAlias(e.HiveShort, origPath, opt.ControlSetOverride);
                    if (aliasFixed != origPath && !controlSetWarningEmitted)
                    {
                        result.Warnings.Add($"Rewrote 'CurrentControlSet' to '{opt.ControlSetOverride}' in 'HKLM\\{origPath}' (and any other affected entries): " +
                            "an offline-loaded SYSTEM hive has no CurrentControlSet symlink (it's a live-kernel alias, not a real key); verify this matches the " +
                            "hive's actual active control set (see SYSTEM\\Select\\Current) if it isn't ControlSet001.");
                        controlSetWarningEmitted = true;
                    }

                    e.KeyPath = ApplyHivePrefix(e.HiveShort, aliasFixed, hivePrefix);
                    e.HiveShort = "HKLM";
                    rewritten.Add(e);
                }
                entries = rewritten;
            }

            result.Output = GenerateScript(entries, opt, result.Warnings);
            return result;
        }

        private static void FillEntryFromTypedBytes(RegEntry entry, int typeId, List<string> tokens, Encoding stringEncoding)
        {
            entry.TypeId = typeId;
            entry.Kind = KindFromTypeId(typeId);
            byte[] bytes = TokensToBytes(tokens);

            switch (entry.Kind)
            {
                case RegValueKind.StringVal:
                    entry.StringValue = DecodeRegString(bytes, stringEncoding);
                    break;
                case RegValueKind.ExpandString:
                    entry.StringValue = DecodeRegString(bytes, stringEncoding);
                    break;
                case RegValueKind.Dword:
                    entry.NumericValue = bytes.Length >= 4
                        ? BitConverter.ToUInt32(bytes, 0)
                        : 0;
                    break;
                case RegValueKind.Qword:
                    entry.NumericValue = bytes.Length >= 8
                        ? BitConverter.ToUInt64(bytes, 0)
                        : 0;
                    break;
                case RegValueKind.MultiString:
                    entry.MultiStrings = DecodeRegMultiString(bytes, stringEncoding);
                    break;
                case RegValueKind.None:
                    // REG_NONE is *usually* zero-length, but the registry API technically
                    // allows REG_NONE with a nonzero payload. Preserve it rather than
                    // silently discarding it, same as Binary/Other below.
                    entry.ByteTokens = tokens;
                    break;
                case RegValueKind.Binary:
                case RegValueKind.Other:
                default:
                    entry.ByteTokens = tokens;
                    break;
            }
        }

        private static string GenerateScript(List<RegEntry> entries, RegConvertOptions opt, List<string> warnings)
        {
            StringBuilder sb = new StringBuilder();

            foreach (RegEntry e in entries)
            {
                string keyArg = ForceQuote(EscapeToken(e.KeyPath, opt));

                if (e.Kind == RegValueKind.DeleteKey)
                {
                    sb.AppendLine($"RegDelete,{e.HiveShort},{keyArg}");
                    continue;
                }

                string nameArg = e.ValueName.Length == 0 ? "\"\"" : ForceQuote(EscapeToken(e.ValueName, opt));

                if (e.Kind == RegValueKind.DeleteValue)
                {
                    sb.AppendLine($"RegDelete,{e.HiveShort},{keyArg},{nameArg}");
                    continue;
                }

                bool isStandard = TryGetKindFromTypeId(e.TypeId, out _);
                string cmd = isStandard ? "RegWrite" : "RegWriteEx";
                string typeArg = TypeIdToScriptArg(e.TypeId);

                switch (e.Kind)
                {
                    case RegValueKind.CreateKey:
                        // HKey,ValueType,KeyPath only. No ValueName/Value arguments supplied,
                        // so we write this out as a bare "[HIVE\Path]" .reg section header with
                        // no value lines under it.
                        sb.AppendLine($"{cmd},{e.HiveShort},{typeArg},{keyArg}");
                        break;
                    case RegValueKind.StringVal:
                    case RegValueKind.ExpandString:
                        {
                            string valArg = ForceQuote(EscapeToken(e.StringValue, opt));
                            sb.AppendLine($"{cmd},{e.HiveShort},{typeArg},{keyArg},{nameArg},{valArg}");
                            break;
                        }
                    case RegValueKind.Dword:
                    case RegValueKind.Qword:
                        sb.AppendLine($"{cmd},{e.HiveShort},{typeArg},{keyArg},{nameArg},{e.NumericValue}");
                        break;
                    case RegValueKind.MultiString:
                        {
                            List<string> args = e.MultiStrings
                                .Select(s => ForceQuote(EscapeToken(s, opt)))
                                .ToList();
                            AppendWrappedCommand(sb, cmd, e.HiveShort, typeArg, keyArg, nameArg, args);
                            break;
                        }
                    case RegValueKind.None:
                    case RegValueKind.Binary:
                    case RegValueKind.Other:
                    default:
                        AppendWrappedCommand(sb, cmd, e.HiveShort, typeArg, keyArg, nameArg, e.ByteTokens);
                        break;
                }
            }

            return sb.ToString();
        }

        private static void AppendWrappedCommand(StringBuilder sb, string cmd, string hive, string typeArg,
            string keyArg, string nameArg, List<string> trailingArgs)
        {
            string head = $"{cmd},{hive},{typeArg},{keyArg},{nameArg},";
            if (trailingArgs.Count == 0)
            {
                // Value is a required argument. output empty string for no value
                sb.AppendLine(head + "\"\"");
                return;
            }

            // PEBakery CodeParser.ParseCommand Trim()s whitespace
			//every raw line, including continuation lines before parsing it 
            // so its safe to use regedit's 2-space indent for .script as well.
            AppendWrappedTokens(sb, head, trailingArgs, "  ");
        }
        #endregion

        #region Script parsing (PEBakery script  ->  .reg)

        private static List<string> SplitArgsRespectingQuotes(string argsText)
        {
            // NOTE: quotes are intentionally NOT stripped here. UnescapeToken() /
            // StringEscaper.QuoteUnescape() expects the token exactly as it appeared in
            // source, surrounding quotes included, so it can tell a quoted empty string
            // ("") apart from a genuinely empty/missing token.
            List<string> tokens = new List<string>();
            StringBuilder cur = new StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < argsText.Length; i++)
            {
                char c = argsText[i];
                if (c == '"')
                {
                    inQuotes = !inQuotes;
                    cur.Append(c);
                }
                else if (c == ',' && !inQuotes)
                {
                    tokens.Add(cur.ToString().Trim());
                    cur.Clear();
                }
                else
                {
                    cur.Append(c);
                }
            }
            tokens.Add(cur.ToString().Trim());
            return tokens;
        }

        public static ConversionResult ConvertScriptToReg(string scriptText, RegConvertOptions? opt = null)
        {
            opt ??= new RegConvertOptions();
            ConversionResult result = new ConversionResult { CommentPrefix = ";" };
            List<(string Text, int LineNumber)> lines = MergeContinuationLines(scriptText);
            List<RegEntry> entries = new List<RegEntry>();
            HashSet<string> loadedHiveVars = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            HashSet<string> warnedHiveVars = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            HashSet<string> warnedPebakeryVars = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string? currentScriptSection = null;

            // If/Else Begin..End nesting tracking (see BlockFrame). openBlocks is the stack
            // of currently-open blocks; lastClosedIfAtDepth remembers the most recently
            // closed If block at each nesting depth, so a following Else,Begin at that same
            // depth can be recognized as its mutually exclusive sibling.
            List<BlockFrame> openBlocks = new List<BlockFrame>();
            Dictionary<int, BlockFrame> lastClosedIfAtDepth = new Dictionary<int, BlockFrame>();
            int nextBlockId = 1;

            foreach ((string rawLine, int lineNo) in lines)
            {
                string line = rawLine.Trim();
                if (line.Length == 0) continue;
                if (line.StartsWith("//")) continue;
                if (line.StartsWith("[") && line.EndsWith("]"))
                {
                    currentScriptSection = line.Substring(1, line.Length - 2).Trim();
                    continue; // section header, e.g. [Process]
                }

                int firstComma = line.IndexOf(',');
                string cmd = (firstComma < 0 ? line : line.Substring(0, firstComma)).Trim();

                if (cmd.Equals("End", StringComparison.OrdinalIgnoreCase))
                {
                    if (openBlocks.Count > 0)
                    {
                        BlockFrame closed = openBlocks[^1];
                        openBlocks.RemoveAt(openBlocks.Count - 1);
                        if (closed.Kind.Equals("If", StringComparison.OrdinalIgnoreCase))
                            lastClosedIfAtDepth[openBlocks.Count] = closed;
                    }
                    continue;
                }

                if ((cmd.Equals("If", StringComparison.OrdinalIgnoreCase) || cmd.Equals("Else", StringComparison.OrdinalIgnoreCase)) &&
                    line.TrimEnd().EndsWith(",Begin", StringComparison.OrdinalIgnoreCase))
                {
                    // Only a block-opening If/Else,Begin is tracked here. A compound
                    // one-liner (If,...,RegWrite,...,End all on one physical line) doesn't
                    // end in ",Begin" and falls through to the RegInsideConditionalRegex
                    // check further below instead, unaffected by this stack.
                    int depth = openBlocks.Count;
                    BlockFrame frame = new BlockFrame
                    {
                        Id = nextBlockId++,
                        Kind = cmd,
                        HeaderText = line,
                        LineNumber = lineNo
                    };

                    if (cmd.Equals("Else", StringComparison.OrdinalIgnoreCase) &&
                        lastClosedIfAtDepth.TryGetValue(depth, out BlockFrame? pairedIf))
                    {
                        // Consume the slot immediately. Without this, an already-paired If
                        // frame stays sitting in lastClosedIfAtDepth[depth] forever; only an
                        // "If" close ever overwrites the slot, an "Else" close does not so a
                        // LATER, unrelated Else,Begin at the same depth would incorrectly
                        // re-pair with this same already-paired If and re-append a second,
                        // bogus "mutually exclusive" clause to every entry already extracted
                        // from it.
                        lastClosedIfAtDepth.Remove(depth);

                        frame.PairedIfFrame = pairedIf;
                        pairedIf.PairedElseFrame = frame;
                        // Retroactively flag the If branch's already-extracted entries:
                        // we only learn they're mutually exclusive with a sibling Else now.
                        foreach (RegEntry e in pairedIf.Entries)
                        {
                            e.BlockMutuallyExclusive = true;
                            e.EnclosingBlockDesc += $" and is mutually exclusive with the Else branch at line {lineNo}";
                        }
                    }

                    openBlocks.Add(frame);
                    continue;
                }

                if (cmd.Equals("RegHiveLoad", StringComparison.OrdinalIgnoreCase))
                {
                    List<string> a = SplitArgsRespectingQuotes(line.Substring(firstComma + 1));
                    if (a.Count >= 1) loadedHiveVars.Add(StripOuterQuotes(a[0]));
                    continue;
                }
                if (cmd.Equals("RegHiveUnLoad", StringComparison.OrdinalIgnoreCase) ||
                    cmd.Equals("RegHiveUnload", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (cmd.Equals("RegMulti", StringComparison.OrdinalIgnoreCase))
                {
                    result.Warnings.Add($"Line {lineNo}: RegMulti can only modify an existing value and has no way of knowing the full value contents" +
                        $" of the key; it cannot be reconstructed into a static .reg entry. ({line})");
                    continue;
                }

                bool isWrite = cmd.Equals("RegWrite", StringComparison.OrdinalIgnoreCase);
                bool isWriteEx = cmd.Equals("RegWriteEx", StringComparison.OrdinalIgnoreCase);
                bool isDelete = cmd.Equals("RegDelete", StringComparison.OrdinalIgnoreCase);

                // Set only when this line was a compound one-liner (e.g.
                // "If,EXISTFILE,Foo,If,%Blah%,Equal,True,RegWrite,...") rather than a bare
                // RegWrite/RegWriteEx/RegDelete line. Holds the condition text that preceded
                // the embedded Reg command, for the BlockNote attached below.
                string? singleLineConditionalPrefix = null;

                if (!isWrite && !isWriteEx && !isDelete)
                {
                    Match condMatch = RegInsideConditionalRegex().Match(line);
                    if (!condMatch.Success)
                        continue; // not a command we handle at all (Echo, plain If, etc.)

                    // Reparse from the embedded Reg command onward as if it were its own
                    // line, same SplitArgsRespectingQuotes-based extraction below then
                    // applies unchanged. The condition text in front of it is kept only to
                    // label the resulting entry; this converter still can't evaluate it.
                    singleLineConditionalPrefix = line[..condMatch.Index].TrimEnd().TrimEnd(',');
                    line = line[condMatch.Index..];
                    firstComma = line.IndexOf(',');
                    cmd = (firstComma < 0 ? line : line[..firstComma]).Trim();
                    isWrite = cmd.Equals("RegWrite", StringComparison.OrdinalIgnoreCase);
                    isWriteEx = cmd.Equals("RegWriteEx", StringComparison.OrdinalIgnoreCase);
                    isDelete = cmd.Equals("RegDelete", StringComparison.OrdinalIgnoreCase);
                }

                List<string> args = SplitArgsRespectingQuotes(line.Substring(firstComma + 1));

                if (isDelete)
                {
                    if (args.Count < 2)
                    {
                        result.Warnings.Add($"Line {lineNo}: Malformed RegDelete line. ({line})");
                        continue;
                    }

                    if (!TryNormalizeHive(StripOuterQuotes(args[0]), out string? delHive))
                    {
                        result.Warnings.Add($"Line {lineNo}: Unrecognized HKEY '{args[0]}'. ({line})");
                        continue;
                    }

                    string delKeyPath = UnescapeToken(args[1], opt);
                    // RegDelete's ValueName is optional; a 3rd argument present (even "" for
                    // the default value) means "delete this value", not "delete the key".
                    RegEntry delEntry;
                    if (args.Count >= 3)
                    {
                        delEntry = new RegEntry
                        {
                            HiveShort = delHive,
                            KeyPath = delKeyPath,
                            ValueName = UnescapeToken(args[2], opt),
                            Kind = RegValueKind.DeleteValue
                        };
                    }
                    else
                    {
                        delEntry = new RegEntry
                        {
                            HiveShort = delHive,
                            KeyPath = delKeyPath,
                            ValueName = "",
                            Kind = RegValueKind.DeleteKey
                        };
                    }

                    FlagLoadedHiveAmbiguity(delEntry, loadedHiveVars, warnedHiveVars, result.Warnings, lineNo);
                    FlagPebakeryVariables(delEntry, warnedPebakeryVars, result.Warnings, lineNo);
                    delEntry.ScriptSection = currentScriptSection;
                    if (singleLineConditionalPrefix != null)
                        TagEntryWithSingleLineConditional(delEntry, singleLineConditionalPrefix, lineNo);
                    TagEntryWithBlockInfo(delEntry, openBlocks);
                    entries.Add(delEntry);
                    continue;
                }

                bool nowarnFlag = args.Count > 0 && StripOuterQuotes(args[args.Count - 1]).Equals("NOWARN", StringComparison.OrdinalIgnoreCase);
                if (nowarnFlag) args.RemoveAt(args.Count - 1); // Strip NOWARN flags; we never output it ourselves

                // HKey,ValueType,KeyPath are the only required arguments. ValueName and Value
                // may both be omitted entirely, which is legal syntax that creates the key
                // itself with no value written. Equivalent to a bare "[HIVE\Path]" .reg
                // section header with no value lines beneath it. ValueName alone (4 args, no
                // Value) is still legal too and is handled further below by each Kind's
                // existing trailing-args defaulting.
                if (args.Count < 3)
                {
                    result.Warnings.Add($"Line {lineNo}: Malformed {cmd} line. ({line})");
                    continue;
                }

                if (!TryNormalizeHive(StripOuterQuotes(args[0]), out string? hiveShort))
                {
                    result.Warnings.Add($"Line {lineNo}: Unrecognized HKEY '{args[0]}'. ({line})");
                    continue;
                }

                int typeId;
                try { typeId = ParseTypeToken(args[1]); }
                catch
                {
                    result.Warnings.Add($"Line {lineNo}: Could not parse ValueType '{args[1]}'. ({line})");
                    continue;
                }

                string keyPath = UnescapeToken(args[2], opt);
                bool keyOnly = args.Count < 4; // no ValueName argument present at all

                RegEntry entry = new RegEntry
                {
                    HiveShort = hiveShort,
                    KeyPath = keyPath,
                    ValueName = keyOnly ? string.Empty : UnescapeToken(args[3], opt),
                    TypeId = typeId,
                    Kind = keyOnly ? RegValueKind.CreateKey : KindFromTypeId(typeId)
                };

                FlagLoadedHiveAmbiguity(entry, loadedHiveVars, warnedHiveVars, result.Warnings, lineNo);
                entry.ScriptSection = currentScriptSection;

                if (keyOnly)
                {
                    FlagPebakeryVariables(entry, warnedPebakeryVars, result.Warnings, lineNo);
                    if (singleLineConditionalPrefix != null)
                        TagEntryWithSingleLineConditional(entry, singleLineConditionalPrefix, lineNo);
                    TagEntryWithBlockInfo(entry, openBlocks);
                    entries.Add(entry);
                    continue;
                }

                string valueName = entry.ValueName;
                List<string> trailing = args.Skip(4).ToList();

                try
                {
                    switch (entry.Kind)
                    {
                        case RegValueKind.StringVal:
                        case RegValueKind.ExpandString:
                            entry.StringValue = UnescapeToken(trailing.Count > 0 ? trailing[0] : "\"\"", opt);
                            break;
                        case RegValueKind.Dword:
                        case RegValueKind.Qword:
                            string rawToken = trailing.Count > 0 ? trailing[0] : "0";
                            string strippedToken = StripOuterQuotes(rawToken);
                            if (PebakeryVariableRegex().IsMatch(strippedToken))
                            {
                                // A %Variable% can't be resolved to a number at conversion time,
                                // and parsing would throw an exception and result in a
                                // "Failed to parse value" warning and drop the whole line.
                                // Instead, stash the literal unresolved text in StringValue so
                                // FlagPebakeryVariables can detect and flag it the same way it already does for
                                // string/multi-string values.
                                entry.StringValue = UnescapeToken(rawToken, opt);
                                entry.NumericValue = 0;
                            }
                            else
                            {
                                entry.NumericValue = ParseRegNumericToken(strippedToken, entry.Kind);
                            }
                            break;
                        case RegValueKind.MultiString:
                            entry.MultiStrings = trailing.Select(t => UnescapeToken(t, opt)).ToList();
                            break;
                        case RegValueKind.None:
                        case RegValueKind.Binary:
                        case RegValueKind.Other:
                        default:
                            // A byte-list argument normally arrives as N separate raw hex
                            // tokens (9E,3E,07,...), where SplitArgsRespectingQuotes
                            // already separates each byte at the top level since they're
                            // unquoted. But a hand-written or third-party-generated script can
                            // instead bundle the whole byte list into a SINGLE quoted,
                            // PEBakery-escaped argument (using "#$c" to represent each internal
                            // comma, exactly like StringVal/MultiString values already do),
                            // so SplitArgsRespectingQuotes sees it as one token, not several.
                            // Just stripping quotes would leave one bogus "byte" with literal "#$c"
                            // text embedded in it Unescaping every token first (reversing #$h/#$c/#$s/#$p/#$x
                            // back to their literal characters, same as UnescapeToken already
                            // does for string values) and THEN re-splitting on any comma that
                            // surfaces handles both shapes uniformly: normal unescaped tokens
                            // have no comma to split on and pass through unchanged, while a
                            // bundled single-argument byte list correctly expands back into its
                            // individual byte tokens.
                            entry.ByteTokens = trailing
                                .Select(t => UnescapeToken(t, opt))
                                .SelectMany(s => s.Split(','))
                                .Select(s => s.Trim())
                                .Where(s => s.Length > 0)
                                .ToList();
                            break;
                    }
                }
                catch (Exception ex)
                {
                    result.Warnings.Add($"Line {lineNo}: Failed to parse value for '{valueName}' under {keyPath}: {ex.Message}. ({line})");
                    continue;
                }

                FlagPebakeryVariables(entry, warnedPebakeryVars, result.Warnings, lineNo);
                entry.ScriptSection = currentScriptSection;

                if (singleLineConditionalPrefix != null)
                    TagEntryWithSingleLineConditional(entry, singleLineConditionalPrefix, lineNo);
                TagEntryWithBlockInfo(entry, openBlocks);
                entries.Add(entry);
            }

            int blockedEntryCount = entries.Count(e => e.BlockNote != null);
            if (blockedEntryCount > 0)
            {
                int mutuallyExclusiveCount = entries.Count(e => e.BlockMutuallyExclusive);
                string mutualNote = mutuallyExclusiveCount > 0
                    ? $"{mutuallyExclusiveCount} of these belong to mutually exclusive If/Else branch pairs where only one branch of each pair will actually run."
                    : string.Empty;
                result.Warnings.Add(
                    $"{blockedEntryCount} value(s) were extracted from inside conditional If/Else block(s) or single-line conditional If commands. " +
                    $"Since this converter cannot evaluate the script's conditions they have been commented out in the .reg output below. " +
                    $"{mutualNote}");
            }

            result.Output = GenerateRegFile(entries, opt);
            return result;
        }

        /// <summary>
        /// Records that entry appeared as the embedded command in a compound one-line
        /// conditional (e.g. "If,EXISTFILE,Foo,If,%Blah%,Equal,True,RegWrite,..."), rather
        /// than as a bare RegWrite/RegWriteEx/RegDelete line. Unlike a Begin..End block,
        /// there's no paired Else and no way to detect mutual exclusivity here. This
        /// only records that the line's real, unevaluated condition text so the user can
        /// review it. Call before TagEntryWithBlockInfo so the two notes combine correctly
        /// if the line also happens to sit inside an enclosing Begin..End block.
        /// </summary>
        private static void TagEntryWithSingleLineConditional(RegEntry entry, string conditionPrefix, int lineNo)
        {
            entry.SingleLineConditionalDesc = $"{conditionPrefix} (line {lineNo})";
        }

        /// <summary>
        /// Records that entry was extracted from inside the innermost currently-open
        /// If/Else block (if any), setting RegEntry.BlockNote to a human-readable
        /// description and BlockMutuallyExclusive if that block is already known to be
        /// paired with a sibling Else/If branch. Also registers entry with every
        /// currently-open ancestor frame (see BlockFrame.Entries) so that if an
        /// enclosing If block's sibling Else is discovered later in the scan, this
        /// entry can still be found and retroactively flagged. Appends to (rather than
        /// overwrites) any BlockNote already set by TagEntryWithSingleLineConditional,
        /// so both notes survive if a single-line conditional also sits inside a
        /// Begin..End block. Like TagEntryWithSingleLineConditional, does not embed its
        /// own "[WARNING]" tag in order to prevent duplicates. GenerateRegFile adds a single
		///  "[WARNING] " prefix once, at the point where the fully-assembled BlockNote is actually printed.
        /// </summary>
        private static void TagEntryWithBlockInfo(RegEntry entry, List<BlockFrame> openBlocks)
        {
            if (openBlocks.Count == 0)
                return;

            foreach (BlockFrame f in openBlocks)
                f.Entries.Add(entry);

            BlockFrame innermost = openBlocks[^1];
            string note = innermost.Kind.Equals("If", StringComparison.OrdinalIgnoreCase)
                ? $"Conditional: {innermost.HeaderText} (line {innermost.LineNumber})"
                : innermost.PairedIfFrame != null
                    ? $"Conditional: Else (line {innermost.LineNumber}, is paired with If at line {innermost.PairedIfFrame.LineNumber})"
                    : $"Conditional: Else (line {innermost.LineNumber})";

            if (openBlocks.Count > 1)
                note += $" [nested {openBlocks.Count} levels deep]";

            if (innermost.Kind.Equals("Else", StringComparison.OrdinalIgnoreCase) && innermost.PairedIfFrame != null)
            {
                note += $" - This is mutually exclusive with the If branch at line {innermost.PairedIfFrame.LineNumber}";
                entry.BlockMutuallyExclusive = true;
            }

            entry.EnclosingBlockDesc = note;
        }

        /// <summary>
        /// Sets RegEntry.LoadedHiveNote whenever KeyPath's first path segment matches a variable
        /// name seen in a RegHiveLoad line earlier in the script, since the true root hive
        /// (SYSTEM/SOFTWARE/NTUSER.DAT/etc.) that variable was mounted from can't be recovered
        /// from the script alone. LoadedHiveNote (and therefore the inline "; [WARNING]:" comment in
        /// the .reg output) is still set on every affected entry, so nothing about the
        /// generated file itself changes. Only the warnings LIST is deduplicated: a script
        /// with many RegWrite lines under one loaded-hive variable would otherwise produce
        /// one near-identical warning per line, which drowns out everything else in
        /// result.Warnings. warnedHiveVars tracks which variable names have already
        /// contributed a warning so only the first affected entry for each one does.
        /// </summary>
        private static void FlagLoadedHiveAmbiguity(RegEntry entry, HashSet<string> loadedHiveVars,
            HashSet<string> warnedHiveVars, List<string> warnings, int lineNo)
        {
            string firstSeg = entry.KeyPath.Split('\\').FirstOrDefault() ?? "";
            if (!loadedHiveVars.Contains(firstSeg))
                return;

            entry.LoadedHiveNote = $"[WARNING] Path was assigned by RegHiveLoad using a variable [{firstSeg}]. The true root hive (SYSTEM/SOFTWARE/NTUSER.DAT/etc.) " +
                                "cannot be recovered from the script and must be fixed manually in the .reg output.";

            if (warnedHiveVars.Add(firstSeg))
                warnings.Add($"Line {lineNo}: Path was assigned by RegHiveLoad using a variable [{firstSeg}]. The true root hive (SYSTEM/SOFTWARE/NTUSER.DAT/etc.) " +
                                "cannot be recovered from the script and must be fixed manually in the .reg output.");
        }

        private static string EscapeRegString(string raw)
        {
            return raw.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        private static List<string> BytesToTokens(byte[] bytes) => bytes.Select(b => b.ToString("x2")).ToList();

        private static string GenerateRegFile(List<RegEntry> entries, RegConvertOptions opt)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Windows Registry Editor Version 5.00");
            sb.AppendLine();
            sb.AppendLine("; ========================================================================================");
            sb.AppendLine("; DISCLAIMER: ");
            sb.AppendLine("; This .reg file was generated by PEBakery's Script -> .reg converter.");
            sb.AppendLine("; This conversion is BEST EFFORT and intended for advanced users only.");
            sb.AppendLine(";");
            sb.AppendLine("; Due to the dynamic nature of PEBakery scripts, the converter cannot evaluate");
            sb.AppendLine("; the runtime state (If/Else branches, %Variables%, Run/Exec calls, loaded hives,");
            sb.AppendLine("; command output, external Macros, etc.), so the output below may be incomplete,");
            sb.AppendLine("; incorrect, or incompatable with your target system.");
            sb.AppendLine(";");
            sb.AppendLine("; Please review and verify every entry before merging this file into a live registry.");
            sb.AppendLine("; ========================================================================================");
            sb.AppendLine();

            string? lastSection = null;
            string? lastScriptSection = null;
            foreach (RegEntry e in entries)
            {
                if (e.ScriptSection != null && e.ScriptSection != lastScriptSection)
                {
                    if (lastScriptSection != null) sb.AppendLine();
                    sb.AppendLine("; ==================================================");
                    sb.AppendLine($"; [{e.ScriptSection}]");
                    sb.AppendLine("; ==================================================");
                    sb.AppendLine();
                    lastScriptSection = e.ScriptSection;
                    lastSection = null; // force the [HIVE\Path] header to reprint under the new banner
                }

                string fullHive = HiveShortToFull(e.HiveShort);

                if (e.Kind == RegValueKind.DeleteKey)
                {
                    // Whole-key deletion is expressed purely by the section header itself.
                    // No value lines follow it.
                    sb.AppendLine();
                    if (e.LoadedHiveNote != null)
                        sb.AppendLine($"; {e.LoadedHiveNote}");
                    if (e.UnresolvedVarNote != null) sb.AppendLine($"; [WARNING] {e.UnresolvedVarNote}");
                    if (e.BlockNote != null) sb.AppendLine($"; [WARNING] {e.BlockNote}");
                    string keyLinePrefix = (e.BlockNote != null || e.HasUnresolvedVariable) ? "; " : "";
                    sb.AppendLine($"{keyLinePrefix}[-{fullHive}\\{e.KeyPath}]");
                    lastSection = null; // force the next normal section to reprint its header
                    continue;
                }

                if (e.Kind == RegValueKind.CreateKey)
                {
                    // HKey,ValueType,KeyPath-only RegWrite ensures the key exists but writes
                    // no value, same as a bare "[HIVE\Path]" .reg section header with no value
                    // lines following it.
                    sb.AppendLine();
                    if (e.LoadedHiveNote != null)
                        sb.AppendLine($"; {e.LoadedHiveNote}");
                    if (e.UnresolvedVarNote != null) sb.AppendLine($"; [WARNING] {e.UnresolvedVarNote}");
                    if (e.BlockNote != null) sb.AppendLine($"; [WARNING] {e.BlockNote}");
                    string keyOnlyPrefix = (e.BlockNote != null || e.HasUnresolvedVariable) ? "; " : "";
                    sb.AppendLine($"{keyOnlyPrefix}[{fullHive}\\{e.KeyPath}]");
                    lastSection = null; // force the next normal section to reprint its header
                    continue;
                }

                string section = $"[{fullHive}\\{e.KeyPath}]";
                if (section != lastSection)
                {
                    if (lastSection != null) sb.AppendLine();
                    sb.AppendLine(section);
                    lastSection = section;

                    // The plain (non-per-entry) LoadedHiveNote describes the KEY's path, not anything
                    // specific to an individual value, so it's identical for every value
                    // entry that shares this KeyPath. Print it once, right under the key
                    // header instead of repeating the same line above every single value
                    // below it. 
                    if (e.LoadedHiveNote != null)
                        sb.AppendLine($"; {e.LoadedHiveNote}");
                }

                string nameOut = e.ValueName.Length == 0 ? "@" : "\"" + EscapeRegString(e.ValueName) + "\"";

                // Entries extracted from inside an If/Else block can't be safely applied
                // unconditionally, only one branch of a conditional ever runs at script
                // execution time, and this converter has no way to evaluate the condition.
                // Entries containing an unresolved "%Variable%" are just as unsafe to apply
                // literally, the true value can only be known at script runtime. Either
                // condition means every physical line for this entry is commented out, and a
                // note above explains why. (The plain loaded-hive LoadedHiveNote case is handled
                // once above, at the key header, rather than per-value here.)
                bool commentOutValue = e.BlockNote != null || e.HasUnresolvedVariable;
                if (e.BlockNote != null)
                    sb.AppendLine($"; [WARNING] {e.BlockNote}");
                if (e.UnresolvedVarNote != null)
                    sb.AppendLine($"; [WARNING] {e.UnresolvedVarNote}");
                string valuePrefix = commentOutValue ? "; " : "";

                if (e.Kind == RegValueKind.DeleteValue)
                {
                    sb.AppendLine($"{valuePrefix}{nameOut}=-");
                    continue;
                }

                switch (e.Kind)
                {
                    case RegValueKind.StringVal:
                        sb.AppendLine($"{valuePrefix}{nameOut}=\"{EscapeRegString(e.StringValue)}\"");
                        break;
                    case RegValueKind.ExpandString:
                        {
                            byte[] b = Encoding.Unicode.GetBytes(e.StringValue + "\0");
                            AppendWrappedRegValue(sb, nameOut, "hex(2)", BytesToTokens(b), valuePrefix);
                            break;
                        }
                    case RegValueKind.Dword:
                        if (e.HasUnresolvedVariable)
                            sb.AppendLine($"{valuePrefix}{nameOut}=dword:{e.StringValue}");
                        else
                            sb.AppendLine($"{valuePrefix}{nameOut}=dword:{(uint)e.NumericValue:x8}");
                        break;
                    case RegValueKind.Qword:
                        {
                            if (e.HasUnresolvedVariable)
                            {
                                sb.AppendLine($"{valuePrefix}{nameOut}=hex(b):{e.StringValue}");
                            }
                            else
                            {
                                byte[] b = BitConverter.GetBytes(e.NumericValue);
                                AppendWrappedRegValue(sb, nameOut, "hex(b)", BytesToTokens(b), valuePrefix);
                            }
                            break;
                        }
                    case RegValueKind.MultiString:
                        {
                            List<byte> all = new List<byte>();
                            foreach (string s in e.MultiStrings)
                            {
                                all.AddRange(Encoding.Unicode.GetBytes(s));
                                all.Add(0); all.Add(0);
                            }
                            all.Add(0); all.Add(0); // final terminator
                            AppendWrappedRegValue(sb, nameOut, "hex(7)", BytesToTokens(all.ToArray()), valuePrefix);
                            break;
                        }
                    case RegValueKind.None:
                        // Preserve any payload bytes (rare).
                        AppendWrappedRegValue(sb, nameOut, "hex(0)", e.ByteTokens, valuePrefix);
                        break;
                    case RegValueKind.Binary:
                        AppendWrappedRegValue(sb, nameOut, "hex", e.ByteTokens, valuePrefix);
                        break;
                    case RegValueKind.Other:
                    default:
                        AppendWrappedRegValue(sb, nameOut, "hex(" + e.TypeId.ToString("x") + ")", e.ByteTokens, valuePrefix);
                        break;
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// Sets RegEntry.UnresolvedVarNote and HasUnresolvedVariable if any of KeyPath,
        /// ValueName, StringValue, or MultiStrings still contains a "%Variable%" token.
        /// PEBakery resolves %Variable% expansion at script *runtime*, based on state (Env
        /// vars, script-Set variables, interface field values) this converter has no access to
        /// so a script value that still contains "%...%" text after our own
        /// parsing/unescaping can only mean it's a genuine runtime variable reference, not
        /// literal data, and would be written into the .reg output as the literal,
        /// wrong-and-unresolved text "%Foo%" if not flagged.
        /// Like FlagLoadedHiveAmbiguity, the warnings LIST is deduplicated per distinct
        /// variable name (via warnedVars) so a variable referenced across many entries
        /// produces one warning, not one per entry; UnresolvedVarNote/the per-entry .reg NOTE
        /// is still set on every affected entry, so nothing about the generated file changes.
        /// </summary>
        private static void FlagPebakeryVariables(RegEntry entry, HashSet<string> warnedVars, List<string> warnings, int lineNo)
        {
            List<string> haystacks = new List<string> { entry.KeyPath, entry.ValueName, entry.StringValue };
            haystacks.AddRange(entry.MultiStrings);

            SortedSet<string> foundVars = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string text in haystacks)
            {
                if (string.IsNullOrEmpty(text)) continue;
                foreach (Match m in PebakeryVariableRegex().Matches(text))
                    foundVars.Add(m.Value);
            }
            if (foundVars.Count == 0)
                return;

            entry.HasUnresolvedVariable = true;

            string varList = string.Join(", ", foundVars);
            string note = $"Contains unresolved PEBakery variable(s) [{varList}]; these variables can only be resolved at script runtime " +
                           "and must be fixed manually.";

            entry.UnresolvedVarNote = note;

            bool anyNew = false;
            foreach (string v in foundVars)
            {
                if (warnedVars.Add(v))
                    anyNew = true;
            }
            if (anyNew)
                warnings.Add($"Line {lineNo}: {note}");
        }

        private static void AppendWrappedRegValue(StringBuilder sb, string nameOut, string typePrefix, List<string> tokens, string commentPrefix = "")
        {
            string head = $"{nameOut}={typePrefix}:";
            if (tokens.Count == 0)
            {
                sb.AppendLine(commentPrefix + head);
                return;
            }
            // regedit.exe uses Two-space continuation indentation
            AppendWrappedTokens(sb, head, tokens, "  ", commentPrefix);
        }

        // regedit.exe's wrap column:  20-char "Name"=hex(N): prefix fits 19 tokens per
        // line, a 2-char continuation indent fits 25)
        private const int RegLineWidth = 80;

        /// <summary>
        /// Wraps a comma-separated token list to RegLineWidth columns, regedit-style:
        /// the first line carries <paramref name="head"/> as its prefix, every
        /// continuation line is indented by <paramref name="continuationIndent"/>, and
        /// each line reserves 2 characters for a trailing ",\" continuation marker
        /// except the true last line, which has no marker and so can hold one token
        /// more than a mid-value line with the same prefix length. Token lengths are
        /// measured directly rather than assumed uniform, so this works for both
        /// fixed-width 2-hex-digit tokens (Binary/Other/None) and variable-length
        /// quoted-string tokens (MultiString's RegWrite argument list).
        /// </summary>
        private static void AppendWrappedTokens(StringBuilder sb, string head, List<string> tokens, string continuationIndent, string commentPrefix = "")
        {
            int idx = 0;
            string prefix = head;
            while (idx < tokens.Count)
            {
                int remaining = tokens.Count - idx;

                // Everything left, joined with commas, no continuation marker needed.
                int wholeRestLen = prefix.Length + (remaining - 1); // separators between remaining tokens
                for (int i = idx; i < tokens.Count; i++)
                    wholeRestLen += tokens[i].Length;

                int take;
                if (wholeRestLen <= RegLineWidth)
                {
                    take = remaining;
                }
                else
                {
                    int budget = RegLineWidth - prefix.Length - 2; // reserve 2 chars for ",\"
                    int used = 0;
                    take = 0;
                    for (int i = idx; i < tokens.Count; i++)
                    {
                        int addLen = (take == 0 ? 0 : 1) + tokens[i].Length; // +1 for comma separator
                        if (used + addLen > budget && take > 0)
                            break;
                        used += addLen;
                        take++;
                    }
                    take = Math.Max(1, take); // always make progress, even if a single token overflows the budget
                }

                string chunk = string.Join(",", tokens.GetRange(idx, take));
                idx += take;
                bool isLast = idx >= tokens.Count;
                string suffix = isLast ? "" : ",\\";
                sb.AppendLine(commentPrefix + prefix + chunk + suffix);

                prefix = continuationIndent;
            }
        }
        #endregion
    }
}
