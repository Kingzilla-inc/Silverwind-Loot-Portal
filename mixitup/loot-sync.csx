// Mix It Up "C# Script" action - the MIDDLE of 3 actions on the !loot command:
//   1. Web Request action (GET) - fetches loot.json from GitHub, extracts
//      the sha and filecontent special identifiers (JSON field extraction, no script needed)
//   2. THIS Script action - decodes filecontent, appends the new loot item,
//      re-encodes to base64, returns it (stored in the scriptresult identifier)
//   3. Web Request action (PUT) - pushes the updated content back to GitHub,
//      using the scriptresult and sha identifiers in its request body
// Paste this ENTIRE file's contents in place of Mix It Up's default script template
// (the "CustomNamespace" / "CustomClass" / "Run()" skeleton it starts you with).
//
// IMPORTANT: Mix It Up substitutes special identifiers as a blind text replace across
// the WHOLE script, including comments - not just inside string literals. Never write a
// literal dollar-sign identifier name (like a real one of these, spelled out with the
// leading symbol) anywhere in this file's comments, or a stale multi-line value from a
// previous run will get spliced into the comment and corrupt the file. That's why this
// comment block deliberately avoids writing any of them with their real prefix character.
//
// This deliberately does ZERO networking here - earlier attempts that tried to call
// GitHub's API directly from this action kept hitting "type or namespace not found"
// errors (HttpClient, System.Text.Json, then even WebClient) because Mix It Up's
// script compiler only references a very small set of .NET assemblies, with no
// networking or JSON libraries included. Convert/Encoding/string, used below, are
// core types that don't require any extra assembly, so they're safe.
//
// The identifiers for target username, loot item, and filecontent get substituted with
// plain text before this script compiles, the same way it already does in the Chat
// Message and File Path fields on the other actions. That means item names in Loot.txt
// must not contain a double-quote or backslash character, or the substituted text will
// break the C# string literal below and the script will fail to compile.

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
                // GitHub wraps base64 content at 60 chars with embedded newlines, and Mix It Up's
                // JSON field extraction unescapes those into real newline/carriage-return bytes.
                // A regular C# string literal can't legally contain a raw newline, so this MUST be
                // a verbatim (@"...") string literal, or the script fails to compile as soon as
                // the value below is substituted in. The .Replace calls still strip both real
                // newlines and literal two-character backslash-n / backslash-r sequences, just in case.
                string currentB64 = @"$filecontent"
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
        // at the end of the object if the key doesn't exist yet.
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
                string arrContent = json.Substring(arrStart + 1, arrEnd - arrStart - 1);
                bool hasExisting = arrContent.Trim().Length > 0;

                // Insert right after the last item's closing quote (not right before "]"),
                // so the bracket's own existing indentation/newline is left untouched.
                int insertPos = arrStart + 1 + arrContent.TrimEnd().Length;
                string sep = hasExisting ? ",\n        " : "\n        ";
                string newText = sep + "\"" + escapedItem + "\"";
                if (!hasExisting) newText += "\n    ";
                return json.Substring(0, insertPos) + newText + json.Substring(insertPos);
            }
            else
            {
                int objEnd = json.LastIndexOf('}');
                string beforeEnd = json.Substring(0, objEnd).TrimEnd();
                bool isEmpty = beforeEnd.EndsWith("{");
                string prefix = isEmpty ? "" : ",";
                string insertion = prefix + "\n    \"" + EscapeJson(key) + "\": [\n        \"" + escapedItem + "\"\n    ]\n";
                return beforeEnd + insertion + json.Substring(objEnd);
            }
        }
    }
}
