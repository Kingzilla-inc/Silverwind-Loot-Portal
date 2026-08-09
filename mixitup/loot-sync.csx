// Mix It Up "C# Script" action — the MIDDLE of 3 actions on the !loot command:
//   1. Web Request action (GET) — fetches loot.json from GitHub, extracts
//      $sha and $filecontent (JSON field extraction, no script needed)
//   2. THIS Script action — decodes $filecontent, appends the new loot item,
//      re-encodes to base64, returns it (stored in $scriptresult)
//   3. Web Request action (PUT) — pushes the updated content back to GitHub,
//      using $scriptresult and $sha in its request body
// Paste this ENTIRE file's contents in place of Mix It Up's default script template
// (the "CustomNamespace" / "CustomClass" / "Run()" skeleton it starts you with).
//
// This deliberately does ZERO networking here — earlier attempts that tried to call
// GitHub's API directly from this action kept hitting "type or namespace not found"
// errors (HttpClient, System.Text.Json, then even WebClient) because Mix It Up's
// script compiler only references a very small set of .NET assemblies, with no
// networking or JSON libraries included. Convert/Encoding/string, used below, are
// core types that don't require any extra assembly, so they're safe.
//
// Mix It Up substitutes $targetusername, $loot, and $filecontent with plain text
// before this script compiles, the same way it already does in the Chat Message and
// File Path fields on the other actions. That means item names in Loot.txt must not
// contain " or \ characters, or the substituted text will break the C# string literal
// below and the script will fail to compile.

using System;
using System.Text;

namespace CustomNamespace
{
    public class CustomClass
    {
        public object Run()
        {
            try
            {
                // GitHub wraps base64 content at 60 chars with embedded newlines. Depending on
                // how Mix It Up's JSON field extraction handles the escaped "\n" in the source
                // JSON, $filecontent may contain either literal backslash-n (two plain characters)
                // or real newline/carriage-return bytes — strip both forms to be safe.
                string currentB64 = "$filecontent"
                    .Replace("\\n", "").Replace("\\r", "")
                    .Replace("\n", "").Replace("\r", "")
                    .Trim();
                string currentJson = Encoding.UTF8.GetString(Convert.FromBase64String(currentB64));

                string chatter = "$targetusername".TrimStart('@').Trim().ToLowerInvariant();
                string newItem = "$loot".Trim();

                string updatedJson = AddLootItem(currentJson, chatter, newItem);

                return Convert.ToBase64String(Encoding.UTF8.GetBytes(updatedJson));
            }
            catch (Exception ex)
            {
                return "ERROR: " + ex.Message;
            }
        }

        private string EscapeJson(string s)
        {
            var sb = new StringBuilder();
            foreach (char c in s)
            {
                if (c == '"') sb.Append("\\\"");
                else if (c == '\\') sb.Append("\\\\");
                else sb.Append(c);
            }
            return sb.ToString();
        }

        // Finds "key": [ ... ] and appends item to it, or adds a new "key": [item] entry
        // at the top of the object if the key doesn't exist yet.
        private string AddLootItem(string json, string key, string item)
        {
            string quotedKey = "\"" + key + "\"";
            int idx = 0;
            int keyIdx = -1;
            while (true)
            {
                idx = json.IndexOf(quotedKey, idx);
                if (idx == -1) break;
                int after = idx + quotedKey.Length;
                int j = after;
                while (j < json.Length && char.IsWhiteSpace(json[j])) j++;
                if (j < json.Length && json[j] == ':') { keyIdx = idx; break; }
                idx = after;
            }

            string escapedItem = EscapeJson(item);

            if (keyIdx != -1)
            {
                int colonIdx = json.IndexOf(':', keyIdx);
                int arrStart = json.IndexOf('[', colonIdx);
                int arrEnd = json.IndexOf(']', arrStart);
                bool hasExisting = json.Substring(arrStart + 1, arrEnd - arrStart - 1).Trim().Length > 0;
                string sep = hasExisting ? ",\n    " : "";
                return json.Substring(0, arrEnd) + sep + "\"" + escapedItem + "\"" + json.Substring(arrEnd);
            }
            else
            {
                int objStart = json.IndexOf('{');
                bool isEmpty = json.Substring(objStart + 1).TrimStart().StartsWith("}");
                string trailer = isEmpty ? "" : ",";
                string insertion = "\n  \"" + EscapeJson(key) + "\": [\"" + escapedItem + "\"]" + trailer;
                return json.Substring(0, objStart + 1) + insertion + json.Substring(objStart + 1);
            }
        }
    }
}
