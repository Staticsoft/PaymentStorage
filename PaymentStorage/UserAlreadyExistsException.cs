namespace Staticsoft.PaymentStorage;

public class UserAlreadyExistsException(string userId)
    : Exception($"User with ID '{userId}' already exists.");
