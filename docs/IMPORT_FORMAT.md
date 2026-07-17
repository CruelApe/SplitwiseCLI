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

## Required columns

The first row of each sheet must be a header row containing exactly these five column names. Matching is **case-insensitive** and columns may appear in **any order**; all five are mandatory on every data row.

| Column | Required | Format | Example |
|---|---|---|---|
| `Description` | Yes | Free text | `Groceries` |
| `Cost` | Yes | Decimal, must be greater than 0. Use `.` as the decimal separator. | `42.50` |
| `Date` | Yes | Any standard date format (e.g. `2026-01-15`), or a native Excel date-formatted cell | `2026-01-15` |
| `Category` | Yes | Name of an existing Splitwise **subcategory** (case-insensitive) — see [Categories](#categories-parent-vs-subcategory) below | `Groceries` |
| `Group` | Yes | Name of an existing Splitwise group you belong to (case-insensitive) | `Roommates` |

If the header row is missing one of these columns, the whole file is rejected before any rows are processed:

```
'<file>': missing expected column(s) [<missing>]. Expected header row: Description, Cost, Date, Category, Group.
```

## Categories: parent vs. subcategory

Per the [Splitwise API documentation](https://dev.splitwise.com/), expense categories are organized as a two-level hierarchy: **parent categories** group together several **subcategories** (for example, the parent category "Utilities" contains subcategories like "Cleaning"). **Expenses must be tagged with a subcategory — never a parent category.**

The `Category` column follows that same rule: put a subcategory name in your spreadsheet, not a parent category name. The importer doesn't use a hardcoded list — on every run it calls Splitwise's `get_categories` API live and matches your `Category` value against the real subcategory names on your account. To see the exact, current, valid values (and how they nest under their parent categories), run:

```powershell
splitwise categories
```

## Split behavior

Every imported expense is split **equally** among all current members of the `Group` you specify, and the account whose API key you're using is always the payer. There is no column for an unequal split, a different payer, or a subset of group members — expenses needing those need to be created directly in Splitwise instead of via `import`.

A group that currently has no members fails validation:

```
Group '<name>' has no members to split with.
```

## Currency

`Cost` is a plain number — there is no currency column. Every expense created by a given import run uses one currency for the whole run:

1. `SPLITWISE_DEFAULT_CURRENCY`, if set (via environment variable or `.env`), otherwise
2. your Splitwise account's default currency.

Run `splitwise currencies` to see the currency codes Splitwise accepts.

## Example workbook

Create an `.xlsx` file with a header row and data rows like these:

| Description | Cost | Date | Category | Group |
|---|---|---|---|---|
| Groceries | 42.50 | 2026-01-15 | Groceries | Roommates |
| Internet bill | 60.00 | 2026-01-01 | Cleaning | Roommates |

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
| `Unknown category '<name>'.` | No subcategory with that name exists on your account |
| `Unknown group '<name>'.` | No group with that name exists on your account |
| `Group '<name>' has no members to split with.` | The matched group has zero members |
| `API error: <message>` | Splitwise's API rejected the expense (e.g. a server-side business-rule validation) |
| `Failed to read row <n>: <message>` | The Excel row itself couldn't be parsed (corrupt cell, etc.) |

**File-level failures:**

| Message | Cause |
|---|---|
| `'<file>': missing expected column(s) [<missing>]. Expected header row: Description, Cost, Date, Category, Group.` | Header row is missing a required column |
| `No files matched pattern '<pattern>'.` | The path/glob didn't resolve to any `.xlsx` files |

## Summary output and exit codes

After processing, the CLI prints a summary line (`N succeeded / N failed`) followed by a table listing the File, Row, Description, and Reason for every failed row. The process exits with code `1` if any row failed across any file, or `0` if every row succeeded.
