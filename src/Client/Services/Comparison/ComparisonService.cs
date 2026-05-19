using PBL3.Shared.DTOs.Storefront;

namespace Client.Services.Comparison;

public class ComparisonService : IComparisonService
{
    private readonly List<ProductCardResponse> _items = new();

    public IReadOnlyList<ProductCardResponse> Items => _items;

    public event Action? OnChanged;

    public bool Contains(int productId) => _items.Any(p => p.Id == productId);

    public ComparisonAddResult TryAdd(ProductCardResponse product)
    {
        if (Contains(product.Id))
            return ComparisonAddResult.AlreadyExists();

        if (_items.Count >= 3)
            return ComparisonAddResult.Fail("Chỉ có thể so sánh tối đa 3 sản phẩm cùng lúc.");

        if (_items.Count > 0 && _items[0].CategoryId != product.CategoryId)
        {
            var oldCategoryName = _items[0].CategoryName;
            _items.Clear();
            _items.Add(product);
            OnChanged?.Invoke();
            return ComparisonAddResult.ReplacedCategory(oldCategoryName);
        }

        _items.Add(product);
        OnChanged?.Invoke();
        return ComparisonAddResult.Added();
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
