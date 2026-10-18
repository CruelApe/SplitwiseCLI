using System.Text;
using System.Text.RegularExpressions;
using SplitwiseCLI.Models;
using Group = SplitwiseCLI.Models.Group;

namespace SplitwiseCLI.Classification;

// CategoryId/GroupId are null when the rule matched but the account has no
// category/group with the rule's name - the cell is then left blank for review,
// the same as a row no rule matched.
public sealed record ExpenseClassification(ExpenseKind Kind, long? CategoryId, long? GroupId);

// Deterministic, first-match-wins classifier. Rules reference categories and groups
// by name; those names are resolved to this account's real ids once, up front, from
// the live category/group lists - so no id is hardcoded, and a rule whose category or
// group doesn't exist on the account simply resolves to blank instead of a wrong id.
public sealed class ExpenseClassifier
{
    // Splitwise's pseudo-group for non-group expenses.
    public const long NonGroupId = 0;

    private readonly List<ResolvedRule> _rules;

    public ExpenseClassifier(
        IEnumerable<ClassificationRule> rules,
        IEnumerable<Category> categories,
        IEnumerable<Group> groups)
    {
        var categoryIds = new Dictionary<(string Parent, string Name), long>();
        foreach (var category in categories)
        {
            foreach (var subcategory in category.Subcategories)
            {
                // TryAdd: if the account somehow has two identically named subcategories
                // under one parent, the first one returned by the API wins, every time.
                categoryIds.TryAdd((NameKey(category.Name), NameKey(subcategory.Name)), subcategory.Id);
            }
        }

        // A group name that matches more than one group (e.g. two "House Mortgage"
        // groups that differ only by a flag emoji) is ambiguous, so it is left
        // unresolved rather than guessed.
        var groupIds = groups
            .Where(g => g.Id != NonGroupId && g.Name is not null)
            .GroupBy(g => NameKey(g.Name!))
            .Where(g => g.Count() == 1)
            .ToDictionary(g => g.Key, g => g.Single().Id);

        _rules = rules.Select(rule => Resolve(rule, categoryIds, groupIds)).ToList();
    }

    public ExpenseClassification? Classify(string description)
    {
        var normalized = NormalizeWhitespace(description);

        foreach (var rule in _rules)
        {
            if (rule.Pattern.IsMatch(normalized))
            {
                return new ExpenseClassification(rule.Kind, rule.CategoryId, rule.GroupId);
            }
        }

        return null;
    }

    private static ResolvedRule Resolve(
        ClassificationRule rule,
        Dictionary<(string Parent, string Name), long> categoryIds,
        Dictionary<string, long> groupIds)
    {
        long? categoryId = categoryIds.TryGetValue((NameKey(rule.Category.Parent), NameKey(rule.Category.Name)), out var cid) ? cid : null;

        long? groupId = rule.Kind == ExpenseKind.NonExpense
            ? NonGroupId
            : rule.GroupName is not null && groupIds.TryGetValue(NameKey(rule.GroupName), out var gid) ? gid : null;

        return new ResolvedRule(rule.Kind, BuildPattern(rule.Keywords), categoryId, groupId);
    }

    // A keyword only needs a word boundary on the side(s) where it starts/ends with a
    // letter or digit: "Kfc" must not match inside "Kfcx", but "Sq *" and "Zlr*" end in
    // punctuation and are meant to be followed directly by the merchant name.
    private static Regex BuildPattern(IEnumerable<string> keywords)
    {
        var alternatives = keywords.Select(raw =>
        {
            var keyword = NormalizeWhitespace(raw);
            var before = char.IsLetterOrDigit(keyword[0]) ? "(?<![A-Za-z0-9])" : "";
            var after = char.IsLetterOrDigit(keyword[^1]) ? "(?![A-Za-z0-9])" : "";
            return $"{before}{Regex.Escape(keyword)}{after}";
        });

        return new Regex(string.Join("|", alternatives), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    private static string NormalizeWhitespace(string text) =>
        Regex.Replace(text, @"\s+", " ").Trim();

    // Lowercased letters and digits only. This is what lets the rule name "Bills" match
    // the group "🧾 Bills" and "Travel / Road Trip" match "🧳 Travel / Road Trip": emoji,
    // spacing, and punctuation never take part in the comparison.
    private static string NameKey(string name)
    {
        var key = new StringBuilder(name.Length);
        foreach (var ch in name)
        {
            if (char.IsLetterOrDigit(ch))
            {
                key.Append(char.ToLowerInvariant(ch));
            }
        }

        return key.ToString();
    }

    private sealed record ResolvedRule(ExpenseKind Kind, Regex Pattern, long? CategoryId, long? GroupId);
}
