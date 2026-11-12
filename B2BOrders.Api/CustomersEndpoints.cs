using B2BOrders.Application.Customers;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace B2BOrders.Api;

public static class CustomersEndpoints {
    public static void MapCustomersEndpoints(this IEndpointRouteBuilder app) {
        app.MapPost("/customers", async(IMediator mediator, [FromBody] CreateCustomerRequest request) => {
            var command = new CreateCustomerCommand(request.Name, request.Email);
            var customerId = await mediator.Send(command);
            return Results.Created($"/customers/{customerId}", new { Id = customerId });
        });
    }
}

public record CreateCustomerRequest(string Name, string Email);
