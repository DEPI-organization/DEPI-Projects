using System.ComponentModel.DataAnnotations;

public class CreateRoomDto
{
    [Required(ErrorMessage = "Room number is required")]
    [StringLength(10, ErrorMessage = "Room number cannot exceed 10 characters")]
    public string RoomNumber { get; set; } = string.Empty;

    [Required(ErrorMessage = "Room type is required")]
    [StringLength(50, ErrorMessage = "Room type cannot exceed 50 characters")]
    public string Type { get; set; } = string.Empty;

    [Required(ErrorMessage = "Price per night is required")]
    [Range(0.01, 10000.00, ErrorMessage = "Price must be between 0.01 and 10000.00")]
    public decimal PricePerNight { get; set; }

    [Required(ErrorMessage = "Capacity is required")]
    [Range(1, 10, ErrorMessage = "Capacity must be between 1 and 10")]
    public int Capacity { get; set; }

    [StringLength(500, ErrorMessage = "Description cannot exceed 500 characters")]
    public string Description { get; set; } = string.Empty;

    public bool IsAvailable { get; set; } = true;
}