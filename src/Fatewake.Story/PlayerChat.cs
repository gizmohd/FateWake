namespace Fatewake.Story;

/// <summary>A player visible through a local encounter or an existing friend relationship.</summary>
public sealed record ChatContact(Guid PlayerId, string Name, bool Nearby, bool Remote, string? Friendship);

/// <summary>Chat preferences and people visible to the current player.</summary>
public sealed record ChatLobby(bool Enabled, IReadOnlyList<ChatContact> Contacts);

/// <summary>A message visible to one of its participants in the current story context.</summary>
public sealed record ChatLine(long Id, bool FromYou, string Text);

/// <summary>Messages and the active means of communication for the selected contact.</summary>
public sealed record ChatConversation(string? Channel, IReadOnlyList<ChatLine> Messages);
