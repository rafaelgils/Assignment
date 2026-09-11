using Order.Application.Commands.CreateOrder;

namespace Order.Application.Tests;

public class CreateOrderCommandValidatorTests
{
    private readonly CreateOrderCommandValidator _validator = new();

    [Fact]
    public void Valid_when_all_fields_are_correct()
    {
        var command = new CreateOrderCommand(Guid.NewGuid(), Guid.NewGuid(), [new CreateOrderItemInput("Widget", 1, 9.99m)]);

        var result = _validator.Validate(command);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Invalid_when_customer_id_is_empty()
    {
        var command = new CreateOrderCommand(Guid.NewGuid(), Guid.Empty, [new CreateOrderItemInput("Widget", 1, 9.99m)]);

        var result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateOrderCommand.CustomerId));
    }

    [Fact]
    public void Invalid_when_there_are_no_items()
    {
        var command = new CreateOrderCommand(Guid.NewGuid(), Guid.NewGuid(), []);

        var result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateOrderCommand.Items));
    }

    [Fact]
    public void Invalid_when_product_name_is_empty()
    {
        var command = new CreateOrderCommand(Guid.NewGuid(), Guid.NewGuid(), [new CreateOrderItemInput("", 1, 9.99m)]);

        var result = _validator.Validate(command);

        Assert.False(result.IsValid);
    }

    [Theory]
    [InlineData(0, 9.99)]
    [InlineData(-1, 9.99)]
    [InlineData(1, 0)]
    [InlineData(1, -9.99)]
    public void Invalid_when_quantity_or_unit_price_is_not_positive(int quantity, double unitPrice)
    {
        var command = new CreateOrderCommand(Guid.NewGuid(), Guid.NewGuid(), [new CreateOrderItemInput("Widget", quantity, (decimal)unitPrice)]);

        var result = _validator.Validate(command);

        Assert.False(result.IsValid);
    }
}
