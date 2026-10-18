namespace SplitwiseCLI.Classification;

// The built-in merchant -> Category/Group mapping, transcribed from the reference
// workbook Expenses_June-September.xlsx: every rule below is a merchant that appears
// there with a consistent assignment. Rules are evaluated top to bottom and the first
// match wins, so order matters wherever one rule is a more specific case of another
// (e.g. "Sq *Itteki Matcha" must stay above the generic "Sq *" rule).
//
// Where the sample is not consistent for a merchant, the rule follows the majority
// and a comment says so - the pre-assignment is a starting point for review, not a
// guarantee. Merchants the sample doesn't cover are deliberately left unmatched
// (Category/Group stay blank) rather than guessed at.
public static class DefaultClassificationRules
{
    private static readonly CategoryRef General = new("Uncategorized", "General");
    private static readonly CategoryRef Groceries = new("Food and drink", "Groceries");
    private static readonly CategoryRef DiningOut = new("Food and drink", "Dining out");
    private static readonly CategoryRef Car = new("Transportation", "Car");
    private static readonly CategoryRef Parking = new("Transportation", "Parking");
    private static readonly CategoryRef GasFuel = new("Transportation", "Gas/fuel");
    private static readonly CategoryRef Hotel = new("Transportation", "Hotel");
    private static readonly CategoryRef Electricity = new("Utilities", "Electricity");
    private static readonly CategoryRef UtilitiesOther = new("Utilities", "Other");
    private static readonly CategoryRef TvPhoneInternet = new("Utilities", "TV/Phone/Internet");

    // Group names are matched against the live account ignoring case, emoji and
    // punctuation (see ExpenseClassifier), so "Bills" finds "🧾 Bills".
    private const string GroceriesAndShopping = "Groceries + Shopping";
    private const string Food = "Food";
    private const string InterestCharges = "Interest Charges";
    private const string Bills = "Bills";
    private const string Travel = "Travel / Road Trip";

    // Every row on the sample's Non-Expense sheet carries this one category,
    // whatever the merchant - it is a placeholder, not a per-merchant assignment.
    public static CategoryRef NonExpenseCategory => DiningOut;

    public static IReadOnlyList<ClassificationRule> All { get; } =
    [
        // ---- Non-Expense (personal, non-group) -------------------------------
        // Itteki must precede the generic "Sq *" food rule below.
        NonExpense("Itteki Matcha"),
        NonExpense("Google"),           // every Google* charge in the sample (Microsoft 365, YouTube, Adobe Acrobat, ...)
        NonExpense("Anthropic"),
        NonExpense("Adobe"),
        NonExpense("Patreon"),
        NonExpense("Telstra"),
        NonExpense("Myki"),
        NonExpense("Orbitremit"),
        NonExpense("Merry Rose Seastre"),
        NonExpense("JB Hi Fi", "JB Hi-Fi"),
        NonExpense("City Phones"),
        NonExpense("Officeworks"),
        NonExpense("Direct Chemist Outlet"),
        NonExpense("Greenvale Dentistry"),
        NonExpense("Wm Massage"),

        // ---- Interest Charges -------------------------------------------------
        Expense(General, InterestCharges, "Interest Charged", "Payment Handling Fee", "Credit Card Fee", "Intl Txn Fee"),

        // ---- Groceries + Shopping --------------------------------------------
        Expense(Groceries, GroceriesAndShopping, "Coles", "Greenvale Fresh", "Aldi"),
        // Amazon items were categorised one by one in the sample (Car, Furniture,
        // Electronics, ...); an unannotated Amazon line is General in every case.
        Expense(General, GroceriesAndShopping, "Amazon", "Aliexpress", "Bunnings"),

        // ---- Bills -------------------------------------------------------------
        // Origin appears three times: Electricity twice, Heat/gas once.
        Expense(Electricity, Bills, "Origin Energy"),
        Expense(TvPhoneInternet, Bills, "Disney Plus", "Aussie Broadband", "Amznprimea", "Prime Vide", "hbomax"),
        Expense(UtilitiesOther, Bills, "Ezi*Public Self Storag"),
        Expense(Car, Bills, "RACV Insurance"),

        // ---- Travel / Road Trip ----------------------------------------------
        // BP appears three times: Gas/fuel twice, Car once (a small convenience purchase).
        Expense(GasFuel, Travel, "BP", "HS Family Group", "Metro Narbethong"),
        Expense(Car, Travel, "Mantello Kia", "Post Greenvale Lpo", "Tap&Gocarwashcampbellf", "Tyreforce", "Liberty Greenvale"),
        Expense(Parking, Travel, "Easypark", "Car Park", "Linkt"),
        Expense(Hotel, Travel, "Booking.com"),

        // ---- Food (Dining out) -------------------------------------------------
        // "Sq *" (Square) and "Zlr*" (Zeller) are card-terminal prefixes; every such
        // charge in the sample other than Itteki is Dining out.
        Expense(DiningOut, Food, "Sq *", "Zlr*"),
        Expense(DiningOut, Food,
            "Rod Laver Arena", "Greenvale Charcoal", "Wings Melbourne", "Village Sunshine", "Rashays",
            "Xu Jiong", "Krispy Kreme", "Arvlakemountainalpine", "North And Eight", "Thejollymiller",
            "Zet & Zaatar", "Dumpling Dynasty", "2 Souls", "Al Shiddiq", "Kfc", "Gyg", "Nandos",
            "S Calusa", "Mahantji", "Calavera"),
    ];

    private static ClassificationRule NonExpense(params string[] keywords) =>
        new(ExpenseKind.NonExpense, keywords, NonExpenseCategory, null);

    private static ClassificationRule Expense(CategoryRef category, string groupName, params string[] keywords) =>
        new(ExpenseKind.Expense, keywords, category, groupName);
}
