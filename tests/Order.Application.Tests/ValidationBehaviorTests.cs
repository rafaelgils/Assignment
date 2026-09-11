using FluentValidation;
using FluentValidation.Results;
using Moq;
using Order.Application.Behaviors;

namespace Order.Application.Tests;

public class ValidationBehaviorTests
{
    public record TestRequest(string Name);

    [Fact]
    public async Task Calls_next_when_there_are_no_validators()
    {
        var behavior = new ValidationBehavior<TestRequest, string>([]);
        var called = false;

        var result = await behavior.Handle(new TestRequest("x"), _ =>
        {
            called = true;
            return Task.FromResult("ok");
        }, CancellationToken.None);

        Assert.True(called);
        Assert.Equal("ok", result);
    }

    [Fact]
    public async Task Calls_next_when_validation_passes()
    {
        var validator = new Mock<IValidator<TestRequest>>();
        validator.Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<TestRequest>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

        var behavior = new ValidationBehavior<TestRequest, string>([validator.Object]);

        var result = await behavior.Handle(new TestRequest("x"), _ => Task.FromResult("ok"), CancellationToken.None);

        Assert.Equal("ok", result);
    }

    [Fact]
    public async Task Throws_and_skips_next_when_validation_fails()
    {
        var failure = new ValidationFailure(nameof(TestRequest.Name), "Name is required");
        var validator = new Mock<IValidator<TestRequest>>();
        validator.Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<TestRequest>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult([failure]));

        var behavior = new ValidationBehavior<TestRequest, string>([validator.Object]);
        var called = false;

        await Assert.ThrowsAsync<ValidationException>(() =>
            behavior.Handle(new TestRequest("x"), _ =>
            {
                called = true;
                return Task.FromResult("ok");
            }, CancellationToken.None));

        Assert.False(called);
    }
}
