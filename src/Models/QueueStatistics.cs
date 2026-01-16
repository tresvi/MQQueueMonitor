namespace MQQueueMonitor.Models;


internal class QueueStatistics
{
    public string QueueName { get; private set; }
    public int MaxDepth { get; private set; }
    public int CurrentDepth { get; private set; }
    public int MinDepth { get; private set; } = int.MaxValue;
    public DateTime MinDepthTimestamp { get; private set; }
    public int MaxDepthRecorded { get; private set; } = int.MinValue;
    public DateTime MaxDepthTimestamp { get; private set; }
    public int SaturationCount { get; private set; }
    public double RatePerSecond { get; private set; } = 0.0;
    
#region Campos privados para calcular la velocidad de cambio
    private int _previousDepth = 0;
    private DateTime _previousTimestamp;
    private bool _hasPreviousMeasurement = false;
#endregion

    private bool _wasAtMaxDepth = false;


    public QueueStatistics(string queueName, int maxDepth)
    {
        QueueName = queueName;
        MaxDepth = maxDepth;
    }

    /// <summary>
    /// Actualiza las estadísticas con un nuevo valor de profundidad actual
    /// </summary>
    /// <param name="currentDepth">Profundidad actual de la cola</param>
    public void Update(int currentDepth)
    {
        DateTime currentTime = DateTime.Now;
        CurrentDepth = currentDepth;

        // Calcular velocidad de cambio por segundo
        if (_hasPreviousMeasurement)
        {
            double timeDifferenceSeconds = (currentTime - _previousTimestamp).TotalSeconds;
            if (timeDifferenceSeconds > 0)
            {
                int depthChange = currentDepth - _previousDepth;
                RatePerSecond = depthChange / timeDifferenceSeconds;
            }
            else
            {
                RatePerSecond = 0.0;
            }
        }
        else
        {
            RatePerSecond = 0.0;
        }

        // Actualizar valores previos para el próximo cálculo
        _previousDepth = currentDepth;
        _previousTimestamp = currentTime;
        _hasPreviousMeasurement = true;

        if (currentDepth < MinDepth)
        {
            MinDepth = currentDepth;
            MinDepthTimestamp = currentTime;
        }

        if (currentDepth > MaxDepthRecorded)
        {
            MaxDepthRecorded = currentDepth;
            MaxDepthTimestamp = currentTime;
        }

        // Detectar saturación: pasar de un valor menor a la profundidad máxima a la profundidad máxima
        if (currentDepth >= MaxDepth && !_wasAtMaxDepth)
        {
            SaturationCount++;
            _wasAtMaxDepth = true;
        }
        else if (currentDepth < MaxDepth)
        {
            _wasAtMaxDepth = false;
        }
    }

    /// <summary>
    /// Indica si hay un valor mínimo registrado válido
    /// </summary>
    public bool HasMinDepth => MinDepth != int.MaxValue;

    /// <summary>
    /// Indica si hay un valor máximo registrado válido
    /// </summary>
    public bool HasMaxDepthRecorded => MaxDepthRecorded != int.MinValue;
}
