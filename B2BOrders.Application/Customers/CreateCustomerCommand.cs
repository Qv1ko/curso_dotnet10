using B2BOrders.Domain.Customers;
using MediatR;

namespace B2BOrders.Application.Customers;

public record CreateCustomerCommand(string Name, string Email): IRequest<int>;

public class CreateCustomerHandler: IRequestHandler<CreateCustomerCommand, int> {
    private readonly ICustomerRepository _customerRepository;

    public CreateCustomerHandler(ICustomerRepository customerRepository) {
        _customerRepository = customerRepository;
    }

    public async Task<int> Handle(CreateCustomerCommand request, CancellationToken cancellationToken) {
        var customer = new Customer(request.Name, request.Email);
        await _customerRepository.AddAsync(customer, cancellationToken);
        return customer.Id;
    }
}