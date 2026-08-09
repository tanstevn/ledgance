using System;

public abstract class BaseUnitTest<TTestClass, TRequest, TResponse, TRequestHandler> : IAsyncLifetime
        where TTestClass : BaseUnitTest<TTestClass, TRequest, TResponse, TRequestHandler>
        where TRequest : IRequest<TResponse>
        where TRequestHandler : class, IRequestHandler<TRequest, TResponse> {
    protected ApplicationDbContext _dbContext = default!;
    private TRequestHandler _requestHandler = default!;
    protected TRequest _request = default!;
    protected TResponse _expected = default!;

    private TResponse _result = default!;
    protected Exception _exception = default!;

    public async Task InitializeAsync() {
        var unitDatabase = Guid.NewGuid()
            .ToString();

        var dbContextOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(unitDatabase)
            .Options;

        _dbContext = new ApplicationDbContext(dbContextOptions);

        _dbContext.Roles.AddRange(
            Role.Create("Anonymous", "Anonymous Role"),
            Role.Create("Citizen", "Citizen Role")
        );

        await _dbContext.SaveChangesAsync();
    }

    protected abstract TRequestHandler CreateRequestHandler();

    protected virtual void SetupRequestHandler() {
        _requestHandler = CreateRequestHandler();
    }

    public TTestClass Arrange(Action<TRequest>? arrangeRequest = null,
        Action<TResponse>? arrangeExpected = null) {
        _request = Activator.CreateInstance<TRequest>();

        if (arrangeRequest is not null) {
            arrangeRequest(_request);
        }

        if (arrangeExpected is not null) {
            _expected = Activator.CreateInstance<TResponse>();
            arrangeExpected(_expected);
        }

        SetupRequestHandler();
        return (TTestClass)this;
    }

    protected virtual TTestClass Act() {
        try {
            _result = _requestHandler
                .HandleAsync(_request)
                .GetAwaiter()
                .GetResult();
        }
        catch (Exception ex) {
            _exception = ex;
        }

        return (TTestClass)this;
    }

    public void Assert(Action<TResponse>? assertion = null) {
        if (_exception is not null) {
            throw new InvalidOperationException($"Request handler threw {_exception?.GetType().Name}. " +
                $"Did you mean to call {nameof(AssertThrows)} instead of {nameof(Assert)}?");
        }

        if (_expected is null && assertion is null) {
            throw new InvalidOperationException("Assert cannot be done. " +
                "Please define either expected output or assertion argument");
        }

        if (_expected is not null) {
            _result.Should()
                .BeEquivalentTo(_expected);
        }

        if (assertion is not null) {
            assertion(_result);
        }
    }

    public void AssertThrows<TException>(Action<TException>? assertion = null)
        where TException : Exception {
        if (_expected is not null) {
            throw new InvalidOperationException($"Expected output is defined in {nameof(Arrange)}. " +
                $"Did you mean to call {nameof(Assert)} instead of {nameof(AssertThrows)}?");
        }

        _exception.Should()
            .NotBeNull();

        _exception.Should()
            .BeOfType<TException>();

        if (assertion is not null) {
            assertion((TException)_exception);
        }
    }

    public async Task DisposeAsync() {
        await _dbContext.DisposeAsync();
    }
}
