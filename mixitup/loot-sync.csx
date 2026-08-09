// Mix It Up "Script" action for the !loot command. See SETUP.md for the full action list.
// Paste this ENTIRE file in place of Mix It Up's default script template.
//
// Mix It Up substitutes special identifiers as a blind text replace across the whole
// script, including comments - so never write a literal dollar-sign identifier name in a
// comment here, or a stale value from a previous run will get spliced in and break the file.
//
// Item names in Loot.txt must not contain a double-quote or backslash character, since
// $loot gets substituted as raw text into a string literal below.

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
                // Must be a verbatim (@"...") string: the substituted content can contain
                // real newlines, which a regular string literal can't contain.
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

        private System.Collections.Generic.List<string> ExtractQuotedStrings(string content)
        {
            var result = new System.Collections.Generic.List<string>();
            int i = 0;
            while (i < content.Length)
            {
                if (content[i] == '"')
                {
                    int start = i + 1;
                    int j = start;
                    while (j < content.Length)
                    {
                        if (content[j] == '\\') { j += 2; continue; }
                        if (content[j] == '"') break;
                        j++;
                    }
                    result.Add(content.Substring(start, j - start));
                    i = j + 1;
                }
                else
                {
                    i++;
                }
            }
            return result;
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

                // Rebuild from all items (existing + new) and recompute single-line vs
                // multi-line by width each time, matching how Prettier formats loot.json.
                var items = ExtractQuotedStrings(arrContent);
                items.Add(escapedItem);

                string singleLine = "[" + string.Join(", ", items.ConvertAll(s => "\"" + s + "\"")) + "]";
                string linePrefix = "    \"" + key + "\": ";
                bool fits = (linePrefix.Length + singleLine.Length + 1) <= 80;

                string replacement;
                if (fits)
                {
                    replacement = singleLine;
                }
                else
                {
                    var sb = new StringBuilder();
                    sb.Append("[\n");
                    for (int k = 0; k < items.Count; k++)
                    {
                        sb.Append("        \"").Append(items[k]).Append("\"");
                        if (k < items.Count - 1) sb.Append(",");
                        sb.Append("\n");
                    }
                    sb.Append("    ]");
                    replacement = sb.ToString();
                }

                return json.Substring(0, arrStart) + replacement + json.Substring(arrEnd + 1);
            }
            else
            {
                int objEnd = json.LastIndexOf('}');
                string beforeEnd = json.Substring(0, objEnd).TrimEnd();
                bool isEmpty = beforeEnd.EndsWith("{");
                string prefix = isEmpty ? "" : ",";
                string insertion = prefix + "\n    \"" + EscapeJson(key) + "\": [\"" + escapedItem + "\"]\n";
                return beforeEnd + insertion + json.Substring(objEnd);
            }
        }
    }
}
