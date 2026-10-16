using FinanceTracker.Cli;
using FinanceTracker.Storage;

namespace FinanceTracker.Tests;

/// <summary>
/// CLI tests. Each invocation builds a fresh <see cref="CommandLine"/> over
/// the same data file, which also exercises persistence across runs.
/// </summary>
public sealed class CommandLineTests : IDisposable
{
    private readonly string _filePath;
    private readonly StringWriter _out = new();
    private readonly StringWriter _err = new();

    public CommandLineTests()
    {
        _filePath = Path.Combine(Path.GetTempPath(), $"ft-cli-test-{Guid.NewGuid():N}.json");
    }

    public void Dispose()
    {
        _out.Dispose();
        _err.Dispose();
        if (File.Exists(_filePath))
            File.Delete(_filePath);
    }

    private int Run(params string[] args) =>
        new CommandLine(new JsonTransactionStore(_filePath), _out, _err).Run(args);

    private void ClearOutput() => _out.GetStringBuilder().Clear();

    [Fact]
    public void Add_ValidArgs_SavesTransactionAndPrintsId()
    {
        var code = Run("add", "--date", "2026-10-16", "--type", "expense",
            "--category", "Food", "--amount", "12.50", "--description", "Lunch");

        Assert.Equal(0, code);
        Assert.Contains("Added expense of 12.50 (Food)", _out.ToString());

        var stored = Assert.Single(new JsonTransactionStore(_filePath).Transactions);
        Assert.Equal("Lunch", stored.Description);
    }

    [Fact]
    public void Add_InvalidAmount_ReturnsUsageError()
    {
        var code = Run("add", "--date", "2026-10-16", "--type", "expense",
            "--category", "Food", "--amount", "abc", "--description", "Lunch");

        Assert.Equal(2, code);
        Assert.Contains("--amount", _err.ToString());
    }

    [Fact]
    public void Add_UnknownCategory_ReturnsUsageError()
    {
        var code = Run("add", "--date", "2026-10-16", "--type", "expense",
            "--category", "Spaceships", "--amount", "10", "--description", "Lunch");

        Assert.Equal(2, code);
        Assert.Contains("not valid", _err.ToString());
    }

    [Fact]
    public void Add_MissingOption_ReturnsUsageError()
    {
        var code = Run("add", "--date", "2026-10-16", "--type", "expense");

        Assert.Equal(2, code);
        Assert.Contains("Missing required option", _err.ToString());
    }

    [Fact]
    public void Summary_PrintsTotalsForMonth()
    {
        Run("add", "--date", "2026-10-01", "--type", "income", "--category", "Salary",
            "--amount", "2500", "--description", "Paycheck");
        Run("add", "--date", "2026-10-02", "--type", "expense", "--category", "Food",
            "--amount", "100", "--description", "Groceries");

        ClearOutput();
        var code = Run("summary", "--month", "2026-10");

        Assert.Equal(0, code);
        var output = _out.ToString();
        Assert.Contains("2500.00", output);
        Assert.Contains("100.00", output);
        Assert.Contains("2400.00", output);
    }

    [Fact]
    public void BudgetSet_ThenStatus_ShowsSpentAgainstLimit()
    {
        Run("add", "--date", "2026-10-02", "--type", "expense", "--category", "Food",
            "--amount", "100", "--description", "Groceries");
        Run("budget", "set", "--category", "Food", "--limit", "300");

        ClearOutput();
        var code = Run("budget", "status", "--month", "2026-10");

        Assert.Equal(0, code);
        var output = _out.ToString();
        Assert.Contains("Food", output);
        Assert.Contains("100.00", output);
        Assert.Contains("300.00", output);
    }

    [Fact]
    public void Delete_UnknownId_ReturnsError()
    {
        var code = Run("delete", "--id", Guid.NewGuid().ToString());

        Assert.Equal(1, code);
        Assert.Contains("no transaction", _err.ToString());
    }

    [Fact]
    public void Delete_ExistingId_RemovesTransaction()
    {
        Run("add", "--date", "2026-10-02", "--type", "expense", "--category", "Food",
            "--amount", "100", "--description", "Groceries");
        var id = new JsonTransactionStore(_filePath).Transactions.Single().Id.ToString();

        var code = Run("delete", "--id", id);

        Assert.Equal(0, code);
        Assert.Empty(new JsonTransactionStore(_filePath).Transactions);
    }

    [Fact]
    public void UnknownCommand_ReturnsUsageError()
    {
        var code = Run("frobnicate");

        Assert.Equal(2, code);
        Assert.Contains("Unknown command", _err.ToString());
    }

    [Fact]
    public void Help_PrintsUsage()
    {
        var code = Run("help");

        Assert.Equal(0, code);
        Assert.Contains("Usage:", _out.ToString());
    }
}
