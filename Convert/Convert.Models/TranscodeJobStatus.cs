namespace Convert.Models
{
    public enum JobStatus
    {
        Pending,
        Analyzing,
        BuildingCommand,
        Transcoding,
        Done,
        Failed,
        Error,
        Stopped
    }
}
