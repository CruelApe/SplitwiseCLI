using SplitwiseCLI.Classification;
using SplitwiseCLI.Models;
using Xunit;

namespace SplitwiseCLI.Tests;

public class ExpenseClassifierTests
{
    // Names and ids mirror the account in the reference workbook, emoji included,
    // so name matching is tested against the real shapes rather than clean strings.
    private static readonly List<Category> Categories =
    [
        new() { Id = 1, Name = "Utilities", Subcategories = [Sub(5, "Electricity"), Sub(8, "TV/Phone/Internet"), Sub(11, "Other")] },
        new() { Id = 2, Name = "Uncategorized", Subcategories = [Sub(18, "General")] },
        new() { Id = 3, Name = "Food and drink", Subcategories = [Sub(12, "Groceries"), Sub(13, "Dining out")] },
        new() { Id = 4, Name = "Transportation", Subcategories = [Sub(9, "Parking"), Sub(15, "Car"), Sub(33, "Gas/fuel"), Sub(47, "Hotel")] },
    ];

    private static readonly List<Group> Groups =
    [
        new() { Id = 0, Name = "Non-group expenses" },
        new() { Id = 51170136, Name = "🛒 Groceries + Shopping" },
        new() { Id = 51170296, Name = "🧾 Bills" },
        new() { Id = 54027341, Name = "🍔 Food" },
        new() { Id = 76558822, Name = "🧳 Travel / Road Trip" },
        new() { Id = 90442215, Name = "💳 Interest Charges" },
    ];

    private static Subcategory Sub(long id, string name) => new() { Id = id, Name = name };

    private static ExpenseClassifier Default() =>
        new(DefaultClassificationRules.All, Categories, Groups);

    [Theory]
    [InlineData("Coles 0608 Greenvale Vic", 12, 51170136)]
    [InlineData("Coles 0608 Gree", 12, 51170136)]
    [InlineData("Amazon Au Marketplace Sydney Aus", 18, 51170136)]
    [InlineData("Interest Charged At 28.99%", 18, 90442215)]
    [InlineData("Payment Handling Fee (Online)", 18, 90442215)]
    [InlineData("Disney Plus 1800-965160 Aus", 8, 51170296)]
    [InlineData("1800-965160 Aus Disney Plus", 8, 51170296)]
    [InlineData("help.hbomax.com St.", 8, 51170296)]
    [InlineData("Ezi*Public Self Storag Epping Aus", 11, 51170296)]
    [InlineData("ORIENE4454702257 Origin Energy Ho", 5, 51170296)]
    [InlineData("246153968-10512100 Linkt Melbourne", 9, 76558822)]
    [InlineData("Hotel at Booking.com", 47, 76558822)]
    [InlineData("BP Ecco Fuels 9559 Port Melbourn Vic", 33, 76558822)]
    [InlineData("Tap&Gocarwashcampbellf Campbellfield Vic", 15, 76558822)]
    [InlineData("Sq *Broadmeadows - Wes Broadmeadows Aus", 13, 54027341)]
    [InlineData("Zlr*coop Fresh Poult", 13, 54027341)]
    [InlineData("Mickleham Vic Kfc Merrifield", 13, 54027341)]
    public void Classify_AssignsTheSamplesCategoryAndGroup_ForExpenseMerchants(string description, long categoryId, long groupId)
    {
        var result = Default().Classify(description);

        Assert.NotNull(result);
        Assert.Equal(ExpenseKind.Expense, result.Kind);
        Assert.Equal(categoryId, result.CategoryId);
        Assert.Equal(groupId, result.GroupId);
    }

    [Theory]
    [InlineData("Google Microsoft 365 Barangaroo Aus")]
    [InlineData("Google*Modern Strike Sydney NSW")]
    [InlineData("myygr1ea Telstra Services")]
    [InlineData("Merry Rose Seastre P 5055058894 Asawa")]
    [InlineData("Orbitremit R31203088 80 100100099148571")]
    [InlineData("JB Hi-Fi Group Pty Ltd Broadmeadows Aus")]
    [InlineData("Anthropic* Claude Sub San Francisco Ca")]
    public void Classify_RoutesPersonalMerchantsToNonExpense_WithDiningOutAndGroupZero(string description)
    {
        var result = Default().Classify(description);

        Assert.NotNull(result);
        Assert.Equal(ExpenseKind.NonExpense, result.Kind);
        Assert.Equal(13, result.CategoryId);
        Assert.Equal(0, result.GroupId);
    }

    [Fact]
    public void Classify_PrefersTheSpecificItteki_NonExpenseRule_OverTheGenericSquareFoodRule()
    {
        var itteki = Default().Classify("Sq *Itteki Matcha + Co Southbank Aus");
        var otherSquare = Default().Classify("Sq *Too Good Bakers Aberfeldie Aus");

        Assert.Equal(ExpenseKind.NonExpense, itteki!.Kind);
        Assert.Equal(ExpenseKind.Expense, otherSquare!.Kind);
    }

    [Fact]
    public void Classify_ReturnsNull_WhenNoRuleMatches()
    {
        Assert.Null(Default().Classify("Some Merchant The Sample Never Saw"));
    }

    [Theory]
    [InlineData("COLES 0608 GREENVALE")]
    [InlineData("  coles    0608\tgreenvale  ")]
    public void Classify_IgnoresCaseAndWhitespaceDifferences(string description)
    {
        Assert.Equal(12, Default().Classify(description)!.CategoryId);
    }

    [Theory]
    [InlineData("Colesworth Supplies")]
    [InlineData("Kfcx Pty Ltd")]
    public void Classify_RequiresAWholeWord_ForKeywordsThatEndInALetterOrDigit(string description)
    {
        Assert.Null(Default().Classify(description));
    }

    [Fact]
    public void Classify_FirstMatchingRuleWins_WhenSeveralRulesMatch()
    {
        var rules = new[]
        {
            new ClassificationRule(ExpenseKind.Expense, ["Shop"], new CategoryRef("Food and drink", "Groceries"), "Food"),
            new ClassificationRule(ExpenseKind.Expense, ["Corner Shop"], new CategoryRef("Food and drink", "Dining out"), "Food"),
        };

        var result = new ExpenseClassifier(rules, Categories, Groups).Classify("The Corner Shop");

        Assert.Equal(12, result!.CategoryId);
    }

    [Fact]
    public void Classify_IsDeterministic_AcrossRepeatedCallsAndSeparateInstances()
    {
        var first = Default();
        var second = Default();

        foreach (var description in new[] { "Coles 0608 Greenvale", "Linkt Melbourne", "Google Youtube Bara", "Unknown" })
        {
            Assert.Equal(first.Classify(description), first.Classify(description));
            Assert.Equal(first.Classify(description), second.Classify(description));
        }
    }

    [Fact]
    public void Classify_LeavesTheGroupBlank_WhenTheAccountHasNoGroupWithThatName()
    {
        var groupsWithoutBills = Groups.Where(g => g.Name != "🧾 Bills").ToList();

        var result = new ExpenseClassifier(DefaultClassificationRules.All, Categories, groupsWithoutBills)
            .Classify("Disney Plus 1800-965160 Aus");

        Assert.Equal(8, result!.CategoryId);
        Assert.Null(result.GroupId);
    }

    [Fact]
    public void Classify_LeavesTheCategoryBlank_WhenTheAccountHasNoSuchSubcategory()
    {
        var withoutGroceries = new List<Category>
        {
            new() { Id = 3, Name = "Food and drink", Subcategories = [Sub(13, "Dining out")] },
        };

        var result = new ExpenseClassifier(DefaultClassificationRules.All, withoutGroceries, Groups)
            .Classify("Coles 0608 Greenvale Vic");

        Assert.Null(result!.CategoryId);
        Assert.Equal(51170136, result.GroupId);
    }

    [Fact]
    public void Classify_LeavesTheGroupBlank_WhenTwoGroupsShareTheSameName()
    {
        // Emoji/punctuation are ignored when comparing, so two groups that differ only by
        // an emoji are indistinguishable - guessing one would risk the wrong group.
        List<Group> duplicateBills =
        [
            new() { Id = 1, Name = "🧾 Bills" },
            new() { Id = 2, Name = "Bills" },
        ];

        var result = new ExpenseClassifier(DefaultClassificationRules.All, Categories, duplicateBills)
            .Classify("Disney Plus 1800-965160 Aus");

        Assert.Null(result!.GroupId);
    }

    [Fact]
    public void Classify_MatchesGroupsByName_IgnoringEmojiSpacingAndPunctuation()
    {
        List<Group> groups = [new() { Id = 7, Name = "🛍️ Groceries+Shopping " }];

        var result = new ExpenseClassifier(DefaultClassificationRules.All, Categories, groups)
            .Classify("Coles 0608 Greenvale Vic");

        Assert.Equal(7, result!.GroupId);
    }
}
