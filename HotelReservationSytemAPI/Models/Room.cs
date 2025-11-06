using hotel_reservation_system.Models;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

[Table("Rooms")]
public class Room
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required(ErrorMessage = "Room number is required")]
    [StringLength(10, ErrorMessage = "Room number cannot exceed 10 characters")]
    [RegularExpression(@"^[A-Z0-9-]+$", ErrorMessage = "Room number can only contain uppercase letters, numbers, and hyphens")]
    public string RoomNumber { get; set; }

    [Required(ErrorMessage = "Room type is required")]
    [StringLength(50, ErrorMessage = "Room type cannot exceed 50 characters")]
    public string Type { get; set; }

    [Required(ErrorMessage = "Price per night is required")]
    [Range(0.01, 10000.00, ErrorMessage = "Price must be between 0.01 and 10000.00")]
    [Column(TypeName = "decimal(10,2)")]
    public decimal PricePerNight { get; set; }

    [Required(ErrorMessage = "Capacity is required")]
    [Range(1, 10, ErrorMessage = "Capacity must be between 1 and 10")]
    public int Capacity { get; set; }

    [StringLength(500, ErrorMessage = "Description cannot exceed 500 characters")]
    public string Description { get; set; }

    [Required]
    public bool IsAvailable { get; set; } = true;

    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

}