using Demo.BlazorWasm.AppStore;

namespace Demo.BlazorWasm.Components.Pages;

public partial class PageProducts
{
    private string _newProductName = string.Empty;
    private decimal _newProductPrice;
    private string _newProductCategory = "Electronics";

    private ValueCollection<Product> Electronics => State.SelectElectronics();
    private int ElectronicsCount => Electronics.Count;
    private bool HasElectronics => ElectronicsCount > 0;
    private decimal TotalPriceOfElectronics => State.SelectTotalPriceOfElectronics();
    private ValueCollection<Product> Clothing => State.SelectClothing();
    private int ClothingCount => Clothing.Count;
    private bool HasClothing => ClothingCount > 0;
    private decimal TotalPriceOfClothing => State.SelectTotalPriceOfClothing();

    private void CreateProduct()
    {
        if (string.IsNullOrWhiteSpace(_newProductName) || _newProductPrice <= 0)
        {
            return;
        }

        Product newProduct = new(Guid.NewGuid(), _newProductName, _newProductPrice, _newProductCategory);
        Dispatcher.AddProduct(newProduct);
        _newProductName = string.Empty;
        _newProductPrice = 0;
        _newProductCategory = "Electronics";
    }

    private void DeleteProduct(Guid id)
    {
        Dispatcher.RemoveProduct(id);
    }
}
