// LogParserOriginal.cs
namespace LogsFolder;

public static class LogParserOriginal
{
    public static (int Total, int Errores5xx, int Lentas) Resumir(IReadOnlyList<string> lineas, int umbralMs)
    {
        int total = 0, errores = 0, lentas = 0;
        for (int i = 0; i < lineas.Count; i++)
        {
            try
            {
                var partes = lineas[i].Split('|');
                int estado = int.Parse(partes[3]);
                int duracion = int.Parse(partes[4]);
                total++;
                if (estado >= 500) errores++;
                if (duracion > umbralMs) lentas++;
            }
            catch (Exception)
            {
                // línea mal formada: se ignora
            }
        }
        return (total, errores, lentas);
    }
}
