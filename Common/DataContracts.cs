using System;
using System.Runtime.Serialization;

namespace Common
{
    [DataContract]
    public class ChargingSample
    {
        [DataMember]
        public DateTime Timestamp { get; set; }

        [DataMember]
        public double VoltageRmsMin { get; set; }

        [DataMember]
        public double VoltageRmsAvg { get; set; }

        [DataMember]
        public double VoltageRmsMax { get; set; }

        [DataMember]
        public double CurrentRmsMin { get; set; }

        [DataMember]
        public double CurrentRmsAvg { get; set; }

        [DataMember]
        public double CurrentRmsMax { get; set; }

        [DataMember]
        public double RealPowerMin { get; set; }

        [DataMember]
        public double RealPowerAvg { get; set; }

        [DataMember]
        public double RealPowerMax { get; set; }

        [DataMember]
        public double ReactivePowerMin { get; set; }

        [DataMember]
        public double ReactivePowerAvg { get; set; }

        [DataMember]
        public double ReactivePowerMax { get; set; }

        [DataMember]
        public double ApparentPowerMin { get; set; }

        [DataMember]
        public double ApparentPowerAvg { get; set; }

        [DataMember]
        public double ApparentPowerMax { get; set; }

        [DataMember]
        public double FrequencyMin { get; set; }

        [DataMember]
        public double FrequencyAvg { get; set; }

        [DataMember]
        public double FrequencyMax { get; set; }

        [DataMember]
        public int RowIndex { get; set; }

        [DataMember]
        public string VehicleId { get; set; }
    }

    [DataContract]
    public class SessionResult
    {
        [DataMember]
        public bool Success { get; set; }

        [DataMember]
        public string Message { get; set; }

        [DataMember]
        public string SessionId { get; set; }
    }

    [DataContract]
    public class SampleResult
    {
        [DataMember]
        public bool Success { get; set; }

        [DataMember]
        public string Message { get; set; }

        [DataMember]
        public ValidationError ValidationError { get; set; }
    }

    [DataContract]
    public enum ValidationError
    {
        [EnumMember]
        None,
        [EnumMember]
        InvalidTimestamp,
        [EnumMember]
        InvalidVoltage,
        [EnumMember]
        InvalidCurrent,
        [EnumMember]
        InvalidFrequency,
        [EnumMember]
        InvalidPower
    }

    // Custom Exception za FaultContract
    [DataContract]
    public class ChargingFault
    {
        [DataMember]
        public string Message { get; set; }

        [DataMember]
        public string Details { get; set; }

        public ChargingFault(string message, string details = null)
        {
            Message = message;
            Details = details;
        }
    }
}