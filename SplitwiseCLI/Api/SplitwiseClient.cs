using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using SplitwiseCLI.Configuration;
using SplitwiseCLI.Models;

namespace SplitwiseCLI.Api;

public sealed class SplitwiseClient : ISplitwiseClient
{
    private const int MaxAttempts = 4;

    // Splitwise isn't fully consistent about whether numeric-looking fields (e.g. ids)
    // are JSON numbers or JSON strings across endpoints, so number fields are read
    // permissively rather than failing deserialization on a quoted number.
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };

    private readonly HttpClient _httpClient;

    public SplitwiseClient(HttpClient httpClient, AppConfig config)
    {
        _httpClient = httpClient;
        _httpClient.BaseAddress = new Uri(config.ApiBaseUrl.TrimEnd('/') + "/");
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", config.ApiKey);
    }

    public async Task<User> GetCurrentUserAsync(CancellationToken cancellationToken = default)
    {
        var result = await GetAsync<CurrentUserResponse>("get_current_user", cancellationToken);
        return result.User;
    }

    public async Task<User> GetUserAsync(long id, CancellationToken cancellationToken = default)
    {
        var result = await GetAsync<CurrentUserResponse>($"get_user/{id}", cancellationToken);
        return result.User;
    }

    public async Task<List<Category>> GetCategoriesAsync(CancellationToken cancellationToken = default)
    {
        var result = await GetAsync<CategoriesResponse>("get_categories", cancellationToken);
        return result.Categories;
    }

    public async Task<List<Group>> GetGroupsAsync(CancellationToken cancellationToken = default)
    {
        var result = await GetAsync<GroupsResponse>("get_groups", cancellationToken);
        return result.Groups;
    }

    public async Task<Group> GetGroupAsync(long id, CancellationToken cancellationToken = default)
    {
        var result = await GetAsync<GroupResponse>($"get_group/{id}", cancellationToken);
        return result.Group;
    }

    public async Task<List<Friend>> GetFriendsAsync(CancellationToken cancellationToken = default)
    {
        var result = await GetAsync<FriendsResponse>("get_friends", cancellationToken);
        return result.Friends;
    }

    public async Task<Friend> GetFriendAsync(long id, CancellationToken cancellationToken = default)
    {
        var result = await GetAsync<FriendResponse>($"get_friend/{id}", cancellationToken);
        return result.Friend;
    }

    public async Task<List<Expense>> GetExpensesAsync(ExpenseFilter filter, CancellationToken cancellationToken = default)
    {
        var query = BuildQueryString(new Dictionary<string, string?>
        {
            ["group_id"] = filter.GroupId?.ToString(),
            ["friend_id"] = filter.FriendId?.ToString(),
            ["dated_after"] = filter.DatedAfter?.ToString("O"),
            ["dated_before"] = filter.DatedBefore?.ToString("O"),
            ["updated_after"] = filter.UpdatedAfter?.ToString("O"),
            ["updated_before"] = filter.UpdatedBefore?.ToString("O"),
            ["limit"] = filter.Limit?.ToString(),
            ["offset"] = filter.Offset?.ToString(),
        });

        var result = await GetAsync<ExpensesResponse>($"get_expenses{query}", cancellationToken);
        return result.Expenses;
    }

    public async Task<Expense> GetExpenseAsync(long id, CancellationToken cancellationToken = default)
    {
        var result = await GetAsync<ExpenseResponse>($"get_expense/{id}", cancellationToken);
        return result.Expense;
    }

    public async Task<List<Comment>> GetCommentsAsync(long expenseId, CancellationToken cancellationToken = default)
    {
        var result = await GetAsync<CommentsResponse>($"get_comments{BuildQueryString(new Dictionary<string, string?> { ["expense_id"] = expenseId.ToString() })}", cancellationToken);
        return result.Comments;
    }

    public async Task<List<Notification>> GetNotificationsAsync(CancellationToken cancellationToken = default)
    {
        var result = await GetAsync<NotificationsResponse>("get_notifications", cancellationToken);
        return result.Notifications;
    }

    public async Task<List<Currency>> GetCurrenciesAsync(CancellationToken cancellationToken = default)
    {
        using var response = await SendWithRetryAsync(() => new HttpRequestMessage(HttpMethod.Get, "get_currencies"), cancellationToken);
        await EnsureTransportSuccessAsync(response, cancellationToken);

        using var document = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);

        var root = document.RootElement;
        // Handles both a bare payload and one wrapped in {"currencies": ...}, and both
        // an array-of-objects shape and an object keyed by currency code - Splitwise
        // isn't fully consistent about which of these get_currencies actually returns.
        var payload = root.ValueKind == JsonValueKind.Object && root.TryGetProperty("currencies", out var wrapped)
            ? wrapped
            : root;

        if (payload.ValueKind == JsonValueKind.Array)
        {
            return payload.Deserialize<List<Currency>>(JsonOptions) ?? [];
        }

        if (payload.ValueKind == JsonValueKind.Object)
        {
            var currencies = new List<Currency>();
            foreach (var property in payload.EnumerateObject())
            {
                var unit = property.Value.ValueKind switch
                {
                    JsonValueKind.String => property.Value.GetString(),
                    JsonValueKind.Object when property.Value.TryGetProperty("unit", out var unitProp) => unitProp.GetString(),
                    _ => null,
                };

                currencies.Add(new Currency { CurrencyCode = property.Name, Unit = unit });
            }

            return currencies;
        }

        return [];
    }

    public async Task<CreateExpenseResponse> CreateExpenseAsync(CreateExpenseRequest request, CancellationToken cancellationToken = default)
    {
        using var response = await SendWithRetryAsync(
            () => new HttpRequestMessage(HttpMethod.Post, "create_expense") { Content = JsonContent.Create(request, options: JsonOptions) },
            cancellationToken);

        await EnsureTransportSuccessAsync(response, cancellationToken);

        var body = await response.Content.ReadFromJsonAsync<CreateExpenseResponse>(JsonOptions, cancellationToken)
            ?? throw new SplitwiseApiException("Splitwise returned an empty response for create_expense.");

        // Some POST endpoints return HTTP 200 even when the operation failed, so the
        // body's success/errors must be checked explicitly rather than trusting the status code.
        if (body.Success == false)
        {
            var errorText = body.Errors?.ToString() ?? "unknown error";
            throw new SplitwiseApiException($"Splitwise rejected the expense: {errorText}");
        }

        return body;
    }

    private static string BuildQueryString(Dictionary<string, string?> parameters)
    {
        var pairs = parameters
            .Where(kvp => kvp.Value is not null)
            .Select(kvp => $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value!)}")
            .ToList();

        return pairs.Count == 0 ? string.Empty : "?" + string.Join('&', pairs);
    }

    private async Task<T> GetAsync<T>(string relativeUrl, CancellationToken cancellationToken)
    {
        using var response = await SendWithRetryAsync(() => new HttpRequestMessage(HttpMethod.Get, relativeUrl), cancellationToken);
        await EnsureTransportSuccessAsync(response, cancellationToken);

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        try
        {
            return JsonSerializer.Deserialize<T>(body, JsonOptions)
                ?? throw new SplitwiseApiException($"Splitwise returned an empty response for {relativeUrl}.");
        }
        catch (JsonException ex)
        {
            // Diagnostic only: reports the JSON *type* (and short raw fragment) at the
            // failing path, not the wider payload, so the mismatch can be fixed without
            // needing to inspect the full (potentially sensitive) response body.
            var detail = DescribeJsonMismatch(body, ex.Path);
            throw new SplitwiseApiException(
                $"Failed to parse Splitwise response for {relativeUrl} at {ex.Path}: {ex.Message}{detail}", innerException: ex);
        }
    }

    private static string DescribeJsonMismatch(string body, string? path)
    {
        if (path is null)
        {
            return string.Empty;
        }

        try
        {
            using var document = JsonDocument.Parse(body);
            JsonElement? current = document.RootElement.Clone();

            foreach (var segment in path.TrimStart('$', '.').Split('.', StringSplitOptions.RemoveEmptyEntries))
            {
                if (current is null)
                {
                    break;
                }

                var arrayMatch = System.Text.RegularExpressions.Regex.Match(segment, @"^(\w+)\[(\d+)\]$");
                if (arrayMatch.Success)
                {
                    current = current.Value.TryGetProperty(arrayMatch.Groups[1].Value, out var arrProp) && arrProp.ValueKind == JsonValueKind.Array
                        && int.Parse(arrayMatch.Groups[2].Value) is var index && index < arrProp.GetArrayLength()
                        ? arrProp[index]
                        : null;
                }
                else
                {
                    current = current.Value.TryGetProperty(segment, out var prop) ? prop : null;
                }
            }

            if (current is null)
            {
                return string.Empty;
            }

            var raw = current.Value.GetRawText();
            return $" (found {current.Value.ValueKind}: {(raw.Length > 80 ? raw[..80] + "..." : raw)})";
        }
        catch
        {
            return string.Empty;
        }
    }

    private static async Task EnsureTransportSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        throw new SplitwiseApiException(
            $"Splitwise API request to {response.RequestMessage?.RequestUri} failed with {(int)response.StatusCode} {response.StatusCode}: {body}",
            response.StatusCode);
    }

    private async Task<HttpResponseMessage> SendWithRetryAsync(Func<HttpRequestMessage> requestFactory, CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            using var request = requestFactory();
            HttpResponseMessage response;
            try
            {
                response = await _httpClient.SendAsync(request, cancellationToken);
            }
            catch (HttpRequestException) when (attempt < MaxAttempts)
            {
                await DelayBeforeRetryAsync(attempt, retryAfter: null, cancellationToken);
                continue;
            }

            var isRetryableStatus = response.StatusCode == HttpStatusCode.TooManyRequests || (int)response.StatusCode >= 500;
            if (isRetryableStatus && attempt < MaxAttempts)
            {
                var retryAfter = response.Headers.RetryAfter?.Delta;
                response.Dispose();
                await DelayBeforeRetryAsync(attempt, retryAfter, cancellationToken);
                continue;
            }

            return response;
        }

        throw new SplitwiseApiException("Splitwise API request failed after multiple retries.");
    }

    private static Task DelayBeforeRetryAsync(int attempt, TimeSpan? retryAfter, CancellationToken cancellationToken)
    {
        var delay = retryAfter ?? TimeSpan.FromMilliseconds(500 * Math.Pow(2, attempt - 1));
        return Task.Delay(delay, cancellationToken);
    }
}
