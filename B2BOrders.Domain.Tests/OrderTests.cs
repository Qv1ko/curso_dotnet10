using B2BOrders.Domain.Orders;

namespace B2BOrders.Domain.Tests;

public class OrderTests
{
    [Fact]
    public void AddLine_Should_CalculateTotalCorrectly()
    {
        var order = new Order(1);

        order.AddLine(1, 2, Money.Of(10, "EUR"));
        order.AddLine(2, 1, Money.Of(20, "EUR"));

        Assert.Equal(40m, order.TotalAmount.Amount);
        Assert.Equal("EUR", order.TotalAmount.Currency);
    }
}
