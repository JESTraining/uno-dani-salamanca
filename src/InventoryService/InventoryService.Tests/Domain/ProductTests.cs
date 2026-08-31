using InventoryService.Domain;

namespace InventoryService.Tests.Domain;

public class ProductTests
{
    private static Product ValidProduct(int stock = 10) => Product.Create("SKU-1", "Cat Tower", null, 49.99m, stock);

    [Fact]
    public void Create_WithValidData_HasZeroReservedAndVersionOne()
    {
        var product = ValidProduct();

        Assert.Equal(0, product.ReservedQuantity);
        Assert.Equal(1, product.Version);
        Assert.Equal(10, product.AvailableQuantity);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Create_WithoutSku_Throws(string sku)
    {
        Assert.Throws<InvalidInventoryOperationException>(() => Product.Create(sku, "Cat Tower", null, 49.99m, 10));
    }

    [Fact]
    public void Create_WithNonPositiveUnitPrice_Throws()
    {
        Assert.Throws<InvalidInventoryOperationException>(() => Product.Create("SKU-1", "Cat Tower", null, 0m, 10));
    }

    [Fact]
    public void Reserve_WithinAvailableStock_IncreasesReservedAndVersion()
    {
        var product = ValidProduct(stock: 10);

        product.Reserve(4);

        Assert.Equal(4, product.ReservedQuantity);
        Assert.Equal(6, product.AvailableQuantity);
        Assert.Equal(2, product.Version);
    }

    [Fact]
    public void Reserve_MoreThanAvailable_Throws_PreventingOverselling()
    {
        var product = ValidProduct(stock: 5);
        product.Reserve(5);

        Assert.Throws<InvalidInventoryOperationException>(() => product.Reserve(1));
        Assert.Equal(0, product.AvailableQuantity);
    }

    [Fact]
    public void Reserve_ExactlyAllRemainingStock_Succeeds()
    {
        var product = ValidProduct(stock: 5);

        product.Reserve(5);

        Assert.Equal(0, product.AvailableQuantity);
    }

    [Fact]
    public void ReleaseReservation_ReducesReservedQuantity()
    {
        var product = ValidProduct(stock: 10);
        product.Reserve(4);

        product.ReleaseReservation(4);

        Assert.Equal(0, product.ReservedQuantity);
        Assert.Equal(10, product.AvailableQuantity);
    }

    [Fact]
    public void ReleaseReservation_MoreThanReserved_Throws()
    {
        var product = ValidProduct(stock: 10);
        product.Reserve(2);

        Assert.Throws<InvalidInventoryOperationException>(() => product.ReleaseReservation(3));
    }

    [Fact]
    public void ConfirmReservation_ReducesBothStockAndReserved()
    {
        var product = ValidProduct(stock: 10);
        product.Reserve(4);

        product.ConfirmReservation(4);

        Assert.Equal(6, product.StockQuantity);
        Assert.Equal(0, product.ReservedQuantity);
    }

    [Fact]
    public void UpdateStock_BelowReservedQuantity_Throws()
    {
        var product = ValidProduct(stock: 10);
        product.Reserve(6);

        Assert.Throws<InvalidInventoryOperationException>(() => product.UpdateStock(5));
    }

    [Fact]
    public void UpdateStock_AtOrAboveReservedQuantity_Succeeds()
    {
        var product = ValidProduct(stock: 10);
        product.Reserve(6);

        product.UpdateStock(20);

        Assert.Equal(20, product.StockQuantity);
        Assert.Equal(14, product.AvailableQuantity);
    }
}
