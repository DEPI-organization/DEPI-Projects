using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class RoomsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ILogger<RoomsController> _logger;

    public RoomsController(AppDbContext context, ILogger<RoomsController> logger)
    {
        _context = context;
        _logger = logger;
    }

    // GET: api/Rooms
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<IEnumerable<RoomDto>>> GetRooms()
    {
        try
        {
            var rooms = await _context.Rooms
                .Where(r => r.IsAvailable)
                .OrderBy(r => r.RoomNumber)
                .Select(r => new RoomDto
                {
                    Id = r.Id,
                    RoomNumber = r.RoomNumber,
                    Type = r.Type,
                    PricePerNight = r.PricePerNight,
                    Capacity = r.Capacity,
                    Description = r.Description,
                    IsAvailable = r.IsAvailable,
                    CreatedAt = r.CreatedAt
                })
                .ToListAsync();

            return Ok(rooms);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving rooms");
            return StatusCode(500, "An error occurred while retrieving rooms");
        }
    }

    // GET: api/Rooms/All (Admin only - includes unavailable rooms)
    [HttpGet("All")]
    public async Task<ActionResult<IEnumerable<RoomDto>>> GetAllRooms()
    {
        try
        {
            var rooms = await _context.Rooms
                .OrderBy(r => r.RoomNumber)
                .Select(r => new RoomDto
                {
                    Id = r.Id,
                    RoomNumber = r.RoomNumber,
                    Type = r.Type,
                    PricePerNight = r.PricePerNight,
                    Capacity = r.Capacity,
                    Description = r.Description,
                    IsAvailable = r.IsAvailable,
                    CreatedAt = r.CreatedAt
                })
                .ToListAsync();

            return Ok(rooms);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all rooms");
            return StatusCode(500, "An error occurred while retrieving rooms");
        }
    }

    // GET: api/Rooms/5
    [HttpGet("{id}")]
    [AllowAnonymous]
    public async Task<ActionResult<RoomDto>> GetRoom(int id)
    {
        try
        {
            var room = await _context.Rooms
                .Where(r => r.Id == id && (r.IsAvailable || User.IsInRole("Admin")))
                .Select(r => new RoomDto
                {
                    Id = r.Id,
                    RoomNumber = r.RoomNumber,
                    Type = r.Type,
                    PricePerNight = r.PricePerNight,
                    Capacity = r.Capacity,
                    Description = r.Description,
                    IsAvailable = r.IsAvailable,
                    CreatedAt = r.CreatedAt
                })
                .FirstOrDefaultAsync();

            if (room == null)
                return NotFound();

            return Ok(room);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving room with ID {RoomId}", id);
            return StatusCode(500, "An error occurred while retrieving the room");
        }
    }

    // GET: api/Rooms/5/Availability
    [HttpGet("{id}/Availability")]
    [AllowAnonymous]
    public async Task<ActionResult<RoomAvailabilityResponseDto>> GetRoomAvailability(int id)
    {
        try
        {
            var startDate = DateTime.Today;
            var endDate = DateTime.Today.AddDays(30);

            // Check if room exists and is available
            var room = await _context.Rooms
                .FirstOrDefaultAsync(r => r.Id == id && r.IsAvailable);

            if (room == null)
                return NotFound("Room not found or not available");

            // Get all reservations for this room within the next 30 days
            var reservations = await _context.RoomReservations
                .Where(r => r.RoomId == id &&
                           r.Status == ReservationStatus.Confirmed &&
                           r.CheckInDate < endDate &&
                           r.CheckOutDate > startDate)
                .OrderBy(r => r.CheckInDate)
                .ToListAsync();

            // Generate availability for each day in the next 30 days
            var dailyAvailability = new List<DailyAvailabilityDto>();
            var currentDate = startDate;

            while (currentDate <= endDate)
            {
                var isAvailable = !reservations.Any(r =>
                    r.CheckInDate <= currentDate && r.CheckOutDate > currentDate);

                // Only add available days to the response
                if (isAvailable)
                {
                    dailyAvailability.Add(new DailyAvailabilityDto
                    {
                        Date = currentDate,
                        IsAvailable = true,
                        Price = room.PricePerNight,
                        DayOfWeek = currentDate.DayOfWeek.ToString()
                    });
                }

                currentDate = currentDate.AddDays(1);
            }

            // Calculate available nights
            var availableNights = dailyAvailability.Count;
            var totalPrice = room.PricePerNight * availableNights;

            var result = new RoomAvailabilityResponseDto
            {
                RoomId = room.Id,
                RoomNumber = room.RoomNumber,
                RoomType = room.Type,
                Capacity = room.Capacity,
                PricePerNight = room.PricePerNight,
                StartDate = startDate,
                EndDate = endDate,
                TotalDays = 31, // Today + 30 days
                AvailableNights = availableNights,
                DailyAvailability = dailyAvailability  // Only available days
            };

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving availability for room {RoomId}", id);
            return StatusCode(500, "An error occurred while retrieving room availability");
        }
    }

    // POST: api/Rooms
    [HttpPost]
    public async Task<ActionResult<RoomDto>> CreateRoom(CreateRoomDto createDto)
    {
        try
        {
            // Check if room number already exists
            if (await _context.Rooms.AnyAsync(r => r.RoomNumber == createDto.RoomNumber))
            {
                return BadRequest("Room number already exists");
            }

            var room = new Room
            {
                RoomNumber = createDto.RoomNumber,
                Type = createDto.Type,
                PricePerNight = createDto.PricePerNight,
                Capacity = createDto.Capacity,
                Description = createDto.Description,
                IsAvailable = createDto.IsAvailable,
                CreatedAt = DateTime.UtcNow
            };

            _context.Rooms.Add(room);
            await _context.SaveChangesAsync();

            var roomDto = new RoomDto
            {
                Id = room.Id,
                RoomNumber = room.RoomNumber,
                Type = room.Type,
                PricePerNight = room.PricePerNight,
                Capacity = room.Capacity,
                Description = room.Description,
                IsAvailable = room.IsAvailable,
                CreatedAt = room.CreatedAt
            };

            return CreatedAtAction(nameof(GetRoom), new { id = room.Id }, roomDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating room");
            return StatusCode(500, "An error occurred while creating the room");
        }
    }

    // PUT: api/Rooms/5
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateRoom(int id, UpdateRoomDto updateDto)
    {
        try
        {
            var room = await _context.Rooms.FindAsync(id);
            if (room == null)
                return NotFound();

            // Check if room number already exists (excluding current room)
            if (await _context.Rooms.AnyAsync(r => r.RoomNumber == updateDto.RoomNumber && r.Id != id))
            {
                return BadRequest("Room number already exists");
            }

            // Update fields
            if (!string.IsNullOrEmpty(updateDto.RoomNumber))
                room.RoomNumber = updateDto.RoomNumber;

            if (!string.IsNullOrEmpty(updateDto.Type))
                room.Type = updateDto.Type;

            if (updateDto.PricePerNight.HasValue)
                room.PricePerNight = updateDto.PricePerNight.Value;

            if (updateDto.Capacity.HasValue)
                room.Capacity = updateDto.Capacity.Value;

            if (!string.IsNullOrEmpty(updateDto.Description))
                room.Description = updateDto.Description;

            if (updateDto.IsAvailable.HasValue)
                room.IsAvailable = updateDto.IsAvailable.Value;

            await _context.SaveChangesAsync();

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating room with ID {RoomId}", id);
            return StatusCode(500, "An error occurred while updating the room");
        }
    }

    // PUT: api/Rooms/5/ToggleAvailability
    [HttpPut("{id}/ToggleAvailability")]
    public async Task<IActionResult> ToggleRoomAvailability(int id)
    {
        try
        {
            var room = await _context.Rooms.FindAsync(id);
            if (room == null)
                return NotFound();

            room.IsAvailable = !room.IsAvailable;
            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = $"Room {(room.IsAvailable ? "enabled" : "disabled")} successfully",
                isAvailable = room.IsAvailable
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error toggling availability for room with ID {RoomId}", id);
            return StatusCode(500, "An error occurred while updating room availability");
        }
    }

    // DELETE: api/Rooms/5
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteRoom(int id)
    {
        try
        {
            var room = await _context.Rooms.FindAsync(id);
            if (room == null)
                return NotFound();

            // Check if room has any active reservations
            var hasActiveReservations = await _context.RoomReservations
                .AnyAsync(r => r.RoomId == id && r.Status == ReservationStatus.Confirmed);

            if (hasActiveReservations)
                return BadRequest("Cannot delete room with active reservations");

            _context.Rooms.Remove(room);
            await _context.SaveChangesAsync();

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting room with ID {RoomId}", id);
            return StatusCode(500, "An error occurred while deleting the room");
        }
    }
}