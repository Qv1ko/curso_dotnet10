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
        if (quantity <= 0) {
            throw new ArgumentException("La cantidad debe ser mayor que cero.", nameof(quantity));
        }

        if (Status != OrderStatus.New) {
            throw new ArgumentException("No se pueden añadir productos a un pedido que no está en estado 'New'.")
        }

        var line = new OrderLine(productId, quantity, unitPrice);
        _orderLines.Add(line);
        RecalculateTotal();
    }

    private void RecalculateTotal() {
        TotalAmount = _orderLines.Aggregate(Money.Zero("EUR"), (current, line) => current + line.TotalPrice);
    }

    private readonly List<IDomainEvent> _domainEvents = new();

    public IReadOnlyCollection<IDomainEvent> PullDomainEvents() {
        var events = _domainEvents.ToList();
        _domainEvents.Clear();
        return events;
    }

    private void AddDomainEvent(IDomainEvent domainEvent) {
        _domainEvents.Add(domainEvent);
    }

    public void MarkAdPaid(int PaymentId) {
        if (Status == OrderStatus.Paid) {
            return;
        }

        if (TotalAmount.Amount <= 0) {
            throw new InvalidOperationException("No se puede pagar un pedido con importe cero o negativo.");
        }

        Status = OrderStatus.Paid;

        AddDomainEvent(new OrderPaidDomainEvent(Id, PaymentId, TotalAmount));
    }
}
