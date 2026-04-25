namespace Convert.Models
{
    public enum TranscodeJobStatus
    {
        Pending,
        Analyzing,
        BuildingCommand,
        Transcoding,
        Done,
        Failed,
        Error
    }
}
