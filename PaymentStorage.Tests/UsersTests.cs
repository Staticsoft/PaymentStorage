using Microsoft.Extensions.DependencyInjection;
using Staticsoft.PartitionedStorage.Abstractions;
using Staticsoft.PartitionedStorage.Memory;
using Staticsoft.Payments.Abstractions;
using Staticsoft.Payments.Memory;
using Staticsoft.Testing;

namespace Staticsoft.PaymentStorage.Tests;

public class UsersTests : TestBase<Users>
{
    protected override IServiceCollection Services
        => base.Services
            .AddSingleton<Users>()
            .UseMemoryBilling()
            .AddSingleton<Partitions, MemoryPartitions>()
            .AddSingleton<ItemSerializer, JsonItemSerializer>()
            .AddSingleton(new PaymentStorageOptions());

    Billing Billing
        => Get<Billing>();

    // Level 1: Exception Tests (No State Changes)

    [Fact]
    public async Task ThrowsUserNotFoundExceptionWhenGettingNonExistingUser()
    {
        await Assert.ThrowsAsync<UserNotFoundException>(
            () => SUT.Get("non-existing-id")
        );
    }

    [Fact]
    public async Task ThrowsUserNotFoundExceptionWhenUpdatingNonExistingUser()
    {
        await Assert.ThrowsAsync<UserNotFoundException>(
            () => SUT.Update("non-existing-id", SubscriptionStatus.Active)
        );
    }

    // Level 2: Read-Only Operations on Empty System

    [Fact]
    public async Task SynchronizeCompletesSuccessfullyWhenNoUsersExist()
    {
        await SUT.Synchronize();
        // No exception should be thrown
    }

    // Level 3: Single Create + Verify

    [Fact]
    public async Task CreatesUserAndVerifiesItExistsWithNewStatus()
    {
        await SUT.Create("user-1", "cus-1");

        var user = await SUT.Get("user-1");

        Assert.Equal("user-1", user.UserId);
        Assert.Equal("cus-1", user.CustomerId);
        Assert.Equal(SubscriptionStatus.New, user.Status);
    }

    [Fact]
    public async Task ThrowsUserAlreadyExistsExceptionWhenCreatingSameUserTwice()
    {
        await SUT.Create("user-1", "cus-1");

        await Assert.ThrowsAsync<UserAlreadyExistsException>(
            () => SUT.Create("user-1", "cus-1")
        );
    }

    // Level 5: Multiple Items

    [Fact]
    public async Task CreatesMultipleUsersAndRetrievesEach()
    {
        await SUT.Create("user-1", "cus-1");
        await SUT.Create("user-2", "cus-2");
        await SUT.Create("user-3", "cus-3");

        var user1 = await SUT.Get("user-1");
        var user2 = await SUT.Get("user-2");
        var user3 = await SUT.Get("user-3");

        Assert.Equal("user-1", user1.UserId);
        Assert.Equal("cus-1", user1.CustomerId);
        Assert.Equal(SubscriptionStatus.New, user1.Status);

        Assert.Equal("user-2", user2.UserId);
        Assert.Equal("cus-2", user2.CustomerId);
        Assert.Equal(SubscriptionStatus.New, user2.Status);

        Assert.Equal("user-3", user3.UserId);
        Assert.Equal("cus-3", user3.CustomerId);
        Assert.Equal(SubscriptionStatus.New, user3.Status);
    }

    [Fact]
    public async Task SynchronizesMultipleUsersWithNoSubscriptions()
    {
        await SUT.Create("user-1", "cus-1");
        await SUT.Create("user-2", "cus-2");
        await SUT.Create("user-3", "cus-3");

        await SUT.Synchronize();

        var user1 = await SUT.Get("user-1");
        var user2 = await SUT.Get("user-2");
        var user3 = await SUT.Get("user-3");

        Assert.Equal(SubscriptionStatus.New, user1.Status);
        Assert.Equal(SubscriptionStatus.New, user2.Status);
        Assert.Equal(SubscriptionStatus.New, user3.Status);
    }

    // Level 6: Update Operations

    [Fact]
    public async Task UpdatesUserStatusToTrial()
    {
        await SUT.Create("user-1", "cus-1");

        await SUT.Update("user-1", SubscriptionStatus.Trial);

        var user = await SUT.Get("user-1");
        Assert.Equal(SubscriptionStatus.Trial, user.Status);
        Assert.Equal("user-1", user.UserId);
        Assert.Equal("cus-1", user.CustomerId);
    }

    [Fact]
    public async Task UpdatesUserStatusToActive()
    {
        await SUT.Create("user-1", "cus-1");

        await SUT.Update("user-1", SubscriptionStatus.Active);

        var user = await SUT.Get("user-1");
        Assert.Equal(SubscriptionStatus.Active, user.Status);
    }

    [Fact]
    public async Task UpdatesUserStatusToExpired()
    {
        await SUT.Create("user-1", "cus-1");
        await SUT.Update("user-1", SubscriptionStatus.Trial);

        await SUT.Update("user-1", SubscriptionStatus.Expired);

        var user = await SUT.Get("user-1");
        Assert.Equal(SubscriptionStatus.Expired, user.Status);
    }

    [Fact]
    public async Task SynchronizesUserWithActiveSubscription()
    {
        var customer = await Billing.Customers.Create(new() { Email = "test@example.com" });
        await Billing.Customers.SetupPayments(customer.Id);
        await Billing.Subscriptions.Create(new() { CustomerId = customer.Id });

        await SUT.Create("user-1", customer.Id);
        await SUT.Synchronize();

        var user = await SUT.Get("user-1");
        Assert.Equal(SubscriptionStatus.Active, user.Status);
    }

    [Fact]
    public async Task SynchronizesUserWithTrialingSubscription()
    {
        var customer = await Billing.Customers.Create(new() { Email = "test@example.com" });
        await Billing.Subscriptions.Create(new()
        {
            CustomerId = customer.Id,
            TrialPeriod = TimeSpan.FromDays(14)
        });

        await SUT.Create("user-1", customer.Id);
        await SUT.Synchronize();

        var user = await SUT.Get("user-1");
        Assert.Equal(SubscriptionStatus.Trial, user.Status);
    }

    [Fact]
    public async Task SynchronizesUserWithCanceledSubscription()
    {
        var customer = await Billing.Customers.Create(new() { Email = "test@example.com" });
        await Billing.Customers.SetupPayments(customer.Id);
        var subscription = await Billing.Subscriptions.Create(new() { CustomerId = customer.Id });
        await Billing.Subscriptions.Cancel(subscription.Id);

        await SUT.Create("user-1", customer.Id);
        await SUT.Synchronize();

        var user = await SUT.Get("user-1");
        Assert.Equal(SubscriptionStatus.Expired, user.Status);
    }

    [Fact]
    public async Task SynchronizesUserWithNoSubscriptions()
    {
        var customer = await Billing.Customers.Create(new() { Email = "test@example.com" });

        await SUT.Create("user-1", customer.Id);
        await SUT.Synchronize();

        var user = await SUT.Get("user-1");
        Assert.Equal(SubscriptionStatus.New, user.Status);
    }

    // Level 7: Advanced Scenarios

    [Fact]
    public async Task SynchronizesWithMultipleSubscriptionsPrioritizesActiveStatus()
    {
        var customer = await Billing.Customers.Create(new() { Email = "test@example.com" });
        await Billing.Customers.SetupPayments(customer.Id);
        await Billing.Subscriptions.Create(new() { CustomerId = customer.Id }); // Active
        await Billing.Subscriptions.Create(new()
        {
            CustomerId = customer.Id,
            TrialPeriod = TimeSpan.FromDays(14)
        }); // Trialing

        await SUT.Create("user-1", customer.Id);
        await SUT.Synchronize();

        var user = await SUT.Get("user-1");
        Assert.Equal(SubscriptionStatus.Active, user.Status);
    }

    [Fact]
    public async Task SynchronizesWithMultipleSubscriptionsPrioritizesTrialOverExpired()
    {
        var customer = await Billing.Customers.Create(new() { Email = "test@example.com" });
        await Billing.Subscriptions.Create(new()
        {
            CustomerId = customer.Id,
            TrialPeriod = TimeSpan.FromDays(14)
        }); // Trialing
        await Billing.Customers.SetupPayments(customer.Id);
        var canceledSub = await Billing.Subscriptions.Create(new() { CustomerId = customer.Id });
        await Billing.Subscriptions.Cancel(canceledSub.Id); // Canceled

        await SUT.Create("user-1", customer.Id);
        await SUT.Synchronize();

        var user = await SUT.Get("user-1");
        Assert.Equal(SubscriptionStatus.Trial, user.Status);
    }

    [Fact]
    public async Task SynchronizesWithMultipleExpiredSubscriptionsSetsExpiredStatus()
    {
        var customer = await Billing.Customers.Create(new() { Email = "test@example.com" });
        await Billing.Customers.SetupPayments(customer.Id);
        var sub1 = await Billing.Subscriptions.Create(new() { CustomerId = customer.Id });
        await Billing.Subscriptions.Cancel(sub1.Id); // Canceled
        var sub2 = await Billing.Subscriptions.Create(new() { CustomerId = customer.Id });
        await Billing.Subscriptions.Cancel(sub2.Id); // Canceled

        await SUT.Create("user-1", customer.Id);
        await SUT.Synchronize();

        var user = await SUT.Get("user-1");
        Assert.Equal(SubscriptionStatus.Expired, user.Status);
    }

    [Fact]
    public async Task SynchronizesWithMultipleSubscriptionsPrioritizesActiveOverExpired()
    {
        var customer = await Billing.Customers.Create(new() { Email = "test@example.com" });
        await Billing.Customers.SetupPayments(customer.Id);
        await Billing.Subscriptions.Create(new() { CustomerId = customer.Id }); // Active
        var canceledSub = await Billing.Subscriptions.Create(new() { CustomerId = customer.Id });
        await Billing.Subscriptions.Cancel(canceledSub.Id); // Canceled

        await SUT.Create("user-1", customer.Id);
        await SUT.Synchronize();

        var user = await SUT.Get("user-1");
        Assert.Equal(SubscriptionStatus.Active, user.Status);
    }

    [Fact]
    public async Task SynchronizesHandlesAllPaymentProviderSubscriptionStatuses()
    {
        var billing = Billing;

        // User 1: Active
        var customer1 = await billing.Customers.Create(new() { Email = "user1@example.com" });
        await billing.Customers.SetupPayments(customer1.Id);
        await billing.Subscriptions.Create(new() { CustomerId = customer1.Id });
        await SUT.Create("user-1", customer1.Id);

        // User 2: Trialing
        var customer2 = await billing.Customers.Create(new() { Email = "user2@example.com" });
        await billing.Subscriptions.Create(new()
        {
            CustomerId = customer2.Id,
            TrialPeriod = TimeSpan.FromDays(14)
        });
        await SUT.Create("user-2", customer2.Id);

        // User 3: Canceled
        var customer3 = await billing.Customers.Create(new() { Email = "user3@example.com" });
        await billing.Customers.SetupPayments(customer3.Id);
        var sub3 = await billing.Subscriptions.Create(new() { CustomerId = customer3.Id });
        await billing.Subscriptions.Cancel(sub3.Id);
        await SUT.Create("user-3", customer3.Id);

        // User 4: Incomplete (no payment setup)
        var customer4 = await billing.Customers.Create(new() { Email = "user4@example.com" });
        await billing.Subscriptions.Create(new() { CustomerId = customer4.Id });
        await SUT.Create("user-4", customer4.Id);

        // User 5: No subscriptions (Unpaid scenario - similar to no subscriptions)
        var customer5 = await billing.Customers.Create(new() { Email = "user5@example.com" });
        await SUT.Create("user-5", customer5.Id);

        await SUT.Synchronize();

        var user1 = await SUT.Get("user-1");
        var user2 = await SUT.Get("user-2");
        var user3 = await SUT.Get("user-3");
        var user4 = await SUT.Get("user-4");
        var user5 = await SUT.Get("user-5");

        Assert.Equal(SubscriptionStatus.Active, user1.Status);
        Assert.Equal(SubscriptionStatus.Trial, user2.Status);
        Assert.Equal(SubscriptionStatus.Expired, user3.Status);
        Assert.Equal(SubscriptionStatus.Expired, user4.Status);
        Assert.Equal(SubscriptionStatus.New, user5.Status);
    }
}
