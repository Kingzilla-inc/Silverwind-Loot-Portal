#!/usr/bin/env python3
"""
Mix It Up "External Program" action target for the !loot command.

Setup in Mix It Up:
  - Add an "External Program" action (replaces the Script/Web Request actions).
  - Program: path to your python executable (e.g. C:\\Python312\\python.exe, or just
    "python" if it's on PATH).
  - Arguments: "C:\\path\\to\\loot-sync.py" $targetusername "$loot"
  - Wait Until Complete: ON
  - Save Output: ON (captures this script's stdout into $externalprogramresult, useful
    for a debug Chat Message action afterward)

Why this exists instead of Mix It Up's built-in Script/Web Request actions: Mix It Up's
C# Script action can't make HTTP calls at all (no networking assemblies referenced by its
compiler — confirmed after HttpClient, System.Text.Json, and WebClient all failed to
resolve), and its Web Request action's result identifiers weren't substituting correctly
in testing. An external Python process has no such sandboxing — it's a real, independent
program with the full standard library available, and runs identically on Windows (where
Mix It Up runs) and Linux/Mac (for local testing before deploying).

One-time setup:
  1. Create a GitHub fine-grained personal access token scoped to ONLY this repo, with
     "Contents: Read and write" permission.
  2. Fill in GITHUB_TOKEN below in your LOCAL copy only — never commit a real token here.
  3. Fill in REPO_OWNER with the GitHub username/org that owns the repo.
  4. Make sure Python 3 is installed on whichever machine actually runs this
     (python.org or the Microsoft Store on Windows).

Usage: python loot-sync.py <chatter_username> <item_name>
"""

import sys
import json
import base64
import urllib.request
import urllib.error

GITHUB_TOKEN = "PASTE_YOUR_FINE_GRAINED_TOKEN_HERE"
REPO_OWNER = "Kingzilla-inc"
REPO_NAME = "Silverwind-Loot-Portal"
FILE_PATH = "loot.json"

API_URL = f"https://api.github.com/repos/{REPO_OWNER}/{REPO_NAME}/contents/{FILE_PATH}"


def api_request(method, body=None):
    data = json.dumps(body).encode("utf-8") if body is not None else None
    req = urllib.request.Request(API_URL, data=data, method=method)
    req.add_header("Authorization", f"Bearer {GITHUB_TOKEN}")
    req.add_header("User-Agent", "MixItUp-LootBot")
    req.add_header("Accept", "application/vnd.github+json")
    if data is not None:
        req.add_header("Content-Type", "application/json")
    with urllib.request.urlopen(req, timeout=15) as resp:
        return json.loads(resp.read().decode("utf-8"))


def main():
    if len(sys.argv) < 3:
        print("ERROR: usage: loot-sync.py <chatter_username> <item_name>")
        sys.exit(1)

    chatter = sys.argv[1].lstrip("@").strip().lower()
    item = sys.argv[2].strip()

    try:
        current = api_request("GET")
    except urllib.error.HTTPError as e:
        print(f"ERROR: GET failed ({e.code}): {e.read().decode('utf-8', 'replace')}")
        sys.exit(1)

    sha = current["sha"]
    loot = json.loads(base64.b64decode(current["content"]).decode("utf-8"))

    loot.setdefault(chatter, []).append(item)

    updated_content = base64.b64encode(
        json.dumps(loot, indent=2, ensure_ascii=False).encode("utf-8")
    ).decode("ascii")

    payload = {
        "message": f"Add loot for {chatter}: {item}",
        "content": updated_content,
        "sha": sha,
    }

    try:
        api_request("PUT", payload)
    except urllib.error.HTTPError as e:
        print(f"ERROR: PUT failed ({e.code}): {e.read().decode('utf-8', 'replace')}")
        sys.exit(1)

    print(f"OK: added '{item}' to {chatter}")


if __name__ == "__main__":
    main()
