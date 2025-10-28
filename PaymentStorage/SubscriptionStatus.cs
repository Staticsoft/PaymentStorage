namespace Staticsoft.PaymentStorage;

/// <summary>
/// Enumeration of possible subscription statuses.
/// </summary>
public enum SubscriptionStatus
{
    /// <summary>
    /// User has never activated trial or paid subscription.
    /// </summary>
    New,
    
    /// <summary>
    /// User is currently in trial period.
    /// </summary>
    Trial,
    
    /// <summary>
    /// User has an active paid subscription.
    /// </summary>
    Active,
    
    /// <summary>
    /// User had trial or paid subscription before, now inactive.
    /// </summary>
    Expired
}
