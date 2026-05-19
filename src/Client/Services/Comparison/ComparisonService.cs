using PBL3.Shared.DTOs.Storefront;

namespace Client.Services.Comparison;

public class ComparisonService : IComparisonService
{
    private readonly List<ProductCardResponse> _items = new();

    public IReadOnlyList<ProductCardResponse> Items => _items;

    public event Action? OnChanged;

    public bool Contains(int productId) => _items.Any(p => p.Id == productId);

    public string? TryAdd(ProductCardResponse product)
    {
        if (Contains(product.Id)) return null;
        if (_items.Count >= 3) return "Chỉ có thể so sánh tối đa 3 sản phẩm cùng lúc.";
        if (_items.Count > 0 && _items[0].CategoryId != product.CategoryId)
            return "Chỉ có thể so sánh các sản phẩm cùng danh mục.";
        _items.Add(product);
        OnChanged?.Invoke();
        return null;
    }

    public void Remove(int productId)
    {
        _items.RemoveAll(p => p.Id == productId);
        OnChanged?.Invoke();
    }

    public void Clear()
    {
        _items.Clear();
        OnChanged?.Invoke();
    }
}
