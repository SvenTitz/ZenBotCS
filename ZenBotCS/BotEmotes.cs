using Discord;

namespace ZenBotCS;

public static class BotEmotes
{
    public static IEmote TH7 => Emote.Parse("<:th7:589494170968653843>");
    public static IEmote TH8 => Emote.Parse("<:th8:589494147736403968>");
    public static IEmote TH9 => Emote.Parse("<:th9:589494120997978112>");
    public static IEmote TH10 => Emote.Parse("<:th10:589493988948443141>");
    public static IEmote TH11 => Emote.Parse("<:th11:589494010977058842>");
    public static IEmote TH12 => Emote.Parse("<:th12:589494045651369990>");
    public static IEmote TH13 => Emote.Parse("<:th13:651100067964518420>");
    public static IEmote TH14 => Emote.Parse("<:th14:1027658536882286653>");
    public static IEmote TH15 => Emote.Parse("<:th15:1027658448697032714>");
    public static IEmote TH16 => Emote.Parse("<:th16:1183775328884228216>");

    public static IEmote GetThEmote(int thLevel)
    {
        return thLevel switch
        {
            7 => TH7,
            8 => TH8,
            9 => TH9,
            10 => TH10,
            11 => TH11,
            12 => TH12,
            13 => TH13,
            14 => TH14,
            15 => TH15,
            16 => TH16,
            _ => TH7,
        };
    }
}
