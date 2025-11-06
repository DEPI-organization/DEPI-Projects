using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class HallsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ILogger<HallsController> _logger;
    private readonly TimeSpan _operatingStart = new TimeSpan(9, 0, 0);  // 9:00 AM
    private readonly TimeSpan _operatingEnd = new TimeSpan(22, 0, 0);   // 10:00 PM

    public HallsController(AppDbContext context, ILogger<HallsController> logger)
    {
        _context = context;
        _logger = logger;
    }

    // GET: api/Halls
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<IEnumerable<HallDto>>> GetHalls()
    {
        try
        {
            var halls = await _context.Halls
                .Where(h => h.IsAvailable)
                .OrderBy(h => h.Name)
                .Select(h => new HallDto
                {
                    Id = h.Id,
                    Name = h.Name,
                    Capacity = h.Capacity,
                    HourlyRate = h.HourlyRate,
                    Description = h.Description,
                    IsAvailable = h.IsAvailable,
                    CreatedAt = h.CreatedAt
                })
                .ToListAsync();

            return Ok(halls);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving halls");
            return StatusCode(500, "An error occurred while retrieving halls");
        }
    }

    // GET: api/Halls/All (Admin only - includes unavailable halls)
    [HttpGet("All")]
    public async Task<ActionResult<IEnumerable<HallDto>>> GetAllHalls()
    {
        try
        {
            var halls = await _context.Halls
                .OrderBy(h => h.Name)
                .Select(h => new HallDto
                {
                    Id = h.Id,
                    Name = h.Name,
                    Capacity = h.Capacity,
                    HourlyRate = h.HourlyRate,
                    Description = h.Description,
                    IsAvailable = h.IsAvailable,
                    CreatedAt = h.CreatedAt
                })
                .ToListAsync();

            return Ok(halls);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all halls");
            return StatusCode(500, "An error occurred while retrieving halls");
        }
    }

    // GET: api/Halls/5
    [HttpGet("{id}")]
    [AllowAnonymous]
    public async Task<ActionResult<HallDto>> GetHall(int id)
    {
        try
        {
            var hall = await _context.Halls
                .Where(h => h.Id == id && (h.IsAvailable || User.IsInRole("Admin")))
                .Select(h => new HallDto
                {
                    Id = h.Id,
                    Name = h.Name,
                    Capacity = h.Capacity,
                    HourlyRate = h.HourlyRate,
                    Description = h.Description,
                    IsAvailable = h.IsAvailable,
                    CreatedAt = h.CreatedAt
                })
                .FirstOrDefaultAsync();

            if (hall == null)
                return NotFound();

            return Ok(hall);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving hall with ID {HallId}", id);
            return StatusCode(500, "An error occurred while retrieving the hall");
        }
    }

    // GET: api/Halls/5/Availability
    [HttpGet("{id}/Availability")]
    [AllowAnonymous]
    public async Task<ActionResult<HallAvailabilityResponseDto>> GetHallAvailability(int id)
    {
        try
        {
            var startDate = DateTime.Today;
            var endDate = DateTime.Today.AddDays(30);

            // Check if hall exists and is available
            var hall = await _context.Halls
                .FirstOrDefaultAsync(h => h.Id == id && h.IsAvailable);

            if (hall == null)
                return NotFound("Hall not found or not available");

            // Get all reservations for this hall within the next 30 days
            var reservations = await _context.HallReservations
                .Where(r => r.HallId == id &&
                           r.Status == ReservationStatus.Confirmed &&
                           r.EventDate >= startDate &&
                           r.EventDate <= endDate)
                .OrderBy(r => r.EventDate)
                .ThenBy(r => r.StartTime)
                .ToListAsync();

            // Generate availability for each day in the next 30 days
            var dailyAvailability = new List<HallDailyAvailabilityDto>();
            var currentDate = startDate;

            while (currentDate <= endDate)
            {
                var dayReservations = reservations.Where(r => r.EventDate == currentDate).ToList();

                // Generate all time slots for the day (9 AM to 10 PM)
                var allTimeSlots = new List<TimeSlotDto>();
                var currentTime = _operatingStart;

                while (currentTime < _operatingEnd)
                {
                    var slotEnd = currentTime.Add(TimeSpan.FromHours(1));
                    if (slotEnd > _operatingEnd)
                        slotEnd = _operatingEnd;

                    var isAvailable = !dayReservations.Any(r =>
                        r.StartTime < slotEnd && r.EndTime > currentTime);

                    allTimeSlots.Add(new TimeSlotDto
                    {
                        StartTime = currentTime,
                        EndTime = slotEnd,
                        IsAvailable = isAvailable
                    });

                    currentTime = slotEnd;
                }

                // Concatenate continuous free hours into intervals
                var continuousFreeSlots = ConcatenateContinuousSlots(allTimeSlots);

                // Only add days that have available time slots
                if (continuousFreeSlots.Any())
                {
                    dailyAvailability.Add(new HallDailyAvailabilityDto
                    {
                        Date = currentDate,
                        DayOfWeek = currentDate.DayOfWeek.ToString(),
                        OperatingHoursStart = _operatingStart,
                        OperatingHoursEnd = _operatingEnd,
                        TimeSlots = continuousFreeSlots,  // Only continuous free intervals
                        AvailableHours = continuousFreeSlots.Sum(slot => (slot.EndTime - slot.StartTime).TotalHours)
                    });
                }

                currentDate = currentDate.AddDays(1);
            }

            // Calculate overall availability statistics
            var totalAvailableHours = dailyAvailability.Sum(d => d.AvailableHours);

            var result = new HallAvailabilityResponseDto
            {
                HallId = hall.Id,
                HallName = hall.Name,
                Capacity = hall.Capacity,
                HourlyRate = hall.HourlyRate,
                StartDate = startDate,
                EndDate = endDate,
                TotalDays = 31, // Today + 30 days
                OperatingHoursStart = _operatingStart,
                OperatingHoursEnd = _operatingEnd,
                TotalAvailableHours = totalAvailableHours,
                DailyAvailability = dailyAvailability  // Only days with available slots
            };

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving availability for hall {HallId}", id);
            return StatusCode(500, "An error occurred while retrieving hall availability");
        }
    }

    // Helper method to concatenate continuous free time slots
    private List<TimeSlotDto> ConcatenateContinuousSlots(List<TimeSlotDto> allSlots)
    {
        var continuousSlots = new List<TimeSlotDto>();
        TimeSlotDto currentSlot = null;

        foreach (var slot in allSlots)
        {
            if (slot.IsAvailable)
            {
                if (currentSlot == null)
                {
                    // Start a new continuous slot
                    currentSlot = new TimeSlotDto
                    {
                        StartTime = slot.StartTime,
                        EndTime = slot.EndTime,
                        IsAvailable = true
                    };
                }
                else if (currentSlot.EndTime == slot.StartTime)
                {
                    // Continue the current slot (continuous)
                    currentSlot.EndTime = slot.EndTime;
                }
                else
                {
                    // Non-continuous slot, save current and start new
                    continuousSlots.Add(currentSlot);
                    currentSlot = new TimeSlotDto
                    {
                        StartTime = slot.StartTime,
                        EndTime = slot.EndTime,
                        IsAvailable = true
                    };
                }
            }
            else
            {
                // Slot is not available, save current continuous slot if exists
                if (currentSlot != null)
                {
                    continuousSlots.Add(currentSlot);
                    currentSlot = null;
                }
            }
        }

        // Don't forget to add the last continuous slot
        if (currentSlot != null)
        {
            continuousSlots.Add(currentSlot);
        }

        // Format display time for each continuous slot
        foreach (var slot in continuousSlots)
        {
            var hours = (slot.EndTime - slot.StartTime).TotalHours;
            slot.DisplayTime = hours == 1
                ? $"{slot.StartTime:hh\\:mm} - {slot.EndTime:hh\\:mm}"
                : $"{slot.StartTime:hh\\:mm} - {slot.EndTime:hh\\:mm} ({hours} hours)";
        }

        return continuousSlots;
    }

    // POST: api/Halls
    [HttpPost]
    public async Task<ActionResult<HallDto>> CreateHall(CreateHallDto createDto)
    {
        try
        {
            // Check if hall name already exists
            if (await _context.Halls.AnyAsync(h => h.Name == createDto.Name))
            {
                return BadRequest("Hall name already exists");
            }

            var hall = new Hall
            {
                Name = createDto.Name,
                Capacity = createDto.Capacity,
                HourlyRate = createDto.HourlyRate,
                Description = createDto.Description,
                IsAvailable = createDto.IsAvailable,
                CreatedAt = DateTime.UtcNow
            };

            _context.Halls.Add(hall);
            await _context.SaveChangesAsync();

            var hallDto = new HallDto
            {
                Id = hall.Id,
                Name = hall.Name,
                Capacity = hall.Capacity,
                HourlyRate = hall.HourlyRate,
                Description = hall.Description,
                IsAvailable = hall.IsAvailable,
                CreatedAt = hall.CreatedAt
            };

            return CreatedAtAction(nameof(GetHall), new { id = hall.Id }, hallDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating hall");
            return StatusCode(500, "An error occurred while creating the hall");
        }
    }

    // PUT: api/Halls/5
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateHall(int id, UpdateHallDto updateDto)
    {
        try
        {
            var hall = await _context.Halls.FindAsync(id);
            if (hall == null)
                return NotFound();

            // Check if hall name already exists (excluding current hall)
            if (await _context.Halls.AnyAsync(h => h.Name == updateDto.Name && h.Id != id))
            {
                return BadRequest("Hall name already exists");
            }

            // Update fields
            if (!string.IsNullOrEmpty(updateDto.Name))
                hall.Name = updateDto.Name;

            if (updateDto.Capacity.HasValue)
                hall.Capacity = updateDto.Capacity.Value;

            if (updateDto.HourlyRate.HasValue)
                hall.HourlyRate = updateDto.HourlyRate.Value;

            if (!string.IsNullOrEmpty(updateDto.Description))
                hall.Description = updateDto.Description;

            if (updateDto.IsAvailable.HasValue)
                hall.IsAvailable = updateDto.IsAvailable.Value;

            await _context.SaveChangesAsync();

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating hall with ID {HallId}", id);
            return StatusCode(500, "An error occurred while updating the hall");
        }
    }

    // PUT: api/Halls/5/ToggleAvailability
    [HttpPut("{id}/ToggleAvailability")]
    public async Task<IActionResult> ToggleHallAvailability(int id)
    {
        try
        {
            var hall = await _context.Halls.FindAsync(id);
            if (hall == null)
                return NotFound();

            hall.IsAvailable = !hall.IsAvailable;
            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = $"Hall {(hall.IsAvailable ? "enabled" : "disabled")} successfully",
                isAvailable = hall.IsAvailable
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error toggling availability for hall with ID {HallId}", id);
            return StatusCode(500, "An error occurred while updating hall availability");
        }
    }

    // DELETE: api/Halls/5
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteHall(int id)
    {
        try
        {
            var hall = await _context.Halls.FindAsync(id);
            if (hall == null)
                return NotFound();

            // Check if hall has any active reservations
            var hasActiveReservations = await _context.HallReservations
                .AnyAsync(r => r.HallId == id && r.Status == ReservationStatus.Confirmed);

            if (hasActiveReservations)
                return BadRequest("Cannot delete hall with active reservations");

            _context.Halls.Remove(hall);
            await _context.SaveChangesAsync();

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting hall with ID {HallId}", id);
            return StatusCode(500, "An error occurred while deleting the hall");
        }
    }
}