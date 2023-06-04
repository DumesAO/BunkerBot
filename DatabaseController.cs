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
}




