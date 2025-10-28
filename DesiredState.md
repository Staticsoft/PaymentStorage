# PaymentStorage - Desired State

## Overview
A library that orchestrates payment subscription management by coordinating between payment providers (via Payments library) and persistent storage (via PartitionedStorage library). It maintains user subscription status and provides synchronization capabilities for keeping local state in sync with the payment provider.

## Core Class

### Users
Concrete class that manages user subscription status by coordinating between Billing and Partitions.

**Constructor**:
```csharp
public class Users(
    Billing billing,
    Partitions partitions,
    PaymentStorageOptions options
)
```

**Methods**:
```csharp
public class Users
{
    /// <summary>
    /// Retrieves subscription information for a user.
    /// </summary>
    /// <param name="userId">The unique identifier of the user.</param>
    /// <returns>The user's subscription information.</returns>
    /// <exception cref="UserNotFoundException">Thrown when the user does not exist.</exception>
    Task<UserSubscription> Get(string userId);
    
    /// <summary>
    /// Creates a new user record linking user ID to customer ID with initial status of New.
    /// </summary>
    /// <param name="userId">The unique identifier of the user.</param>
    /// <param name="customerId">The payment provider customer identifier.</param>
    /// <exception cref="UserAlreadyExistsException">Thrown when a user with the specified ID already exists.</exception>
    Task Create(string userId, string customerId);
    
    /// <summary>
    /// Updates subscription status for a specific user identified by their customer ID.
    /// </summary>
    /// <param name="customerId">The payment provider customer identifier.</param>
    /// <param name="status">The new subscription status.</param>
    /// <exception cref="UserNotFoundException">Thrown when a user with the specified customer ID does not exist.</exception>
    Task Update(string customerId, SubscriptionStatus status);
    
    /// <summary>
    /// Synchronizes subscription status for all users by fetching current state from the payment provider.
    /// For each user, retrieves their subscriptions and updates the stored status accordingly.
    /// When multiple subscriptions exist, uses priority logic: Active > Trial > Expired.
    /// </summary>
    Task Synchronize();
}
```

**Exceptions**:
- `UserNotFoundException` - Thrown when a user with the specified ID or customer ID does not exist
- `UserAlreadyExistsException` - Thrown when attempting to create a user that already exists

## Data Types

### UserSubscription
Represents a user's subscription information.

```csharp
public record UserSubscription
{
    public required string UserId { get; init; }
    public required string CustomerId { get; init; }
    public required SubscriptionStatus Status { get; init; }
}
```

### SubscriptionStatus
Enumeration of possible subscription statuses.

```csharp
public enum SubscriptionStatus
{
    New,      // User has never activated trial or paid subscription
    Trial,    // User is currently in trial period
    Active,   // User has an active paid subscription
    Expired   // User had trial or paid subscription before, now inactive
}
```

### PaymentStorageOptions
Configuration options for PaymentStorage.

```csharp
public class PaymentStorageOptions
{
    public string UsersPartitionName { get; init; } = "Users";
}
```

## Exceptions

### UserNotFoundException
Exception thrown when a user with the specified ID is not found.

```csharp
public class UserNotFoundException(string userId) 
    : Exception($"User with ID '{userId}' not found.");
```

### UserAlreadyExistsException
Exception thrown when attempting to create a user that already exists.

```csharp
public class UserAlreadyExistsException(string userId)
    : Exception($"User with ID '{userId}' already exists.");
```

## Implementation Details

### Status Mapping During Synchronization

When `Synchronize()` is called, for each user:
1. Fetch subscriptions from the payment provider using the stored `customerId`
2. Apply the following logic:
   - **No subscriptions** → Set status to `New`
   - **One subscription** → Map payment provider status to local status:
     - Provider `Active` → `Active`
     - Provider `Trialing` → `Trial`
     - Provider `Canceled`, `Incomplete`, `IncompleteExpired`, `Unpaid` → `Expired`
   - **Multiple subscriptions** → Use priority logic (Active > Trial > Expired):
     - If any subscription is `Active` → `Active`
     - Else if any subscription is `Trialing` → `Trial`
     - Else → `Expired`

### Initial Status

When `Create(userId, customerId)` is called, the user is created with status `New`.

## Test Scenarios

Test scenarios are ordered by increasing complexity, following the test ordering strategy.

### Level 1: Exception Tests (No State Changes)

#### Scenario: Get non-existing user throws UserNotFoundException
**Given** the system is empty  
**When** I try to get a user with ID "non-existing-id"  
**Then** a `UserNotFoundException` is thrown

#### Scenario: Update non-existing user throws UserNotFoundException
**Given** the system is empty  
**When** I try to update a user with customer ID "non-existing-customer-id"  
**Then** a `UserNotFoundException` is thrown

### Level 2: Read-Only Operations on Empty System

#### Scenario: Synchronize completes successfully when no users exist
**Given** the system is empty  
**When** I call Synchronize  
**Then** the operation completes without errors

### Level 3: Single Create + Verify

#### Scenario: Create user and verify it exists with New status
**Given** the system is empty  
**When** I create a user with ID "user-1" and customer ID "cus-1"  
**Then** the user is created successfully  
**And** retrieving the user returns the correct user ID and customer ID  
**And** the user has status `New`

#### Scenario: Create same user twice throws exception
**Given** the system is empty  
**When** I create a user with ID "user-1" and customer ID "cus-1"  
**And** I try to create another user with the same ID "user-1"  
**Then** an exception is thrown

### Level 4: Create + Delete Cycle

*Note: This library doesn't have a Delete operation, so this level is skipped*

### Level 5: Multiple Items

#### Scenario: Create multiple users and retrieve each
**Given** the system is empty  
**When** I create three users with different IDs and customer IDs  
**Then** I can retrieve each user individually  
**And** each user has the correct data and status `New`

#### Scenario: Synchronize with multiple users having no subscriptions
**Given** I have created three users  
**And** none of the users have payment provider subscriptions  
**When** I call Synchronize  
**Then** all users have status `New`

### Level 6: Update Operations

#### Scenario: Update user status to Trial
**Given** I have created a user with status `New`  
**When** I update the user's status to `Trial` using their customer ID  
**Then** retrieving the user shows status `Trial`  
**And** the user ID and customer ID remain unchanged

#### Scenario: Update user status to Active
**Given** I have created a user with status `New`  
**When** I update the user's status to `Active` using their customer ID  
**Then** retrieving the user shows status `Active`

#### Scenario: Update user status to Expired
**Given** I have created a user with status `Trial`  
**When** I update the user's status to `Expired` using their customer ID  
**Then** retrieving the user shows status `Expired`

#### Scenario: Synchronize updates user with active subscription
**Given** I have created a user  
**And** the user has an active payment provider subscription  
**When** I call Synchronize  
**Then** the user's status is updated to `Active`

#### Scenario: Synchronize updates user with trialing subscription
**Given** I have created a user  
**And** the user has a trialing payment provider subscription  
**When** I call Synchronize  
**Then** the user's status is updated to `Trial`

#### Scenario: Synchronize updates user with canceled subscription
**Given** I have created a user  
**And** the user has a canceled payment provider subscription  
**When** I call Synchronize  
**Then** the user's status is updated to `Expired`

#### Scenario: Synchronize updates user with no subscriptions
**Given** I have created a user  
**And** the user has no payment provider subscriptions  
**When** I call Synchronize  
**Then** the user's status is updated to `New`

### Level 7: Advanced Scenarios

#### Scenario: Synchronize with multiple subscriptions prioritizes Active status
**Given** I have created a user  
**And** the user has an Active subscription  
**And** the user has a Trialing subscription  
**When** I call Synchronize  
**Then** the user's status is updated to `Active`

#### Scenario: Synchronize with multiple subscriptions prioritizes Trial over Expired
**Given** I have created a user  
**And** the user has a Trialing subscription  
**And** the user has a Canceled subscription  
**When** I call Synchronize  
**Then** the user's status is updated to `Trial`

#### Scenario: Synchronize with multiple expired subscriptions sets Expired status
**Given** I have created a user  
**And** the user has a Canceled subscription  
**And** the user has an Incomplete subscription  
**When** I call Synchronize  
**Then** the user's status is updated to `Expired`

#### Scenario: Synchronize with multiple subscriptions prioritizes Active over Expired
**Given** I have created a user  
**And** the user has an Active subscription  
**And** the user has a Canceled subscription  
**When** I call Synchronize  
**Then** the user's status is updated to `Active`

#### Scenario: Synchronize handles all payment provider subscription statuses correctly
**Given** I have created five users  
**And** user 1 has an Active subscription  
**And** user 2 has a Trialing subscription  
**And** user 3 has a Canceled subscription  
**And** user 4 has an Incomplete subscription  
**And** user 5 has an Unpaid subscription  
**When** I call Synchronize  
**Then** user 1 has status `Active`  
**And** user 2 has status `Trial`  
**And** user 3 has status `Expired`  
**And** user 4 has status `Expired`  
**And** user 5 has status `Expired`
