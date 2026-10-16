using System.Text.Json;
using FinanceTracker.Models;

namespace FinanceTracker.Storage;

/// <summary>
/// File-backed <see cref="ITransactionStore"/>. Transactions and budgets live
/// in a single JSON document so the data file is always self-consistent.
/// </summary>
public sealed class JsonTransactionStore : ITransactionStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly string _filePath;
    private readonly List<Transaction> _transactions = new();
    private readonly List<Budget> _budgets = new();

    public JsonTransactionStore(string filePath)
    {
        _filePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
        Load();
    }

    public IReadOnlyList<Transaction> Transactions => _transactions.AsReadOnly();
    public IReadOnlyList<Budget> Budgets => _budgets.AsReadOnly();

    public void AddTransaction(Transaction transaction)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        _transactions.Add(transaction);
    }

    public bool RemoveTransaction(Guid id)
    {
        var transaction = _transactions.FirstOrDefault(t => t.Id == id);
        if (transaction is null)
            return false;

        _transactions.Remove(transaction);
        return true;
    }

    public void SetBudget(Budget budget)
    {
        ArgumentNullException.ThrowIfNull(budget);
        _budgets.RemoveAll(b => b.Category == budget.Category);
        _budgets.Add(budget);
    }

    public void Save()
    {
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        var data = new FinanceData
        {
            Transactions = _transactions,
            Budgets = _budgets,
        };
        var json = JsonSerializer.Serialize(data, JsonOptions);

        // Write to a temp file and move it into place: a crash mid-write
        // can never leave a half-written data file behind.
        var tempPath = _filePath + ".tmp";
        File.WriteAllText(tempPath, json);
        File.Move(tempPath, _filePath, overwrite: true);
    }

    private void Load()
    {
        if (!File.Exists(_filePath))
            return;

        string json;
        try
        {
            json = File.ReadAllText(_filePath);
        }
        catch (Exception ex)
        {
            throw new InvalidDataException($"Could not read finance data file '{_filePath}'.", ex);
        }

        FinanceData? data;
        try
        {
            data = JsonSerializer.Deserialize<FinanceData>(json, JsonOptions);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"Finance data file '{_filePath}' is not valid JSON.", ex);
        }

        if (data is null)
            return;

        _transactions.AddRange(data.Transactions ?? Enumerable.Empty<Transaction>());
        _budgets.AddRange(data.Budgets ?? Enumerable.Empty<Budget>());
    }

    internal sealed class FinanceData
    {
        public List<Transaction>? Transactions { get; set; }
        public List<Budget>? Budgets { get; set; }
    }
}
