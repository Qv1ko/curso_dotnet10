namespace B2BOrders.Domain.Orders;

public record Money(decimal Amount, string Currency) {
    public static Money Of(decimal amount, string currency) {
        if (amount < 0) {
            throw new ArgumentException("El dinero no puede ser negativo", nameof(amount));
        }

        return new Money(amount, currency);
    }

    public static Money operator +(Money a, Money b) {
        if (a.Currency != b.Currency) {
            throw new InvalidOperationException("No se pueden sumar cantidades con diferentes monedas");
        }

        return new Money(a.Amount + b.Amount, a.Currency);
    }

    public static Money Zero(string currency) => new Money(0, currency);
}