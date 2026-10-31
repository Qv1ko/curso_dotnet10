using MediatR;

namespace B2BOrders.Application.Orders.PlaceOrder;

public record OrderItemDto(int ProductId, int Quantity, decimal UnitPrice, string Currency);
public record PlaceOrderCommand(int CustomerId, List<OrderItemDto> Items): IRequest<int>;
