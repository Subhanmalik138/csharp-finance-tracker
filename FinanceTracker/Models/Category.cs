namespace FinanceTracker.Models;

/// <summary>
/// Fixed category set. <see cref="Other"/> is the escape hatch for anything unlisted.
/// </summary>
public enum Category
{
    Food,
    Transport,
    Housing,
    Utilities,
    Health,
    Entertainment,
    Shopping,
    Education,
    Savings,
    Salary,
    Freelance,
    Other
}
