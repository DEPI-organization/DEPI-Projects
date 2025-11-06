using Microsoft.EntityFrameworkCore;
using hotel_reservation_system.Models;
using HotelReservationSytemAPI.Models;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users { get; set; }
    public DbSet<Room> Rooms { get; set; }
    public DbSet<Hall> Halls { get; set; }
    public DbSet<RoomReservation> RoomReservations { get; set; }
    public DbSet<HallReservation> HallReservations { get; set; }
    public DbSet<RefreshToken> RefreshTokens { get; set; }
    public DbSet<BlackListToken> BlackListTokens { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Configure User Role as enum
        modelBuilder.Entity<User>()
            .Property(u => u.Role)
            .HasConversion<int>();

        // Configure unique constraints
        modelBuilder.Entity<User>()
            .HasIndex(u => u.Username)
            .IsUnique();

        modelBuilder.Entity<User>()
            .HasIndex(u => u.Email)
            .IsUnique();

        // Configure Room number uniqueness
        modelBuilder.Entity<Room>()
            .HasIndex(r => r.RoomNumber)
            .IsUnique();

        // Configure ReservationStatus as enum
        modelBuilder.Entity<RoomReservation>()
            .Property(r => r.Status)
            .HasConversion<int>();

        modelBuilder.Entity<HallReservation>()
            .Property(h => h.Status)
            .HasConversion<int>();

        // ========== FIX CASCADE DELETE CONFIGURATION ==========

        // Configure RoomReservations with Restrict delete behavior
        modelBuilder.Entity<RoomReservation>()
            .HasOne(rr => rr.User)
            .WithMany()
            .HasForeignKey(rr => rr.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<RoomReservation>()
            .HasOne(rr => rr.Room)
            .WithMany()
            .HasForeignKey(rr => rr.RoomId)
            .OnDelete(DeleteBehavior.Restrict);

        // Configure HallReservations with Restrict delete behavior
        modelBuilder.Entity<HallReservation>()
            .HasOne(hr => hr.User)
            .WithMany()
            .HasForeignKey(hr => hr.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<HallReservation>()
            .HasOne(hr => hr.Hall)
            .WithMany()
            .HasForeignKey(hr => hr.HallId)
            .OnDelete(DeleteBehavior.Restrict);
        
    }
}