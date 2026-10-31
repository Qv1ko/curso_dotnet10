using B2BOrders.Domain.Orders;
using MediatR;

namespace B2BOrders.Application.Orders.PlaceOrder;

public class PlaceOrderHandler {
    private readonly IOrderRepository _orderRepository;

    public PlaceOrderHandler(IOrderRepository orderRepository) {
        _orderRepository = orderRepository;
    }

    public async Task<int> Handle(PlaceOrderCommand command, CancellationToken cancellationToken) {
        var order = new Order(command.CustomerId);

        foreach (var item in command.Items) {
            var price = Money.Of(item.UnitPrice, item.Currency);
            order.AddLine(item.ProductId, item.Quantity, price);
        }

        await _orderRepository.AddAsync(order, cancellationToken);

        return order.Id;
    }
}
