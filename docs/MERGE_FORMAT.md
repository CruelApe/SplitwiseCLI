# Merge File Format

`splitwise merge <path> [<path> ...] [-o|--output <file>]` combines several already-formatted `import`-style Excel workbooks **and/or** recognized PDF bank/credit-card statements into a single workbook. This document is the full reference for the merge output format; see the main [README](../README.md) for general CLI usage and [IMPORT_FORMAT.md](IMPORT_FORMAT.md) for the xlsx input format merge reads.

`merge` is a local, offline step — it never calls Splitwise's create or delete endpoints. It only reads your input files and your live Category/Group lists (so the output's reference sheets are accurate), then writes one output file. Run `splitwise import` on the result afterward to actually create expenses.

## Path arguments

Each `<path>` can be a single file, a directory (every `.xlsx`/`.pdf` file directly inside it), or a glob pattern — same rules as `import`'s path argument (see [IMPORT_FORMAT.md](IMPORT_FORMAT.md#path-argument)), extended to also match `.pdf` files. Unlike `import`, `merge` accepts **multiple** `<path>` arguments in one call, so you can combine files from different locations and formats:

```powershell
splitwise merge jan.xlsx feb.xlsx "C:/statements/*.xlsx" march-statement.pdf
```

All paths are resolved, combined, and de-duplicated before merging; if none resolve to any files, the command fails with `No files matched pattern(s) '<patterns>'.` A file whose extension is neither `.xlsx` nor `.pdf` is reported as a skipped-row issue rather than silently ignored.

## Xlsx input columns

Every `.xlsx` input file must already have the columns `import` expects: `Description`, `Cost`, `Date`, `Category` (numeric subcategory id), `Group` (numeric group id), and optional `Details`. Each row is validated the same way `import` validates it (see [IMPORT_FORMAT.md](IMPORT_FORMAT.md#validation-errors) for the exact rules) — **except** `merge` does **not** check that a row's `Category`/`Group` id actually exists on your account. That check still happens later, when you run `import` on the merged output.

Rows that fail validation (missing fields, unparseable cost/date, non-numeric ids) are excluded from the merged output and reported in a "skipped rows" table, same shape as `import`'s failure table.

## PDF statement input

`merge` also accepts PDF bank/credit-card statements directly, extracting each debit transaction as a row. Three formats are currently recognized, detected automatically from the statement's own header row — you don't specify which:

| Institution | Detected by header containing |
|---|---|
| Latitude Go Mastercard | `Date`, `Card`, `Description`, `Debits`, `Credits` |
| Coles Platinum Mastercard | `Processed Date`, `Transaction Date`, `Details`, `Amount` |
| NAB Classic Banking | `Date`, `Particulars`, `Debits`, `Credits`, `Balance` |

A PDF that doesn't match any of these is reported as a skipped-row issue (`Unrecognized statement format`), not silently dropped.

Every row extracted from a PDF has its **`Category` and `Group` left blank** — unlike an xlsx-sourced row, there's no id to carry over, so it's intentionally left for you to fill in by hand afterward using the workbook's reference sheets, the same way you already review a spreadsheet before importing it.

Certain statement lines are recognized and **intentionally excluded entirely** (not added as a row, not surfaced anywhere) because they aren't spending: BPAY payments received, `Cr`-suffixed credit/adjustment entries, and (NAB only) internal transfers funding the Latitude Go or Coles Mastercard cards — those would otherwise double-count the same spend already captured on those cards' own statements.

**Known limitations, read before relying on this for a real statement:**
- PDF text-layout extraction is inherently less reliable than reading a structured spreadsheet. Column bleed, a transaction wrapping across lines, or your statement's exact wording differing slightly from the patterns above can cause a row to be missed or misread. Always check the merged output's row count and a few rows against the real statement before importing.
- The NAB parser can't always tell a debit-only line from a credit-only line: when a line has just one amount plus the running balance (the common case), there's no column position information left in the extracted text to know which column that amount was really under. It currently assumes such a line is a debit, so an income or refund line (e.g. `Salary`) can occasionally show up as a false "expense" — reviewing the Description column before importing is how this gets caught.
- NAB dates that omit the year (e.g. `15 Jul` with no `2026`) assume the current year, which is wrong for a statement spanning a year boundary.

## Output workbook

The output is a single `.xlsx` with three sheets:

### `Expenses`

| Column | Source |
|---|---|
| `Cost` | Copied from the input row |
| `Description` | Copied from the input row |
| `Date` | Copied from the input row, written as `yyyy-MM-dd` |
| `Currency Code` | Your resolved default currency (`SPLITWISE_DEFAULT_CURRENCY` override, or your Splitwise account's default) |
| `Category Name` | An `XLOOKUP` formula resolving `Category` against the `Category Reference Data` sheet |
| `Group Name` | An `XLOOKUP` formula resolving `Group` against the `Group Reference Data` sheet |
| `Split Equally` | Always `true` |
| `Category` | Copied from the input row (xlsx source); **blank** for a PDF-sourced row, for you to fill in |
| `Group` | Copied from the input row (xlsx source); **blank** for a PDF-sourced row, for you to fill in |
| `Details` | Copied from the input row (blank if omitted, and always blank for a PDF-sourced row) — preserved so notes aren't lost when merging |

Rows from all input files are concatenated in the order the files were resolved, then row order within each file.

`Category` and `Group` each have an Excel list data validation (a dropdown) on every data row, sourced from the `Category Id` column of `Category Reference Data` and the `Group Id` column of `Group Reference Data` respectively — so filling in (or correcting) a blank/wrong id in Excel is a pick-from-list rather than a copy-pasted number you have to cross-check by eye.

### `Category Reference Data`

Every subcategory on your account, fetched live via `get_categories` — `Category Type` (the parent category's name), `Name` (the subcategory's name), `Category Id`. Not filtered down to only the ids used in `Expenses` — it's a full, browsable reference so you can edit `Category` values by hand afterward.

### `Group Reference Data`

Every group on your account, fetched live via `get_groups` — `Group Id`, `Group Name Reference`, `Members Count`. Same full-list behavior as the category sheet.

## Output file name and location

Pass `-o`/`--output <file>` to choose the output path explicitly. If omitted, the file name defaults to:

- `Expenses_<Month>.xlsx` if every merged row falls in the same calendar month, or
- `Expenses_<StartMonth>-<EndMonth>.xlsx` otherwise (e.g. `Expenses_May-July.xlsx`),

based on the earliest and latest `Date` among the merged rows, and the folder defaults to:

- the merged files' own folder, if every input file (across all your `<path>` arguments) resolved to the same folder, or
- a `Merged Files` folder in the current directory otherwise (created automatically if it doesn't exist yet), since there's no single source folder to place it alongside.

Either way, the printed summary line always shows the full, absolute path of the file that was written.

## Summary output and exit codes

After merging, the CLI prints `"<N> row(s) merged from <M> file(s) into '<outputPath>'."`, followed by a table of any skipped rows (File, Row, Description, Reason) if there were any. The process exits with code `1` if any row was skipped, or `0` if every row across every file merged cleanly.
