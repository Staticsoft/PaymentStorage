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
    readonly Partition<UserData> Partition = partitions.Get<UserData>(options.UsersPartitionName);

    public async Task<UserSubscription> Get(string userId)
    {
        try
        {
            var item = await Partition.Get(userId);
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

    public async Task Update(string userId, SubscriptionStatus status)
    {
        try
        {
            var item = await Partition.Get(userId);
            var updatedData = new UserData
            {
                CustomerId = item.Data.CustomerId,
                Status = status
            };
            await Partition.Save(new Item<UserData>
            {
                Id = userId,
                Version = item.Version,
                Data = updatedData
            });
        }
        catch (PartitionedStorageItemNotFoundException)
        {
            throw new UserNotFoundException(userId);
        }
    }

    public async Task Create(string userId, string customerId)
    {
        try
        {
            var userData = new UserData
            {
                CustomerId = customerId,
                Status = SubscriptionStatus.New
            };
            
            await Partition.Save(new Item<UserData>
            {
                Id = userId,
                Data = userData
            });
        }
        catch (PartitionedStorageItemAlreadyExistsException)
        {
            throw new UserAlreadyExistsException(userId);
        }
    }

    public async Task Synchronize()
    {
        var allUsers = await Partition.Scan(new ScanOptions());
        
        foreach (var userItem in allUsers)
        {
            var subscriptions = await Billing.Subscriptions.List(userItem.Data.CustomerId);
            var newStatus = DetermineStatus(subscriptions);
            
            if (userItem.Data.Status != newStatus)
            {
                await Update(userItem.Id, newStatus);
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
}
