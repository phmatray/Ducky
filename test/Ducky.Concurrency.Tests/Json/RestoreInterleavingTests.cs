using System.Text.Json;
using System.Text.Json.Serialization;
using Ducky.ConcurrencyTests;
using Microsoft.Extensions.Logging.Abstractions;

// Outside Ducky.Concurrency.Tests: STJ's generated code names its types by full name, and under that namespace
// Ducky.Concurrency is the public enum (CS0435), as for the RootNamespace in the project file.
namespace Ducky.ConcurrencyTests
{
    internal sealed record Saved(int Value);

    internal sealed class SavedSlice : Slice<Saved>
    {
        protected override Saved Initial => new(0);
    }

    [JsonSerializable(typeof(Saved))]
    internal sealed partial class ConcurrencyJson : JsonSerializerContext;
}

namespace Ducky.Concurrency.Tests
{
    // SPEC §5.2 (Restore clones a JsonElement synchronously, before enqueue), §17.3 row 9: INV-01, INV-04, INV-07.
    [Collection(nameof(Interleaving))]
    public sealed class RestoreInterleavingTests
    {
        // Another thread is draining (parked in a System action's reducer), so the restore is only queued; the caller disposes
        // its JsonDocument at once, before the drainer reaches the restore, which must still apply the value.
        [Theory]
        [MemberData(nameof(Interleaving.Repeat), MemberType = typeof(Interleaving))]
        public async Task Restore_JsonElementFromDisposedDocument_WhileOtherThreadDrains_Restores(int repeat)
        {
            _ = repeat;
            var cancellationToken = TestContext.Current.CancellationToken;
            var log = new LogSlice();
            var saved = new SavedSlice();
            var parked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var store = new DuckyStore([log, saved], NullLogger.Instance, json: ConcurrencyJson.Default.Options);
            log.OnBlock = () =>
            {
                parked.SetResult();
                release.Task.Wait(cancellationToken);
            };
            var drainer = Task.Factory.StartNew(
                () => store.Dispatcher.Dispatch(new Block(), Origin.System), cancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            await Interleaving.Within(parked.Task);

            try
            {
                using (var document = JsonDocument.Parse("""{"Value":7}"""))
                {
                    store.Restore(new Dictionary<string, object> { [saved.Key] = document.RootElement }, Origin.Hydration);
                }

                store.Dispatcher.State.Get<Saved>().Value.ShouldBe(0);
                release.SetResult();
                await Interleaving.Within(drainer);
                store.Dispatcher.State.Get<Saved>().Value.ShouldBe(7);
                store.Dispatcher.State.WasRestored<Saved>().ShouldBeTrue();
            }
            finally
            {
                release.TrySetResult(); // a failed assertion must not leave the drainer thread parked
            }
        }
    }
}
