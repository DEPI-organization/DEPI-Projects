using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class HallReservationsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ILogger<HallReservationsController> _logger;
    private readonly TimeSpan _operatingStart = new TimeSpan(9, 0, 0);  // 9:00 AM
    private readonly TimeSpan _operatingEnd = new TimeSpan(22, 0, 0);   // 10:00 PM
    private readonly TimeSpan _maintenanceDuration = new TimeSpan(0, 30, 0); // 30 minutes maintenance

    public HallReservationsController(AppDbContext context, ILogger<HallReservationsController> logger)
    {
        _context = context;
        _logger = logger;
    }

    // GET: api/HallReservations
    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<IEnumerable<HallReservationDto>>> GetHallReservations()
    {
        try
        {
            var reservations = await _context.HallReservations
                .Include(r => r.User)
                .Include(r => r.Hall)
                .OrderByDescending(r => r.CreatedAt)
                .Select(r => new HallReservationDto
                {
                    Id = r.Id,
                    UserId = r.UserId,
                    UserName = r.User.Username,
                    HallId = r.HallId,
                    HallName = r.Hall.Name,
                    EventDate = r.EventDate,
                    StartTime = r.StartTime,
                    EndTime = r.EndTime,
                    GuestCount = r.GuestCount,
                    TotalPrice = r.TotalPrice,
                    Status = r.Status.ToString(),
                    EventType = r.EventType,
                    CreatedAt = r.CreatedAt,
                    UpdatedAt = r.UpdatedAt
                })
                .ToListAsync();

            return Ok(reservations);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving hall reservations");
            return StatusCode(500, "An error occurred while retrieving reservations");
        }
    }

    // GET: api/HallReservations/MyReservations
    [HttpGet("MyReservations")]
    public async Task<ActionResult<IEnumerable<HallReservationDto>>> GetMyReservations()
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == null)
                return Unauthorized();

            var reservations = await _context.HallReservations
                .Where(r => r.UserId == userId)
                .Include(r => r.Hall)
                .OrderByDescending(r => r.CreatedAt)
                .Select(r => new HallReservationDto
                {
                    Id = r.Id,
                    UserId = r.UserId,
                    HallId = r.HallId,
                    HallName = r.Hall.Name,
                    EventDate = r.EventDate,
                    StartTime = r.StartTime,
                    EndTime = r.EndTime,
                    GuestCount = r.GuestCount,
                    TotalPrice = r.TotalPrice,
                    Status = r.Status.ToString(),
                    EventType = r.EventType,
                    CreatedAt = r.CreatedAt,
                    UpdatedAt = r.UpdatedAt
                })
                .ToListAsync();

            return Ok(reservations);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving user's hall reservations");
            return StatusCode(500, "An error occurred while retrieving your reservations");
        }
    }

    // GET: api/HallReservations/5
    [HttpGet("{id}")]
    public async Task<ActionResult<HallReservationDto>> GetHallReservation(int id)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == null)
                return Unauthorized();

            var reservation = await _context.HallReservations
                .Include(r => r.User)
                .Include(r => r.Hall)
                .Where(r => r.Id == id && (r.UserId == userId || User.IsInRole("Admin")))
                .Select(r => new HallReservationDto
                {
                    Id = r.Id,
                    UserId = r.UserId,
                    UserName = r.User.Username,
                    HallId = r.HallId,
                    HallName = r.Hall.Name,
                    EventDate = r.EventDate,
                    StartTime = r.StartTime,
                    EndTime = r.EndTime,
                    GuestCount = r.GuestCount,
                    TotalPrice = r.TotalPrice,
                    Status = r.Status.ToString(),
                    EventType = r.EventType,
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
            _logger.LogError(ex, "Error retrieving hall reservation with ID {ReservationId}", id);
            return StatusCode(500, "An error occurred while retrieving the reservation");
        }
    }

    // POST: api/HallReservations
    [HttpPost]
    public async Task<ActionResult<HallReservationDto>> CreateHallReservation(CreateHallReservationDto createDto)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == null)
                return Unauthorized();

            // Validate 30-day restriction for the event date
            var maxAllowedDate = DateTime.Today.AddDays(30);
            if (createDto.EventDate > maxAllowedDate)
                return BadRequest($"Reservations are only allowed up to 30 days in advance. Maximum allowed date is {maxAllowedDate:yyyy-MM-dd}");

            if (createDto.EventDate < DateTime.Today)
                return BadRequest("Event date cannot be in the past");

            // Remove seconds from times (keep hours and minutes only)
            var startTime = new TimeSpan(createDto.StartTime.Hours, createDto.StartTime.Minutes, 0);
            var endTime = new TimeSpan(createDto.EndTime.Hours, createDto.EndTime.Minutes, 0);

            // Validate time
            if (startTime >= endTime)
                return BadRequest("End time must be after start time");

            // Validate that time difference is in whole hours
            var timeDifference = endTime - startTime;
            if (timeDifference.Minutes != 0 || timeDifference.Seconds != 0)
                return BadRequest("Reservation duration must be in whole hours (e.g., 2 hours, 3 hours)");

            // Check if within operating hours (9 AM to 10 PM)
            if (startTime < _operatingStart || endTime > _operatingEnd)
                return BadRequest("Reservations are only allowed between 9:00 AM and 10:00 PM");

            // Check if hall exists and is available
            var hall = await _context.Halls
                .FirstOrDefaultAsync(r => r.Id == createDto.HallId && r.IsAvailable);

            if (hall == null)
                return BadRequest("Hall not found or not available");

            // Check if hall capacity is sufficient
            if (createDto.GuestCount > hall.Capacity)
                return BadRequest($"Hall capacity is {hall.Capacity} guests");

            // Calculate maintenance end time (actual end time + 30 minutes maintenance)
            var maintenanceEndTime = endTime.Add(_maintenanceDuration);

            // Check for overlapping reservations (same hall, same date, overlapping time including maintenance)
            var overlappingReservation = await _context.HallReservations
                .Where(r => r.HallId == createDto.HallId &&
                           r.EventDate == createDto.EventDate &&
                           r.Status == ReservationStatus.Confirmed &&
                           r.StartTime < maintenanceEndTime &&
                           r.EndTime.Add(_maintenanceDuration) > startTime)
                .FirstOrDefaultAsync();

            if (overlappingReservation != null)
                return BadRequest("Hall is already booked for the selected date and time (including maintenance period)");

            // Calculate total price (hourly rate * hours) - user only pays for actual reservation time
            var hours = (endTime - startTime).TotalHours;
            if (hours <= 0)
                return BadRequest("Invalid time range");

            var totalPrice = hall.HourlyRate * (decimal)hours;

            var reservation = new HallReservation
            {
                UserId = userId.Value,
                HallId = createDto.HallId,
                EventDate = createDto.EventDate,
                StartTime = startTime,
                EndTime = endTime,
                GuestCount = createDto.GuestCount,
                TotalPrice = totalPrice,
                Status = ReservationStatus.Confirmed,
                EventType = createDto.EventType,
                CreatedAt = DateTime.UtcNow
            };

            _context.HallReservations.Add(reservation);
            await _context.SaveChangesAsync();

            // Return the created reservation
            var reservationDto = new HallReservationDto
            {
                Id = reservation.Id,
                UserId = reservation.UserId,
                HallId = reservation.HallId,
                HallName = hall.Name,
                EventDate = reservation.EventDate,
                StartTime = reservation.StartTime,
                EndTime = reservation.EndTime,
                GuestCount = reservation.GuestCount,
                TotalPrice = reservation.TotalPrice,
                Status = reservation.Status.ToString(),
                EventType = reservation.EventType,
                CreatedAt = reservation.CreatedAt
            };

            return CreatedAtAction(nameof(GetHallReservation), new { id = reservation.Id }, reservationDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating hall reservation");
            return StatusCode(500, "An error occurred while creating the reservation");
        }
    }

    // PUT: api/HallReservations/5
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateHallReservation(int id, UpdateHallReservationDto updateDto)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == null)
                return Unauthorized();

            var reservation = await _context.HallReservations
                .Include(r => r.Hall)
                .FirstOrDefaultAsync(r => r.Id == id && (r.UserId == userId || User.IsInRole("Admin")));

            if (reservation == null)
                return NotFound();

            // Only allow updates for confirmed reservations
            if (reservation.Status != ReservationStatus.Confirmed)
                return BadRequest("Only confirmed reservations can be modified");

            // Store original values for validation
            var originalEventDate = reservation.EventDate;
            var originalStartTime = reservation.StartTime;
            var originalEndTime = reservation.EndTime;

            // Update fields if provided
            if (updateDto.EventDate.HasValue)
                reservation.EventDate = updateDto.EventDate.Value;

            TimeSpan? newStartTime = null;
            TimeSpan? newEndTime = null;

            if (updateDto.StartTime.HasValue)
            {
                newStartTime = new TimeSpan(updateDto.StartTime.Value.Hours, updateDto.StartTime.Value.Minutes, 0);
                reservation.StartTime = newStartTime.Value;
            }

            if (updateDto.EndTime.HasValue)
            {
                newEndTime = new TimeSpan(updateDto.EndTime.Value.Hours, updateDto.EndTime.Value.Minutes, 0);
                reservation.EndTime = newEndTime.Value;
            }

            if (updateDto.GuestCount.HasValue)
            {
                if (updateDto.GuestCount.Value > reservation.Hall.Capacity)
                    return BadRequest($"Hall capacity is {reservation.Hall.Capacity} guests");

                reservation.GuestCount = updateDto.GuestCount.Value;
            }

            if (!string.IsNullOrEmpty(updateDto.EventType))
                reservation.EventType = updateDto.EventType;

            // Validate dates and times after update
            if (reservation.EventDate < DateTime.Today)
                return BadRequest("Event date cannot be in the past");

            // Validate 30-day restriction for updated date
            var maxAllowedDate = DateTime.Today.AddDays(30);
            if (reservation.EventDate > maxAllowedDate)
                return BadRequest($"Reservations are only allowed up to 30 days in advance. Maximum allowed date is {maxAllowedDate:yyyy-MM-dd}");

            // Validate time difference is in whole hours
            var timeDifference = reservation.EndTime - reservation.StartTime;
            if (timeDifference.Minutes != 0 || timeDifference.Seconds != 0)
                return BadRequest("Reservation duration must be in whole hours (e.g., 2 hours, 3 hours)");

            if (reservation.StartTime >= reservation.EndTime)
                return BadRequest("End time must be after start time");

            // Check if within operating hours (9 AM to 10 PM)
            if (reservation.StartTime < _operatingStart || reservation.EndTime > _operatingEnd)
                return BadRequest("Reservations are only allowed between 9:00 AM and 10:00 PM");

            // Only check for overlapping reservations if date or time changed
            if (updateDto.EventDate.HasValue || updateDto.StartTime.HasValue || updateDto.EndTime.HasValue)
            {
                var overlappingReservation = await _context.HallReservations
                    .Where(r => r.HallId == reservation.HallId &&
                               r.Id != reservation.Id &&
                               r.EventDate == reservation.EventDate &&
                               r.Status == ReservationStatus.Confirmed &&
                               r.StartTime < reservation.EndTime.Add(_maintenanceDuration) &&
                               r.EndTime.Add(_maintenanceDuration) > reservation.StartTime)
                    .FirstOrDefaultAsync();

                if (overlappingReservation != null)
                    return BadRequest("Hall is already booked for the selected date and time (including maintenance period)");
            }

            // Recalculate total price if time changed
            if (updateDto.StartTime.HasValue || updateDto.EndTime.HasValue)
            {
                var hours = (reservation.EndTime - reservation.StartTime).TotalHours;
                reservation.TotalPrice = reservation.Hall.HourlyRate * (decimal)hours;
            }

            reservation.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating hall reservation with ID {ReservationId}", id);
            return StatusCode(500, "An error occurred while updating the reservation");
        }
    }

    // PUT: api/HallReservations/5/Cancel
    [HttpPut("{id}/Cancel")]
    public async Task<IActionResult> CancelHallReservation(int id)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == null)
                return Unauthorized();

            var reservation = await _context.HallReservations
                .FirstOrDefaultAsync(r => r.Id == id && (r.UserId == userId || User.IsInRole("Admin")));

            if (reservation == null)
                return NotFound();

            if (reservation.Status == ReservationStatus.Cancelled)
                return BadRequest("Reservation is already cancelled");

            // Only allow cancellation for confirmed reservations
            if (reservation.Status != ReservationStatus.Confirmed)
                return BadRequest("Only confirmed reservations can be cancelled");

            // Check if cancellation is allowed (e.g., not too close to event date)
            if (reservation.EventDate <= DateTime.Today.AddDays(1))
                return BadRequest("Hall reservations can only be cancelled at least 24 hours before the event date");

            reservation.Status = ReservationStatus.Cancelled;
            reservation.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return Ok(new { message = "Hall reservation cancelled successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling hall reservation with ID {ReservationId}", id);
            return StatusCode(500, "An error occurred while cancelling the reservation");
        }
    }

    // DELETE: api/HallReservations/5
    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteHallReservation(int id)
    {
        try
        {
            var reservation = await _context.HallReservations.FindAsync(id);
            if (reservation == null)
                return NotFound();

            _context.HallReservations.Remove(reservation);
            await _context.SaveChangesAsync();

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting hall reservation with ID {ReservationId}", id);
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