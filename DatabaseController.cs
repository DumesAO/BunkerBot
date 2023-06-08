using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace BunkerBot;



internal class AppDbContext : DbContext
{
    public DbSet<Profession> Professions { get; set; }
    public DbSet<BUser> Users { get; set; }
    public DbSet<BGame> Games { get; set; }
    public DbSet<Hobby> Hobbies { get; set; }
    public DbSet<AdditionalInfo> AdditionalInfo { get; set; }
    public DbSet<HealthCondition> HealthConditions { get; set; }
    public DbSet<Luggage> Luggages { get; set; }
    public DbSet<Biology> Biologies { get; set; }
    public DbSet<SpecialCard> SpecialCards { get; set; }
    public DbSet<Hazard> Hazards { get; set; }
    public DbSet<BunkerInfo> BunkerInfos { get; set; }
    public DbSet<Catastrophe> Catastrophes { get; set; }
    public DbSet<VotingList> VotingLists { get; set; }

    public AppDbContext()
    {

    }
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) 
    {

    }
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        base.OnConfiguring(optionsBuilder);
        string connectionString = "Data Source=data.db;";
        optionsBuilder.UseSqlite(connectionString);
        optionsBuilder.UseLazyLoadingProxies();
    }
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {

        modelBuilder.Entity<BGame>()
            .HasMany(g => g.MaxVotesUsers)
            .WithOne()
            .HasForeignKey(u => u.GameMaxVotesId);

        modelBuilder.Entity<BGame>()
            .HasMany(g => g.VotingUsers)
            .WithOne()
            .HasForeignKey(u => u.GameVotingId);
        modelBuilder.Entity<BGame>()
            .HasMany(g => g.BunkerInfos)
            .WithMany(b => b.Games)
            .UsingEntity<Dictionary<string, object>>(
                "BunkerInfoGame",
                j => j
                    .HasOne<BunkerInfo>()
                    .WithMany()
                    .HasForeignKey("GameId"),
                j => j
                    .HasOne<BGame>()
                    .WithMany()
                    .HasForeignKey("BunkerInfoId"),
                j =>
                {
                    j.HasKey("BunkerInfoId", "GameId");
                    j.ToTable("BunkerInfoGame");
                }
            );
        modelBuilder.Entity<BGame>()
            .HasMany(g => g.ExileBunkerInfos)
            .WithMany(b => b.GamesExile)
            .UsingEntity<Dictionary<string, object>>(
                "BunkerInfoExileGame",
                j => j
                    .HasOne<BunkerInfo>()
                    .WithMany()
                    .HasForeignKey("GameId"),
                j => j
                    .HasOne<BGame>()
                    .WithMany()
                    .HasForeignKey("BunkerInfoExileId"),
                j =>
                {
                    j.HasKey("BunkerInfoExileId", "GameId");
                    j.ToTable("BunkerInfoExileGame");
                }
            );
        modelBuilder.Entity<BGame>()
            .HasMany(g => g.Hazards)
            .WithMany(b => b.Games)
            .UsingEntity<Dictionary<string, object>>(
                "HazardGame",
                j => j
                    .HasOne<Hazard>()
                    .WithMany()
                    .HasForeignKey("GameId"),
                j => j
                    .HasOne<BGame>()
                    .WithMany()
                    .HasForeignKey("HazardId"),
                j =>
                {
                    j.HasKey("HazardId", "GameId");
                    j.ToTable("HazardGame");
                }
            );
        modelBuilder.Entity<BUser>()
            .HasMany(g => g.Luggages)
            .WithMany(b => b.Users)
            .UsingEntity<Dictionary<string, object>>(
                "UserLuggage",
                j => j
                    .HasOne<Luggage>()
                    .WithMany()
                    .HasForeignKey("UserId"),
                j => j
                    .HasOne<BUser>()
                    .WithMany()
                    .HasForeignKey("LuggageId"),
                j =>
                {
                    j.HasKey("LuggageId", "UserId");
                    j.ToTable("UserLuggage");
                }
            );
        modelBuilder.Entity<BUser>()
            .HasMany(g => g.SpecialCards)
            .WithMany(b => b.Users)
            .UsingEntity<Dictionary<string, object>>(
                "UserSpecialCard",
                j => j
                    .HasOne<SpecialCard>()
                    .WithMany()
                    .HasForeignKey("UserId"),
                j => j
                    .HasOne<BUser>()
                    .WithMany()
                    .HasForeignKey("SpecialCardId"),
                j =>
                {
                    j.HasKey("SpecialCardId", "UserId");
                    j.ToTable("UserSpecialCard");
                }
            );

        // Добавьте настройку внешних ключей для других свойств, если они присутствуют
    }
}




