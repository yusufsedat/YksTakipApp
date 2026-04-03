namespace YksTakipApp.Api.DTOs
{
    public class ScheduleEntryDto
    {
        public int Id { get; set; }
        public string Recurrence { get; set; } = "";
        public int? DayOfWeek { get; set; }
        public int? DayOfMonth { get; set; }
        public int StartMinute { get; set; }
        public int EndMinute { get; set; }
        public string Title { get; set; } = "";
        public int? TopicId { get; set; }
    }

    public class ScheduleCreateRequest
    {
        public string Recurrence { get; set; } = "Weekly";
        public int? DayOfWeek { get; set; }
        public int? DayOfMonth { get; set; }
        public int StartMinute { get; set; }
        public int EndMinute { get; set; }
        public string Title { get; set; } = "";
        public int? TopicId { get; set; }
    }

    public class ScheduleUpdateRequest
    {
        public string Recurrence { get; set; } = "Weekly";
        public int? DayOfWeek { get; set; }
        public int? DayOfMonth { get; set; }
        public int StartMinute { get; set; }
        public int EndMinute { get; set; }
        public string Title { get; set; } = "";
        public int? TopicId { get; set; }
    }
}
