using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

[Table("Halls")]
public class Hall
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required(ErrorMessage = "Hall name is required")]
    [StringLength(100, ErrorMessage = "Hall name cannot exceed 100 characters")]
    public string Name { get; set; }

    [Required(ErrorMessage = "Capacity is required")]
    [Range(1, 10000, ErrorMessage = "Capacity must be between 1 and 10000")]
    public int Capacity { get; set; }

    [Required(ErrorMessage = "Hourly rate is required")]
    [Range(0.01, 10000.00, ErrorMessage = "Hourly rate must be between 0.01 and 10000.00")]
    [Column(TypeName = "decimal(10,2)")]
    public decimal HourlyRate { get; set; }

    [StringLength(500, ErrorMessage = "Description cannot exceed 500 characters")]
    public string Description { get; set; }

    [Required]
    public bool IsAvailable { get; set; } = true;

    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}