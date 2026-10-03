namespace Laminar;

public interface IOperationResilience
{
    Func<CancellationToken, ValueTask<TResult>> Apply<TResult>(
        Func<CancellationToken, ValueTask<TResult>> operation);
}
