﻿namespace Yumiko.Commands
{
    using System.Globalization;
    using System.Threading.Tasks;

    public class Help : ApplicationCommandModule
    {
        public IConfigurationRoot Configuration { private get; set; } = null!;

        [SlashCommand("help", "Help and information about Yumiko")]
        public async Task HelpAsync(InteractionContext ctx)
        {
            await ctx.CreateResponseAsync(InteractionResponseType.DeferredChannelMessageWithSource);

            var types = typeof(Program).Assembly.GetTypes();
            var commandTypes = types.Where(type => type.FullName!.StartsWith("Yumiko.Commands", true, CultureInfo.InvariantCulture));

            var sections = GetCategories(commandTypes);
            bool debugGuild = ctx.Guild.Id == ConfigurationUtils.GetConfiguration<ulong>(Configuration, Configurations.LogginGuildId);

            string description = $"{Formatter.BlockCode($"{ctx.Client.CurrentUser.Username} is an anime trivia bot, with other games and funcionalities.")}\n";

            sections.ForEach(section =>
            {
                if ((section != nameof(Help)) && ((!debugGuild && section != nameof(Owner)) || (debugGuild)))
                {
                    description += $"{Formatter.Bold(section)}\n";
                    var sectionCommands = GetCategoryCommands(commandTypes, section);
                    var fromGroup = IsSlashCommandGroup(commandTypes, section);

                    sectionCommands.ForEach(cmd =>
                    {
                        if (fromGroup)
                        {
                            description += $"{Formatter.InlineCode($"/{section.ToLower()} {cmd.Name.ToLower()}")} {cmd.Description}\n";
                        }
                        else
                        {
                            description += $"{Formatter.InlineCode($"/{cmd.Name.ToLower()}")} {cmd.Description}\n";
                        }
                    });

                    description += "\n";
                }
            });

            var embed = new DiscordEmbedBuilder
            {
                Title = $"About {ctx.Client.CurrentUser.Username}",
                Description = Common.NormalizarDescription(description),
                Color = Constants.YumikoColor,
            };

            List<DiscordLinkButtonComponent> components = new()
            {
                new(ConfigurationUtils.GetConfiguration<string>(Configuration, Configurations.InviteUrl), "Invite"),
                new(ConfigurationUtils.GetConfiguration<string>(Configuration, Configurations.Website), "Website"),
            };

            if (!string.IsNullOrEmpty(ctx.Client.CurrentApplication.PrivacyPolicyUrl))
            {
                components.Add(new(ctx.Client.CurrentApplication.PrivacyPolicyUrl, "Privacy Policy"));
            }

            if (Program.TopggEnabled)
            {
                components.Add(new($"https://top.gg/bot/{ctx.Client.CurrentApplication.Id}/vote", "Vote"));
            }

            await ctx.EditResponseAsync(new DiscordWebhookBuilder().AddEmbed(embed).AddComponents(components));
        }

        private static List<string> GetCategories(IEnumerable<Type> commandTypes)
        {
            var filteredFromCommands = commandTypes.Where(type => type.ReflectedType == null);
            return filteredFromCommands.Select(type => type.Name).ToList();
        }

        private static bool IsSlashCommandGroup(IEnumerable<Type> commandTypes, string category)
        {
            Type? commandCategory = commandTypes.Where(type => type.ReflectedType == null && type.Name == category).FirstOrDefault();

            if (commandCategory == null)
            {
                return false;
            }

            var att = commandCategory.GetCustomAttributes(typeof(SlashCommandGroupAttribute), false).FirstOrDefault();
            if (att != null)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        private static List<SlashCommandAttribute> GetCategoryCommands(IEnumerable<Type> commandTypes, string category)
        {
            var ret = new List<SlashCommandAttribute>();
            Type? commandCategory = commandTypes.Where(type => type.ReflectedType == null && type.Name == category).FirstOrDefault();

            if (commandCategory == null)
            {
                return ret;
            }

            var methods = commandCategory.GetMethods();

            foreach (var method in methods)
            {
                var att = method.GetCustomAttributes(typeof(SlashCommandAttribute), false).FirstOrDefault();
                if (att != null)
                {
                    ret.Add((SlashCommandAttribute)att);
                }
            }

            return ret;
        }
    }
}
