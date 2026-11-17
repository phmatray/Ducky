using System.Diagnostics;
using System.IO.Compression;
using System.Text;
using System.Text.Json;

// Envelope shape of src/Ducky.Blazor/Persistence/Envelope/EnvelopeWriter.cs: {"v":2,"at":"...","s":<payload>}.
// Payloads mirror the samples' persisted slices (TodoList, BasketState, orders), built deterministically.
var rng = new Random(309);
string[] words = ["buy", "milk", "write", "report", "call", "review", "plan", "fix", "ship", "deploy", "duck", "pond", "bread", "invoice"];
string Title() => string.Join(' ', Enumerable.Range(0, rng.Next(2, 6)).Select(_ => words[rng.Next(words.Length)]));

object Todos(int n) => new { Items = Enumerable.Range(1, n).Select(i => new { Id = i, Title = Title(), Done = rng.Next(2) == 0 }).ToList(), NextId = n + 1 };
object Basket(int n) => new { Lines = Enumerable.Range(1, n).Select(i => new { DuckId = $"duck-{rng.Next(1000):D4}", Quantity = rng.Next(1, 5) }).ToList() };
object Orders(int n) => new
{
    Orders = Enumerable.Range(1, n).Select(i => new
    {
        Id = Guid.NewGuid(),
        Placed = DateTimeOffset.UnixEpoch.AddMinutes(rng.Next(1_000_000)),
        Total = Math.Round(rng.NextDouble() * 200, 2),
        Lines = Enumerable.Range(0, rng.Next(1, 6)).Select(_ => new { DuckId = $"duck-{rng.Next(1000):D4}", Quantity = rng.Next(1, 5) }).ToList(),
    }).ToList(),
};

var cases = new (string Name, object State)[]
{
    ("basket, 3 lines", Basket(3)),
    ("todos, 10 items", Todos(10)),
    ("todos, 100 items", Todos(100)),
    ("orders, 50", Orders(50)),
    ("todos, 1000 items", Todos(1000)),
    ("orders, 500", Orders(500)),
};

string Envelope(object state) =>
    "{\"v\":2,\"at\":\"2026-11-17T12:00:00Z\",\"s\":" + JsonSerializer.Serialize(state) + "}";

byte[] Run(byte[] input, Func<Stream, Stream> wrap)
{
    using var ms = new MemoryStream();
    using (var z = wrap(ms))
    {
        z.Write(input);
    }

    return ms.ToArray();
}

(byte[] Out, double Ms) Time(byte[] input, Func<Stream, Stream> wrap)
{
    var o = Run(input, wrap);
    var iterations = Math.Max(5, 2_000_000 / Math.Max(input.Length, 1));
    var sw = Stopwatch.StartNew();
    for (var i = 0; i < iterations; i++)
    {
        Run(input, wrap);
    }

    return (o, sw.Elapsed.TotalMilliseconds / iterations);
}

var codecs = new (string Name, Func<Stream, Stream> Wrap)[]
{
    ("brotli-q4", s => new BrotliStream(s, new BrotliCompressionOptions { Quality = 4 }, leaveOpen: true)),
    ("brotli-q11", s => new BrotliStream(s, CompressionLevel.SmallestSize, leaveOpen: true)),
    ("zstd-fast", s => new ZstandardStream(s, CompressionLevel.Fastest, leaveOpen: true)),
    ("zstd-opt", s => new ZstandardStream(s, CompressionLevel.Optimal, leaveOpen: true)),
    ("zstd-max", s => new ZstandardStream(s, CompressionLevel.SmallestSize, leaveOpen: true)),
};

Console.WriteLine("| slice | raw B | codec | compressed B | ratio | as base64 B | base64 ratio | compress ms (host) |");
Console.WriteLine("|---|--:|---|--:|--:|--:|--:|--:|");
foreach (var (name, state) in cases)
{
    var raw = Encoding.UTF8.GetBytes(Envelope(state));
    foreach (var (cn, wrap) in codecs)
    {
        var (o, ms) = Time(raw, wrap);
        var b64 = Convert.ToBase64String(o).Length;
        Console.WriteLine($"| {name} | {raw.Length} | {cn} | {o.Length} | {(double)o.Length / raw.Length:F2} | {b64} | {(double)b64 / raw.Length:F2} | {ms:F3} |");
    }
}
