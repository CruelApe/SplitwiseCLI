# Bulk Import File Format

`splitwise import <path>` bulk-creates Splitwise expenses from Excel workbooks. This document is the full reference for the file format; see the main [README](../README.md) for general CLI usage.

## Path argument

`<path>` can be:

- a single `.xlsx` file,
- a directory (every `.xlsx` file directly inside it is imported), or
- a glob pattern, e.g. `C:/expenses/*.xlsx`.

Quote glob patterns so PowerShell/cmd doesn't try to expand the wildcard itself before the CLI sees it:

```powershell
splitwise import "C:/expenses/*.xlsx"
```

If a path/glob matches no files, the command fails with `No files matched pattern '<pattern>'.` When multiple files match, all of them are processed in a single run and their results are combined into one summary.

## Columns

The first row of each sheet must be a header row. Matching is **case-insensitive** and columns may appear in **any order**. All columns are mandatory on every data row **except `Details`**, which is optional.

| Column | Required | Format | Example |
|---|---|---|---|
| `Description` | Yes | Free text | `Groceries` |
| `Cost` | Yes | Decimal, must be greater than 0. Use `.` as the decimal separator. | `42.50` |
| `Date` | Yes | Any standard date format (e.g. `2026-01-15`), or a native Excel date-formatted cell | `2026-01-15` |
| `Category` | Yes | The **numeric id** of an existing Splitwise **subcategory** — see [Categories](#categories-parent-vs-subcategory) below | `12` |
| `Group` | Yes | The **numeric id** of an existing Splitwise group you belong to | `51170136` |
| `Details` | No | Free text — becomes the expense's notes/details field in Splitwise. Leave cells blank, or omit the column entirely, if you don't need it. | `Paid via credit card` |

`Category` and `Group` take **ids, not names**. This is deliberate: a misspelled name fails silently as an "unknown category/group" error, while an id is copy-pasted from `splitwise categories`/`splitwise groups` and can't be typo'd into a different valid-looking value.

If the header row is missing one of the **required** columns, the whole file is rejected before any rows are processed:

```
'<file>': missing expected column(s) [<missing>]. Expected header row: Description, Cost, Date, Category, Group.
```

## Categories: parent vs. subcategory

Per the [Splitwise API documentation](https://dev.splitwise.com/), expense categories are organized as a two-level hierarchy: **parent categories** group together several **subcategories** (for example, the parent category "Utilities" contains subcategories like "Cleaning"). **Expenses must be tagged with a subcategory — never a parent category.**

The `Category` column follows that same rule: put a subcategory **id** in your spreadsheet, not a parent category's id. The importer doesn't use a hardcoded list — on every run it calls Splitwise's `get_categories` API live and checks that your `Category` id matches a real subcategory id on your account. To see the exact, current, valid ids (and how they nest under their parent categories), run:

```powershell
splitwise categories
```

This prints each subcategory's id next to its name, e.g. `Groceries (id: 12)` — copy the id, not the name, into your spreadsheet. Likewise, `splitwise groups` prints each group's id for the `Group` column.

## Split behavior

Every imported expense is split **equally** among all current members of the `Group` you specify, and the account whose API key you're using is always the payer. There is no column for an unequal split, a different payer, or a subset of group members — expenses needing those need to be created directly in Splitwise instead of via `import`.

A group that currently has no members fails validation:

```
Group '<id>' has no members to split with.
```

## Currency

`Cost` is a plain number — there is no currency column. Every expense created by a given import run uses one currency for the whole run:

1. `SPLITWISE_DEFAULT_CURRENCY`, if set (via environment variable or `.env`), otherwise
2. your Splitwise account's default currency.

Run `splitwise currencies` to see the currency codes Splitwise accepts.

## Example workbook

Create an `.xlsx` file with a header row and data rows like these (the `Details` column is optional and can be omitted entirely):

| Description | Cost | Date | Category | Group | Details |
|---|---|---|---|---|---|
| Groceries | 42.50 | 2026-01-15 | 12 | 51170136 | Weekly shop |
| Internet bill | 60.00 | 2026-01-01 | 47 | 51170136 | |

(`12` and `47` are subcategory ids from `splitwise categories`; `51170136` is a group id from `splitwise groups` — substitute your own account's real ids.)

```powershell
splitwise import C:/expenses/january.xlsx
```

## Validation errors

Each row is validated independently — one bad row does **not** stop the rest of the file (or batch) from being imported; it's recorded as a failure and processing continues.

**Per-row failures:**

| Message | Cause |
|---|---|
| `Description is required.` | Blank `Description` cell |
| `Category is required.` | Blank `Category` cell |
| `Group is required.` | Blank `Group` cell |
| `Cost '<value>' is not a valid number.` | `Cost` isn't a parseable decimal |
| `Cost must be greater than zero (got <value>).` | `Cost` is zero or negative |
| `Date '<value>' is not a valid date.` | `Date` isn't a parseable date |
| `Category '<value>' is not a valid numeric category id.` | `Category` isn't a parseable integer |
| `Group '<value>' is not a valid numeric group id.` | `Group` isn't a parseable integer |
| `Unknown category id '<id>'.` | No subcategory with that id exists on your account |
| `Unknown group id '<id>'.` | No group with that id exists on your account |
| `Group '<id>' has no members to split with.` | The matched group has zero members |
| `API error: <message>` | Splitwise's API rejected the expense (e.g. a server-side business-rule validation) |
| `Failed to read row <n>: <message>` | The Excel row itself couldn't be parsed (corrupt cell, etc.) |

**File-level failures:**

| Message | Cause |
|---|---|
| `'<file>': missing expected column(s) [<missing>]. Expected header row: Description, Cost, Date, Category, Group.` | Header row is missing a required column |
| `No files matched pattern '<pattern>'.` | The path/glob didn't resolve to any `.xlsx` files |

## Confirmation prompt

Before creating anything, `import` fully validates and maps **every** row across every matched file — including checking that each `Category`/`Group` id actually exists on your account — without calling Splitwise's API to create expenses yet.

- **If every single row validates cleanly** (no errors anywhere, across all matched files), you're asked once: `"<N> expense(s) across <M> file(s) validated with no errors. Create them in Splitwise?"`. Answering no aborts the entire run — nothing is created, and the process exits with code `1`.
- **If any row has an error** (a missing/invalid field, an unknown category/group, or a file that fails to read), the prompt is skipped entirely and today's resilient behavior applies as-is: valid rows are still created, invalid rows are recorded as failures, and the summary/failure table is shown as usual.

Pass `-y`/`--yes` to skip the confirmation prompt (e.g. for scripted/non-interactive use) — the run proceeds exactly as if you'd answered yes.

## Duplicate detection

During the same pre-flight pass, every row that validates cleanly is also checked against expenses that **already exist in Splitwise**, so re-importing overlapping data (or a bank statement covering expenses you already entered by hand) doesn't create duplicates. A row is flagged when it matches an existing expense on `Description`, `Cost`, `Category`, and `Group`, and then further classified by how far apart the two dates are:

| Condition | Result |
|---|---|
| `Date` also matches exactly | **Exact duplicate** — `Exact duplicate of expense #<id> - same description, cost, date, category and group.` |
| `Date` differs by an exact multiple of 7 days (7, 14, 21, ...), **before or after** | Not flagged — treated as a legitimate recurring weekly charge (e.g. a subscription) |
| `Date` differs by anything else | **Possible duplicate** — `Possible duplicate of expense #<id> - same description, cost, category and group, <n> day(s) apart (not an exact multiple of a week).` |

The week-apart check works in both directions — an existing expense dated a week (or two, or three, ...) *before or after* the imported row is equally exempt. The check queries Splitwise once per `import` run for expenses dated within 35 days on either side of the earliest and latest `Date` across every row being imported — comfortably wide enough to catch several weekly cycles without scanning your full expense history. It only compares against real Splitwise data; it does not compare rows within the same import file/batch to each other.

**By default, flagged rows are skipped** — they are not sent to Splitwise's `create_expense` endpoint, and are not counted as failures (a run with only skipped duplicates still exits `0`). Before the confirmation prompt, a warning and a table (File, Row, Description, Reason) list every flagged row. Pass `--include-duplicates` to create them anyway:

```powershell
splitwise import C:/expenses/january.xlsx --include-duplicates
```

## Activity feedback

While `import` runs, it shows what it's doing: a spinner labeled "Validating rows..." during the pre-flight validation/mapping pass, then a progress bar labeled "Creating expenses" that advances by one for every row processed (success or failure) during the actual creation pass. On a run with zero rows to create, the progress bar is skipped entirely.

## Summary output and exit codes

After processing, the CLI prints a summary line (`N created / N skipped as duplicate(s) / N failed`), then a table of every **created** expense (Expense Id, File, Description, Cost, Date, Category Id, Group Id, and the exact `Details` value that was sent — i.e. the batch tag), then a table of every row **skipped as a duplicate** (File, Row, Description, Reason), then a table listing the File, Row, Description, and Reason for every **failed** row, and finally a **batch id** line per file (see below). The process exits with code `1` if any row failed across any file — skipped duplicates alone never cause a non-zero exit code — or `0` otherwise.

## Batch ids and rollback

Every expense created by an `import` run is tagged so it can be found and undone later. The tag **replaces** the row's `Details` value entirely: every created expense's `Details` field is forced to exactly `SPLITWISE_CLI_<BatchId>`, regardless of whatever text (if any) was in that row's `Details` cell. This is deliberate — rollback matches on the exact tag string, so it can't be diluted by arbitrary notes text, and it can't accidentally end up looking like `SPLITWISE_CLI | SPLITWISE_CLI_<BatchId>` if your sheet happens to already contain that literal text. If you want your own notes preserved on the Splitwise side, add them after the fact (e.g. via the Splitwise app) rather than in the import sheet's `Details` column.

**BatchId format:** `yyyyMM-yyyyMM-xxxxxx`, e.g. `202605-202607-a1b2c3`.

- The two `yyyyMM` segments are the earliest and latest `Date` among that file's valid rows (both segments are the same value if every row falls in one month).
- The trailing 6-character hex suffix is random and generated fresh for every file, every run. It exists **specifically to prevent collisions** — two separate `import` runs over files with the *same* date range (e.g. re-importing a corrected version of last month's data) would otherwise produce the same date-based id. Rollback always matches on the full id, suffix included, never just the date-range portion.
- **The BatchId is computed once per file**, not once per run — if `import` matches multiple files (e.g. via a glob), each file gets its own independent id.
- The id is only printed once, at the end of the `import` run that created it (one line per file). It isn't recoverable afterward except by reading it back off an already-tagged expense's `Details`, so save it if you might need to undo the import.

**Undoing a batch:**

```powershell
splitwise rollback <batchId>
```

This decodes the date range out of `<batchId>`, fetches expenses in that range from Splitwise, filters them down to the ones whose `Details` contains the exact tag, previews the matches, and — after you confirm — deletes each one via Splitwise's `delete_expense` endpoint. A spinner ("Searching for tagged expenses... (N scanned)") tracks the search/pagination step, and a progress bar ("Deleting expenses") tracks the deletion step.

| Option | Effect |
|---|---|
| (none) | Prints the matched expenses, then prompts `Delete these N expense(s)? This cannot be undone.` before deleting |
| `--dry-run` | Prints the matched expenses and stops — nothing is deleted |
| `-y`, `--yes` | Skips the confirmation prompt and deletes immediately |

If no expenses match the batch id, the command reports that and exits `0` without prompting. Deletions follow the same resilience contract as `import`: one failed delete is recorded and reported, but never aborts the rest. The process exits `1` if any delete failed, `0` otherwise.

**Automatic rollback offer on partial failure:** if a batch (a single file's rows) ends up with **some rows succeeded and some failed**, `import` asks right away, once per such batch: `"<file>: <N> succeeded, <M> failed for batch <BatchId>. Roll back the succeeded expense(s) for this batch?"`. Answering yes immediately deletes just that batch's successfully created expenses (using the expense ids from the run itself, not a re-fetch) and prints a rollback summary; answering no leaves the partial import in place, exactly as before. This prompt always defaults to **no** if you just press enter. It's independent of `import`'s own `-y`/`--yes` flag — with `--yes`, the prompt is skipped entirely (never auto-rolls-back) and a one-line notice is printed instead, telling you the batch id to pass to `splitwise rollback` manually if you want to undo it later.

A batch where every row succeeded, or every row failed, is never offered this prompt — there's nothing partial to reconsider.
