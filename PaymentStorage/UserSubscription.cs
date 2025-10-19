namespace Staticsoft.PaymentStorage;

public record UserSubscription
{
    public required string UserId { get; init; }
    public required string CustomerId { get; init; }
    public required SubscriptionStatus Status { get; init; }
}
