using System;

[Serializable]
public sealed class DHEventChatTemplate
{
    public int ChatId { get; }
    public int ChatType { get; }
    public string CharacterName { get; }
    public string CharacterProfile { get; }
    public string MessageText { get; }
    public string ImageResource { get; }
    public int NextChatId { get; }
    public int BranchGroupId { get; }

    public DHEventChatTemplate(
        int chatId,
        int chatType,
        string characterName,
        string characterProfile,
        string messageText,
        string imageResource,
        int nextChatId,
        int branchGroupId)
    {
        ChatId = chatId;
        ChatType = chatType;
        CharacterName = Normalize(characterName);
        CharacterProfile = Normalize(characterProfile);
        MessageText = Normalize(messageText);
        ImageResource = Normalize(imageResource);
        NextChatId = nextChatId;
        BranchGroupId = branchGroupId;
    }

    private static string Normalize(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }
}
