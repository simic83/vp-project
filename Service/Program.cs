using System;
using System.ServiceModel;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;

namespace Service
{
    class Program
    {
        private static readonly string[] LoadingFrames = { "⣾", "⣽", "⣻", "⢿", "⡿", "⣟", "⣯", "⣷" };
        private static int loadingFrameIndex = 0;
        private static bool isAnimating = false;
        private static readonly object consoleLock = new object();

        // ASCII Art za logo
        private static readonly string[] LogoArt = new string[]
        {
            @"   ╔═══════════════════════════════════════════════════════════════════╗",
            @"   ║  ⚡  _____ _                   _               ____        _       ║",
            @"   ║    / ____| |                 (_)             |  _ \      | |      ║",
            @"   ║   | |    | |__   __ _ _ __ __ _ _ _ __   __ _| |_) | __ _| |_ __ _║",
            @"   ║   | |    | '_ \ / _` | '__/ _` | | '_ \ / _` |  _ < / _` | __/ _` ║",
            @"   ║   | |____| | | | (_| | | | (_| | | | | | (_| | |_) | (_| | || (_| ║",
            @"   ║    \_____|_| |_|\__,_|_|  \__, |_|_| |_|\__, |____/ \__,_|\__\__,_║",
            @"   ║                             __/ |         __/ |                    ║",
            @"   ║  ⚡                         |___/         |___/              ⚡     ║",
            @"   ╚═══════════════════════════════════════════════════════════════════╝"
        };

        private static readonly Dictionary<string, string> StatusIcons = new Dictionary<string, string>
        {
            { "START", "🚀" },
            { "DATA", "📊" },
            { "COMPLETE", "✅" },
            { "WARNING", "⚠️" },
            { "ERROR", "❌" },
            { "INFO", "ℹ️" },
            { "SUCCESS", "🎉" }
        };

        static void Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            ServiceHost host = null;
            ChargingService service = null;

            try
            {
                // Animacija pokretanja
                ShowStartupAnimation();

                // Kreiranje instance servisa
                service = new ChargingService();

                // Pretplata na događaje
                service.OnTransferStarted += Service_OnTransferStarted;
                service.OnSampleReceived += Service_OnSampleReceived;
                service.OnTransferCompleted += Service_OnTransferCompleted;
                service.OnWarningRaised += Service_OnWarningRaised;

                // Kreiranje ServiceHost-a sa postojećom instancom
                host = new ServiceHost(service);

                // Prikaži loading bar
                ShowProgressBar("Initializing Service", 100, ConsoleColor.Cyan);

                host.Open();

                // Clear screen za čist interface
                Console.Clear();

                // Prikaži logo
                DisplayLogo();

                // Status panel
                DisplayStatusPanel();

                // Start pulsating indicator
                Task.Run(() => ShowPulsatingIndicator());

                Console.WriteLine();
                DrawColorfulLine('═', ConsoleColor.DarkCyan);
                Console.WriteLine();

                WriteColoredLine("  📍 Service Status: ", ConsoleColor.White, false);
                WriteColoredLine("● ONLINE", ConsoleColor.Green, true);

                Console.WriteLine();
                WriteColoredLine($"  🕐 Started at: {DateTime.Now:yyyy-MM-dd HH:mm:ss}", ConsoleColor.Gray);
                WriteColoredLine("  🌐 Listening for connections...", ConsoleColor.Cyan);

                Console.WriteLine();
                DrawColorfulLine('═', ConsoleColor.DarkCyan);

                // Instruction panel
                DisplayInstructionPanel();

                Console.WriteLine();
                DrawSeparator('─', ConsoleColor.DarkGray);
                WriteColoredLine("  📋 ACTIVITY LOG", ConsoleColor.Yellow);
                DrawSeparator('─', ConsoleColor.DarkGray);
                Console.WriteLine();

                // Čekaj input
                Console.ReadLine();

            }
            catch (Exception ex)
            {
                ShowErrorAnimation();
                Console.WriteLine();
                WriteColoredLine($"  {StatusIcons["ERROR"]} Error starting service: {ex.Message}", ConsoleColor.Red);
                WriteColoredLine($"  📝 Stack trace: {ex.StackTrace}", ConsoleColor.DarkRed);
            }
            finally
            {
                isAnimating = false;

                // Animacija zatvaranja
                Console.WriteLine();
                ShowShutdownAnimation();

                if (host != null)
                {
                    try
                    {
                        if (host.State == CommunicationState.Opened)
                        {
                            ShowProgressBar("Closing Service", 50, ConsoleColor.Yellow);
                            host.Close();
                            WriteColoredLine($"  {StatusIcons["SUCCESS"]} Service host closed successfully.", ConsoleColor.Green);
                        }
                    }
                    catch
                    {
                        host.Abort();
                        WriteColoredLine($"  {StatusIcons["WARNING"]} Service host aborted.", ConsoleColor.Yellow);
                    }
                    finally
                    {
                        (host as IDisposable)?.Dispose();
                    }
                }

                service?.Dispose();

                DisplayShutdownMessage();
                Console.ReadKey();
            }
        }

        private static void Service_OnTransferStarted(object sender, TransferEventArgs e)
        {
            lock (consoleLock)
            {
                AnimateLogEntry();
                Console.Write("  ");
                WriteColoredText($"{StatusIcons["START"]} ", ConsoleColor.Green);
                WriteColoredText("[", ConsoleColor.DarkGray);
                WriteColoredText("START", ConsoleColor.Green);
                WriteColoredText("] ", ConsoleColor.DarkGray);
                WriteColoredText($"{e.VehicleId}", ConsoleColor.White);
                WriteColoredText(" ▸ ", ConsoleColor.DarkGray);
                WriteColoredLine(e.Message, ConsoleColor.Green);

                // Mini progress indicator
                ShowMiniProgress(ConsoleColor.Green);
            }
        }

        private static void Service_OnSampleReceived(object sender, TransferEventArgs e)
        {
            lock (consoleLock)
            {
                Console.Write("  ");
                WriteColoredText($"{StatusIcons["DATA"]} ", ConsoleColor.Cyan);
                WriteColoredText("[", ConsoleColor.DarkGray);
                WriteColoredText("DATA", ConsoleColor.Cyan);
                WriteColoredText("] ", ConsoleColor.DarkGray);
                WriteColoredText($"{e.VehicleId}", ConsoleColor.White);
                WriteColoredText(" ▸ ", ConsoleColor.DarkGray);

                // Simuliraj data flow animaciju
                string dataFlow = "◦◦◦◦◦";
                foreach (char c in dataFlow)
                {
                    Console.ForegroundColor = ConsoleColor.DarkCyan;
                    Console.Write(c);
                    Thread.Sleep(20);
                }
                Console.Write(" ");

                WriteColoredLine(e.Message, ConsoleColor.Cyan);
            }
        }

        private static void Service_OnTransferCompleted(object sender, TransferEventArgs e)
        {
            lock (consoleLock)
            {
                AnimateLogEntry();
                Console.Write("  ");
                WriteColoredText($"{StatusIcons["COMPLETE"]} ", ConsoleColor.Blue);
                WriteColoredText("[", ConsoleColor.DarkGray);
                WriteColoredText("COMPLETE", ConsoleColor.Blue);
                WriteColoredText("] ", ConsoleColor.DarkGray);
                WriteColoredText($"{e.VehicleId}", ConsoleColor.White);
                WriteColoredText(" ▸ ", ConsoleColor.DarkGray);
                WriteColoredLine(e.Message, ConsoleColor.Blue);

                // Success animation
                ShowSuccessAnimation();
            }
        }

        private static void Service_OnWarningRaised(object sender, TransferEventArgs e)
        {
            lock (consoleLock)
            {
                // Warning blink effect
                for (int i = 0; i < 2; i++)
                {
                    Console.Write("\r  ");
                    WriteColoredText("   ", ConsoleColor.Yellow);
                    Thread.Sleep(100);
                    Console.Write("\r");
                }

                Console.Write("  ");
                WriteColoredText($"{StatusIcons["WARNING"]} ", ConsoleColor.Yellow);
                WriteColoredText("[", ConsoleColor.DarkGray);
                WriteColoredText("WARNING", ConsoleColor.Yellow);
                WriteColoredText("] ", ConsoleColor.DarkGray);
                WriteColoredText($"{e.VehicleId}", ConsoleColor.White);
                WriteColoredText(" ▸ ", ConsoleColor.DarkGray);
                WriteColoredLine(e.Message, ConsoleColor.Yellow);
            }
        }

        private static void DisplayLogo()
        {
            foreach (var line in LogoArt)
            {
                // Gradient effect za logo
                Console.ForegroundColor = ConsoleColor.Cyan;
                foreach (char c in line)
                {
                    if (c == '⚡')
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.Write(c);
                        Console.ForegroundColor = ConsoleColor.Cyan;
                    }
                    else
                    {
                        Console.Write(c);
                    }
                }
                Console.WriteLine();
            }
            Console.ResetColor();
        }

        private static void DisplayStatusPanel()
        {
            Console.WriteLine();
            string[] statusBox = new string[]
            {
                "   ┌─────────────────────────────────────────────────────────────────┐",
                "   │  💾 Data Source: Podaci pronađeni ✓                              │",
                "   │  🔌 Connection: Active                                           │",
                "   │  📡 Protocol: WCF Service                                        │",
                "   │  🔄 Transfer Mode: Real-time                                     │",
                "   └─────────────────────────────────────────────────────────────────┘"
            };

            foreach (var line in statusBox)
            {
                Console.ForegroundColor = ConsoleColor.DarkGreen;
                Console.WriteLine(line);
            }
            Console.ResetColor();
        }

        private static void DisplayInstructionPanel()
        {
            Console.WriteLine();
            string[] instructions = new string[]
            {
                "   ╭───────────────────────────────────────────────────────────────╮",
                "   │                      CONTROL PANEL                             │",
                "   ├───────────────────────────────────────────────────────────────┤",
                "   │  ⌨️  Commands:                                                 │",
                "   │     • Press [ENTER] to stop the service                       │",
                "   │     • Service will save all data before shutdown              │",
                "   ╰───────────────────────────────────────────────────────────────╯"
            };

            foreach (var line in instructions)
            {
                Console.ForegroundColor = ConsoleColor.Magenta;
                Console.WriteLine(line);
            }
            Console.ResetColor();
        }

        private static void ShowStartupAnimation()
        {
            Console.Clear();
            string[] startup = new string[]
            {
                "⚡ INITIALIZING CHARGING SERVICE ⚡",
                "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
            };

            foreach (var line in startup)
            {
                foreach (char c in line)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.Write(c);
                    Thread.Sleep(10);
                }
                Console.WriteLine();
            }
            Console.ResetColor();
            Thread.Sleep(500);
        }

        private static void ShowShutdownAnimation()
        {
            string[] frames = { "◐", "◓", "◑", "◒" };
            Console.Write("  Shutting down ");
            for (int i = 0; i < 8; i++)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Write(frames[i % frames.Length]);
                Thread.Sleep(200);
                Console.Write("\b");
            }
            Console.WriteLine();
            Console.ResetColor();
        }

        private static void ShowProgressBar(string message, int duration, ConsoleColor color)
        {
            Console.WriteLine();
            Console.Write($"  {message} ");
            Console.ForegroundColor = color;

            int width = 30;
            Console.Write("[");

            for (int i = 0; i <= width; i++)
            {
                Console.SetCursorPosition(message.Length + 4 + i, Console.CursorTop);
                Console.Write("█");
                Thread.Sleep(duration / width);
            }

            Console.Write("] ");
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("✓ Complete");
            Console.ResetColor();
        }

        private static void ShowMiniProgress(ConsoleColor color)
        {
            string[] miniBar = { "▰", "▰▰", "▰▰▰", "▰▰▰▰" };
            foreach (var bar in miniBar)
            {
                Console.Write("\r" + new string(' ', Console.WindowWidth - 1));
                Console.Write($"\r  {bar}");
                Console.ForegroundColor = color;
                Thread.Sleep(50);
            }
            Console.Write("\r" + new string(' ', Console.WindowWidth - 1) + "\r");
            Console.ResetColor();
        }

        private static void ShowPulsatingIndicator()
        {
            isAnimating = true;
            while (isAnimating)
            {
                lock (consoleLock)
                {
                    int currentLine = Console.CursorTop;
                    Console.SetCursorPosition(2, 15);
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.Write($"⚡ {LoadingFrames[loadingFrameIndex % LoadingFrames.Length]} Active ");
                    Console.SetCursorPosition(0, currentLine);
                    Console.ResetColor();
                }
                loadingFrameIndex++;
                Thread.Sleep(100);
            }
        }

        private static void ShowSuccessAnimation()
        {
            string[] success = { "✨", "🎊", "✅" };
            Console.Write("  ");
            foreach (var s in success)
            {
                Console.Write(s + " ");
                Thread.Sleep(100);
            }
            Console.WriteLine();
        }

        private static void ShowErrorAnimation()
        {
            for (int i = 0; i < 3; i++)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Write("█");
                Thread.Sleep(100);
                Console.ForegroundColor = ConsoleColor.DarkRed;
                Console.Write("█");
                Thread.Sleep(100);
            }
            Console.WriteLine();
            Console.ResetColor();
        }

        private static void AnimateLogEntry()
        {
            Console.Write("  ");
            for (int i = 0; i < 3; i++)
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.Write("•");
                Thread.Sleep(30);
            }
            Console.Write("\r   ");
            Console.ResetColor();
        }

        private static void DisplayShutdownMessage()
        {
            Console.WriteLine();
            DrawColorfulLine('═', ConsoleColor.DarkRed);
            Console.WriteLine();

            string[] goodbye = new string[]
            {
                "   ╔═══════════════════════════════════════════════╗",
                "   ║          SERVICE SHUTDOWN COMPLETE            ║",
                "   ║                                               ║",
                "   ║     Thank you for using Charging Service     ║",
                "   ║              Have a great day! 🌟             ║",
                "   ╚═══════════════════════════════════════════════╝"
            };

            foreach (var line in goodbye)
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine(line);
                Thread.Sleep(100);
            }

            Console.ResetColor();
            Console.WriteLine();
            WriteColoredLine("  Press any key to exit...", ConsoleColor.Gray);
        }

        private static void DrawSeparator(char character, ConsoleColor color)
        {
            Console.ForegroundColor = color;
            Console.WriteLine("  " + new string(character, 65));
            Console.ResetColor();
        }

        private static void DrawColorfulLine(char character, ConsoleColor color)
        {
            Console.ForegroundColor = color;
            Console.WriteLine("   " + new string(character, 70));
            Console.ResetColor();
        }

        private static void WriteColoredLine(string text, ConsoleColor color, bool newLine = true)
        {
            Console.ForegroundColor = color;
            if (newLine)
                Console.WriteLine(text);
            else
                Console.Write(text);
            Console.ResetColor();
        }

        private static void WriteColoredText(string text, ConsoleColor color)
        {
            Console.ForegroundColor = color;
            Console.Write(text);
            Console.ResetColor();
        }
    }
}