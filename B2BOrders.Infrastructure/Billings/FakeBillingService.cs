using B2BOrders.Application.Billings;

namespace B2BOrders.Infrastructure.Billings;

public class FakeBillingService: IBillingService {
    public async Task<PaymentResult> CreatePaymentAsync(int orderId, decimal amount, string currency) {
        await Task.Delay(500);

        var TransactionId = Guid.NewGuid().ToString();

        return new PaymentResult(true, TransactionId, null);
    }
}