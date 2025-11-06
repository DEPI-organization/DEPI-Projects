using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class RoomReservationsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ILogger<RoomReservationsController> _logger;

    public RoomReservationsController(AppDbContext context, ILogger<RoomReservationsController> logger)
    {
        _context = context;
        _logger = logger;
    }

    // GET: api/RoomReservations
    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<IEnumerable<RoomReservationDto>>> GetRoomReservations()
    {
        try
        {
            var reservations = await _context.RoomReservations
                .Include(r => r.User)
                .Include(r => r.Room)
                .OrderByDescending(r => r.CreatedAt)
                .Select(r => new RoomReservationDto
                {
                    Id = r.Id,
                    UserId = r.UserId,
                    UserName = r.User.Username,
                    RoomId = r.RoomId,
                    RoomNumber = r.Room.RoomNumber,
                    RoomType = r.Room.Type,
                    CheckInDate = r.CheckInDate,
                    CheckOutDate = r.CheckOutDate,
                    GuestCount = r.GuestCount,
                    TotalPrice = r.TotalPrice,
                    Status = r.Status.ToString(),
                    CreatedAt = r.CreatedAt,
                    UpdatedAt = r.UpdatedAt
                })
                .ToListAsync();

            return Ok(reservations);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving room reservations");
            return StatusCode(500, "An error occurred while retrieving reservations");
        }
    }

    // GET: api/RoomReservations/MyReservations
    [HttpGet("MyReservations")]
    public async Task<ActionResult<IEnumerable<RoomReservationDto>>> GetMyReservations()
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == null)
                return Unauthorized();

            var reservations = await _context.RoomReservations
                .Where(r => r.UserId == userId)
                .Include(r => r.Room)
                .OrderByDescending(r => r.CreatedAt)
                .Select(r => new RoomReservationDto
                {
                    Id = r.Id,
                    UserId = r.UserId,
                    RoomId = r.RoomId,
                    RoomNumber = r.Room.RoomNumber,
                    RoomType = r.Room.Type,
                    CheckInDate = r.CheckInDate,
                    CheckOutDate = r.CheckOutDate,
                    GuestCount = r.GuestCount,
                    TotalPrice = r.TotalPrice,
                    Status = r.Status.ToString(),
                    CreatedAt = r.CreatedAt,
                    UpdatedAt = r.UpdatedAt
                })
                .ToListAsync();

            return Ok(reservations);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving user's room reservations");
            return StatusCode(500, "An error occurred while retrieving your reservations");
        }
    }

    // GET: api/RoomReservations/5
    [HttpGet("{id}")]
    public async Task<ActionResult<RoomReservationDto>> GetRoomReservation(int id)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == null)
                return Unauthorized();

            var reservation = await _context.RoomReservations
                .Include(r => r.User)
                .Include(r => r.Room)
                .Where(r => r.Id == id && (r.UserId == userId || User.IsInRole("Admin")))
                .Select(r => new RoomReservationDto
                {
                    Id = r.Id,
                    UserId = r.UserId,
                    UserName = r.User.Username,
                    RoomId = r.RoomId,
                    RoomNumber = r.Room.RoomNumber,
                    RoomType = r.Room.Type,
                    CheckInDate = r.CheckInDate,
                    CheckOutDate = r.CheckOutDate,
                    GuestCount = r.GuestCount,
                    TotalPrice = r.TotalPrice,
                    Status = r.Status.ToString(),
                    CreatedAt = r.CreatedAt,
                    UpdatedAt = r.UpdatedAt
                })
                .FirstOrDefaultAsync();

            if (reservation == null)
                return NotFound();

            return Ok(reservation);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving room reservation with ID {ReservationId}", id);
            return StatusCode(500, "An error occurred while retrieving the reservation");
        }
    }

    // POST: api/RoomReservations
    [HttpPost]
    public async Task<ActionResult<RoomReservationDto>> CreateRoomReservation(CreateRoomReservationDto createDto)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == null)
                return Unauthorized();

            // Validate 30-day restriction for BOTH dates
            var maxAllowedDate = DateTime.Today.AddDays(30);
            if (createDto.CheckInDate > maxAllowedDate || createDto.CheckOutDate > maxAllowedDate)
                return BadRequest($"Reservations are only allowed up to 30 days in advance. Maximum allowed date is {maxAllowedDate:yyyy-MM-dd}");

            // Validate dates
            if (createDto.CheckInDate >= createDto.CheckOutDate)
                return BadRequest("Check-out date must be after check-in date");

            if (createDto.CheckInDate < DateTime.Today)
                return BadRequest("Check-in date cannot be in the past");

            // Check if room exists and is available
            var room = await _context.Rooms
                .FirstOrDefaultAsync(r => r.Id == createDto.RoomId && r.IsAvailable);

            if (room == null)
                return BadRequest("Room not found or not available");

            // Check if room capacity is sufficient
            if (createDto.GuestCount > room.Capacity)
                return BadRequest($"Room capacity is {room.Capacity} guests");

            // Check for overlapping reservations (only consider confirmed reservations)
            var overlappingReservation = await _context.RoomReservations
                .Where(r => r.RoomId == createDto.RoomId &&
                           r.Status == ReservationStatus.Confirmed &&
                           r.CheckInDate < createDto.CheckOutDate &&
                           r.CheckOutDate > createDto.CheckInDate)
                .FirstOrDefaultAsync();

            if (overlappingReservation != null)
                return BadRequest("Room is already booked for the selected dates");

            // Calculate total price
            var nights = (createDto.CheckOutDate - createDto.CheckInDate).Days;
            if (nights <= 0)
                return BadRequest("Invalid date range");

            var totalPrice = room.PricePerNight * nights;

            var reservation = new RoomReservation
            {
                UserId = userId.Value,
                RoomId = createDto.RoomId,
                CheckInDate = createDto.CheckInDate,
                CheckOutDate = createDto.CheckOutDate,
                GuestCount = createDto.GuestCount,
                TotalPrice = totalPrice,
                Status = ReservationStatus.Confirmed,
                CreatedAt = DateTime.UtcNow
            };

            _context.RoomReservations.Add(reservation);
            await _context.SaveChangesAsync();

            // Return the created reservation
            var reservationDto = new RoomReservationDto
            {
                Id = reservation.Id,
                UserId = reservation.UserId,
                RoomId = reservation.RoomId,
                RoomNumber = room.RoomNumber,
                RoomType = room.Type,
                CheckInDate = reservation.CheckInDate,
                CheckOutDate = reservation.CheckOutDate,
                GuestCount = reservation.GuestCount,
                TotalPrice = reservation.TotalPrice,
                Status = reservation.Status.ToString(),
                CreatedAt = reservation.CreatedAt
            };

            return CreatedAtAction(nameof(GetRoomReservation), new { id = reservation.Id }, reservationDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating room reservation");
            return StatusCode(500, "An error occurred while creating the reservation");
        }
    }

    // PUT: api/RoomReservations/5
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateRoomReservation(int id, UpdateRoomReservationDto updateDto)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == null)
                return Unauthorized();

            var reservation = await _context.RoomReservations
                .Include(r => r.Room)
                .FirstOrDefaultAsync(r => r.Id == id && (r.UserId == userId || User.IsInRole("Admin")));

            if (reservation == null)
                return NotFound();

            // Only allow updates for confirmed reservations
            if (reservation.Status != ReservationStatus.Confirmed)
                return BadRequest("Only confirmed reservations can be modified");

            // Store original dates for validation
            var originalCheckIn = reservation.CheckInDate;
            var originalCheckOut = reservation.CheckOutDate;

            // Update fields if provided
            if (updateDto.CheckInDate.HasValue)
                reservation.CheckInDate = updateDto.CheckInDate.Value;

            if (updateDto.CheckOutDate.HasValue)
                reservation.CheckOutDate = updateDto.CheckOutDate.Value;

            if (updateDto.GuestCount.HasValue)
            {
                if (updateDto.GuestCount.Value > reservation.Room.Capacity)
                    return BadRequest($"Room capacity is {reservation.Room.Capacity} guests");

                reservation.GuestCount = updateDto.GuestCount.Value;
            }

            // Validate dates after update
            if (reservation.CheckInDate >= reservation.CheckOutDate)
                return BadRequest("Check-out date must be after check-in date");

            if (reservation.CheckInDate < DateTime.Today)
                return BadRequest("Check-in date cannot be in the past");

            // Validate 30-day restriction for updated dates
            var maxAllowedDate = DateTime.Today.AddDays(30);
            if (reservation.CheckInDate > maxAllowedDate || reservation.CheckOutDate > maxAllowedDate)
                return BadRequest($"Reservations are only allowed up to 30 days in advance. Maximum allowed date is {maxAllowedDate:yyyy-MM-dd}");

            // Only check for overlapping reservations if dates changed
            if (updateDto.CheckInDate.HasValue || updateDto.CheckOutDate.HasValue)
            {
                var overlappingReservation = await _context.RoomReservations
                    .Where(r => r.RoomId == reservation.RoomId &&
                               r.Id != reservation.Id &&
                               r.Status == ReservationStatus.Confirmed &&
                               r.CheckInDate < reservation.CheckOutDate &&
                               r.CheckOutDate > reservation.CheckInDate)
                    .FirstOrDefaultAsync();

                if (overlappingReservation != null)
                    return BadRequest("Room is already booked for the selected dates");
            }

            // Recalculate total price if dates changed
            if (updateDto.CheckInDate.HasValue || updateDto.CheckOutDate.HasValue)
            {
                var nights = (reservation.CheckOutDate - reservation.CheckInDate).Days;
                reservation.TotalPrice = reservation.Room.PricePerNight * nights;
            }

            reservation.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating room reservation with ID {ReservationId}", id);
            return StatusCode(500, "An error occurred while updating the reservation");
        }
    }

    // PUT: api/RoomReservations/5/Cancel
    [HttpPut("{id}/Cancel")]
    public async Task<IActionResult> CancelRoomReservation(int id)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == null)
                return Unauthorized();

            var reservation = await _context.RoomReservations
                .FirstOrDefaultAsync(r => r.Id == id && (r.UserId == userId || User.IsInRole("Admin")));

            if (reservation == null)
                return NotFound();

            if (reservation.Status == ReservationStatus.Cancelled)
                return BadRequest("Reservation is already cancelled");

            // Only allow cancellation for confirmed reservations
            if (reservation.Status != ReservationStatus.Confirmed)
                return BadRequest("Only confirmed reservations can be cancelled");

            // Check if cancellation is allowed (e.g., not too close to check-in date)
            if (reservation.CheckInDate <= DateTime.Today.AddDays(1))
                return BadRequest("Reservations can only be cancelled at least 24 hours before check-in");

            reservation.Status = ReservationStatus.Cancelled;
            reservation.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return Ok(new { message = "Reservation cancelled successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling room reservation with ID {ReservationId}", id);
            return StatusCode(500, "An error occurred while cancelling the reservation");
        }
    }

    // DELETE: api/RoomReservations/5
    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteRoomReservation(int id)
    {
        try
        {
            var reservation = await _context.RoomReservations.FindAsync(id);
            if (reservation == null)
                return NotFound();

            _context.RoomReservations.Remove(reservation);
            await _context.SaveChangesAsync();

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting room reservation with ID {ReservationId}", id);
            return StatusCode(500, "An error occurred while deleting the reservation");
        }
    }

    private int? GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (int.TryParse(userIdClaim, out int userId))
            return userId;
        return null;
    }
}
