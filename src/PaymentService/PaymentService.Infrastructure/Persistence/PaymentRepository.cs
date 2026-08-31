using Microsoft.EntityFrameworkCore;
using PaymentService.Application.Abstractions;
using PaymentService.Domain;

namespace PaymentService.Infrastructure.Persistence;

public class PaymentRepository : IPaymentRepository
{
    private readonly PaymentDbContext _context;

    public PaymentRepository(PaymentDbContext context)
    {
        _context = context;
    }

    public async Task<Payment?> GetByOrderIdAsync(Guid orderId, CancellationToken cancellationToken) =>
        await _context.Payments.FirstOrDefaultAsync(p => p.OrderId == orderId, cancellationToken);

    public async Task<Payment?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        await _context.Payments.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public async Task AddAsync(Payment payment, CancellationToken cancellationToken) =>
        await _context.Payments.AddAsync(payment, cancellationToken);

    public async Task AddTransactionLogAsync(PaymentTransactionLog log, CancellationToken cancellationToken) =>
        await _context.PaymentTransactionLogs.AddAsync(log, cancellationToken);
}
