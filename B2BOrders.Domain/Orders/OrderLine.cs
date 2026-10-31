namespace B2BOrders.Domain.Orders;

public class OrderLine {
    public int Id { get; private set; }
    public int ProductId { get; private set; }
    public int Quantity { get; private set; }
    public Money UnitPrice { get; private set; }

    public Money TotalPrice => new Money(UnitPrice.Amount * Quantity, UnitPrice.Currency);

    private OrderLine() {}

    public OrderLine(int productId, int quantity, Money unitPrice) {
        if (quantity <= 0) {
            throw new ArgumentException("La cantidad debe ser mayor que cero", nameof(quantity));
        }

        ProductId = productId;
        Quantity = quantity;
        UnitPrice = unitPrice;
    }
}