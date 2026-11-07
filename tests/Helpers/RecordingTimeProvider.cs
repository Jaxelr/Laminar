using System.Threading.Channels;
using Microsoft.Extensions.Time.Testing;

namespace Laminar.Tests.Helpers;

internal sealed class RecordingTimeProvider : TimeProvider
{
    private readonly FakeTimeProvider fake = new();
    private readonly Channel<TimeSpan> registrations = Channel.CreateUnbounded<TimeSpan>();
    private int created;
    private int disposed;

    internal int Created => Volatile.Read(ref created);
    internal int Disposed => Volatile.Read(ref disposed);
    internal void Advance(TimeSpan amount) => fake.Advance(amount);
    internal Task<TimeSpan> Registered() => registrations.Reader.ReadAsync().AsTask().Guard();
    public override DateTimeOffset GetUtcNow() => fake.GetUtcNow();
    public override long GetTimestamp() => fake.GetTimestamp();
    public override long TimestampFrequency => fake.TimestampFrequency;

    public override ITimer CreateTimer(
        TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = fake.CreateTimer(callback, state, dueTime, period);
        Interlocked.Increment(ref created);
        Record(dueTime);
        return new RecordedTimer(this, timer);
    }

    private void Record(TimeSpan dueTime)
    {
        if (dueTime >= TimeSpan.Zero)
        {
            registrations.Writer.TryWrite(dueTime);
        }
    }

    private sealed class RecordedTimer : ITimer
    {
        private readonly RecordingTimeProvider owner;
        private readonly ITimer timer;
        private int isDisposed;

        internal RecordedTimer(RecordingTimeProvider owner, ITimer timer)
        {
            this.owner = owner;
            this.timer = timer;
        }

        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            bool changed = timer.Change(dueTime, period);
            if (changed && Volatile.Read(ref isDisposed) == 0)
            {
                owner.Record(dueTime);
            }
            return changed;
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref isDisposed, 1) == 0)
            {
                timer.Dispose();
                Interlocked.Increment(ref owner.disposed);
            }
        }

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
