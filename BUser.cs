using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BunkerBot;

public class BUser
{
    public int Id { get; set; }
    public long TelegramId { get; set; }
    public string Name { get; set; }
    public virtual Profession? Profession { get; set; }
    public virtual List<Luggage>? Luggages { get; set; } = new();
    public virtual Hobby? Hobby { get; set; }
    public virtual HealthCondition? HealthCondition { get; set; }
    public virtual Biology? Biology { get; set; }
    public virtual AdditionalInfo? AdditionalInfo { get; set; }
    public virtual List<SpecialCard>? SpecialCards { get; set; } = new();
    public virtual BGame? BGame { get; set; }
    public virtual int? GameMaxVotesId { get; set; }
    public virtual int? GameVotingId { get; set; }
    public virtual Hazard? AsignedHazard { get; set; }
    public bool IsVoteDoubled { get; set; } = false;
    public bool IsVotedOut { get; set; } = false;
    public bool IsDead { get; set; } = false;
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
