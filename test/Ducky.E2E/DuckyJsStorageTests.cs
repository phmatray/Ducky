using System.Text;
using Microsoft.Playwright;

namespace Ducky.E2E;

// The storage exports of ducky.js (SPEC §11.9, INV-23) on the static harness page, on Playwright's clock (§17.2).
public sealed class DuckyJsStorageTests
{
    private const string TooLarge = "\0ducky:too-large";
    private static readonly string[] _areas = ["local", "session"];

    // 4 CJK characters (3 UTF-8 bytes each) and a quote that JSON escapes: 5 UTF-16 code units, but 16 bytes once
    // JSON-encoded ("…" quotes 2, CJK 12, \" 2), which is what crosses the Blazor Server hub.
    [Fact]
    public async Task DuckyJs_StorageGet_ReturnsSentinelAboveUtf8Budget()
    {
        await using var harness = await Harness.StartAsync();
        var page = await OpenAsync(harness);

        var reads = await page.EvaluateAsync<string?[]>("""
            async () => {
                const ducky = await import('/ducky.js');
                const value = '語語語語"';
                sessionStorage.setItem('app:big', value);
                return [
                    ducky.storageGet('session', 'app:big', 16),
                    ducky.storageGet('session', 'app:big', 15),
                    ducky.storageGet('session', 'app:big', value.length),
                    ducky.storageGet('session', 'app:missing', 0),
                ];
            }
            """);

        reads.ShouldBe(["語語語語\"", TooLarge, TooLarge, null]);
    }

    [Fact]
    public async Task DuckyJs_StorageSetGetRemove_RoundTrip()
    {
        await using var harness = await Harness.StartAsync();
        var page = await OpenAsync(harness);

        var steps = await page.EvaluateAsync<string?[]>("""
            async () => {
                const ducky = await import('/ducky.js');
                const results = [];
                for (const [area, own, other] of [['local', localStorage, sessionStorage], ['session', sessionStorage, localStorage]]) {
                    ducky.watchStorage('store-a', null, 'app', false, 600000);
                    results.push(
                        `${area} set registered: ${ducky.storageSet(area, 'app:k', '{"v":1}', 'store-a')}`,
                        `${area} get: ${ducky.storageGet(area, 'app:k', 1000)}`,
                        `${area} kept in its own area only: ${own.getItem('app:k')} / ${other.getItem('app:k')}`,
                        `${area} remove registered: ${ducky.storageRemove(area, 'app:k', 'store-a')}`,
                        `${area} get after remove: ${ducky.storageGet(area, 'app:k', 1000)}`,
                        `${area} set unregistered: ${ducky.storageSet(area, 'app:k', '{"v":2}', 'store-b')}`,
                        `${area} get after unregistered set: ${ducky.storageGet(area, 'app:k', 1000)}`,
                        `${area} remove unregistered: ${ducky.storageRemove(area, 'app:k', 'store-b')}`,
                        `${area} get after unregistered remove: ${ducky.storageGet(area, 'app:k', 1000)}`);
                    ducky.unwatchStorage('store-a');
                    results.push(`${area} set after unwatch: ${ducky.storageSet(area, 'app:k', '{"v":3}', 'store-a')}`);
                    own.clear();
                }
                return results;
            }
            """);

        steps.ShouldBe(_areas.SelectMany(area => new[]
        {
            $"{area} set registered: true",
            $$"""{{area}} get: {"v":1}""",
            $$"""{{area}} kept in its own area only: {"v":1} / null""",
            $"{area} remove registered: true",
            $"{area} get after remove: null",
            $"{area} set unregistered: false",
            $$"""{{area}} get after unregistered set: {"v":2}""",
            $"{area} remove unregistered: false",
            $"{area} get after unregistered remove: null",
            $"{area} set after unwatch: false",
        }).ToArray());
    }

    // storageGetStream (devtoolsTakeMessage joins in M8-01): never null nor an empty array, which Blazor's stream
    // reference rejects (spike S-5), but one NUL byte when nothing is kept under the key any more.
    [Fact]
    public async Task DuckyJs_PullExports_ReturnOneByteWhenGone()
    {
        await using var harness = await Harness.StartAsync();
        var page = await OpenAsync(harness);
        const string Envelope = """{"v":1,"s":"語"}""";

        var pulls = await page.EvaluateAsync<int[][]>($$"""
            async () => {
                const ducky = await import('/ducky.js');
                const pull = (area, key) => {
                    const bytes = ducky.storageGetStream(area, key);
                    if (!(bytes instanceof Uint8Array)) throw new Error(`not a Uint8Array: ${bytes}`);
                    return Array.from(bytes);
                };
                localStorage.setItem('app:k', '{{Envelope}}');
                const kept = pull('local', 'app:k');
                localStorage.removeItem('app:k');
                localStorage.setItem('app:empty', '');
                return [kept, pull('local', 'app:k'), pull('session', 'app:never'), pull('local', 'app:empty')];
            }
            """);

        pulls.ShouldBe([[.. Encoding.UTF8.GetBytes(Envelope).Select(b => (int)b)], [0], [0], [0]]);
    }

    private static async Task<IPage> OpenAsync(Harness harness)
    {
        var page = await harness.NewPageAsync();
        await page.GotoAsync("index.html");
        return page;
    }
}
