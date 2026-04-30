using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using MediatR;
using Moq;
using OrderFlow.Orders.Application.Common;
using OrderFlow.Orders.Application.Common.Behaviors;

namespace OrderFlow.Orders.Application.Tests.Common;

public class ValidationBehaviorTests
{
    [Fact]
    public async Task Handle_NoValidators_CallsNext()
    {
        var behavior = new ValidationBehavior<TestRequest, Result>(
            Enumerable.Empty<IValidator<TestRequest>>());

        var nextCalled = false;
        RequestHandlerDelegate<Result> next = _ =>
        {
            nextCalled = true;
            return Task.FromResult(Result.Success());
        };

        await behavior.Handle(new TestRequest(), next, TestContext.Current.CancellationToken);

        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WithFailures_ThrowsValidationException()
    {
        var validatorMock = new Mock<IValidator<TestRequest>>();
        validatorMock.Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<TestRequest>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult([new ValidationFailure("Name", "Name is required")]));

        var behavior = new ValidationBehavior<TestRequest, Result>(
            [validatorMock.Object]);

        RequestHandlerDelegate<Result> next = _ => Task.FromResult(Result.Success());

        var act = () => behavior.Handle(new TestRequest(), next, TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<ValidationException>();
    }

    public sealed record TestRequest;
}
