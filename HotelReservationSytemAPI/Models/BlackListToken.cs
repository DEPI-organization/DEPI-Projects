using System.ComponentModel.DataAnnotations;

namespace HotelReservationSytemAPI.Models
{
    public class BlackListToken
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string Token { get; set; } = string.Empty;

        public DateTime BlacklistedAt { get; set; } = DateTime.UtcNow;

        // Optional: store reason or user reference
        public int? UserId { get; set; }
        public User? User { get; set; }
    }
}
