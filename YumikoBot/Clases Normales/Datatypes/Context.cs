﻿using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.Entities;

namespace Discord_Bot
{
    public class Context
    {
        public DiscordClient Client { get; set; }
        public DiscordGuild Guild { get; set; }
        public DiscordChannel Channel { get; set; }
        public Command Command { get; set; }
        public DiscordMessage Message { get; set; }
        public DiscordUser User { get; set; }
        public DiscordMember Member { get; set; }
        public string Prefix { get; set; }
        public DiscordInteraction Interaction { get; set; }
    }
}
