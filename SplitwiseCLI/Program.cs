using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;
using Spectre.Console.Cli;
using SplitwiseCLI.Cli;

var services = new ServiceCollection();
services.AddSingleton(new SplitwiseClientFactory());
services.AddSingleton<CategoriesCommand>();
services.AddSingleton<ImportCommand>();
services.AddSingleton<ConfigSetKeyCommand>();
services.AddSingleton<ConfigShowCommand>();
services.AddSingleton<ConfigClearCommand>();
services.AddSingleton<MeCommand>();
services.AddSingleton<UserCommand>();
services.AddSingleton<GroupsCommand>();
services.AddSingleton<GroupCommand>();
services.AddSingleton<FriendsCommand>();
services.AddSingleton<FriendCommand>();
services.AddSingleton<ExpensesCommand>();
services.AddSingleton<ExpenseCommand>();
services.AddSingleton<CommentsCommand>();
services.AddSingleton<NotificationsCommand>();
services.AddSingleton<CurrenciesCommand>();
services.AddSingleton<VersionCommand>();
services.AddSingleton<AboutCommand>();

var app = new CommandApp(new TypeRegistrar(services));
app.Configure(config =>
{
    config.SetApplicationName("splitwise");
    config.ValidateExamples();

    config.SetExceptionHandler((ex, _) =>
    {
        AnsiConsole.MarkupLine($"[red]Error:[/] {ex.Message.EscapeMarkup()}");
        return -1;
    });

    config.AddCommand<CategoriesCommand>("categories")
        .WithDescription("List Splitwise expense categories (parent categories and their subcategories).");

    config.AddCommand<ImportCommand>("import")
        .WithDescription("Bulk-import expenses from Excel file(s). Columns: Description, Cost, Date, Category, Group.")
        .WithExample("import", "C:/expenses/january.xlsx")
        .WithExample("import", "C:/expenses/*.xlsx");

    config.AddBranch("config", branch =>
    {
        branch.SetDescription("Manage the locally saved Splitwise API key.");
        branch.AddCommand<ConfigSetKeyCommand>("set-key")
            .WithDescription("Prompt for and save your Splitwise API key.");
        branch.AddCommand<ConfigShowCommand>("show")
            .WithDescription("Show whether an API key is configured and where it's stored.");
        branch.AddCommand<ConfigClearCommand>("clear")
            .WithDescription("Remove the saved API key.");
    });

    config.AddCommand<MeCommand>("me")
        .WithDescription("Show your Splitwise account details.");

    config.AddCommand<UserCommand>("user")
        .WithDescription("Show another Splitwise user's details by id.")
        .WithExample("user", "12345");

    config.AddCommand<GroupsCommand>("groups")
        .WithDescription("List all your Splitwise groups.");

    config.AddCommand<GroupCommand>("group")
        .WithDescription("Show a group's details and members by id.")
        .WithExample("group", "12345");

    config.AddCommand<FriendsCommand>("friends")
        .WithDescription("List your Splitwise friends and balances.");

    config.AddCommand<FriendCommand>("friend")
        .WithDescription("Show a friend's details and balance by id.")
        .WithExample("friend", "12345");

    config.AddCommand<ExpensesCommand>("expenses")
        .WithDescription("List expenses, optionally filtered by group, friend, or date range.")
        .WithExample("expenses")
        .WithExample("expenses", "--group", "12345")
        .WithExample("expenses", "--dated-after", "2026-01-01");

    config.AddCommand<ExpenseCommand>("expense")
        .WithDescription("Show a single expense's details by id.")
        .WithExample("expense", "12345");

    config.AddCommand<CommentsCommand>("comments")
        .WithDescription("List comments on an expense by the expense's id.")
        .WithExample("comments", "12345");

    config.AddCommand<NotificationsCommand>("notifications")
        .WithDescription("List your recent Splitwise activity notifications.");

    config.AddCommand<CurrenciesCommand>("currencies")
        .WithDescription("List currency codes supported by Splitwise.");

    config.AddCommand<VersionCommand>("version")
        .WithDescription("Show the SplitwiseCLI version.");

    config.AddCommand<AboutCommand>("about")
        .WithDescription("Show information about SplitwiseCLI, including author and repository.");
});

if (args.Length == 0)
{
    await InteractiveShell.RunAsync(app);
    return 0;
}

return await app.RunAsync(args);
