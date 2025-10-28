namespace Staticsoft.PaymentStorage;

/// <summary>
/// Exception thrown when attempting to create a user that already exists.
/// </summary>
public class UserAlreadyExistsException(string userId)
    : Exception($"User with ID '{userId}' already exists.");
