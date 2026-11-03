namespace B2BOrders.Application.Billings;

public record PaymentResult(bool Success, string? TransactionId, string? errorMessage = null);

public interface IBillingService {
    Task<PaymentResult> CreatePaymentAsync(int orderId, decimal amount, string currency);
}
