# Mix It Up `!loot` command setup

Full action list for the `!loot @chatter` command, in order. The first three already
existed; the last three sync each loot drop to `loot.json` on GitHub.

## Action 1 — File (Read & Write) *(existing)*

- Action: Read Random Line From File
- File Path: `Loot.txt`
- Special Identifier Name: `loot`

## Action 2 — Chat Message *(existing)*

- Message: `$targetusername has obtained $loot!`

## Action 3 — File (Read & Write) *(existing)*

- Action: Append To File
- File Path: `LootBackpacks.txt`
- Text To Save: `$targetusername - $loot`

## Action 4 — Web Request (GET)

- Method: `GET`
- URL: `https://api.github.com/repos/Kingzilla-inc/Silverwind-Loot-Portal/contents/loot.json`
- Headers:

  | Key | Value |
  | --- | --- |
  | `Authorization` | `Bearer <your token>` |
  | `User-Agent` | `MixItUp-LootBot` |
  | `Accept` | `application/vnd.github+json` |

- Response Processing Type: **JSON to Special Identifiers**
- Pairs:

  | JSON Value Name | Special Identifier Name |
  | --- | --- |
  | `sha` | `sha` |
  | `content` | `filecontent` |

## Action 5 — Script

Paste the full contents of [`loot-sync.csx`](./loot-sync.csx) in place of Mix It Up's
default script template.

## Action 6 — Web Request (PUT)

- Method: `PUT`
- URL: same as Action 4
- Headers:

  | Key | Value |
  | --- | --- |
  | `Authorization` | `Bearer <your token>` |
  | `User-Agent` | `MixItUp-LootBot` |
  | `Accept` | `application/vnd.github+json` |
  | `Content-Type` | `application/json` |

- Body:

  ```
  {"message": "Add loot for $targetusername: $loot", "content": "$scriptresult", "sha": "$sha"}
  ```

- Response Processing Type: Plain Text
