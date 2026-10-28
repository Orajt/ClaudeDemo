using Shop.SharedKernel;

namespace Shop.IntegrationTests;

public sealed class SharedKernelTests
{
    private const string Code = "orders.sample-code";
    private const string Message = "Opis błędu";

    [Fact]
    [Trait("AC", "AC-3")]
    public void BusinessRuleViolationException_ExposesCodeAndMessage()
    {
        var exception = new BusinessRuleViolationException(Code, Message);

        exception.Code.ShouldBe(Code);
        exception.Message.ShouldBe(Message);
    }

    [Fact]
    [Trait("AC", "AC-3")]
    public void NotFoundException_ExposesCodeAndMessage()
    {
        var exception = new NotFoundException(Code, Message);

        exception.Code.ShouldBe(Code);
        exception.Message.ShouldBe(Message);
    }

    [Fact]
    [Trait("AC", "AC-3")]
    public void DuplicateException_ExposesCodeAndMessage()
    {
        var exception = new DuplicateException(Code, Message);

        exception.Code.ShouldBe(Code);
        exception.Message.ShouldBe(Message);
    }
}
