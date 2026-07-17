# SplitwiseCLI

A Windows command-line client for [Splitwise](https://www.splitwise.com/) — view your account, groups, friends, and expenses from the terminal, and bulk-import expenses from Excel.

## Features

- Read your Splitwise account, groups, friends, expenses, comments, notifications, categories, and supported currencies
- Bulk-import expenses from one or more Excel (`.xlsx`) files
- Interactive shell mode for running multiple commands in one session
- API key stored either via environment variable or encrypted locally (Windows DPAPI) — never in plain text in the repo

## Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- **Windows** — the CLI encrypts your saved API key using the Windows Data Protection API (DPAPI), so it currently only runs on Windows
- A [Splitwise](https://www.splitwise.com/) account

## Installation

Clone the repository:

```powershell
git clone https://github.com/CruelApe/SplitwiseCLI.git
cd SplitwiseCLI
```

### Option A — install as a global tool (recommended)

The project is already configured as a .NET global tool (command name `splitwise`). Pack and install it locally:

```powershell
dotnet pack SplitwiseCLI -o nupkg
dotnet tool install --global --add-source ./nupkg SplitwiseCLI
```

You can now run `splitwise` from any terminal. After pulling changes, reinstall with:

```powershell
dotnet pack SplitwiseCLI -o nupkg
dotnet tool update --global --add-source ./nupkg SplitwiseCLI
```

To remove it: `dotnet tool uninstall --global SplitwiseCLI`.

### Option B — run from source

```powershell
dotnet run --project SplitwiseCLI -- <command> [arguments]
```

### Option C — download a release binary

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
| `import <path>` | `<path>` (file, directory, or glob) | Bulk-import expenses from Excel file(s) — see [docs/IMPORT_FORMAT.md](docs/IMPORT_FORMAT.md) |
| `config set-key` | — | Prompt for and save your Splitwise API key |
| `config show` | — | Show whether an API key is configured and where it's stored |
| `config clear` | — | Remove the saved API key |

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

Output is rendered as formatted tables/trees in the console; there is currently no JSON or other machine-readable output mode. Errors print as `Error: <message>` and the process exits with a non-zero code.

## Bulk import

`splitwise import <path>` reads one or more `.xlsx` files (a single file, a directory of `.xlsx` files, or a glob pattern) and creates one Splitwise expense per row. Each row needs a `Description`, `Cost`, `Date`, `Category`, and `Group`; every expense is split **equally** across the named group's current members with you as the payer, and category/group names are validated live against your real Splitwise account.

See **[docs/IMPORT_FORMAT.md](docs/IMPORT_FORMAT.md)** for the full column reference, validation rules, and example data.

## Development

Run the test suite:

```powershell
dotnet test
```
