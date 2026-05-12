namespace YksTakipApp.Api.DTOs
{
    public sealed class CreateUserGoalRequest
    {
        public string TargetUniversity { get; set; } = "";
        public string TargetDepartment { get; set; } = "";
        public decimal? TargetTytNet { get; set; }
        public decimal? TargetAytNet { get; set; }
        public int DailyAvailableMinutes { get; set; } = 120;
    }
}
