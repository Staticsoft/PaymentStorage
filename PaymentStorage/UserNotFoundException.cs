namespace Staticsoft.PaymentStorage;

public class UserNotFoundException(string userId)
    : Exception($"User with ID '{userId}' not found.");
