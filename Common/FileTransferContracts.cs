using System;
using System.IO;
using System.Runtime.Serialization;
using System.ServiceModel;

namespace Common
{
    [ServiceContract]
    public interface IFileTransferService
    {
        [OperationContract]
        FileTransferResult SendChargingDataFile(FileTransferRequest request);

        [OperationContract]
        FileTransferResult GetChargingDataFiles(string vehicleId, DateTime date);
    }

    [DataContract]
    public class FileTransferRequest : IDisposable
    {
        [DataMember]
        public string VehicleId { get; set; }

        [DataMember]
        public string FileName { get; set; }

        [DataMember]
        public byte[] FileData { get; set; }

        private MemoryStream memoryStream;
        private bool disposed = false;

        public MemoryStream GetMemoryStream()
        {
            if (memoryStream == null && FileData != null)
            {
                memoryStream = new MemoryStream(FileData);
            }
            return memoryStream;
        }

        public void SetFromMemoryStream(MemoryStream stream)
        {
            if (stream != null)
            {
                stream.Position = 0;
                FileData = stream.ToArray();
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
                    memoryStream?.Dispose();
                }
                disposed = true;
            }
        }

        ~FileTransferRequest()
        {
            Dispose(false);
        }
    }

    [DataContract]
    public class FileTransferResult : IDisposable
    {
        [DataMember]
        public bool Success { get; set; }

        [DataMember]
        public string Message { get; set; }

        [DataMember]
        public byte[] FileData { get; set; }

        [DataMember]
        public string FileName { get; set; }

        private MemoryStream memoryStream;
        private bool disposed = false;

        public MemoryStream GetMemoryStream()
        {
            if (memoryStream == null && FileData != null)
            {
                memoryStream = new MemoryStream(FileData);
            }
            return memoryStream;
        }

        public void SetFromMemoryStream(MemoryStream stream)
        {
            if (stream != null)
            {
                stream.Position = 0;
                FileData = stream.ToArray();
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
                    memoryStream?.Dispose();
                }
                disposed = true;
            }
        }

        ~FileTransferResult()
        {
            Dispose(false);
        }
    }
}