using System.ComponentModel.DataAnnotations;

public class CreateHallReservationDto
{
    [Required(ErrorMessage = "Hall ID is required")]
    public int HallId { get; set; }

    [Required(ErrorMessage = "Event date is required")]
    [DataType(DataType.Date)]
    public DateTime EventDate { get; set; }

    [Required(ErrorMessage = "Start time is required")]
    public TimeSpan StartTime { get; set; }

    [Required(ErrorMessage = "End time is required")]
    public TimeSpan EndTime { get; set; }

    [Required(ErrorMessage = "Guest count is required")]
    [Range(1, 10000, ErrorMessage = "Guest count must be between 1 and 10000")]
    public int GuestCount { get; set; }

    [Required(ErrorMessage = "Event type is required")]
    [StringLength(100, ErrorMessage = "Event type cannot exceed 100 characters")]
    public string EventType { get; set; } = string.Empty;
}