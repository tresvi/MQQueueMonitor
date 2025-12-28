using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MQQueueMonitor
{
    internal static class ReportHelper
    {
        /// <summary>
        /// Actualiza una línea específica del informe sin borrar el resto
        /// </summary>
        /// <param name="line">Número de línea a actualizar</param>
        /// <param name="text">Texto a mostrar</param>
        internal static void UpdateReportLine(int line, string text)
        {
            int currentTop = Console.CursorTop;
            int currentLeft = Console.CursorLeft;
            ConsoleColor originalColor = Console.ForegroundColor;

            // Mantener el cursor oculto (ya está oculto desde el inicio)
            Console.SetCursorPosition(0, line);
            Console.ForegroundColor = ConsoleColor.White; // Color blanco por defecto
            Console.Write(text.PadRight(Console.WindowWidth - 1)); // Limpiar el resto de la línea
            Console.ForegroundColor = originalColor;

            Console.SetCursorPosition(currentLeft, currentTop);
        }

        /// <summary>
        /// Actualiza una línea específica del informe con texto parcialmente coloreado
        /// </summary>
        /// <param name="line">Número de línea a actualizar</param>
        /// <param name="prefix">Texto inicial en color blanco</param>
        /// <param name="value">Valor a mostrar con color específico</param>
        /// <param name="suffix">Texto final en color blanco</param>
        /// <param name="valueColor">Color para el valor</param>
        internal static void UpdateReportLineWithPartialColor(int line, string prefix, string value, string suffix, ConsoleColor valueColor)
        {
            int currentTop = Console.CursorTop;
            int currentLeft = Console.CursorLeft;
            ConsoleColor originalColor = Console.ForegroundColor;

            // Mantener el cursor oculto (ya está oculto desde el inicio)
            Console.SetCursorPosition(0, line);
            
            // Escribir prefijo en blanco
            Console.ForegroundColor = ConsoleColor.White;
            Console.Write(prefix);
            
            // Escribir valor con color específico
            Console.ForegroundColor = valueColor;
            Console.Write(value);
            
            // Escribir sufijo en blanco y limpiar el resto de la línea
            Console.ForegroundColor = ConsoleColor.White;
            string fullText = prefix + value + suffix;
            Console.Write(suffix.PadRight(Console.WindowWidth - fullText.Length));
            
            Console.ForegroundColor = originalColor;
            Console.SetCursorPosition(currentLeft, currentTop);
        }

    }
}
