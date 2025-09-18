using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.ServiceModel;
using System.Threading;
using Common;

namespace Client
{
    class Program : IDisposable
    {
        private static ChannelFactory<IChargingService> channelFactory;
        private static IChargingService proxy;
        private static StreamWriter logWriter;
        private static bool disposed = false;

        // === Formati datuma koje podržavamo (uključujući 2023/08/04 10:15:00) ===
        private static readonly string[] DateFormats = new[]
        {
            "yyyy/MM/dd HH:mm:ss",
            "yyyy-MM-dd HH:mm:ss",
            "yyyy-MM-ddTHH:mm:ss",
            "dd.MM.yyyy HH:mm:ss",
            "MM/dd/yyyy HH:mm:ss",
            "M/d/yyyy H:mm:ss"
        };

        static void Main(string[] args)
        {
            try
            {
                InitializeClient();
                ShowMainMenu();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Fatal error: {ex.Message}");
                LogError($"Fatal error: {ex.Message}");
            }
            finally
            {
                Cleanup();
                Console.WriteLine("\nPress any key to exit...");
                Console.ReadKey();
            }
        }

        private static void InitializeClient()
        {
            try
            {
                // Kreiranje log fajla
                if (!Directory.Exists("Logs"))
                {
                    Directory.CreateDirectory("Logs");
                }

                string logPath = Path.Combine("Logs", $"client_{DateTime.Now:yyyyMMdd_HHmmss}.log");
                logWriter = new StreamWriter(logPath, append: true);
                LogMessage("Client started");

                // Kreiranje kanala prema servisu
                channelFactory = new ChannelFactory<IChargingService>("ChargingServiceEndpoint");
                proxy = channelFactory.CreateChannel();

                Console.WriteLine("====================================");
                Console.WriteLine("  CHARGING DATA CLIENT");
                Console.WriteLine("====================================");
                Console.WriteLine("Connected to service successfully!");
                Console.WriteLine();
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to initialize client: {ex.Message}", ex);
            }
        }

        private static void ShowMainMenu()
        {
            bool exit = false;

            while (!exit)
            {
                try
                {
                    Console.WriteLine("\n=== MAIN MENU ===");
                    Console.WriteLine("1. Select vehicle and send charging data");
                    Console.WriteLine("2. View available vehicles");
                    Console.WriteLine("3. Exit");
                    Console.Write("\nSelect option: ");

                    string choice = Console.ReadLine();

                    switch (choice)
                    {
                        case "1":
                            ProcessVehicleData();
                            break;
                        case "2":
                            ShowAvailableVehicles();
                            break;
                        case "3":
                            exit = true;
                            break;
                        default:
                            Console.WriteLine("Invalid option. Please try again.");
                            break;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error: {ex.Message}");
                    LogError($"Menu error: {ex.Message}");

                    // Pokušaj ponovnog povezivanja
                    Console.WriteLine("Attempting to reconnect...");
                    try
                    {
                        RecreateChannel();
                        Console.WriteLine("Reconnected successfully!");
                    }
                    catch
                    {
                        Console.WriteLine("Failed to reconnect. Please restart the application.");
                        exit = true;
                    }
                }
            }
        }

        private static void ShowAvailableVehicles()
        {
            Console.WriteLine("\n=== AVAILABLE VEHICLES ===");

            var vehicles = new List<string>
            {
                "BMW_iX_xDrive50",
                "Ford_Mustang",
                "Hyundai_Ioniq5",
                "Hyundai_Ioniq_Electric",
                "Kia_Nero",
                "Lexus",
                "Mitsubishi_Outlander",
                "Nissan_Leaf",
                "Tesla_Model3",
                "Tesla_ModelY",
                "Toyota_Prius_Prime2021",
                "Volvo_XC40"
            };

            for (int i = 0; i < vehicles.Count; i++)
            {
                Console.WriteLine($"{i + 1}. {vehicles[i].Replace('_', ' ')}");
            }
        }

        private static void ProcessVehicleData()
        {
            try
            {
                Console.WriteLine("\n=== VEHICLE DATA TRANSFER ===");

                // Prikazivanje dostupnih vozila
                var vehicles = GetVehicleList();

                for (int i = 0; i < vehicles.Count; i++)
                {
                    Console.WriteLine($"{i + 1}. {vehicles[i].Replace('_', ' ')}");
                }

                Console.Write("\nSelect vehicle (1-12): ");
                if (!int.TryParse(Console.ReadLine(), out int selection) || selection < 1 || selection > 12)
                {
                    Console.WriteLine("Invalid selection.");
                    return;
                }

                string selectedVehicle = vehicles[selection - 1];
                Console.WriteLine($"\nSelected: {selectedVehicle.Replace('_', ' ')}");

                // Putanja do CSV fajla
                string csvPath = GetCsvPath(selectedVehicle);

                if (!File.Exists(csvPath))
                {
                    Console.WriteLine($"CSV file not found at path: {csvPath}");
                    LogError($"CSV file not found: {csvPath}");
                    return;
                }

                VerifyCsvFile(csvPath);

                // Početak sesije
                Console.WriteLine("\nStarting charging session...");
                var startResult = proxy.StartSession(selectedVehicle);

                if (!startResult.Success)
                {
                    Console.WriteLine($"Failed to start session: {startResult.Message}");
                    LogError($"Failed to start session for {selectedVehicle}: {startResult.Message}");
                    return;
                }

                Console.WriteLine($"Session started successfully. ID: {startResult.SessionId}");
                LogMessage($"Session started for {selectedVehicle}, ID: {startResult.SessionId}");

                // Čitanje i slanje podataka
                ProcessCsvFile(csvPath, selectedVehicle);

                // Završetak sesije
                Console.WriteLine("\nEnding session...");
                var endResult = proxy.EndSession(selectedVehicle);

                if (endResult.Success)
                {
                    Console.WriteLine($"Session ended successfully: {endResult.Message}");
                    LogMessage($"Session ended for {selectedVehicle}: {endResult.Message}");
                }
                else
                {
                    Console.WriteLine($"Failed to end session: {endResult.Message}");
                    LogError($"Failed to end session for {selectedVehicle}: {endResult.Message}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing vehicle data: {ex.Message}");
                LogError($"Error processing vehicle data: {ex.Message}");
            }
        }

        // === GLAVNO: čitanje CSV-a i slanje uz ispravno parsiranje ===
        private static void ProcessCsvFile(string csvPath, string vehicleId)
        {
            using (var reader = new StreamReader(csvPath))
            {
                string headerLine = reader.ReadLine(); // Skip header
                if (headerLine == null)
                {
                    Console.WriteLine("Empty CSV.");
                    LogError("Empty CSV.");
                    return;
                }

                char sep = DetectSeparator(headerLine);

                int rowIndex = 0;
                int successCount = 0;
                int errorCount = 0;

                Console.WriteLine("\nSending data samples...");
                Console.WriteLine("Status: Transfer in progress...");

                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    rowIndex++;
                    try
                    {
                        var sample = ParseCsvLine(line, rowIndex, vehicleId, sep);
                        // Uvek šalje, čak i ako je sample možda neispravan
                        var result = proxy.PushSample(sample);

                        if (result.Success)
                        {
                            successCount++;
                        }
                        else
                        {
                            errorCount++;
                            LogError($"Row {rowIndex}: {result.Message}");
                        }
                    }
                    catch (Exception ex)
                    {
                        // Pokušaj slanja delimično parsiranog podatka
                        // ili kreiraj "dummy" sample sa greškom
                        errorCount++;
                        LogError($"Row {rowIndex}: Parse error - {ex.Message}");
                    }
                }

                Console.WriteLine($"\nStatus: Transfer completed!");
                Console.WriteLine($"Total samples sent: {successCount}");
                Console.WriteLine($"Total errors: {errorCount}");

                LogMessage($"Transfer completed for {vehicleId}: {successCount} success, {errorCount} errors");
            }
        }

        private static void VerifyCsvFile(string csvPath)
        {
            Console.WriteLine($"\n=== CSV FILE VERIFICATION ===");
            Console.WriteLine($"Path: {csvPath}");

            if (!File.Exists(csvPath))
            {
                Console.WriteLine("File exists: NO");
                Console.WriteLine("=============================\n");
                return;
            }

            Console.WriteLine("File exists: YES");

            var allLines = File.ReadAllLines(csvPath);
            var dataLines = allLines.Skip(1).Take(5).ToArray(); // prve 3-5 data linije

            Console.WriteLine($"Total lines in file: {allLines.Length}");
            Console.WriteLine($"Header: {allLines[0]}");

            if (dataLines.Length > 0)
            {
                var first = dataLines[0];
                int commas = first.Count(c => c == ',');
                int semis = first.Count(c => c == ';');
                char sep = semis > commas ? ';' : ',';
                var parts = first.Split(sep);

                Console.WriteLine($"\nDetected delimiter: '{sep}'");
                Console.WriteLine($"Parts in first data line: {parts.Length} (expected 19 or 20)");
            }

            Console.WriteLine("=============================\n");
        }

        // === Robusno parsiranje jedne data linije ===
        // Očekujemo: 1 kolona Timestamp + 18 numeričkih polja (ukupno 19),
        // opciono 20. kolona = Index (ignorišemo, koristimo rowIndex)
        private static ChargingSample ParseCsvLine(string line, int rowIndex, string vehicleId, char sep)
        {
            // Kreiraj prazan sample koji će uvek biti vraćen
            var sample = new ChargingSample
            {
                VehicleId = vehicleId,
                RowIndex = rowIndex,
                // Postavi default vrednosti koje će server prepoznati kao nevalidne
                Timestamp = DateTime.MinValue,
                VoltageRmsMin = 0,
                VoltageRmsAvg = 0,
                VoltageRmsMax = 0,
                CurrentRmsMin = 0,
                CurrentRmsAvg = 0,
                CurrentRmsMax = 0,
                RealPowerMin = 0,
                RealPowerAvg = 0,
                RealPowerMax = 0,
                ReactivePowerMin = 0,
                ReactivePowerAvg = 0,
                ReactivePowerMax = 0,
                ApparentPowerMin = 0,
                ApparentPowerAvg = 0,
                ApparentPowerMax = 0,
                FrequencyMin = 0,
                FrequencyAvg = 0,
                FrequencyMax = 0
            };

            // Ako je linija prazna, vrati sample sa default vrednostima
            if (string.IsNullOrWhiteSpace(line))
                return sample;

            var parts = line.Split(sep);

            // Ako nema dovoljno kolona, vrati sample sa default vrednostima
            if (parts.Length < 19)
                return sample;

            // Pokušaj parsirati timestamp
            if (parts.Length > 0 && TryParseTimestamp(parts[0], out var ts))
                sample.Timestamp = ts;

            // Pomoćna funkcija za sigurno parsiranje double vrednosti
            double SafeParse(int index, double defaultValue = 0)
            {
                if (index < parts.Length)
                {
                    if (double.TryParse(parts[index].Trim(),
                        NumberStyles.Float | NumberStyles.AllowThousands,
                        CultureInfo.InvariantCulture, out var value))
                    {
                        return value;
                    }
                }
                return defaultValue;
            }

            // Parsiraj sve vrednosti - ako ne uspe, ostaju default vrednosti
            sample.VoltageRmsMin = SafeParse(1);
            sample.VoltageRmsAvg = SafeParse(2);
            sample.VoltageRmsMax = SafeParse(3);

            sample.CurrentRmsMin = SafeParse(4);
            sample.CurrentRmsAvg = SafeParse(5);
            sample.CurrentRmsMax = SafeParse(6);

            sample.RealPowerMin = SafeParse(7);
            sample.RealPowerAvg = SafeParse(8);
            sample.RealPowerMax = SafeParse(9);

            sample.ReactivePowerMin = SafeParse(10);
            sample.ReactivePowerAvg = SafeParse(11);
            sample.ReactivePowerMax = SafeParse(12);

            sample.ApparentPowerMin = SafeParse(13);
            sample.ApparentPowerAvg = SafeParse(14);
            sample.ApparentPowerMax = SafeParse(15);

            sample.FrequencyMin = SafeParse(16);
            sample.FrequencyAvg = SafeParse(17);
            sample.FrequencyMax = SafeParse(18);

            // Uvek vraća sample, nikad null ili exception
            return sample;
        }

        private static bool TryParseTimestamp(string s, out DateTime ts)
        {
            // Prvo probamo striktne formate, zatim generalno parsiranje sa InvariantCulture
            if (DateTime.TryParseExact(s?.Trim(), DateFormats, CultureInfo.InvariantCulture,
                                       DateTimeStyles.AssumeLocal | DateTimeStyles.AllowWhiteSpaces, out ts))
                return true;

            return DateTime.TryParse(s?.Trim(), CultureInfo.InvariantCulture,
                                     DateTimeStyles.AssumeLocal | DateTimeStyles.AllowWhiteSpaces, out ts);
        }

        private static char DetectSeparator(string header)
        {
            if (string.IsNullOrWhiteSpace(header)) return ',';
            // Brojimo pojave ; i ,
            int sc = header.Count(c => c == ';');
            int cc = header.Count(c => c == ',');
            return sc > cc ? ';' : ',';
        }

        private static List<string> GetVehicleList()
        {
            return new List<string>
            {
                "BMW_iX_xDrive50",
                "Ford_Mustang",
                "Hyundai_Ioniq5",
                "Hyundai_Ioniq_Electric",
                "Kia_Nero",
                "Lexus",
                "Mitsubishi_Outlander",
                "Nissan_Leaf",
                "Tesla_Model3",
                "Tesla_ModelY",
                "Toyota_Prius_Prime2021",
                "Volvo_XC40"
            };
        }

        private static string GetCsvPath(string vehicleId)
        {
            return Path.Combine("TestData", vehicleId, "Charging_Profile.csv");
        }

        private static void RecreateChannel()
        {
            try
            {
                if (channelFactory != null && channelFactory.State != CommunicationState.Closed)
                {
                    channelFactory.Close();
                }

                channelFactory = new ChannelFactory<IChargingService>("ChargingServiceEndpoint");
                proxy = channelFactory.CreateChannel();
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to recreate channel: {ex.Message}", ex);
            }
        }

        private static void LogMessage(string message)
        {
            logWriter?.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] INFO: {message}");
            logWriter?.Flush();
        }

        private static void LogError(string message)
        {
            logWriter?.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] ERROR: {message}");
            logWriter?.Flush();
        }

        private static void Cleanup()
        {
            try
            {
                if (channelFactory != null)
                {
                    if (channelFactory.State == CommunicationState.Opened)
                    {
                        channelFactory.Close();
                    }
                    ((IDisposable)channelFactory).Dispose();
                }

                logWriter?.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] INFO: Client stopped");
                logWriter?.Dispose();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during cleanup: {ex.Message}");
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposed)
            {
                if (disposing)
                {
                    Cleanup();
                }
                disposed = true;
            }
        }
    }
}
