using FluentAssertions;
using Xunit;
using TransactionProcessor.Application.Domain.Events;
using TransactionProcessor.Application.Services;

namespace TransactionProcessor.Tests.Services;

public class CategoryServiceTests
{
    private readonly CategoryService _sut = new();

    [Theory]
    [InlineData("WOOLWORTHS FOOD", "Groceries", 1)]
    [InlineData("SHELL GARAGE PRETORIA", "Fuel", 2)]
    [InlineData("NETFLIX SUBSCRIPTION", "Entertainment", 4)]
    [InlineData("SALARY PAYMENT", "Salary", 9)]
    [InlineData("ATM WITHDRAWAL SANDTON", "ATM", 11)]
    [InlineData("SPUR RESTAURANTS FOURWAYS", "Dining", 5)]
    [InlineData("DISCHEM PHARMACY", "Healthcare", 6)]
    [InlineData("UNKNOWN MERCHANT XYZ", "Other", 99)]
    public void Categorise_ShouldReturnCorrectCategory(string merchant, string expectedName, int expectedId)
    {
        var evt = new TransactionEvent { MerchantName = merchant, Description = merchant };
        var (id, name) = _sut.Categorise(evt);
        id.Should().Be(expectedId);
        name.Should().Be(expectedName);
    }

    [Fact]
    public void Categorise_WhenDescriptionMatches_ShouldUseDescription()
    {
        var evt = new TransactionEvent { Description = "TAKEALOT ONLINE PURCHASE", MerchantName = "" };
        var (id, name) = _sut.Categorise(evt);
        id.Should().Be(8);
        name.Should().Be("Shopping");
    }
}
