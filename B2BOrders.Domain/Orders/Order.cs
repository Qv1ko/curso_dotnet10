namespace B2BOrders.Domain.Orders;

public class Order {
    public int Id { get; private set; }
    public int CustomerId { get; private set; }
    public DateTime CreateAt { get; private set; }
    public OrderStatus Status { get; private set; }
    public Money TotalAmount { get; private set; }

    private readonly List<OrderLine> _orderLines = new();
    public IReadOnlyCollection<OrderLine> OrderLines => _orderLines.AsReadOnly();

    private Order(){}

    public Order(int customerId) {
        CustomerId = customerId;
        CreateAt = DateTime.UtcNow;
        Status = OrderStatus.New;
        TotalAmount = Money.Zero("EUR");
    }

    public void AddLine(int productId, int quantity, Money unitPrice) {
        var line = new OrderLine(productId, quantity, unitPrice);
        _orderLines.Add(line);
        RecalculateTotal();
    }

    private void RecalculateTotal() {
        TotalAmount = _orderLines.Aggregate(Money.Zero("EUR"), (current, line) => current + line.TotalPrice);
    }
}
