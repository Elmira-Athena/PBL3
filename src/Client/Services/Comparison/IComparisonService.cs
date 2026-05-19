using PBL3.Shared.DTOs.Storefront;

namespace Client.Services.Comparison;

public interface IComparisonService
{
    IReadOnlyList<ProductCardResponse> Items { get; }
    bool Contains(int productId);
    string? TryAdd(ProductCardResponse product);
    void Remove(int productId);
    void Clear();
    event Action OnChanged;
}
