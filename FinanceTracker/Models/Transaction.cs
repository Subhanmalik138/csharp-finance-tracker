namespace FinanceTracker.Models;

/// <summary>
/// A single money movement. Amounts are always stored positive; <see cref="Type"/>
/// decides the sign. This removes an entire class of sign-convention bugs
/// (e.g. a negative "expense" that accidentally adds money).
/// </summary>
public sealed record Transaction
{
    public Guid Id { get; init; }
    public DateOnly Date { get; init; }
    public string Description { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public TransactionType Type { get; init; }
    public Category Category { get; init; }

    /// <summary>Signed value: negative for expenses, positive for income.</summary>
    public decimal SignedAmount => Type == TransactionType.Expense ? -Amount : Amount;

    public static Transaction Create(
        DateOnly date,
        string description,
        decimal amount,
        TransactionType type,
        Category category)
    {
        if (string.IsNullOrWhiteSpace(description))
            throw new ArgumentException("Description must not be empty.", nameof(description));
        if (amount <= 0)
            throw new ArgumentOutOfRangeException(nameof(amount), amount, "Amount must be greater than zero.");
        if (date > DateOnly.FromDateTime(DateTime.Today))
            throw new ArgumentOutOfRangeException(nameof(date), date, "Transaction date cannot be in the future.");
        if (!Enum.IsDefined(type))
            throw new ArgumentException($"Unknown transaction type: {type}.", nameof(type));
        if (!Enum.IsDefined(category))
            throw new ArgumentException($"Unknown category: {category}.", nameof(category));

        return new Transaction
        {
            Id = Guid.NewGuid(),
            Date = date,
            Description = description.Trim(),
            Amount = decimal.Round(amount, 2),
            Type = type,
            Category = category,
        };
    }
}
