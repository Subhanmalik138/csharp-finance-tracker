using FinanceTracker.Cli;
using FinanceTracker.Storage;

var dataPath = Environment.GetEnvironmentVariable("FINANCE_TRACKER_DATA")
    ?? Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".finance-tracker",
        "data.json");

ITransactionStore store;
try
{
    store = new JsonTransactionStore(dataPath);
}
catch (InvalidDataException ex)
{
    Console.Error.WriteLine($"Error: {ex.Message}");
    return 1;
}

return new CommandLine(store, Console.Out, Console.Error).Run(args);
