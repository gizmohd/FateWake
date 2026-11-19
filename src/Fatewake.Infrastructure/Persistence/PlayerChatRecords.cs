using Microsoft.EntityFrameworkCore;

namespace Fatewake.Infrastructure.Persistence;

internal sealed class PlayerFriendRecord
{
    public Guid FirstPlayer { get; set; }
    public Guid SecondPlayer { get; set; }
    public Guid RequestedBy { get; set; }
    public bool Accepted { get; set; }
}

internal sealed class PlayerMessageRecord
{
    public long Id { get; set; }
    public Guid FirstPlayer { get; set; }
    public Guid SecondPlayer { get; set; }
    public Guid Sender { get; set; }
    public required string Scope { get; set; }
    public required string Text { get; set; }
    public long StoryTick { get; set; }
    public DateTimeOffset SentAt { get; set; }
}

internal static class PlayerChatModel
{
    internal static void Configure(ModelBuilder model)
    {
        model.Entity<PlayerFriendRecord>(b =>
        {
            b.ToTable("player_friend");
            b.HasKey(x => new { x.FirstPlayer, x.SecondPlayer });
            b.HasIndex(x => x.SecondPlayer).HasDatabaseName("ix_player_friend_second");
            b.HasOne<AccountRecord>().WithMany().HasForeignKey(x => x.FirstPlayer);
            b.HasOne<AccountRecord>().WithMany().HasForeignKey(x => x.SecondPlayer);
        });
        model.Entity<PlayerMessageRecord>(b =>
        {
            b.ToTable("player_message");
            b.HasKey(x => x.Id);
            b.Property(x => x.Text).HasMaxLength(400);
            b.HasIndex(x => new { x.FirstPlayer, x.SecondPlayer, x.Scope, x.Id }).HasDatabaseName("ix_player_message_conversation");
            b.HasOne<AccountRecord>().WithMany().HasForeignKey(x => x.FirstPlayer);
            b.HasOne<AccountRecord>().WithMany().HasForeignKey(x => x.SecondPlayer);
        });
    }
}
