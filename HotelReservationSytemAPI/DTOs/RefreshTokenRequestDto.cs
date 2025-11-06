namespace HotelReservationSytemAPI.DTOs
{
    public class RefreshTokenRequestDto
    {
        public int userId {  get; set; }
        public string RefreshToken { get; set; }
    }
}
