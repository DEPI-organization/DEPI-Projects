public class HallDailyAvailabilityDto
{
    public DateTime Date { get; set; }
    public string DayOfWeek { get; set; } = string.Empty;
    public TimeSpan OperatingHoursStart { get; set; }
    public TimeSpan OperatingHoursEnd { get; set; }
    public List<TimeSlotDto> TimeSlots { get; set; } = new();  // Only continuous free intervals
    public double AvailableHours { get; set; }
}