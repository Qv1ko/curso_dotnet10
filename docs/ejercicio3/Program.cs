using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using LogsFolder;

BenchmarkRunner.Run<ResumenBenchmarks>(args: args);

[MemoryDiagnoser]
public class ResumenBenchmarks {
    private const int UmbralMs = 1000;
    private List<string> lineas = null!;

    [Params(1_000, 100_000)]
    public int NumLineas { get; set; }

    [GlobalSetup]
    public void Preparar() {
        lineas = new List<string>(NumLineas);
        for (int i = 0; i < NumLineas; i++) {
            lineas.Add(i % 10 == 0
                ? "linea-corrupta"
                : $"2026-09-14T10:15:{i % 60:D2}Z|GET|/api/pedidos/{i}|{(i % 7 == 0 ? 503 : 200)}|{(i % 13 == 0 ? 1250 : 37)}");
        }
    }

    [Benchmark(Baseline = true)]
    public (int, int, int) Original() => LogParserOriginal.Resumir(lineas, UmbralMs);

    [Benchmark]
    public (int, int, int) Optimizado() => LogParser.Resumir(lineas, UmbralMs);
}

// | Method     | NumLineas | Mean         | Error      | StdDev     | Median       | Ratio | RatioSD | Gen0      | Allocated  | Alloc Ratio |
// |----------- |---------- |-------------:|-----------:|-----------:|-------------:|------:|--------:|----------:|-----------:|------------:|
// | Original   | 1000      |    265.30 us |   5.304 us |   9.832 us |    260.95 us |  1.00 |    0.05 |   45.4102 |   287200 B |        1.00 |
// | Optimizado | 1000      |     35.58 us |   0.602 us |   0.502 us |     35.37 us |  0.13 |    0.01 |         - |          - |        0.00 |
// |            |           |              |            |            |              |       |         |           |            |             |
// | Original   | 100000    | 25,971.00 us | 373.286 us | 311.711 us | 25,815.64 us |  1.00 |    0.02 | 4656.2500 | 29368000 B |        1.00 |
// | Optimizado | 100000    |  3,552.82 us |  34.725 us |  32.481 us |  3,549.87 us |  0.14 |    0.00 |         - |          - |        0.00 |
