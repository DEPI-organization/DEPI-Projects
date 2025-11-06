using System.ComponentModel.DataAnnotations;
public class CreateRoomReservationDto
{
    [Required(ErrorMessage = "Room ID is required")]
    public int RoomId { get; set; }

    [Required(ErrorMessage = "Check-in date is required")]
    [DataType(DataType.Date)]
    public DateTime CheckInDate { get; set; }

    [Required(ErrorMessage = "Check-out date is required")]
    [DataType(DataType.Date)]
    public DateTime CheckOutDate { get; set; }

    [Required(ErrorMessage = "Guest count is required")]
    [Range(1, 10, ErrorMessage = "Guest count must be between 1 and 10")]
    public int GuestCount { get; set; }
}
