using System.ComponentModel.DataAnnotations;

public class UpdateHallReservationDto
{
    [DataType(DataType.Date)]
    public DateTime? EventDate { get; set; }

    public TimeSpan? StartTime { get; set; }

    public TimeSpan? EndTime { get; set; }

    [Range(1, 10000, ErrorMessage = "Guest count must be between 1 and 10000")]
    public int? GuestCount { get; set; }

    [StringLength(100, ErrorMessage = "Event type cannot exceed 100 characters")]
    public string? EventType { get; set; }
}