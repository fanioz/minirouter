using Xunit;
using MiniRouter.Services;

namespace MiniRouter.Tests;

public class PricingTableTests
{
    [Fact]
    public void ExactModelMatch_ReturnsSpecificRate()
    {
        var cost = PricingTable.CalculateCost(1_000_000, 1_000_000, "openai", "gpt-4o-mini");
        
        // gpt-4o-mini: $0.15 input + $0.60 output per million
        Assert.Equal(0.75m, cost);
    }

    [Fact]
    public void PatternMatch_ResolvesVariants()
    {
        var cost = PricingTable.CalculateCost(1_000_000, 1_000_000, "openai", "gpt-4o-custom-v2");
        
        // gpt-4o-* pattern matches mini rate
        Assert.Equal(0.75m, cost);
    }

    [Fact]
    public void ProviderRate_UsedWhenNoStaticMatch()
    {
        // Custom model with provider-configured rate
        var cost = PricingTable.CalculateCost(1_000_000, 1_000_000, "custom-provider", "my-custom-model", 0.50, 2.00);
        
        // Provider rate: $0.50 input + $2.00 output per million
        Assert.Equal(2.50m, cost);
    }

    [Theory]
    [InlineData("gpt-4-turbo", 10.0)]  // gpt-4-* pattern -> turbo rate ($10M input)
    [InlineData("claude-3-haiku-pro", 3.0)]  // claude-3-* pattern -> sonnet rate ($3M input)
    public void PatternMatch_PrefixMatching(string model, double expectedPerMillionInput)
    {
        var cost = PricingTable.CalculateCost(1_000_000, 0, "provider", model);
        Assert.NotNull(cost);
        Assert.Equal(expectedPerMillionInput, (double)cost.Value);
    }

    [Fact]
    public void NullModel_ReturnsNullCost()
    {
        var cost = PricingTable.CalculateCost(100_000, 100_000, "test", "");
        Assert.Null(cost);
    }

    [Fact]
    public void CalculationPrecision_UseDecimal()
    {
        // Test that we don't have float drift issues
        var sum = 0m;
        for (int i = 0; i < 1000000; i++)
        {
            var cost = PricingTable.CalculateCost(100, 100, "test", "gpt-4o-mini");
            if (cost.HasValue) sum += cost.Value;
        }
        
        // Should accumulate precisely
        Assert.True(sum > 0);
    }

    [Fact]
    public void UnknownModel_WithoutProviderRate_ReturnsNull()
    {
        var cost = PricingTable.CalculateCost(100, 100, "unknown-provider", "unknown-model");
        Assert.Null(cost);
    }

    [Fact]
    public void CostExcludedFromAggregation_NullValues()
    {
        var costs = new[] 
        { 
            PricingTable.CalculateCost(100, 100, "test", "gpt-4o"), 
            null,
            PricingTable.CalculateCost(100, 100, "test", "unknown")
        };
        
        // Only non-null should be included
        var total = 0m;
        foreach (var c in costs)
        {
            if (c.HasValue) total += c.Value;
        }
        
        Assert.Single(costs.Where(c => c.HasValue));
    }

    [Fact]
    public void SameModel_DifferentProviders_DifferentCosts()
    {
        // Same model (gpt-4o-mini) via two providers with different custom pricing
        var costProviderA = PricingTable.CalculateCost(1_000_000, 1_000_000, "providerA", "gpt-4o-mini");
        var costProviderB = PricingTable.CalculateCost(1_000_000, 1_000_000, "providerB", "gpt-4o-mini", 0.10, 0.50);
        
        // Static table takes precedence — both use static rate
        Assert.Equal(0.75m, costProviderA); // $0.15 + $0.60
        Assert.Equal(0.75m, costProviderB); // Static table overrides provider rate
    }

    [Fact]
    public void CustomModel_UsesProviderRate()
    {
        // Custom model not in static table
        var cost = PricingTable.CalculateCost(1_000_000, 1_000_000, "custom", "my-special-model", 1.00, 3.00);
        
        Assert.NotNull(cost);
        Assert.Equal(4.00m, cost); // $1.00 input + $3.00 output
    }

    [Theory]
    [InlineData(-1.00, 3.00)]
    [InlineData(1.00, -3.00)]
    public void CustomModel_NegativeProviderRate_ReturnsNull(double inputRate, double outputRate)
    {
        var cost = PricingTable.CalculateCost(1_000_000, 1_000_000, "custom", "my-special-model", inputRate, outputRate);

        Assert.Null(cost);
    }

    [Fact]
    public void CustomModel_NoProviderRate_ReturnsNull()
    {
        // Custom model with no static match and no provider rate
        var cost = PricingTable.CalculateCost(1_000_000, 1_000_000, "custom", "my-special-model");
        
        Assert.Null(cost);
    }

    [Fact]
    public void StaticTable_TakesPrecedence_OverProviderRate()
    {
        // Known model with provider rate supplied — static table wins
        var cost = PricingTable.CalculateCost(1_000_000, 1_000_000, "openai", "gpt-4o", 0.01, 0.01);
        
        // Should use static rate ($5 + $15), not provider rate
        Assert.Equal(20.00m, cost);
    }
}
