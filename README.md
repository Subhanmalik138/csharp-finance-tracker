# Finance Tracker

A command-line personal finance tracker in C# (.NET 8). Record income and expenses, set monthly budgets per category, and get spending summaries — all stored in a single JSON file, no database or cloud account needed.

## Why this exists

Most budgeting tools are either web apps that want your bank credentials or spreadsheets you forget to update. This is the middle ground: a fast terminal tool you can script, with just enough structure (categories, budgets, monthly reports) to actually see where money goes.

## Features

- **Transactions** — add, list (with month/category filters), and delete income/expense entries
- **Monthly summaries** — income, expenses, net, and a per-category expense breakdown
- **Category budgets** — set monthly spending caps and see at a glance what's over budget
- **JSON persistence** — everything lives in one human-readable file (`~/.finance-tracker/data.json`, overridable via `FINANCE_TRACKER_DATA`)

## Usage

```bash
# record spending
dotnet run --project FinanceTracker -- add --date 2026-10-16 --type expense \
  --category Food --amount 12.50 --description "Lunch"

# this month's summary
dotnet run --project FinanceTracker -- summary --month 2026-10

# cap food spending at 300/month, then check where you stand
dotnet run --project FinanceTracker -- budget set --category Food --limit 300
dotnet run --project FinanceTracker -- budget status --month 2026-10

# list and delete
dotnet run --project FinanceTracker -- list --month 2026-10 --category Food
dotnet run --project FinanceTracker -- delete --id <guid>
```

## Design decisions

1. **Amounts are always stored positive; the transaction type carries the sign.** This kills an entire class of sign-convention bugs (e.g. a negative "expense" that accidentally adds money). `SignedAmount` derives the sign when reporting needs it.
2. **One JSON document, written atomically.** Transactions and budgets serialize to a single file via a temp-file-then-move write, so a crash mid-save can never leave half-written data behind.
3. **Storage hides behind an `ITransactionStore` interface.** The CLI and reporting code never touch the file system directly, which makes them unit-testable with an in-memory fake.
4. **Hand-rolled argument parser, zero dependencies.** The command surface is small enough that a CLI framework would be more machinery than value — and the tool runs anywhere the .NET runtime exists.

## Running the tests

```bash
dotnet test
```

32 xUnit tests cover domain validation, JSON persistence round-trips (including corrupt-file handling), reporting math, and the CLI commands end to end.

## Tech stack

- C# 12 / .NET 8 (console app, nullable reference types on)
- `System.Text.Json` for persistence (no external packages in the app itself)
- xUnit for tests
