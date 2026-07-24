# SplitwiseCLI

A Windows command-line client for [Splitwise](https://www.splitwise.com/) — view your account, groups, friends, and expenses from the terminal, and bulk-import expenses from Excel.

## Features

- Read your Splitwise account, groups, friends, expenses, comments, notifications, categories, and supported currencies
- Bulk-import expenses from one or more Excel (`.xlsx`) files, with a `rollback` command to undo a specific import run
- Interactive shell mode for running multiple commands in one session
- API key stored either via environment variable or encrypted locally (Windows DPAPI) — never in plain text in the repo

## Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- **Windows** — the CLI encrypts your saved API key using the Windows Data Protection API (DPAPI), so it currently only runs on Windows
- A [Splitwise](https://www.splitwise.com/) account

## Installation

**Prerequisite:** the [.NET 10 SDK](https://dotnet.microsoft.com/download) — needed once, to build/install the tool (there's no pre-built installer/download yet).

Everything below works identically whether you use **PowerShell** or **Command Prompt (cmd.exe)** — every step just invokes `git`/`dotnet`, not a shell built-in, so there's nothing shell-specific to translate.

### 1. Get the source

Either clone it with git:

```
git clone https://github.com/CruelApe/SplitwiseCLI.git
cd SplitwiseCLI
```

or, if you don't have git installed, download it instead: on the [repository page](https://github.com/CruelApe/SplitwiseCLI), click **Code → Download ZIP**, extract it, then open PowerShell or Command Prompt in the extracted `SplitwiseCLI` folder.

> **cmd.exe tip:** if that folder is on a different drive than your prompt (e.g. it's on `D:` but cmd opened on `C:`), plain `cd` won't switch drives — use `cd /d D:\path\to\SplitwiseCLI` instead. PowerShell's `cd` switches drives automatically, so this only matters in cmd.

### 2. Install

**Option A — install as a global tool (recommended for regular use)**

The project is already configured as a .NET global tool (command name `splitwise`). From the `SplitwiseCLI` folder, run:

```
dotnet pack SplitwiseCLI -o nupkg
dotnet tool install --global --add-source nupkg SplitwiseCLI
```

Confirm it worked:

```
splitwise about
```

> If Windows says `splitwise` is not recognized right after installing, close and reopen your terminal. The .NET global tools folder (`%USERPROFILE%\.dotnet\tools`) is added to `PATH` by the SDK installer, but a terminal window that was already open won't pick up the change until restarted.

To pick up a newer version later (after pulling or re-downloading), reinstall with `update` instead of `install`:

```
dotnet pack SplitwiseCLI -o nupkg
dotnet tool update --global --add-source nupkg SplitwiseCLI
```

To remove it: `dotnet tool uninstall --global SplitwiseCLI`.

**Option B — run from source (for development)**

```
dotnet run --project SplitwiseCLI -- <command> [arguments]
```

**Option C — download a release binary**

Download `SplitwiseCLI-<version>-win-x64.zip` from the [Releases](https://github.com/CruelApe/SplitwiseCLI/releases) page, extract it, and run `splitwise.exe` directly — no .NET SDK required. Optionally add the extracted folder to your `PATH` so `splitwise` is available from any terminal.

## Authentication

SplitwiseCLI needs a personal Splitwise API key to call your account.

1. Generate a key at https://secure.splitwise.com/apps.
2. Provide it to the CLI using **any one** of the following (checked in this order):
   1. **Environment variable** — set `SPLITWISE_API_KEY`.
   2. **`.env` file** — copy [`.env.example`](.env.example) to `.env` in the directory you run `splitwise` from (or next to `SplitwiseCLI.csproj`) and fill in `SPLITWISE_API_KEY`. `.env` is already git-ignored, so your key never gets committed.
   3. **Saved, encrypted config** — run `splitwise config set-key` and paste your key when prompted. It's encrypted with Windows DPAPI (current-user scope) and stored at `%APPDATA%\SplitwiseCLI\config.json`.
   4. **First-run prompt** — if no key is configured yet and you're running interactively, any command will prompt for a key and save it the same way `config set-key` does.

Manage the saved key at any time:

```powershell
splitwise config show   # shows whether a key is configured and where it's stored
splitwise config clear  # removes the saved key
```

Optionally set `SPLITWISE_DEFAULT_CURRENCY` (env var or `.env`) to override the currency code used for **imported** expenses; if unset, your Splitwise account's default currency is used.

## Usage

Run any command as:

```powershell
splitwise <command> [arguments] [options]
```

(or `dotnet run --project SplitwiseCLI -- <command> [arguments] [options]` if running from source, without installing the tool).

Running `splitwise` with **no arguments** launches an interactive shell, so you can run several commands in a row without relaunching the process:

```
> splitwise
SplitwiseCLI interactive mode. Type "exit" or "quit" to leave.
splitwise> expenses --group 12345
splitwise> exit
```

The interactive shell supports quoted arguments, e.g. `import "C:/My Expenses/*.xlsx"`.

### Commands

| Command | Arguments / options | Description |
|---|---|---|
| `me` | — | Show your Splitwise account details |
| `user <id>` | `<id>` | Show another user's details by id |
| `groups` | — | List all your Splitwise groups |
| `group <id>` | `<id>` | Show a group's details and members by id |
| `friends` | — | List your friends and balances |
| `friend <id>` | `<id>` | Show a friend's details and balance by id |
| `expenses` | `--group <id>`, `--friend <id>`, `--dated-after <date>`, `--dated-before <date>`, `--updated-after <date>`, `--updated-before <date>`, `--limit <n>` (Splitwise default: 20), `--offset <n>` | List expenses, optionally filtered by group, friend, or date range |
| `expense <id>` | `<id>` | Show a single expense's details by id |
| `comments <id>` | `<id>` (expense id) | List comments on an expense |
| `categories` | — | List Splitwise expense categories as a tree (parent categories and their subcategories) |
| `currencies` | — | List currency codes supported by Splitwise |
| `notifications` | — | List your recent Splitwise activity notifications |
| `import <path>` | `<path>` (file, directory, or glob), `-y`/`--yes` | Bulk-import expenses from Excel file(s) — see [docs/IMPORT_FORMAT.md](docs/IMPORT_FORMAT.md) |
| `rollback <batchId>` | `<batchId>`, `--dry-run`, `-y`/`--yes` | Delete all expenses created by a specific `import` run, identified by the batch id printed at the end of that run |
| `config set-key` | — | Prompt for and save your Splitwise API key |
| `config show` | — | Show whether an API key is configured and where it's stored |
| `config clear` | — | Remove the saved API key |
| `version` | — | Show the SplitwiseCLI version |
| `about` | — | Show information about SplitwiseCLI, including author and repository |

### Examples

```powershell
splitwise me
splitwise groups
splitwise group 12345
splitwise friend 12345
splitwise expenses
splitwise expenses --group 12345
splitwise expenses --dated-after 2026-01-01
splitwise expense 12345
splitwise comments 12345
splitwise import C:/expenses/january.xlsx
splitwise import "C:/expenses/*.xlsx"
```

Output is rendered as formatted tables/trees in the console; there is currently no JSON or other machine-readable output mode. Errors print as `Error: <message>` and the process exits with a non-zero code. Every command shows a loading spinner (or, for `import`/`rollback`'s multi-step work, a progress bar) while it's waiting on the Splitwise API, so it's always clear the CLI is doing something rather than hanging.

## Bulk import

`splitwise import <path>` reads one or more `.xlsx` files (a single file, a directory of `.xlsx` files, or a glob pattern) and creates one Splitwise expense per row. Each row needs a `Description`, `Cost`, `Date`, `Category` (a numeric subcategory id), and `Group` (a numeric group id), plus an optional `Details` column for notes; every expense is split **equally** across the group's current members with you as the payer, and category/group ids are validated live against your real Splitwise account. IDs are used instead of names to avoid typo-prone matching — run `splitwise categories`/`splitwise groups` to look up the ids you need.

If every row across every matched file validates with no errors, you're asked to confirm before anything is actually created in Splitwise (skip this with `-y`/`--yes`). If any row has an error, the prompt is skipped and today's behavior applies: valid rows are still created and invalid ones are reported as failures.

See **[docs/IMPORT_FORMAT.md](docs/IMPORT_FORMAT.md)** for the full column reference, validation rules, and example data.

## Rolling back an import

Every expense created by `import` is tagged with a batch id in its `Details` field, and `import` prints that id (per file) when the run finishes. If a batch turns out to be wrong, undo it with:

```powershell
splitwise rollback 202605-202607-a1b2c3           # previews matches, then asks for confirmation
splitwise rollback 202605-202607-a1b2c3 --dry-run # preview only, deletes nothing
splitwise rollback 202605-202607-a1b2c3 --yes     # skip the confirmation prompt
```

See **[docs/IMPORT_FORMAT.md](docs/IMPORT_FORMAT.md#batch-ids-and-rollback)** for how batch ids are formed and matched.

## Development

Run the test suite:

```powershell
dotnet test
```
