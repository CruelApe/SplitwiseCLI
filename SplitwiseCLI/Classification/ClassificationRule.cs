namespace SplitwiseCLI.Classification;

public enum ExpenseKind
{
    // Shared with a Splitwise group - lands on the "Expenses" sheet.
    Expense,

    // Personal / non-group spending - lands on the "Non-Expense" sheet.
    NonExpense,
}

// A subcategory is identified by its parent category's name plus its own name,
// never by id: "Other" exists under five different parents, and names (unlike ids)
// are what the rules were written against.
public sealed record CategoryRef(string Parent, string Name);

// Keywords are matched case-insensitively against the transaction description, on
// word boundaries (see ExpenseClassifier). GroupName is null for NonExpense rules -
// those always go to Splitwise's pseudo-group 0 ("Non-group expenses").
public sealed record ClassificationRule(
    ExpenseKind Kind,
    IReadOnlyList<string> Keywords,
    CategoryRef Category,
    string? GroupName);
