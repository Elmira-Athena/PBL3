using PBL3.Shared.DTOs.Storefront;

namespace Client.Services.Comparison;

public sealed class ComparisonAddResult
{
    public bool Success { get; init; }
    public bool Replaced { get; init; }
    public string? OldCategoryName { get; init; }
    public string? ErrorMessage { get; init; }

    public static ComparisonAddResult Added()
        => new() { Success = true };

    public static ComparisonAddResult ReplacedCategory(string oldCategoryName)
        => new() { Success = true, Replaced = true, OldCategoryName = oldCategoryName };

    public static ComparisonAddResult Fail(string message)
        => new() { Success = false, ErrorMessage = message };

    public static ComparisonAddResult AlreadyExists()
        => new() { Success = false };
}

public interface IComparisonService
{
    IReadOnlyList<ProductCardResponse> Items { get; }
    bool Contains(int productId);
    ComparisonAddResult TryAdd(ProductCardResponse product);
    void Remove(int productId);
    void Clear();
    event Action OnChanged;
}
