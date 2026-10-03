namespace Laminar.Tests;

public sealed class RejectionExceptionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ResilienceRejectedException_ProtectedConstructorPreservesContract(bool withInner)
    {
        // Arrange
        var inner = withInner ? new InvalidOperationException("inner") : null;
        // Act
        var exception = new CustomRejection("policy rejected work", inner);
        // Assert
        Assert.Equal("policy rejected work", exception.Message);
        Assert.Same(inner, exception.InnerException);
        Assert.IsType<Exception>(exception, exactMatch: false);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RateLimitRejectedException_PublicConstructorPreservesContract(bool metadata)
    {
        // Arrange
        // Act
        var exception = metadata ? new RateLimitRejectedException(TimeSpan.FromSeconds(7)) : new RateLimitRejectedException();
        // Assert
        Assert.Equal(metadata ? TimeSpan.FromSeconds(7) : (TimeSpan?) null, exception.RetryAfter);
        Assert.Equal("The rate limiter rejected the operation.", exception.Message);
        Assert.Null(exception.InnerException);
        Assert.IsType<ResilienceRejectedException>(exception, exactMatch: false);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TimeoutRejectedException_PublicConstructorPreservesContract(bool withInner)
    {
        // Arrange
        var inner = withInner ? new OperationCanceledException("deadline") : null;
        // Act
        var exception = withInner ? new TimeoutRejectedException(Period, inner) : new TimeoutRejectedException(Period);
        // Assert
        Assert.Equal(Period, exception.Timeout);
        Assert.Equal($"The operation exceeded its timeout of {Period}.", exception.Message);
        Assert.Same(inner, exception.InnerException);
        Assert.IsType<ResilienceRejectedException>(exception, exactMatch: false);
    }

    [Fact]
    public void CircuitOpenException_PublicConstructorPreservesContract()
    {
        // Arrange
        // Act
        var exception = new CircuitOpenException();
        // Assert
        Assert.Equal("The circuit breaker is open.", exception.Message);
        Assert.Null(exception.InnerException);
        Assert.IsType<ResilienceRejectedException>(exception, exactMatch: false);
    }

    internal sealed class CustomRejection : ResilienceRejectedException
    {
        internal CustomRejection(string message, Exception? inner = null)
            : base(message, inner)
        {
        }
    }
}
