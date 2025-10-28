namespace Staticsoft.PaymentStorage;

/// <summary>
/// Exception thrown when a user with the specified ID or customer ID is not found.
/// </summary>
public class UserNotFoundException(string userId)
    : Exception($"User with ID '{userId}' not found.");
