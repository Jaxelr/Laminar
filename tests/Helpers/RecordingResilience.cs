namespace Laminar.Tests.Helpers;

internal sealed class RecordingResilience : IOperationResilience
{
    private readonly string name;
    private readonly List<string> log;

    internal RecordingResilience(string name, List<string> log)
    {
        this.name = name;
        this.log = log;
    }

    internal int Applies { get; private set; }
    internal Type? ResultType { get; private set; }
    internal CancellationToken Token { get; private set; }
    internal object? Result { get; private set; }

    public Func<CancellationToken, ValueTask<T>> Apply<T>(Func<CancellationToken, ValueTask<T>> operation)
    {
        Applies++;
        ResultType = typeof(T);
        return async token =>
        {
            Token = token;
            log.Add($"{name}-enter");
            var result = await operation(token).ConfigureAwait(false);
            Result = result;
            log.Add($"{name}-exit");
            return result;
        };
    }
}
