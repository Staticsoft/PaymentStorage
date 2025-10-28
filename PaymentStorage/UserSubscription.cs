namespace Staticsoft.PaymentStorage;

/// <summary>
/// Represents a user's subscription information.
/// </summary>
public record UserSubscription
{
    /// <summary>
    /// Gets the unique identifier of the user.
    /// </summary>
    public required string UserId { get; init; }
    
    /// <summary>
    /// Gets the payment provider customer identifier.
    /// </summary>
    public required string CustomerId { get; init; }
    
    /// <summary>
    /// Gets the current subscription status.
    /// </summary>
    public required SubscriptionStatus Status { get; init; }
}
