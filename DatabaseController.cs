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

public class Profession
{
    public int Id { get; set; }
    public string Name { get; set; }= string.Empty;
}

public class Luggage
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class HealthCondition
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class Biology
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class AdditionalInfo
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class Hobby
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class SpecialCard
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class BUser
{
    public int Id { get; set; }
    public long TelegramId { get; set; }
    public string Name { get; set; }
    public virtual Profession? Profession { get; set; }
    public virtual List<Luggage> Luggages { get; set; } = new();
    public virtual Hobby? Hobby { get; set; }
    public virtual HealthCondition? HealthCondition { get; set; }
    public virtual Biology? Biology { get; set; }
    public virtual AdditionalInfo? AdditionalInfo { get; set; }
    public virtual List<SpecialCard> SpecialCards { get; set; } = new();
    public virtual BGame? BGame { get; set; }
    public virtual int MenuMessageId { get; set; }
    public virtual BUser? VotedFor { get; set; }

    public bool FirstSpecialCardUsed { get; set; } = false;
    public bool SecondSpecialCardUsed { get; set; } = false;
    public bool ProfessionOpened { get; set; } = false;
    public bool LuggagesOpened { get; set; } = false;
    public bool HobbyOpened { get; set; } = false;
    public bool BiologyOpened { get; set; } = false;
    public bool HealthConditionOpened { get; set; } = false;
    public bool AdditionalInfoOpened { get; set; } = false;


}
public class BunkerInfo
{
    public int Id { get; set; }
    public string Name { get; set; }
}

public class Catastrophe
{
    public int Id { get; set; }
    public string Name { get; set; }
}

public class Hazard
{
    public int Id { get; set; }
    public string Name { get; set; }
}

public class BGame
{
    public int Id { get; set; }
    public virtual List<BUser> Users { get; set; } = new();
    public virtual List<BunkerInfo> BunkerInfos { get; set; } = new();
    public virtual List<Hazard> Hazards { get; set; } = new();
    public virtual Catastrophe? Catastrophe { get; set; }
    public long GroupId { get; set; }
    public virtual BUser? Admin { get; set; }
    public virtual long? StartGameBotMessageId { get; set; }
    public bool IsPaused { get; set; } = false;
    public virtual VotingList VotingList{get;set;}
    public int Status { get; set; } = 0;
    public int RoundPart { get; set; } = 0;
}

public class VotingList
{
    public int Id { get; set; }
    public string roundVotings { get; set; }
}