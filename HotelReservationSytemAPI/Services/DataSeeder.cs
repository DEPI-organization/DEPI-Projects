public interface IDataSeeder
{
    Task SeedAdminUserAsync();
}

public class DataSeeder : IDataSeeder
{
    private readonly AppDbContext _context;
    private readonly IPasswordService _passwordService;

    public DataSeeder(AppDbContext context, IPasswordService passwordService)
    {
        _context = context;
        _passwordService = passwordService;
    }

    public async Task SeedAdminUserAsync()
    {
        // Check if admin user already exists
        if (_context.Users.Any(u => u.Username == "admin"))
        {
            return; // Admin already exists
        }

        var adminUser = new User
        {
            Username = "admin",
            Email = "admin@hotel.com",
            Role = UserRole.Admin,
            CreatedAt = DateTime.UtcNow
        };

        // Hash the password
        var passwordHash = _passwordService.HashPassword("admin123");
        adminUser.PasswordHash = passwordHash;

        _context.Users.Add(adminUser);
        await _context.SaveChangesAsync();
    }
}
