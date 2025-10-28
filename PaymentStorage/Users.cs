using Staticsoft.Payments.Abstractions;
using Staticsoft.PartitionedStorage.Abstractions;

namespace Staticsoft.PaymentStorage;

public class Users(
    Billing billing,
    Partitions partitions,
    PaymentStorageOptions options
)
{
    readonly Billing Billing = billing;
    readonly Partition<UserData> UsersPartition = partitions.Get<UserData>(options.UsersPartitionName);
    readonly Partition<CustomerData> CustomersPartition = partitions.Get<CustomerData>(options.CustomersPartitionName);

    public async Task<UserSubscription> Get(string userId)
    {
        try
        {
            var item = await UsersPartition.Get(userId);
            return new UserSubscription
            {
                UserId = userId,
                CustomerId = item.Data.CustomerId,
                Status = item.Data.Status
            };
        }
        catch (PartitionedStorageItemNotFoundException)
        {
            throw new UserNotFoundException(userId);
        }
    }

    public async Task Update(string customerId, SubscriptionStatus status)
    {
        try
        {
            var customerItem = await CustomersPartition.Get(customerId);
            var userId = customerItem.Data.UserId;
            
            var userItem = await UsersPartition.Get(userId);
            
            var updatedData = new UserData
            {
                CustomerId = customerId,
                Status = status
            };
            
            await UsersPartition.Save(new Item<UserData>
            {
                Id = userId,
                Version = userItem.Version,
                Data = updatedData
            });
        }
        catch (PartitionedStorageItemNotFoundException)
        {
            throw new UserNotFoundException(customerId);
        }
    }

    public async Task Create(string userId, string customerId)
    {
        try
        {
            await UsersPartition.Save(new Item<UserData>
            {
                Id = userId,
                Data = new UserData
                {
                    CustomerId = customerId,
                    Status = SubscriptionStatus.New
                }
            });
            
            await CustomersPartition.Save(new Item<CustomerData>
            {
                Id = customerId,
                Data = new CustomerData
                {
                    UserId = userId
                }
            });
        }
        catch (PartitionedStorageItemAlreadyExistsException)
        {
            throw new UserAlreadyExistsException(userId);
        }
    }

    public async Task Synchronize()
    {
        var allUsers = await UsersPartition.Scan(new ScanOptions());
        
        foreach (var userItem in allUsers)
        {
            var subscriptions = await Billing.Subscriptions.List(userItem.Data.CustomerId);
            var newStatus = DetermineStatus(subscriptions);
            
            if (userItem.Data.Status != newStatus)
            {
                await Update(userItem.Data.CustomerId, newStatus);
            }
        }
    }

    static SubscriptionStatus DetermineStatus(IReadOnlyCollection<Subscription> subscriptions)
    {
        if (subscriptions.Count == 0)
            return SubscriptionStatus.New;
        
        var hasActive = false;
        var hasTrialing = false;
        
        foreach (var subscription in subscriptions)
        {
            if (subscription.Status == Payments.Abstractions.SubscriptionStatus.Active)
                hasActive = true;
            else if (subscription.Status == Payments.Abstractions.SubscriptionStatus.Trialing)
                hasTrialing = true;
        }
        
        if (hasActive)
            return SubscriptionStatus.Active;
        
        if (hasTrialing)
            return SubscriptionStatus.Trial;
        
        return SubscriptionStatus.Expired;
    }

    class UserData
    {
        public string CustomerId { get; init; } = string.Empty;
        public SubscriptionStatus Status { get; init; }
    }
    
    class CustomerData
    {
        public string UserId { get; init; } = string.Empty;
    }
}
