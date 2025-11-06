// DTOs/UpdateRoomReservationDto.cs
using System.ComponentModel.DataAnnotations;
public class UpdateRoomReservationDto
{
    [DataType(DataType.Date)]
    public DateTime? CheckInDate { get; set; }

    [DataType(DataType.Date)]
    public DateTime? CheckOutDate { get; set; }

    [Range(1, 10, ErrorMessage = "Guest count must be between 1 and 10")]
    public int? GuestCount { get; set; }
}