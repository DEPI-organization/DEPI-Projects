public class HallAvailabilityResponseDto
{
    public int HallId { get; set; }
    public string HallName { get; set; } = string.Empty;
    public int Capacity { get; set; }
    public decimal HourlyRate { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int TotalDays { get; set; }
    public TimeSpan OperatingHoursStart { get; set; }
    public TimeSpan OperatingHoursEnd { get; set; }
    public double TotalAvailableHours { get; set; }
    public List<HallDailyAvailabilityDto> DailyAvailability { get; set; } = new();
}