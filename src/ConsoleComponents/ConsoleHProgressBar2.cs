namespace MQQueueMonitor.ConsoleComponents;

/// <summary>
/// Clase para generar barras de progreso horizontal con markup de Spectre.Console
/// </summary>
internal class ConsoleHProgressBar2
{
    /// <summary>
    /// Longitud de la barra en caracteres (dentro de los corchetes)
    /// </summary>
    public int BarLength { get; set; }

    /// <summary>
    /// Porcentaje a partir del cual se mostrará la zona amarilla (0-100)
    /// </summary>
    public double YellowThreshold { get; set; }

    /// <summary>
    /// Porcentaje a partir del cual se mostrará la zona roja (0-100)
    /// </summary>
    public double RedThreshold { get; set; }

    /// <summary>
    /// Indica si se debe mostrar el porcentaje al costado de la barra
    /// </summary>
    public bool ShowPercentage { get; set; } = false;

    /// <summary>
    /// Formato para mostrar el porcentaje (por defecto "F2" para 2 decimales)
    /// </summary>
    public string PercentageFormat { get; set; } = "F2";

    /// <summary>
    /// Constructor de la barra de progreso
    /// </summary>
    /// <param name="barLength">Longitud de la barra en caracteres</param>
    /// <param name="yellowThreshold">Porcentaje a partir del cual se mostrará la zona amarilla (0-100)</param>
    /// <param name="redThreshold">Porcentaje a partir del cual se mostrará la zona roja (0-100)</param>
    /// <param name="showPercentage">Indica si se debe mostrar el porcentaje al costado de la barra</param>
    public ConsoleHProgressBar2(int barLength, double yellowThreshold, double redThreshold, bool showPercentage)
    {
        if (barLength <= 0)
            throw new ArgumentException("La longitud de la barra debe ser mayor que 0.", nameof(barLength));
        
        if (yellowThreshold < 0 || yellowThreshold > 100)
            throw new ArgumentException("El umbral amarillo debe estar entre 0 y 100.", nameof(yellowThreshold));
        
        if (redThreshold < 0 || redThreshold > 100)
            throw new ArgumentException("El umbral rojo debe estar entre 0 y 100.", nameof(redThreshold));
        
        if (yellowThreshold >= redThreshold)
            throw new ArgumentException("El umbral amarillo debe ser menor que el umbral rojo.");

        BarLength = barLength;
        YellowThreshold = yellowThreshold;
        RedThreshold = redThreshold;
        ShowPercentage = showPercentage;
    }

    /// <summary>
    /// Calcula el porcentaje actual
    /// </summary>
    /// <param name="currentValue">Valor actual</param>
    /// <param name="maxValue">Valor máximo</param>
    /// <returns>Porcentaje calculado (0-100)</returns>
    private double CalculatePercentage(int currentValue, int maxValue)
    {
        return maxValue > 0 ? Math.Min(100.0, (double)currentValue / maxValue * 100.0) : 0.0;
    }

    /// <summary>
    /// Obtiene el color de la barra según el porcentaje actual
    /// </summary>
    /// <param name="currentValue">Valor actual</param>
    /// <param name="maxValue">Valor máximo</param>
    /// <returns>Nombre del color como string para Spectre.Console</returns>
    public string GetBarColor(int currentValue, int maxValue)
    {
        double percentage = CalculatePercentage(currentValue, maxValue);
        return percentage < YellowThreshold ? "green" : (percentage < RedThreshold ? "yellow" : "red");
    }

    /// <summary>
    /// Genera el string de la barra de progreso con markup de Spectre.Console
    /// </summary>
    /// <param name="currentValue">Valor actual</param>
    /// <param name="maxValue">Valor máximo</param>
    /// <returns>String con markup de Spectre.Console para la barra de progreso</returns>
    public string GenerateMarkup(int currentValue, int maxValue)
    {
        double percentage = CalculatePercentage(currentValue, maxValue);
        string barColor = GetBarColor(currentValue, maxValue);

        // Calcular el número de caracteres a llenar
        int filledChars = (int)Math.Round(percentage / 100.0 * BarLength);
        filledChars = Math.Min(filledChars, BarLength);

        // Crear barra de progreso con markup de Spectre.Console
        string progressBar = $"[{barColor}]{new string('█', filledChars)}[/][dim]{new string('░', BarLength - filledChars)}[/]";

        if (ShowPercentage)
        {
            progressBar += $" {percentage.ToString(PercentageFormat)}%";
        }

        return progressBar;
    }
}
