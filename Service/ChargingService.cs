using System;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.IO;
using System.ServiceModel;
using Common;

namespace Service
{
    [ServiceBehavior(InstanceContextMode = InstanceContextMode.Single)]
    public class ChargingService : IChargingService, IDisposable
    {
        private readonly Dictionary<string, SessionInfo> activeSessions;
        private readonly string dataPath;
        private bool disposed = false;
        private StreamWriter logWriter;

        // Događaji
        public delegate void TransferEventHandler(object sender, TransferEventArgs e);
        public event TransferEventHandler OnTransferStarted;
        public event TransferEventHandler OnSampleReceived;
        public event TransferEventHandler OnTransferCompleted;
        public event TransferEventHandler OnWarningRaised;

        private class SessionInfo : IDisposable
        {
            public string SessionId { get; set; }
            public string VehicleId { get; set; }
            public DateTime StartTime { get; set; }
            public StreamWriter DataWriter { get; set; }
            public StreamWriter RejectsWriter { get; set; }
            public int SampleCount { get; set; }
            public ChargingSample LastSample { get; set; }

            private bool disposed = false;

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
                        DataWriter?.Dispose();
                        RejectsWriter?.Dispose();
                    }
                    disposed = true;
                }
            }

            ~SessionInfo()
            {
                Dispose(false);
            }
        }

        public ChargingService()
        {
            activeSessions = new Dictionary<string, SessionInfo>();
            dataPath = ConfigurationManager.AppSettings["DataPath"] ?? "Data";

            if (!Directory.Exists(dataPath))
            {
                Directory.CreateDirectory(dataPath);
            }

            string logPath = Path.Combine(dataPath, "service.log");
            logWriter = new StreamWriter(logPath, append: true);
            LogMessage("Service started");
        }

        public SessionResult StartSession(string vehicleId)
        {
            try
            {
                if (string.IsNullOrEmpty(vehicleId))
                {
                    return new SessionResult
                    {
                        Success = false,
                        Message = "Vehicle ID cannot be empty"
                    };
                }

                if (activeSessions.ContainsKey(vehicleId))
                {
                    return new SessionResult
                    {
                        Success = false,
                        Message = "Session already active for this vehicle"
                    };
                }

                string sessionId = Guid.NewGuid().ToString();
                DateTime now = DateTime.Now;

                // Kreiranje strukture direktorijuma: Data/<VehicleId>/<YYYY-MM-DD>/
                string vehiclePath = Path.Combine(dataPath, vehicleId);
                string datePath = Path.Combine(vehiclePath, now.ToString("yyyy-MM-dd"));

                if (!Directory.Exists(datePath))
                {
                    Directory.CreateDirectory(datePath);
                }

                string dataFile = Path.Combine(datePath, $"session_{sessionId}.csv");
                string rejectsFile = Path.Combine(datePath, $"rejects_{sessionId}.csv");

                var dataWriter = new StreamWriter(dataFile);
                var rejectsWriter = new StreamWriter(rejectsFile);

                // Pisanje header-a
                dataWriter.WriteLine("Timestamp,VoltageRmsMin,VoltageRmsAvg,VoltageRmsMax," +
                    "CurrentRmsMin,CurrentRmsAvg,CurrentRmsMax," +
                    "RealPowerMin,RealPowerAvg,RealPowerMax," +
                    "ReactivePowerMin,ReactivePowerAvg,ReactivePowerMax," +
                    "ApparentPowerMin,ApparentPowerAvg,ApparentPowerMax," +
                    "FrequencyMin,FrequencyAvg,FrequencyMax,RowIndex");
                dataWriter.Flush();

                rejectsWriter.WriteLine("Timestamp,Reason,RawData");
                rejectsWriter.Flush();

                var sessionInfo = new SessionInfo
                {
                    SessionId = sessionId,
                    VehicleId = vehicleId,
                    StartTime = now,
                    DataWriter = dataWriter,
                    RejectsWriter = rejectsWriter,
                    SampleCount = 0
                };

                activeSessions[vehicleId] = sessionInfo;

                // Okidanje događaja
                OnTransferStarted?.Invoke(this, new TransferEventArgs
                {
                    VehicleId = vehicleId,
                    Message = $"Session started: {sessionId}"
                });

                LogMessage($"Session started for vehicle {vehicleId}, ID: {sessionId}");

                return new SessionResult
                {
                    Success = true,
                    Message = "Session started successfully",
                    SessionId = sessionId
                };
            }
            catch (Exception ex)
            {
                LogMessage($"Error starting session: {ex.Message}");
                return new SessionResult
                {
                    Success = false,
                    Message = $"Error: {ex.Message}"
                };
            }
        }

        public SampleResult PushSample(ChargingSample sample)
        {
            try
            {
                if (sample == null)
                {
                    return new SampleResult
                    {
                        Success = false,
                        Message = "Sample cannot be null"
                    };
                }

                if (!activeSessions.ContainsKey(sample.VehicleId))
                {
                    return new SampleResult
                    {
                        Success = false,
                        Message = "No active session for this vehicle"
                    };
                }

                // Validacija podataka
                var validationError = ValidateSample(sample);
                if (validationError != ValidationError.None)
                {
                    var session = activeSessions[sample.VehicleId];
                    string rejectReason = GetValidationErrorMessage(validationError);

                    // Upisivanje u rejects.csv
                    session.RejectsWriter.WriteLine($"{sample.Timestamp:yyyy-MM-dd HH:mm:ss},{rejectReason}," +
                        $"Row{sample.RowIndex}");
                    session.RejectsWriter.Flush();

                    return new SampleResult
                    {
                        Success = false,
                        Message = rejectReason,
                        ValidationError = validationError
                    };
                }

                var sessionInfo = activeSessions[sample.VehicleId];

                // Analitika 1: Provera naponskih i strujnih pikova
                if (sessionInfo.LastSample != null)
                {
                    double deltaV = Math.Abs(sample.VoltageRmsAvg - sessionInfo.LastSample.VoltageRmsAvg);
                    double deltaI = Math.Abs(sample.CurrentRmsAvg - sessionInfo.LastSample.CurrentRmsAvg);

                    if (deltaV > 10.0) // Prag za napon
                    {
                        OnWarningRaised?.Invoke(this, new TransferEventArgs
                        {
                            VehicleId = sample.VehicleId,
                            Message = $"Voltage spike detected: ΔV={deltaV:F2}V"
                        });
                    }

                    if (deltaI > 5.0) // Prag za struju
                    {
                        OnWarningRaised?.Invoke(this, new TransferEventArgs
                        {
                            VehicleId = sample.VehicleId,
                            Message = $"Current spike detected: ΔI={deltaI:F2}A"
                        });
                    }
                }

                // Analitika 2: Power Factor
                if (sample.ApparentPowerAvg > 0)
                {
                    double powerFactor = sample.RealPowerAvg / sample.ApparentPowerAvg;
                    if (powerFactor < 0.85) // Prag za power factor
                    {
                        OnWarningRaised?.Invoke(this, new TransferEventArgs
                        {
                            VehicleId = sample.VehicleId,
                            Message = $"Low power factor: {powerFactor:F2}"
                        });
                    }
                }

                // Upisivanje u CSV fajl
                var culture = CultureInfo.InvariantCulture;
                sessionInfo.DataWriter.WriteLine(
                    $"{sample.Timestamp:yyyy-MM-dd HH:mm:ss}," +
                    $"{sample.VoltageRmsMin.ToString(culture)}," +
                    $"{sample.VoltageRmsAvg.ToString(culture)}," +
                    $"{sample.VoltageRmsMax.ToString(culture)}," +
                    $"{sample.CurrentRmsMin.ToString(culture)}," +
                    $"{sample.CurrentRmsAvg.ToString(culture)}," +
                    $"{sample.CurrentRmsMax.ToString(culture)}," +
                    $"{sample.RealPowerMin.ToString(culture)}," +
                    $"{sample.RealPowerAvg.ToString(culture)}," +
                    $"{sample.RealPowerMax.ToString(culture)}," +
                    $"{sample.ReactivePowerMin.ToString(culture)}," +
                    $"{sample.ReactivePowerAvg.ToString(culture)}," +
                    $"{sample.ReactivePowerMax.ToString(culture)}," +
                    $"{sample.ApparentPowerMin.ToString(culture)}," +
                    $"{sample.ApparentPowerAvg.ToString(culture)}," +
                    $"{sample.ApparentPowerMax.ToString(culture)}," +
                    $"{sample.FrequencyMin.ToString(culture)}," +
                    $"{sample.FrequencyAvg.ToString(culture)}," +
                    $"{sample.FrequencyMax.ToString(culture)}," +
                    $"{sample.RowIndex}");
                sessionInfo.DataWriter.Flush();

                sessionInfo.SampleCount++;
                sessionInfo.LastSample = sample;

                // Okidanje događaja
                OnSampleReceived?.Invoke(this, new TransferEventArgs
                {
                    VehicleId = sample.VehicleId,
                    Message = $"Sample {sessionInfo.SampleCount} received"
                });

                return new SampleResult
                {
                    Success = true,
                    Message = "Sample processed successfully"
                };
            }
            catch (Exception ex)
            {
                LogMessage($"Error processing sample: {ex.Message}");
                return new SampleResult
                {
                    Success = false,
                    Message = $"Error: {ex.Message}"
                };
            }
        }

        public SessionResult EndSession(string vehicleId)
        {
            try
            {
                if (!activeSessions.ContainsKey(vehicleId))
                {
                    return new SessionResult
                    {
                        Success = false,
                        Message = "No active session for this vehicle"
                    };
                }

                var sessionInfo = activeSessions[vehicleId];

                // Zatvaranje stream-ova
                sessionInfo.Dispose();

                string sessionId = sessionInfo.SessionId;
                int totalSamples = sessionInfo.SampleCount;

                activeSessions.Remove(vehicleId);

                // Okidanje događaja
                OnTransferCompleted?.Invoke(this, new TransferEventArgs
                {
                    VehicleId = vehicleId,
                    Message = $"Session completed. Total samples: {totalSamples}"
                });

                LogMessage($"Session ended for vehicle {vehicleId}, Total samples: {totalSamples}");

                return new SessionResult
                {
                    Success = true,
                    Message = $"Session ended successfully. Processed {totalSamples} samples",
                    SessionId = sessionId
                };
            }
            catch (Exception ex)
            {
                LogMessage($"Error ending session: {ex.Message}");
                return new SessionResult
                {
                    Success = false,
                    Message = $"Error: {ex.Message}"
                };
            }
        }

        private ValidationError ValidateSample(ChargingSample sample)
        {
            // Validacija timestamp-a
            if (sample.Timestamp == DateTime.MinValue ||
        sample.Timestamp > DateTime.Now ||
        sample.Timestamp.Year < 2020) // Primer dodatne provere
            {
                return ValidationError.InvalidTimestamp;
            }

            // Validacija napona
            if (sample.VoltageRmsAvg <= 0 || sample.VoltageRmsMin < 0 || sample.VoltageRmsMax <= 0)
            {
                return ValidationError.InvalidVoltage;
            }

            // Validacija struje
            if (sample.CurrentRmsMin < 0 || sample.CurrentRmsMax < 0)
            {
                return ValidationError.InvalidCurrent;
            }

            // Validacija frekvencije
            if (sample.FrequencyAvg <= 0 || sample.FrequencyMin <= 0 || sample.FrequencyMax <= 0)
            {
                return ValidationError.InvalidFrequency;
            }

            // Validacija snage
            if (sample.ApparentPowerAvg < 0 || sample.RealPowerAvg < 0)
            {
                return ValidationError.InvalidPower;
            }

            // Proveri realne opsege
            if (sample.VoltageRmsMax > 1000) // Primer: max 1000V
            {
                return ValidationError.InvalidVoltage;
            }

            return ValidationError.None;
        }

        private string GetValidationErrorMessage(ValidationError error)
        {
            switch (error)
            {
                case ValidationError.InvalidTimestamp:
                    return "Invalid timestamp";
                case ValidationError.InvalidVoltage:
                    return "Invalid voltage values";
                case ValidationError.InvalidCurrent:
                    return "Invalid current values";
                case ValidationError.InvalidFrequency:
                    return "Invalid frequency values";
                case ValidationError.InvalidPower:
                    return "Invalid power values";
                default:
                    return "Unknown validation error";
            }
        }

        private void LogMessage(string message)
        {
            if (logWriter != null)
            {
                logWriter.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}");
                logWriter.Flush();
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
                    foreach (var session in activeSessions.Values)
                    {
                        session?.Dispose();
                    }
                    activeSessions.Clear();

                    logWriter?.Dispose();
                }
                disposed = true;
            }
        }

        ~ChargingService()
        {
            Dispose(false);
        }

        // MemoryStream metode za Zadatak 7
        [OperationBehavior(AutoDisposeParameters = true)]
        public FileTransferResult UploadChargingData(FileTransferRequest request)
        {
            MemoryStream memoryStream = null;
            StreamWriter writer = null;

            try
            {
                if (request == null || request.FileData == null)
                {
                    return new FileTransferResult
                    {
                        Success = false,
                        Message = "Invalid file data"
                    };
                }

                // Kreiranje MemoryStream-a od primljenih bajtova
                memoryStream = request.GetMemoryStream();

                // Čitanje podataka iz MemoryStream-a
                memoryStream.Position = 0;
                using (var reader = new StreamReader(memoryStream))
                {
                    string content = reader.ReadToEnd();

                    // Čuvanje u fajl sistem
                    string vehiclePath = Path.Combine(dataPath, request.VehicleId);
                    string datePath = Path.Combine(vehiclePath, DateTime.Now.ToString("yyyy-MM-dd"));

                    if (!Directory.Exists(datePath))
                    {
                        Directory.CreateDirectory(datePath);
                    }

                    string filePath = Path.Combine(datePath, request.FileName);
                    File.WriteAllText(filePath, content);

                    LogMessage($"File uploaded via MemoryStream: {request.FileName} for {request.VehicleId}");

                    return new FileTransferResult
                    {
                        Success = true,
                        Message = "File uploaded successfully",
                        FileName = request.FileName
                    };
                }
            }
            catch (Exception ex)
            {
                LogMessage($"Error uploading file: {ex.Message}");
                return new FileTransferResult
                {
                    Success = false,
                    Message = $"Error: {ex.Message}"
                };
            }
            finally
            {
                writer?.Dispose();
                // memoryStream će biti disposed automatski zbog AutoDisposeParameters
            }
        }

        [OperationBehavior(AutoDisposeParameters = false)]
        public FileTransferResult DownloadSessionData(string vehicleId, string sessionId)
        {
            MemoryStream memoryStream = null;

            try
            {
                string vehiclePath = Path.Combine(dataPath, vehicleId);
                string datePath = Path.Combine(vehiclePath, DateTime.Now.ToString("yyyy-MM-dd"));
                string filePath = Path.Combine(datePath, $"session_{sessionId}.csv");

                if (!File.Exists(filePath))
                {
                    return new FileTransferResult
                    {
                        Success = false,
                        Message = "Session file not found"
                    };
                }

                // Čitanje fajla u MemoryStream
                byte[] fileBytes = File.ReadAllBytes(filePath);
                memoryStream = new MemoryStream(fileBytes);

                var result = new FileTransferResult
                {
                    Success = true,
                    Message = "File downloaded successfully",
                    FileName = Path.GetFileName(filePath)
                };

                // Konvertovanje MemoryStream-a u byte array za prenos
                result.SetFromMemoryStream(memoryStream);

                LogMessage($"File downloaded via MemoryStream: session_{sessionId}.csv for {vehicleId}");

                return result;
            }
            catch (Exception ex)
            {
                LogMessage($"Error downloading file: {ex.Message}");
                return new FileTransferResult
                {
                    Success = false,
                    Message = $"Error: {ex.Message}"
                };
            }
            finally
            {
                memoryStream?.Dispose();
            }
        }
    }

    // Event Args klasa za događaje
    public class TransferEventArgs : EventArgs
    {
        public string VehicleId { get; set; }
        public string Message { get; set; }
    }
}