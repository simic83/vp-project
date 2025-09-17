using System;
using System.IO;
using System.ServiceModel;

namespace Common
{
    [ServiceContract]
    public interface IChargingService
    {
        [OperationContract]
        SessionResult StartSession(string vehicleId);

        [OperationContract]
        SampleResult PushSample(ChargingSample sample);

        [OperationContract]
        SessionResult EndSession(string vehicleId);

        [OperationContract]
        FileTransferResult UploadChargingData(FileTransferRequest request);

        [OperationContract]
        FileTransferResult DownloadSessionData(string vehicleId, string sessionId);
    }
}