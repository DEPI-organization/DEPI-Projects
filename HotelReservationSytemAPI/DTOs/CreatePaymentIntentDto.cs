namespace HotelReservationSytemAPI.DTOs
{
    public class CreatePaymentIntentDto
    {
        public double Amount { get; set; }
        public string Currency { get; set; } = "usd";
        public int UserId { get; set; }
    }
}
