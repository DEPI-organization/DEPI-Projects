public class DailyAvailabilityDto
{
    public DateTime Date { get; set; }
    public bool IsAvailable { get; set; }
    public decimal Price { get; set; }
    public string DayOfWeek { get; set; } = string.Empty;
}