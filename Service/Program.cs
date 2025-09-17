using System;
using System.ServiceModel;

namespace Service
{
    class Program
    {
        static void Main(string[] args)
        {
            ServiceHost host = null;
            ChargingService service = null;

            try
            {
                // Kreiranje instance servisa
                service = new ChargingService();

                // Pretplata na događaje
                service.OnTransferStarted += Service_OnTransferStarted;
                service.OnSampleReceived += Service_OnSampleReceived;
                service.OnTransferCompleted += Service_OnTransferCompleted;
                service.OnWarningRaised += Service_OnWarningRaised;

                // Kreiranje ServiceHost-a sa postojećom instancom
                host = new ServiceHost(service);

                host.Open();

                Console.WriteLine("====================================");
                Console.WriteLine("  CHARGING DATA SERVICE STARTED");
                Console.WriteLine("====================================");
                Console.WriteLine($"Service is running at: {DateTime.Now}");
                Console.WriteLine("Listening for connections...");
                Console.WriteLine();
                Console.WriteLine("Press [Enter] to stop the service.");
                Console.WriteLine("------------------------------------");

                Console.ReadLine();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error starting service: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }
            finally
            {
                // Proper cleanup using Dispose pattern
                if (host != null)
                {
                    try
                    {
                        if (host.State == CommunicationState.Opened)
                        {
                            host.Close();
                            Console.WriteLine("Service host closed successfully.");
                        }
                    }
                    catch
                    {
                        host.Abort();
                        Console.WriteLine("Service host aborted.");
                    }
                    finally
                    {
                        (host as IDisposable)?.Dispose();
                    }
                }

                // Dispose service
                service?.Dispose();

                Console.WriteLine("Service stopped.");
                Console.WriteLine("Press any key to exit...");
                Console.ReadKey();
            }
        }

        private static void Service_OnTransferStarted(object sender, TransferEventArgs e)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"[START] {e.VehicleId}: {e.Message}");
            Console.ResetColor();
        }

        private static void Service_OnSampleReceived(object sender, TransferEventArgs e)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"[DATA] {e.VehicleId}: {e.Message}");
            Console.ResetColor();
        }

        private static void Service_OnTransferCompleted(object sender, TransferEventArgs e)
        {
            Console.ForegroundColor = ConsoleColor.Blue;
            Console.WriteLine($"[COMPLETE] {e.VehicleId}: {e.Message}");
            Console.ResetColor();
        }

        private static void Service_OnWarningRaised(object sender, TransferEventArgs e)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"[WARNING] {e.VehicleId}: {e.Message}");
            Console.ResetColor();
        }
    }
}