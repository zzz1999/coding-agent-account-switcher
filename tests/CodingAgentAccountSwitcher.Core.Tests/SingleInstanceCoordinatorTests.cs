using CodingAgentAccountSwitcher.App;

namespace CodingAgentAccountSwitcher.Core.Tests;

public sealed class SingleInstanceCoordinatorTests
{
    [Fact]
    public void FirstCoordinatorIsPrimaryAndLaterCoordinatorIsSecondary()
    {
        var instanceKey = $"CodingAgentAccountSwitcherTests.{Guid.NewGuid():N}";
        using var first = new SingleInstanceCoordinator(instanceKey);
        using var second = new SingleInstanceCoordinator(instanceKey);

        Assert.True(first.IsPrimary);
        Assert.False(second.IsPrimary);
        Assert.False(first.NotifyPrimary());
    }

    [Fact]
    public void SecondaryCoordinatorNotifiesThePrimaryCoordinator()
    {
        var instanceKey = $"CodingAgentAccountSwitcherTests.{Guid.NewGuid():N}";
        using var primary = new SingleInstanceCoordinator(instanceKey);
        using var secondary = new SingleInstanceCoordinator(instanceKey);
        using var activationReceived = new ManualResetEventSlim();
        primary.ActivationRequested += (_, _) => activationReceived.Set();

        Assert.True(secondary.NotifyPrimary());
        Assert.True(activationReceived.Wait(TimeSpan.FromSeconds(2)));
    }

    [Fact]
    public void PrimaryContinuesListeningAfterAnActivationCallbackFails()
    {
        var instanceKey = $"CodingAgentAccountSwitcherTests.{Guid.NewGuid():N}";
        using var primary = new SingleInstanceCoordinator(instanceKey);
        using var secondary = new SingleInstanceCoordinator(instanceKey);
        using var secondActivationReceived = new ManualResetEventSlim();
        var invocationCount = 0;
        primary.ActivationRequested += (_, _) =>
        {
            if (Interlocked.Increment(ref invocationCount) == 1)
            {
                throw new InvalidOperationException("Simulated activation failure.");
            }

            secondActivationReceived.Set();
        };

        Assert.True(secondary.NotifyPrimary());
        Assert.True(SpinWait.SpinUntil(
            () => Volatile.Read(ref invocationCount) == 1,
            TimeSpan.FromSeconds(2)));
        Assert.True(secondary.NotifyPrimary());
        Assert.True(secondActivationReceived.Wait(TimeSpan.FromSeconds(2)));
    }

    [Fact]
    public void DisposingThePrimaryAllowsTheNextLaunchToBecomePrimary()
    {
        var instanceKey = $"CodingAgentAccountSwitcherTests.{Guid.NewGuid():N}";
        var primary = new SingleInstanceCoordinator(instanceKey);
        Assert.True(primary.IsPrimary);

        primary.Dispose();

        using var replacement = new SingleInstanceCoordinator(instanceKey);
        Assert.True(replacement.IsPrimary);
    }

    [Fact]
    public async Task ConcurrentCoordinatorsElectExactlyOnePrimary()
    {
        var instanceKey = $"CodingAgentAccountSwitcherTests.{Guid.NewGuid():N}";
        var start = new ManualResetEventSlim();
        var coordinators = Enumerable.Range(0, 12)
            .Select(_ => Task.Run(() =>
            {
                start.Wait();
                return new SingleInstanceCoordinator(instanceKey);
            }))
            .ToArray();

        start.Set();
        var instances = await Task.WhenAll(coordinators);
        try
        {
            Assert.Single(instances, coordinator => coordinator.IsPrimary);
        }
        finally
        {
            foreach (var coordinator in instances)
            {
                coordinator.Dispose();
            }
            start.Dispose();
        }
    }

    [Fact]
    public void DisposedSecondaryCannotNotifyThePrimary()
    {
        var instanceKey = $"CodingAgentAccountSwitcherTests.{Guid.NewGuid():N}";
        using var primary = new SingleInstanceCoordinator(instanceKey);
        var secondary = new SingleInstanceCoordinator(instanceKey);

        secondary.Dispose();
        secondary.Dispose();

        Assert.False(secondary.NotifyPrimary());
    }
}
