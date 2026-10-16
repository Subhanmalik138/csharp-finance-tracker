using System.Globalization;
using System.Text.RegularExpressions;
using FinanceTracker.Models;
using FinanceTracker.Services;
using FinanceTracker.Storage;

namespace FinanceTracker.Cli;

/// <summary>
/// Minimal hand-rolled argument parser. Deliberately dependency-free: the
/// command surface is small enough that a library like System.CommandLine
/// would be more machinery than value, and zero dependencies means the
/// tool runs anywhere the .NET runtime exists.
/// </summary>
public sealed class CommandLine
{
    private readonly ITransactionStore _store;
    private readonly TextWriter _out;
    private readonly TextWriter _err;

    public CommandLine(ITransactionStore store, TextWriter? output = null, TextWriter? error = null)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _out = output ?? Console.Out;
        _err = error ?? Console.Error;
    }

    public int Run(string[] args)
    {
        if (args.Length == 0 || args[0] is "-h" or "--help" or "help")
        {
            PrintHelp();
            return 0;
        }

        return args[0].ToLowerInvariant() switch
        {
            "add" => Add(args[1..]),
            "list" => List(args[1..]),
            "summary" => Summary(args[1..]),
            "budget" => BudgetCommand(args[1..]),
            "delete" => Delete(args[1..]),
            _ => Fail($"Unknown command '{args[0]}'. Run 'help' to see usage."),
        };
    }

    // add --date 2026-10-16 --type expense --category Food --amount 12.50 --description "Lunch"
    private int Add(string[] args)
    {
        var options = ParseOptions(args, out var error);
        if (error is not null) return Fail(error);

        if (!Require(options, "date", out var dateText, out error)) return Fail(error!);
        if (!Require(options, "type", out var typeText, out error)) return Fail(error!);
        if (!Require(options, "category", out var categoryText, out error)) return Fail(error!);
        if (!Require(options, "amount", out var amountText, out error)) return Fail(error!);
        if (!Require(options, "description", out var description, out error)) return Fail(error!);

        if (!DateOnly.TryParse(dateText, CultureInfo.InvariantCulture, out var date))
            return Fail($"--date '{dateText}' is not a valid date. Use YYYY-MM-DD.");
        if (!Enum.TryParse<TransactionType>(typeText, ignoreCase: true, out var type))
            return Fail($"--type '{typeText}' is not valid. Use 'income' or 'expense'.");
        if (!Enum.TryParse<Category>(categoryText, ignoreCase: true, out var category))
            return Fail($"--category '{categoryText}' is not valid. Valid values: {ValidCategories()}.");
        if (!decimal.TryParse(amountText, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount))
            return Fail($"--amount '{amountText}' is not a valid number.");

        Transaction transaction;
        try
        {
            transaction = Transaction.Create(date, description, amount, type, category);
        }
        catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException)
        {
            return Fail(ex.Message);
        }

        _store.AddTransaction(transaction);
        _store.Save();
        _out.WriteLine($"Added {transaction.Type.ToString().ToLowerInvariant()} of {transaction.Amount:0.00} ({transaction.Category}) — id {transaction.Id}");
        return 0;
    }

    // list [--month 2026-10] [--category Food]
    private int List(string[] args)
    {
        var options = ParseOptions(args, out var error);
        if (error is not null) return Fail(error);

        var transactions = _store.Transactions.OrderBy(t => t.Date).ThenBy(t => t.Description).ToList();

        if (options.TryGetValue("month", out var monthText))
        {
            if (!TryParseMonth(monthText, out var year, out var month))
                return Fail($"--month '{monthText}' is not valid. Use YYYY-MM.");
            transactions = transactions.Where(t => t.Date.Year == year && t.Date.Month == month).ToList();
        }

        if (options.TryGetValue("category", out var categoryText))
        {
            if (!Enum.TryParse<Category>(categoryText, ignoreCase: true, out var category))
                return Fail($"--category '{categoryText}' is not valid. Valid values: {ValidCategories()}.");
            transactions = transactions.Where(t => t.Category == category).ToList();
        }

        if (transactions.Count == 0)
        {
            _out.WriteLine("No transactions found.");
            return 0;
        }

        _out.WriteLine($"{"Date",-12}{"Type",-9}{"Category",-14}{"Amount",10}  Description");
        foreach (var t in transactions)
        {
            var signed = t.Type == TransactionType.Expense ? "-" : "+";
            _out.WriteLine($"{t.Date:yyyy-MM-dd}  {t.Type,-9}{t.Category,-14}{signed}{t.Amount,9:0.00}  {t.Description}");
        }
        return 0;
    }

    // summary [--month 2026-10]
    private int Summary(string[] args)
    {
        var options = ParseOptions(args, out var error);
        if (error is not null) return Fail(error);

        if (!ResolveMonth(options, out var year, out var month, out error)) return Fail(error!);

        var report = new ReportService(_store).MonthlyReport(year, month);

        _out.WriteLine($"Summary for {year}-{month:00}");
        _out.WriteLine($"  Income:    {report.TotalIncome,10:0.00}");
        _out.WriteLine($"  Expenses:  {report.TotalExpenses,10:0.00}");
        _out.WriteLine($"  Net:       {report.Net,10:0.00}");
        _out.WriteLine();

        if (report.ExpensesByCategory.Count == 0)
        {
            _out.WriteLine("No expenses this month.");
            return 0;
        }

        _out.WriteLine("Expenses by category:");
        foreach (var (category, total) in report.ExpensesByCategory.OrderByDescending(kv => kv.Value))
            _out.WriteLine($"  {category,-14}{total,10:0.00}");
        return 0;
    }

    // budget set --category Food --limit 300
    // budget status [--month 2026-10]
    private int BudgetCommand(string[] args)
    {
        if (args.Length == 0) return Fail("Usage: budget set ... | budget status ...");

        switch (args[0].ToLowerInvariant())
        {
            case "set":
            {
                var options = ParseOptions(args[1..], out var error);
                if (error is not null) return Fail(error);
                if (!Require(options, "category", out var categoryText, out error)) return Fail(error!);
                if (!Require(options, "limit", out var limitText, out error)) return Fail(error!);

                if (!Enum.TryParse<Category>(categoryText, ignoreCase: true, out var category))
                    return Fail($"--category '{categoryText}' is not valid. Valid values: {ValidCategories()}.");
                if (!decimal.TryParse(limitText, NumberStyles.Number, CultureInfo.InvariantCulture, out var limit))
                    return Fail($"--limit '{limitText}' is not a valid number.");

                Budget budget;
                try
                {
                    budget = Budget.Create(category, limit);
                }
                catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException)
                {
                    return Fail(ex.Message);
                }

                _store.SetBudget(budget);
                _store.Save();
                _out.WriteLine($"Budget set: {budget.Category} limited to {budget.MonthlyLimit:0.00} per month.");
                return 0;
            }
            case "status":
            {
                var options = ParseOptions(args[1..], out var error);
                if (error is not null) return Fail(error);
                if (!ResolveMonth(options, out var year, out var month, out error)) return Fail(error!);

                var statuses = new ReportService(_store).BudgetStatus(year, month);
                if (statuses.Count == 0)
                {
                    _out.WriteLine("No budgets set. Use 'budget set --category X --limit N'.");
                    return 0;
                }

                _out.WriteLine($"Budgets for {year}-{month:00}");
                foreach (var s in statuses)
                {
                    var tail = s.OverBudget
                        ? $"OVER by {(s.Spent - s.Limit):0.00}"
                        : $"{s.Remaining:0.00} left";
                    _out.WriteLine($"  {s.Category,-14}spent {s.Spent,8:0.00} / {s.Limit,8:0.00}  {tail}");
                }
                return 0;
            }
            default:
                return Fail($"Unknown budget subcommand '{args[0]}'. Use 'set' or 'status'.");
        }
    }

    // delete --id <guid>
    private int Delete(string[] args)
    {
        var options = ParseOptions(args, out var error);
        if (error is not null) return Fail(error);
        if (!Require(options, "id", out var idText, out error)) return Fail(error!);

        if (!Guid.TryParse(idText, out var id))
            return Fail($"--id '{idText}' is not a valid id.");

        if (!_store.RemoveTransaction(id))
        {
            _err.WriteLine($"Error: no transaction with id '{idText}'.");
            return 1;
        }

        _store.Save();
        _out.WriteLine($"Deleted transaction {id}.");
        return 0;
    }

    private bool ResolveMonth(Dictionary<string, string> options, out int year, out int month, out string? error)
    {
        year = DateTime.Today.Year;
        month = DateTime.Today.Month;
        error = null;

        if (options.TryGetValue("month", out var monthText))
        {
            if (!TryParseMonth(monthText, out year, out month))
            {
                error = $"--month '{monthText}' is not valid. Use YYYY-MM.";
                return false;
            }
        }
        return true;
    }

    private static bool TryParseMonth(string text, out int year, out int month)
    {
        year = 0;
        month = 0;
        if (!Regex.IsMatch(text, @"^\d{4}-\d{2}$"))
            return false;
        year = int.Parse(text[..4], CultureInfo.InvariantCulture);
        month = int.Parse(text[5..], CultureInfo.InvariantCulture);
        return month is >= 1 and <= 12;
    }

    private static Dictionary<string, string> ParseOptions(string[] args, out string? error)
    {
        var options = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        error = null;
        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (!arg.StartsWith("--", StringComparison.Ordinal))
            {
                error = $"Unexpected argument '{arg}'. Options look like --key value.";
                return options;
            }
            var key = arg[2..];
            if (string.IsNullOrEmpty(key))
            {
                error = "Option name must not be empty.";
                return options;
            }
            if (i + 1 >= args.Length || args[i + 1].StartsWith("--", StringComparison.Ordinal))
            {
                error = $"Option '--{key}' needs a value.";
                return options;
            }
            options[key] = args[++i];
        }
        return options;
    }

    private static bool Require(
        Dictionary<string, string> options, string key,
        out string value, out string? error)
    {
        if (!options.TryGetValue(key, out value!))
        {
            error = $"Missing required option '--{key}'.";
            return false;
        }
        error = null;
        return true;
    }

    private static string ValidCategories() =>
        string.Join(", ", Enum.GetNames<Category>());

    private int Fail(string message)
    {
        _err.WriteLine($"Error: {message}");
        return 2;
    }

    private void PrintHelp()
    {
        _out.WriteLine("""
            finance-tracker — a tiny CLI for tracking personal income and expenses.

            Usage:
              finance-tracker add --date YYYY-MM-DD --type income|expense --category CATEGORY --amount N --description "text"
              finance-tracker list [--month YYYY-MM] [--category CATEGORY]
              finance-tracker summary [--month YYYY-MM]
              finance-tracker budget set --category CATEGORY --limit N
              finance-tracker budget status [--month YYYY-MM]
              finance-tracker delete --id GUID
              finance-tracker help

            Categories: Food, Transport, Housing, Utilities, Health, Entertainment,
                        Shopping, Education, Savings, Salary, Freelance, Other

            Data is stored as JSON in ~/.finance-tracker/data.json
            (override with the FINANCE_TRACKER_DATA environment variable).
            """);
    }
}
