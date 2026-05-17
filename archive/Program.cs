using System;
using Microsoft.Win32;

class Program
{
    static void Main(string[] args)
    {
        RegistryKey key = null;
        try
        {
            key = Registry.LocalMachine.OpenSubKey(@"System\CurrentControlSet\Control\Windows");
            if (key != null)
            {
                var value = key.GetValue("ShutdownTime");
                if (value != null)
                {
                    long shutdownTime = BitConverter.ToInt64((byte[])value, 0);
                    DateTime lastShutdown = DateTime.FromFileTimeUtc(shutdownTime);

                    // Convert to Eastern Time (ETC)
                    TimeZoneInfo etcZone = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
                    DateTime etcTime = TimeZoneInfo.ConvertTimeFromUtc(lastShutdown, etcZone);

                    TimeSpan uptime = DateTime.UtcNow.Subtract(lastShutdown);
                    Console.WriteLine("Last boot time (UTC): {0}", lastShutdown);
                    Console.WriteLine("Last boot time (ETC): {0}", etcTime);
                    Console.WriteLine("System uptime: {0}", uptime);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error: " + ex.Message);
        }
        finally
        {
            key?.Dispose(); // Dispose the registry key in the finally block
        }
    }
}
