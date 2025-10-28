namespace Staticsoft.PaymentStorage;

/// <summary>
/// Configuration options for PaymentStorage.
/// </summary>
public class PaymentStorageOptions
{
    /// <summary>
    /// Gets or sets the name of the partition used to store user data.
    /// Default value is "Users".
    /// </summary>
    public string UsersPartitionName { get; init; } = "Users";
    
    /// <summary>
    /// Gets or sets the name of the partition used to store customer-to-user mappings.
    /// Default value is "Customers".
    /// </summary>
    public string CustomersPartitionName { get; init; } = "Customers";
}
