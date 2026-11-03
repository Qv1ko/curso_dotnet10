namespace B2BOrders.Domain.Customers;

public class Customer {
    public int Id { get; private set; }
    public string Name { get; private set; }
    public string Email { get; private set; }

    private Customer() {}

    public Customer(string name, string email) {
        Name = name;
        Email = email;
    }
}
