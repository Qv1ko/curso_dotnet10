using System.Buffers;
using System.Globalization;
namespace LogsFolder;

public static class LogParser {
    private static readonly SearchValues<char> Separadores = SearchValues.Create("|\t");

    public static bool TryParse(ReadOnlySpan<char> linea, out int estado, out int duracionMs) {
        estado = duracionMs = 0;
       
        for (int i = 0; i < 3; i++) { int p = linea.IndexOfAny(Separadores); if (p < 0) return false; linea = linea[(p + 1)..]; }
        int q = linea.IndexOfAny(Separadores);
        if (q < 0 || linea[(q + 1)..].IndexOfAny(Separadores) >= 0) return false;
        if (Entero(linea[..q], out estado) && Entero(linea[(q + 1)..], out duracionMs)) return true;
        estado = duracionMs = 0;
        return false;
    }

    private static bool Entero(ReadOnlySpan<char> s, out int v) {
        int r = 0, i = 0;
        if (s.Length is >= 1 and <= 9) {
            for (; i < s.Length && (uint)(s[i] - '0') <= 9; i++) r = r * 10 + s[i] - '0';
            if (i == s.Length) { v = r; return true; }
        }
        return int.TryParse(s, CultureInfo.InvariantCulture, out v);
    }

    public static (int Total, int Errores5xx, int Lentas) Resumir(IReadOnlyList<string> lineas, int umbralMs) {
        int total = 0, errores = 0, lentas = 0;
        for (int i = 0; i < lineas.Count; i++)
            if (TryParse(lineas[i], out int estado, out int duracion)) { total++; if (estado >= 500) errores++; if (duracion > umbralMs) lentas++; }
        return (total, errores, lentas);
    }
}
