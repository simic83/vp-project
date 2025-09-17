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
                        if (sample == null)
                        {
                            errorCount++;
                            LogError($"Row {rowIndex}: Failed to parse CSV line");
                            continue;
                        }

                        var result = proxy.PushSample(sample);

                        if (result.Success)
                        {
                            successCount++;
                            Console.Write($"\rProcessed: {successCount} samples, Errors: {errorCount}");
                        }
                        else
                        {
                            errorCount++;
                            LogError($"Row {rowIndex}: {result.Message} ({result.ValidationError})");
                        }

                        // Malo kašnjenje (simulacija toka)
                        Thread.Sleep(10);
                    }
                    catch (Exception ex)
                    {
                        errorCount++;
                        LogError($"Row {rowIndex}: {ex.Message}");
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
            if (string.IsNullOrWhiteSpace(line)) return null;

            var parts = line.Split(sep);
            if (parts.Length < 19)
                throw new FormatException($"Too few columns (got {parts.Length}, expected >= 19).");

            // 0: Timestamp
            if (!TryParseTimestamp(parts[0], out var ts))
                throw new FormatException($"Invalid Timestamp value: '{parts[0]}'.");

            // Parsiranje 18 double vrednosti, sa InvariantCulture (decimalna TAČKA!)
            double Read(int i, string name)
            {
                if (!double.TryParse(parts[i].Trim(), NumberStyles.Float | NumberStyles.AllowThousands,
                                     CultureInfo.InvariantCulture, out var v))
                    throw new FormatException($"Field '{name}' is not a number: '{parts[i]}'.");
                return v;
            }

            // Mapiranje prema zadatku:
            // 1-3: Voltage RMS (Min, Avg, Max)
            // 4-6: Current RMS (Min, Avg, Max)
            // 7-9: Real Power (Min, Avg, Max)
            // 10-12: Reactive Power (Min, Avg, Max)
            // 13-15: Apparent Power (Min, Avg, Max)
            // 16-18: Frequency (Min, Avg, Max)
            var vMin = Read(1, "VoltageRmsMin");
            var vAvg = Read(2, "VoltageRmsAvg");
            var vMax = Read(3, "VoltageRmsMax");

            var cMin = Read(4, "CurrentRmsMin");
            var cAvg = Read(5, "CurrentRmsAvg");
            var cMax = Read(6, "CurrentRmsMax");

            var pRealMin = Read(7, "RealPowerMin");
            var pRealAvg = Read(8, "RealPowerAvg");
            var pRealMax = Read(9, "RealPowerMax");

            var pReacMin = Read(10, "ReactivePowerMin");
            var pReacAvg = Read(11, "ReactivePowerAvg");
            var pReacMax = Read(12, "ReactivePowerMax");

            var pAppMin = Read(13, "ApparentPowerMin");
            var pAppAvg = Read(14, "ApparentPowerAvg");
            var pAppMax = Read(15, "ApparentPowerMax");

            var fMin = Read(16, "FrequencyMin");
            var fAvg = Read(17, "FrequencyAvg");
            var fMax = Read(18, "FrequencyMax");

            // Ako postoji 20. kolona (Index) – možemo je ignorisati ili pročitati:
            // int csvIndex = (parts.Length >= 20 && int.TryParse(parts[19].Trim(), out var idx)) ? idx : rowIndex;

            return new ChargingSample
            {
                VehicleId = vehicleId,
                Timestamp = ts,

                VoltageRmsMin = vMin,
                VoltageRmsAvg = vAvg,
                VoltageRmsMax = vMax,

                CurrentRmsMin = cMin,
                CurrentRmsAvg = cAvg,
                CurrentRmsMax = cMax,

                RealPowerMin = pRealMin,
                RealPowerAvg = pRealAvg,
                RealPowerMax = pRealMax,

                ReactivePowerMin = pReacMin,
                ReactivePowerAvg = pReacAvg,
                ReactivePowerMax = pReacMax,

                ApparentPowerMin = pAppMin,
                ApparentPowerAvg = pAppAvg,
                ApparentPowerMax = pAppMax,

                FrequencyMin = fMin,
                FrequencyAvg = fAvg,
                FrequencyMax = fMax,

                RowIndex = rowIndex
            };
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
