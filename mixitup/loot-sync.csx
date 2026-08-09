// Mix It Up "C# Script" action — add this as a 4th action on the !loot command,
// after the existing "Read Random Line From File" and "Append To File" actions.
// Paste this ENTIRE file's contents in place of Mix It Up's default script template
// (the "CustomNamespace" / "CustomClass" / "Run()" skeleton it starts you with) —
// this keeps that exact required shape, just filled in.
//
// One-time setup before this will work:
//   1. Create a GitHub fine-grained personal access token scoped to ONLY this repo,
//      with "Contents: Read and write" permission.
//      (GitHub -> Settings -> Developer settings -> Personal access tokens -> Fine-grained tokens)
//   2. Paste that token into GITHUB_TOKEN below, in your LOCAL copy only — never commit
//      a real token here. Keep this tracked file's token as the placeholder.
//   3. Fill in REPO_OWNER with the GitHub username or org that owns the repo.
//
// Mix It Up compiles scripts as a real class via Roslyn's compiler API (not the
// top-level-statements scripting API), and only references .NET's native namespaces —
// no System.Net.Http, no System.Text.Json/Newtonsoft.Json. So this uses:
//   - System.Net.WebClient instead of HttpClient
//   - hand-rolled string-based JSON editing instead of a JSON library
// loot.json's shape is simple and fully under our control (a flat object of
// string -> array-of-strings, no nesting), so editing it with plain string
// search/insert is safe and doesn't need a real parser.
//
// Mix It Up substitutes $targetusername and $loot with plain text before this script
// compiles, the same way it already does in the Chat Message and File Path fields on
// the other actions. That means item names in Loot.txt must not contain " or \
// characters, or the substituted text will break the C# string literal below and the
// script will fail to compile.

using System;
using System.Net;
using System.Text;

namespace CustomNamespace
{
    public class CustomClass
    {
        public object Run()
        {
            string GITHUB_TOKEN = "PASTE_YOUR_FINE_GRAINED_TOKEN_HERE";
            string REPO_OWNER = "Kingzilla-inc";
            string REPO_NAME = "Silverwind-Loot-Portal";
            string FILE_PATH = "loot.json";

            string chatter = "$targetusername".TrimStart('@').Trim().ToLowerInvariant();
            string newItem = "$loot".Trim();

            string apiUrl = $"https://api.github.com/repos/{REPO_OWNER}/{REPO_NAME}/contents/{FILE_PATH}";

            using (var client = new WebClient())
            {
                client.Headers[HttpRequestHeader.Authorization] = "Bearer " + GITHUB_TOKEN;
                client.Headers[HttpRequestHeader.UserAgent] = "MixItUp-LootBot";
                client.Headers[HttpRequestHeader.Accept] = "application/vnd.github+json";

                // 1. Get the current file so we have its SHA (GitHub requires this to update
                // a file) and its current contents.
                string getBody;
                try
                {
                    getBody = client.DownloadString(apiUrl);
                }
                catch (WebException ex)
                {
                    // LootBackpacks.txt already has the record even if this fails.
                    return "GET failed: " + ex.Message;
                }

                string sha = ExtractJsonString(getBody, "sha");
                string currentB64 = ExtractJsonString(getBody, "content");
                string currentJson = Encoding.UTF8.GetString(Convert.FromBase64String(currentB64.Replace("\\n", "").Replace("\n", "")));

                // 2. Add the new item, creating the chatter's entry if this is their first loot drop.
                string updatedJson = AddLootItem(currentJson, chatter, newItem);

                // 3. Push the updated file back to GitHub.
                string updatedB64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(updatedJson));
                string commitMessage = EscapeJson("Add loot for " + chatter + ": " + newItem);
                string payload = "{\"message\":\"" + commitMessage + "\",\"content\":\"" + updatedB64 + "\",\"sha\":\"" + sha + "\"}";

                client.Headers[HttpRequestHeader.ContentType] = "application/json";
                try
                {
                    client.UploadString(apiUrl, "PUT", payload);
                }
                catch (WebException ex)
                {
                    // Same as above — LootBackpacks.txt already has the record even if this fails.
                    return "PUT failed: " + ex.Message;
                }

                // No retry-on-conflict logic here: only the streamer runs !loot, so there's
                // only ever one writer and no risk of two requests racing for the same file SHA.
                return "OK: added '" + newItem + "' to " + chatter;
            }
        }

        private string ExtractJsonString(string json, string key)
        {
            string marker = "\"" + key + "\"";
            int keyIdx = json.IndexOf(marker);
            int colonIdx = json.IndexOf(':', keyIdx + marker.Length);
            int firstQuote = json.IndexOf('"', colonIdx + 1);
            int secondQuote = json.IndexOf('"', firstQuote + 1);
            return json.Substring(firstQuote + 1, secondQuote - firstQuote - 1);
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
