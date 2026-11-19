using System.Globalization;
using Yarp.ReverseProxy.LoadBalancing;
using Yarp.ReverseProxy.Model;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));
builder.Services.AddSingleton<ILoadBalancingPolicy, CanaryPonderadaPolicy>();

var app = builder.Build();
app.MapReverseProxy();
app.Run();

public sealed class CanaryPonderadaPolicy : ILoadBalancingPolicy {
    public string Name => "CanaryPonderada";

    public DestinationState? PickDestination(HttpContext context, ClusterState cluster, IReadOnlyList<DestinationState> availableDestinations) {
        if (context.Request.Headers["X-Canal"] == "canary" && availableDestinations.FirstOrDefault(d => Metadato(d, "Canal") == "canary") is { } canary){
            return canary;
        }

        var pesos = availableDestinations.Select(d => (Destino: d, Peso: Peso(d))).ToList();
        var total = pesos.Sum(p => (long)p.Peso);
        if (total == 0) return null;

        var punto = Random.Shared.NextInt64(total);
        foreach (var (destino, peso) in pesos) {
            punto -= peso;
            if (punto < 0) return destino;
        }
        
        return null;
    }

    private static string? Metadato(DestinationState d, string clave) =>
        d.Model.Config.Metadata is { } m && m.TryGetValue(clave, out var v) ? v : null;

    private static int Peso(DestinationState d) =>
        int.TryParse(Metadato(d, "Peso"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var p) && p > 0
            ? p
            : 0;
}