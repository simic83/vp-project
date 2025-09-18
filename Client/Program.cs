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

        // Console colors
        private static ConsoleColor primaryColor = ConsoleColor.Cyan;
        private static ConsoleColor accentColor = ConsoleColor.Yellow;
        private static ConsoleColor successColor = ConsoleColor.Green;
        private static ConsoleColor errorColor = ConsoleColor.Red;
        private static ConsoleColor warningColor = ConsoleColor.DarkYellow;

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

        // ASCII Art and animations
        private static string[] loadingFrames = new[] { "⠋", "⠙", "⠹", "⠸", "⠼", "⠴", "⠦", "⠧", "⠇", "⠏" };
        private static string[] sparkleFrames = new[] { "✦", "✧", "✨", "✧" };

        static void Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.CursorVisible = false;

            try
            {
                ShowSplashScreen();
                InitializeClient();
                ShowMainMenu();
            }
            catch (Exception ex)
            {
                ShowError($"Fatal error: {ex.Message}");
                LogError($"Fatal error: {ex.Message}");
            }
            finally
            {
                Cleanup();
                Console.CursorVisible = true;
                WriteColoredLine("\n╔════════════════════════════════════════╗", ConsoleColor.DarkGray);
                WriteColoredLine("║     Press any key to exit...  👋      ║", ConsoleColor.DarkGray);
                WriteColoredLine("╚════════════════════════════════════════╝", ConsoleColor.DarkGray);
                Console.ReadKey();
            }
        }

        private static void ShowSplashScreen()
        {
            Console.Clear();
            string[] logo = new[]
            {
                @"",
                @"    ╔═══════════════════════════════════════════════════════════╗",
                @"    ║                                                           ║",
                @"    ║   ⚡ ███████╗██╗   ██╗     ██████╗██╗     ██╗███████╗    ║",
                @"    ║      ██╔════╝██║   ██║    ██╔════╝██║     ██║██╔════╝    ║",
                @"    ║      █████╗  ██║   ██║    ██║     ██║     ██║█████╗      ║",
                @"    ║      ██╔══╝  ╚██╗ ██╔╝    ██║     ██║     ██║██╔══╝      ║",
                @"    ║   ⚡ ███████╗ ╚████╔╝     ╚██████╗███████╗██║███████╗    ║",
                @"    ║      ╚══════╝  ╚═══╝       ╚═════╝╚══════╝╚═╝╚══════╝    ║",
                @"    ║                                                           ║",
                @"    ║          🚗  Electric Vehicle Charging System  🔋         ║",
                @"    ║                                                           ║",
                @"    ╚═══════════════════════════════════════════════════════════╝",
                @""
            };

            foreach (var line in logo)
            {
                if (line.Contains("⚡"))
                    WriteColoredLine(line, ConsoleColor.Yellow);
                else if (line.Contains("█"))
                    WriteColoredLine(line, ConsoleColor.Cyan);
                else if (line.Contains("🚗") || line.Contains("🔋"))
                    WriteColoredLine(line, ConsoleColor.Green);
                else
                    WriteColoredLine(line, ConsoleColor.DarkCyan);
                Thread.Sleep(50);
            }

            AnimateText("    Initializing system", 3, ConsoleColor.White);
            Thread.Sleep(500);
        }

        private static void InitializeClient()
        {
            try
            {
                Console.WriteLine();
                ShowLoadingAnimation("    📡 Connecting to service", 2);

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

                Console.Clear();
                ShowSuccessMessage("    ✅ Connected successfully!");
                Thread.Sleep(1000);
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
                    Console.Clear();
                    DrawMainMenuHeader();

                    string[] menuOptions = new[]
                    {
                        "🔌  Select vehicle and send charging data",
                        "🚗  View available vehicles",
                        "🚪  Exit application"
                    };

                    for (int i = 0; i < menuOptions.Length; i++)
                    {
                        Console.ForegroundColor = ConsoleColor.White;
                        Console.Write($"    [{i + 1}] ");
                        Console.ForegroundColor = primaryColor;
                        Console.WriteLine(menuOptions[i]);
                    }

                    Console.WriteLine();
                    Console.ForegroundColor = ConsoleColor.Gray;
                    Console.Write("    ➤ Select option: ");
                    Console.ForegroundColor = ConsoleColor.White;

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
                            ShowGoodbyeMessage();
                            exit = true;
                            break;
                        default:
                            ShowWarningMessage("    ⚠️  Invalid option. Please try again.");
                            Thread.Sleep(1500);
                            break;
                    }
                }
                catch (Exception ex)
                {
                    ShowError($"Error: {ex.Message}");
                    LogError($"Menu error: {ex.Message}");

                    ShowLoadingAnimation("    🔄 Attempting to reconnect", 2);
                    try
                    {
                        RecreateChannel();
                        ShowSuccessMessage("    ✅ Reconnected successfully!");
                        Thread.Sleep(1500);
                    }
                    catch
                    {
                        ShowError("    ❌ Failed to reconnect. Please restart the application.");
                        exit = true;
                    }
                }
            }
        }

        private static void DrawMainMenuHeader()
        {
            Console.ForegroundColor = ConsoleColor.DarkCyan;
            Console.WriteLine(@"
    ╔═══════════════════════════════════════════════════════╗
    ║                                                       ║");
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine(@"    ║              ⚡ MAIN CONTROL PANEL ⚡                 ║");
            Console.ForegroundColor = ConsoleColor.DarkCyan;
            Console.WriteLine(@"    ║                                                       ║
    ╠═══════════════════════════════════════════════════════╣");
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.WriteLine(@"    ║  Welcome to EV Charging Management System v2.0       ║
    ╚═══════════════════════════════════════════════════════╝
");
            Console.ResetColor();
        }

        private static void ShowAvailableVehicles()
        {
            Console.Clear();

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine(@"
    ╔═══════════════════════════════════════════════════════╗
    ║           🚗 AVAILABLE ELECTRIC VEHICLES 🚗          ║
    ╚═══════════════════════════════════════════════════════╝
");
            Console.ResetColor();

            var vehicles = new List<(string id, string emoji)>
            {
                ("BMW_iX_xDrive50", "🏎️"),
                ("Ford_Mustang", "🚗"),
                ("Hyundai_Ioniq5", "⚡"),
                ("Hyundai_Ioniq_Electric", "🔋"),
                ("Kia_Nero", "🚙"),
                ("Lexus", "💎"),
                ("Mitsubishi_Outlander", "🚐"),
                ("Nissan_Leaf", "🍃"),
                ("Tesla_Model3", "🚀"),
                ("Tesla_ModelY", "🛸"),
                ("Toyota_Prius_Prime2021", "🌿"),
                ("Volvo_XC40", "🛡️")
            };

            for (int i = 0; i < vehicles.Count; i++)
            {
                Thread.Sleep(50);
                Console.ForegroundColor = ConsoleColor.White;
                Console.Write($"    [{i + 1:D2}] ");
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.Write($"{vehicles[i].emoji}  ");
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"{vehicles[i].id.Replace('_', ' ')}");
            }

            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine("    Press any key to return to main menu...");
            Console.ResetColor();
            Console.ReadKey();
        }

        private static void ProcessVehicleData()
        {
            try
            {
                Console.Clear();
                DrawTransferHeader();

                var vehicles = GetVehicleListWithEmojis();

                for (int i = 0; i < vehicles.Count; i++)
                {
                    Console.ForegroundColor = ConsoleColor.White;
                    Console.Write($"    [{i + 1:D2}] ");
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.Write($"{vehicles[i].emoji}  ");
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.WriteLine($"{vehicles[i].id.Replace('_', ' ')}");
                }

                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.Gray;
                Console.Write("    ➤ Select vehicle (1-12): ");
                Console.ForegroundColor = ConsoleColor.White;

                if (!int.TryParse(Console.ReadLine(), out int selection) || selection < 1 || selection > 12)
                {
                    ShowWarningMessage("    ⚠️  Invalid selection.");
                    Thread.Sleep(2000);
                    return;
                }

                string selectedVehicle = vehicles[selection - 1].id;
                string selectedEmoji = vehicles[selection - 1].emoji;

                Console.WriteLine();
                ShowHighlightedMessage($"    {selectedEmoji} Selected: {selectedVehicle.Replace('_', ' ')}", ConsoleColor.Green);
                Thread.Sleep(1000);

                // Putanja do CSV fajla
                string csvPath = GetCsvPath(selectedVehicle);

                if (!File.Exists(csvPath))
                {
                    ShowError($"    ❌ CSV file not found");
                    LogError($"CSV file not found: {csvPath}");
                    Thread.Sleep(2000);
                    return;
                }

                // Stilizovana verifikacija
                Console.WriteLine();
                ShowSuccessMessage($"    ✅ Data for {selectedVehicle.Replace('_', ' ')} found!");
                Thread.Sleep(500);

                // Početak sesije sa animacijom
                Console.WriteLine();
                ShowLoadingAnimation("    🔐 Starting charging session", 2);

                var startResult = proxy.StartSession(selectedVehicle);

                if (!startResult.Success)
                {
                    ShowError($"    ❌ Failed to start session: {startResult.Message}");
                    LogError($"Failed to start session for {selectedVehicle}: {startResult.Message}");
                    Thread.Sleep(2000);
                    return;
                }

                Console.WriteLine();
                ShowSuccessMessage($"    ✅ Session started! ID: {startResult.SessionId}");
                LogMessage($"Session started for {selectedVehicle}, ID: {startResult.SessionId}");
                Thread.Sleep(1000);

                // Čitanje i slanje podataka sa progress barom
                ProcessCsvFileWithProgress(csvPath, selectedVehicle, selectedEmoji);

                // Završetak sesije
                Console.WriteLine();
                ShowLoadingAnimation("    🔒 Ending session", 1);

                var endResult = proxy.EndSession(selectedVehicle);

                if (endResult.Success)
                {
                    Console.WriteLine();
                    ShowSuccessMessage($"    ✅ Session completed: {endResult.Message}");
                    LogMessage($"Session ended for {selectedVehicle}: {endResult.Message}");
                }
                else
                {
                    ShowError($"    ❌ Failed to end session: {endResult.Message}");
                    LogError($"Failed to end session for {selectedVehicle}: {endResult.Message}");
                }

                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine("    Press any key to continue...");
                Console.ResetColor();
                Console.ReadKey();
            }
            catch (Exception ex)
            {
                ShowError($"    ❌ Error: {ex.Message}");
                LogError($"Error processing vehicle data: {ex.Message}");
                Thread.Sleep(3000);
            }
        }

        private static void DrawTransferHeader()
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine(@"
    ╔═══════════════════════════════════════════════════════╗
    ║          📊 VEHICLE DATA TRANSFER MODULE 📊          ║
    ╚═══════════════════════════════════════════════════════╝
");
            Console.ResetColor();
        }

        private static void ProcessCsvFileWithProgress(string csvPath, string vehicleId, string emoji)
        {
            var allLines = File.ReadAllLines(csvPath);
            int totalLines = allLines.Length - 1; // Minus header

            using (var reader = new StreamReader(csvPath))
            {
                string headerLine = reader.ReadLine();
                if (headerLine == null)
                {
                    ShowError("    ❌ Empty CSV.");
                    LogError("Empty CSV.");
                    return;
                }

                char sep = DetectSeparator(headerLine);

                int rowIndex = 0;
                int successCount = 0;
                int errorCount = 0;

                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"    {emoji} Sending data samples for {vehicleId.Replace('_', ' ')}");
                Console.ResetColor();
                Console.WriteLine();

                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    rowIndex++;

                    // Update progress bar
                    DrawProgressBar(rowIndex, totalLines, successCount, errorCount);

                    try
                    {
                        var sample = ParseCsvLine(line, rowIndex, vehicleId, sep);
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
                        errorCount++;
                        LogError($"Row {rowIndex}: Parse error - {ex.Message}");
                    }

                    // Small delay for visual effect on smaller files
                    if (totalLines < 100)
                        Thread.Sleep(10);
                }

                // Final progress bar at 100%
                DrawProgressBar(totalLines, totalLines, successCount, errorCount);
                Console.WriteLine();
                Console.WriteLine();

                // Display final statistics with style
                DrawStatisticsBox(successCount, errorCount, totalLines);

                LogMessage($"Transfer completed for {vehicleId}: {successCount} success, {errorCount} errors");
            }
        }

        private static void DrawProgressBar(int current, int total, int success, int errors)
        {
            Console.SetCursorPosition(0, Console.CursorTop);

            int barWidth = 40;
            double progress = (double)current / total;
            int filled = (int)(barWidth * progress);

            Console.ForegroundColor = ConsoleColor.White;
            Console.Write("    [");

            // Draw filled part
            Console.ForegroundColor = errors > 0 ? ConsoleColor.Yellow : ConsoleColor.Green;
            for (int i = 0; i < filled; i++)
                Console.Write("█");

            // Draw empty part
            Console.ForegroundColor = ConsoleColor.DarkGray;
            for (int i = filled; i < barWidth; i++)
                Console.Write("░");

            Console.ForegroundColor = ConsoleColor.White;
            Console.Write("] ");

            // Percentage and stats
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.Write($"{progress:P0} ");

            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write($"✓{success} ");

            if (errors > 0)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Write($"✗{errors}");
            }

            Console.Write("    "); // Clear any remaining characters
            Console.ResetColor();
        }

        private static void DrawStatisticsBox(int success, int errors, int total)
        {
            var successRate = (double)success / total * 100;
            var status = successRate >= 95 ? "EXCELLENT" : successRate >= 80 ? "GOOD" : successRate >= 60 ? "FAIR" : "NEEDS ATTENTION";
            var statusEmoji = successRate >= 95 ? "🏆" : successRate >= 80 ? "✨" : successRate >= 60 ? "⚠️" : "❌";

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine(@"    ╔═══════════════════════════════════════════════════════╗");
            Console.WriteLine(@"    ║              📈 TRANSFER STATISTICS 📈                ║");
            Console.WriteLine(@"    ╠═══════════════════════════════════════════════════════╣");

            Console.ForegroundColor = ConsoleColor.White;
            Console.Write(@"    ║  ");
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write($"✅ Successful: {success,5}");
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine(@"                                ║");

            Console.Write(@"    ║  ");
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Write($"❌ Errors:     {errors,5}");
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine(@"                                ║");

            Console.Write(@"    ║  ");
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write($"📊 Total:      {total,5}");
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine(@"                                ║");

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine(@"    ╠═══════════════════════════════════════════════════════╣");

            Console.ForegroundColor = ConsoleColor.White;
            Console.Write(@"    ║  ");
            Console.ForegroundColor = successRate >= 80 ? ConsoleColor.Green : ConsoleColor.Yellow;
            Console.Write($"{statusEmoji} Status: {status} ({successRate:F1}%)");

            // Padding to align the box
            int padding = 35 - status.Length - successRate.ToString("F1").Length;
            for (int i = 0; i < padding; i++)
                Console.Write(" ");

            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine(@"║");

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine(@"    ╚═══════════════════════════════════════════════════════╝");
            Console.ResetColor();
        }

        // Helper methods for colored output and animations
        private static void WriteColoredLine(string text, ConsoleColor color)
        {
            Console.ForegroundColor = color;
            Console.WriteLine(text);
            Console.ResetColor();
        }

        private static void ShowLoadingAnimation(string message, int seconds)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            int iterations = seconds * 10;
            for (int i = 0; i < iterations; i++)
            {
                Console.SetCursorPosition(0, Console.CursorTop);
                Console.Write($"{message} {loadingFrames[i % loadingFrames.Length]}  ");
                Thread.Sleep(100);
            }
            Console.SetCursorPosition(0, Console.CursorTop);
            Console.Write(new string(' ', message.Length + 5));
            Console.SetCursorPosition(0, Console.CursorTop);
            Console.ResetColor();
        }

        private static void AnimateText(string text, int dots, ConsoleColor color)
        {
            Console.ForegroundColor = color;
            Console.Write(text);
            for (int i = 0; i < dots; i++)
            {
                Thread.Sleep(300);
                Console.Write(".");
            }
            Console.WriteLine();
            Console.ResetColor();
        }

        private static void ShowSuccessMessage(string message)
        {
            Console.ForegroundColor = successColor;
            Console.WriteLine(message);
            Console.ResetColor();
        }

        private static void ShowError(string message)
        {
            Console.ForegroundColor = errorColor;
            Console.WriteLine(message);
            Console.ResetColor();
        }

        private static void ShowWarningMessage(string message)
        {
            Console.ForegroundColor = warningColor;
            Console.WriteLine(message);
            Console.ResetColor();
        }

        private static void ShowHighlightedMessage(string message, ConsoleColor color)
        {
            Console.ForegroundColor = color;
            Console.WriteLine($"╔{'═'.ToString().PadRight(message.Length + 2, '═')}╗");
            Console.WriteLine($"║ {message} ║");
            Console.WriteLine($"╚{'═'.ToString().PadRight(message.Length + 2, '═')}╝");
            Console.ResetColor();
        }

        private static void ShowGoodbyeMessage()
        {
            Console.Clear();
            string[] goodbye = new[]
            {
                @"",
                @"    ╔═══════════════════════════════════════════════════════╗",
                @"    ║                                                       ║",
                @"    ║              👋 Thank you for using                  ║",
                @"    ║            EV Charging Management System              ║",
                @"    ║                                                       ║",
                @"    ║                  Drive safely! 🚗⚡                   ║",
                @"    ║                                                       ║",
                @"    ╚═══════════════════════════════════════════════════════╝",
                @""
            };

            foreach (var line in goodbye)
            {
                WriteColoredLine(line, ConsoleColor.Cyan);
                Thread.Sleep(100);
            }

            Thread.Sleep(1000);
        }

        private static List<(string id, string emoji)> GetVehicleListWithEmojis()
        {
            return new List<(string id, string emoji)>
            {
                ("BMW_iX_xDrive50", "🏎️"),
                ("Ford_Mustang", "🚗"),
                ("Hyundai_Ioniq5", "⚡"),
                ("Hyundai_Ioniq_Electric", "🔋"),
                ("Kia_Nero", "🚙"),
                ("Lexus", "💎"),
                ("Mitsubishi_Outlander", "🚐"),
                ("Nissan_Leaf", "🍃"),
                ("Tesla_Model3", "🚀"),
                ("Tesla_ModelY", "🛸"),
                ("Toyota_Prius_Prime2021", "🌿"),
                ("Volvo_XC40", "🛡️")
            };
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

        // === Robusno parsiranje jedne data linije ===
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