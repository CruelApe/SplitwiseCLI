using SplitwiseCLI.Models;

namespace SplitwiseCLI.Api;

public interface ISplitwiseClient
{
    Task<User> GetCurrentUserAsync(CancellationToken cancellationToken = default);

    Task<User> GetUserAsync(long id, CancellationToken cancellationToken = default);

    Task<List<Category>> GetCategoriesAsync(CancellationToken cancellationToken = default);

    Task<List<Group>> GetGroupsAsync(CancellationToken cancellationToken = default);

    Task<Group> GetGroupAsync(long id, CancellationToken cancellationToken = default);

    Task<List<Friend>> GetFriendsAsync(CancellationToken cancellationToken = default);

    Task<Friend> GetFriendAsync(long id, CancellationToken cancellationToken = default);

    Task<List<Expense>> GetExpensesAsync(ExpenseFilter filter, CancellationToken cancellationToken = default);

    Task<Expense> GetExpenseAsync(long id, CancellationToken cancellationToken = default);

    Task<List<Comment>> GetCommentsAsync(long expenseId, CancellationToken cancellationToken = default);

    Task<List<Notification>> GetNotificationsAsync(CancellationToken cancellationToken = default);

    Task<List<Currency>> GetCurrenciesAsync(CancellationToken cancellationToken = default);

    Task<CreateExpenseResponse> CreateExpenseAsync(CreateExpenseRequest request, CancellationToken cancellationToken = default);
}
