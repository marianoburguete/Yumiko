﻿using Discord_Bot.Modulos;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Exceptions;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using DSharpPlus.Interactivity;
using DSharpPlus.Interactivity.Extensions;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using YumikoBot;
using DSharpPlus.SlashCommands;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.SlashCommands.EventArgs;
using static DSharpPlus.Entities.DiscordEmbedBuilder;

namespace Discord_Bot
{
    public class Yumiko
    {
        public DiscordClient Client { get; private set; }
        public CommandsNextExtension Commands { get; private set; }
        public SlashCommandsExtension ApplicationCommands { get; private set; }

        private DiscordChannel LogChannelGeneral;

        private DiscordChannel LogChannelSlash;

        private DiscordChannel LogChannelContextMenus;

        private DiscordChannel LogChannelServers;

        private DiscordChannel LogChannelErrores;

        private readonly FuncionesAuxiliares funciones = new();

        private bool Debug;

        public async Task RunAsync()
        {
            var json = string.Empty;
            using (var fs = File.OpenRead("config.json"))
            {
                using var sr = new StreamReader(fs, new UTF8Encoding(false));
                json = await sr.ReadToEndAsync().ConfigureAwait(false);
            }

            var configJson = JsonConvert.DeserializeObject<ConfigJson>(json);

            string token, prefix;
            IDebuggingService mode = new DebuggingService();
            Debug = mode.RunningInDebugMode();
            if (Debug)
            {
                token = configJson.TokenTest;
                prefix = ConfigurationManager.AppSettings["PrefixTest"];
            }
            else
            {
                token = configJson.TokenProd;
                prefix = ConfigurationManager.AppSettings["PrefixProd"];
            }

            var Config = new DiscordConfiguration
            {
                Token = token,
                TokenType = TokenType.Bot,
                AutoReconnect = true,
                ReconnectIndefinitely = true,
                MinimumLogLevel = LogLevel.Information,
                Intents = DiscordIntents.AllUnprivileged
            };
            Client = new DiscordClient(Config);

            Client.Ready += OnClientReady;
            Client.ClientErrored += Client_ClientError;
            Client.GuildCreated += Client_GuildCreated;
            Client.GuildDeleted += Client_GuildDeleted;
            Client.Resumed += Client_Resumed;
            Client.ComponentInteractionCreated += async (_, args) => await args.Interaction.CreateResponseAsync(InteractionResponseType.DeferredMessageUpdate);

            Client.UseInteractivity(new InteractivityConfiguration());

            var commandsConfig = new CommandsNextConfiguration
            {
                StringPrefixes = new string[] { prefix },
                EnableDms = false,
                DmHelp = false,
                EnableDefaultHelp = false,
                IgnoreExtraArguments = true
            };

            Commands = Client.UseCommandsNext(commandsConfig);

            Commands.CommandExecuted += Commands_CommandExecuted;
            Commands.CommandErrored += Commands_CommandErrored;

            Commands.RegisterCommands<Interactuar>();
            Commands.RegisterCommands<NSFW>();
            Commands.RegisterCommands<Otros>();
            Commands.RegisterCommands<Help>();

            ApplicationCommands = Client.UseSlashCommands();

            ApplicationCommands.SlashCommandExecuted += SlashCommands_SlashCommandExecuted;
            ApplicationCommands.SlashCommandErrored += SlashCommands_SlashCommandErrored;

            ApplicationCommands.ContextMenuExecuted += SlashCommands_ContextMenuExecuted;
            ApplicationCommands.ContextMenuErrored += SlashCommands_ContextMenuErrored;

            if (Debug)
            {
                ulong idGuildTest = 713809173573271613;

                // Slash Commands
                ApplicationCommands.RegisterCommands<JuegosSlashCommands>(idGuildTest);
                ApplicationCommands.RegisterCommands<InteractuarSlashCommands>(idGuildTest);
                ApplicationCommands.RegisterCommands<AnilistSlashCommands>(idGuildTest);
                ApplicationCommands.RegisterCommands<OtrosSlashCommands>(idGuildTest);
                ApplicationCommands.RegisterCommands<HelpSlashCommands>(idGuildTest);

                // Context Menus
                ApplicationCommands.RegisterCommands<InteractuarConextMenus>(idGuildTest);
            }
            else
            {
                // Slash Commands
                ApplicationCommands.RegisterCommands<JuegosSlashCommands>();
                ApplicationCommands.RegisterCommands<InteractuarSlashCommands>();
                ApplicationCommands.RegisterCommands<AnilistSlashCommands>();
                ApplicationCommands.RegisterCommands<OtrosSlashCommands>();
                ApplicationCommands.RegisterCommands<HelpSlashCommands>();

                // Context Menus
                ApplicationCommands.RegisterCommands<InteractuarConextMenus>();
            }

            Commands.RegisterConverter(new MemberConverter());

            await Client.ConnectAsync(new DiscordActivity { ActivityType = ActivityType.ListeningTo, Name = "/help" }, UserStatus.Online);

            var LogGuild = await Client.GetGuildAsync(713809173573271613);
            if (Debug)
            {
                LogChannelGeneral      = LogGuild.GetChannel(820711607796891658);
                LogChannelSlash        = LogGuild.GetChannel(866810782360928306);
                LogChannelContextMenus = LogGuild.GetChannel(875049729455693824);
                LogChannelServers      = LogGuild.GetChannel(840440818921897985);
                LogChannelErrores      = LogGuild.GetChannel(840440877565739008);
            }
            else
            {
                LogChannelGeneral      = LogGuild.GetChannel(781679685838569502);
                LogChannelSlash        = LogGuild.GetChannel(866810567644676126);
                LogChannelContextMenus = LogGuild.GetChannel(875049729585725450);
                LogChannelServers      = LogGuild.GetChannel(840437931847974932);
                LogChannelErrores      = LogGuild.GetChannel(840439731011452959);
            }

            await RotarEstado();
        }

        private async Task RotarEstado()
        {
            while (true)
            {
                await Task.Delay(30000);
                await Client.UpdateStatusAsync(new DiscordActivity { ActivityType = ActivityType.ListeningTo, Name = "yumiko.uwu.ai" }, UserStatus.Online);
                await Task.Delay(10000);
                await Client.UpdateStatusAsync(new DiscordActivity { ActivityType = ActivityType.ListeningTo, Name = "/help" }, UserStatus.Online);
            }
        }

        private Task OnClientReady(DiscordClient c, ReadyEventArgs e)
        {
            c.Logger.LogInformation("Client startup completed", DateTime.Now);
            return Task.CompletedTask;
        }

        private Task Client_Resumed(DiscordClient c, ReadyEventArgs e)
        {
            c.Logger.LogInformation("The client is ready to process events again", DateTime.Now);
            return Task.CompletedTask;
        }

        private Task Client_GuildCreated(DiscordClient c, GuildCreateEventArgs e)
        {
            e.Handled = true;
            _ = Task.Run(async () =>
            {
                await LogChannelServers.SendMessageAsync(embed: new DiscordEmbedBuilder()
                {
                    Author = new EmbedAuthor()
                    {
                        IconUrl = e.Guild.IconUrl,
                        Name = $"{e.Guild.Name}"
                    },
                    Title = "New guild",
                    Description =
                    $"   **Id**: {e.Guild.Id}\n" +
                    $"   **Members**: {e.Guild.MemberCount - 1}\n" +
                    $"   **Owner**: {e.Guild.Owner.Username}#{e.Guild.Owner.Discriminator}\n\n" +
                    $"   **Server count**: {c.Guilds.Count}",
                    Footer = new EmbedFooter()
                    {
                        Text = $"{DateTimeOffset.Now}"
                    },
                    Color = DiscordColor.Green
                });
                if (!Debug)
                    await funciones.UpdateStatsTopGG(c).ConfigureAwait(false);
            });
            return Task.CompletedTask;
        }

        private Task Client_GuildDeleted(DiscordClient c, GuildDeleteEventArgs e)
        {
            e.Handled = true;
            _ = Task.Run(async () =>
            {
                await LogChannelServers.SendMessageAsync(embed: new DiscordEmbedBuilder()
                {
                    Author = new EmbedAuthor()
                    {
                        IconUrl = e.Guild.IconUrl,
                        Name = $"{e.Guild.Name}"
                    },
                    Title = "Bye-bye guild",
                    Description =
                    $"   **Id**: {e.Guild.Id}\n" +
                    $"   **Members**: {e.Guild.MemberCount - 1}\n\n" +
                    $"   **Server count**: {c.Guilds.Count}",
                    Footer = new EmbedFooter()
                    {
                        Text = $"{DateTimeOffset.Now}"
                    },
                    Color = DiscordColor.Red
                });
                if (!Debug)
                    await funciones.UpdateStatsTopGG(c).ConfigureAwait(false);
            });
            return Task.CompletedTask;
        }

        private Task Client_ClientError(DiscordClient c, ClientErrorEventArgs e)
        {
            e.Handled = true;
            _ = Task.Run(async () =>
            {
                if (e.Exception.Message != "An event handler caused the invocation of an asynchronous event to time out." &&
                e.Exception.Message != "One or more errors occurred. (Unauthorized: 403)")
                {
                    await LogChannelErrores.SendMessageAsync(embed: new DiscordEmbedBuilder()
                    {
                        Title = "An exception has occurred ",
                        Description = $"```{e.Exception.StackTrace}```",
                        Footer = new EmbedFooter()
                        {
                            Text = $"{DateTimeOffset.Now}"
                        },
                        Color = DiscordColor.Red
                    }.AddField("Type", $"{e.Exception.GetType()}", false)
                    .AddField("Description", $"{e.Exception.Message}", false)
                    .AddField("Event", $"{e.EventName}", false)
                    );
                }
            });
            return Task.CompletedTask;
        }

        private Task SlashCommands_SlashCommandExecuted(SlashCommandsExtension sender, SlashCommandExecutedEventArgs e)
        {
            _ = Task.Run(async () =>
            {
                string options = string.Empty;
                var args = e.Context.Interaction.Data.Options;
                if(args != null)
                {
                    foreach (var arg in args)
                    {
                        options += $"`{arg.Name}: {arg.Value}` ";
                    }
                }
                await LogChannelSlash.SendMessageAsync(embed: new DiscordEmbedBuilder()
                {
                    Title = "Slash Command executed",
                    Footer = new EmbedFooter()
                    {
                        Text = $"{e.Context.User.Username}#{e.Context.User.Discriminator}",
                        IconUrl = e.Context.User.AvatarUrl
                    },
                    Author = new EmbedAuthor()
                    {
                        IconUrl = e.Context.Guild.IconUrl,
                        Name = $"{e.Context.Guild.Name}"
                    },
                    Color = DiscordColor.Green
                }.AddField("Guild Id", $"{e.Context.Guild.Id}", true)
                .AddField("Channel Id", $"{e.Context.Channel.Id}", true)
                .AddField("User Id", $"{e.Context.User.Id}", true)
                .AddField("Channel", $"#{e.Context.Channel.Name}", false)
                .AddField("Command", $"/{e.Context.CommandName} {options}", false)
                );
            });
            return Task.CompletedTask;
        }

        private Task SlashCommands_SlashCommandErrored(SlashCommandsExtension sender, SlashCommandErrorEventArgs e)
        {
            _ = Task.Run(async () =>
            {
                string options = string.Empty;
                var args = e.Context.Interaction.Data.Options;
                if (args != null)
                {
                    foreach (var arg in args)
                    {
                        options += $"`{arg.Name}: {arg.Value}` ";
                    }
                }
                await LogChannelErrores.SendMessageAsync(embed: new DiscordEmbedBuilder
                {
                    Title = "Unhandled error (Slash Commands)",
                    Description = $"{e.Exception.Message}\n```{e.Exception.StackTrace}```",
                    Color = DiscordColor.Red,
                    Footer = new EmbedFooter()
                    {
                        Text = $"{e.Context.User.Username}#{e.Context.User.Discriminator}",
                        IconUrl = e.Context.User.AvatarUrl
                    },
                    Author = new EmbedAuthor()
                    {
                        IconUrl = e.Context.Guild.IconUrl,
                        Name = $"{e.Context.Guild.Name}"
                    },
                }.AddField("Guild Id", $"{e.Context.Guild.Id}", true)
                .AddField("Channel Id", $"{e.Context.Channel.Id}", true)
                .AddField("User Id", $"{e.Context.User.Id}", true)
                .AddField("Channel", $"#{e.Context.Channel.Name}", false)
                .AddField("Command", $"/{e.Context.CommandName} {options}", false)
                );
            });
            return Task.CompletedTask;
        }

        private Task SlashCommands_ContextMenuExecuted(SlashCommandsExtension sender, ContextMenuExecutedEventArgs e)
        {
            _ = Task.Run(async () =>
            {
                await LogChannelContextMenus.SendMessageAsync(embed: new DiscordEmbedBuilder()
                {
                    Title = "Context Menu executed",
                    Footer = new EmbedFooter()
                    {
                        Text = $"{e.Context.User.Username}#{e.Context.User.Discriminator}",
                        IconUrl = e.Context.User.AvatarUrl
                    },
                    Author = new EmbedAuthor()
                    {
                        IconUrl = e.Context.Guild.IconUrl,
                        Name = $"{e.Context.Guild.Name}"
                    },
                    Color = DiscordColor.Green
                }.AddField("Guild Id", $"{e.Context.Guild.Id}", true)
                .AddField("Channel Id", $"{e.Context.Channel.Id}", true)
                .AddField("User Id", $"{e.Context.User.Id}", true)
                .AddField("Channel", $"#{e.Context.Channel.Name}", false)
                .AddField("Commands", $"/{e.Context.CommandName}", false)
                );
            });
            return Task.CompletedTask;
        }

        private Task SlashCommands_ContextMenuErrored(SlashCommandsExtension sender, ContextMenuErrorEventArgs e)
        {
            _ = Task.Run(async () =>
            {
                string options = string.Empty;
                var args = e.Context.Interaction.Data.Options;
                foreach (var arg in args)
                {
                    options += $"`{arg.Name}: {arg.Value}` ";
                }
                await LogChannelErrores.SendMessageAsync(embed: new DiscordEmbedBuilder
                {
                    Title = "Unhandled error (Context Menus)",
                    Description = $"{e.Exception.Message}\n```{e.Exception.StackTrace}```",
                    Color = DiscordColor.Red,
                    Footer = new EmbedFooter()
                    {
                        Text = $"{e.Context.User.Username}#{e.Context.User.Discriminator}",
                        IconUrl = e.Context.User.AvatarUrl
                    },
                    Author = new EmbedAuthor()
                    {
                        IconUrl = e.Context.Guild.IconUrl,
                        Name = $"{e.Context.Guild.Name}"
                    },
                }.AddField("Guild Id", $"{e.Context.Guild.Id}", true)
                .AddField("Channel Id", $"{e.Context.Channel.Id}", true)
                .AddField("User Id", $"{e.Context.User.Id}", true)
                .AddField("Channel", $"#{e.Context.Channel.Name}", false)
                .AddField("Command", $"/{e.Context.CommandName} {options}", false)
                );
            });
            return Task.CompletedTask;
        }

        private Task Commands_CommandExecuted(CommandsNextExtension cm, CommandExecutionEventArgs e)
        {
            e.Handled = true;
            _ = Task.Run(async () =>
            {
                await LogChannelGeneral.SendMessageAsync(embed: new DiscordEmbedBuilder()
                {
                    Title = "Command executed",
                    Footer = new EmbedFooter()
                    {
                        Text = $"{e.Context.User.Username}#{e.Context.User.Discriminator} - {e.Context.Message.Timestamp}",
                        IconUrl = e.Context.User.AvatarUrl
                    },
                    Author = new EmbedAuthor()
                    {
                        IconUrl = e.Context.Guild.IconUrl,
                        Name = $"{e.Context.Guild.Name}"
                    },
                    Color = DiscordColor.Green
                }.AddField("Guild Id", $"{e.Context.Guild.Id}", true)
                .AddField("Channel Id", $"{e.Context.Channel.Id}", true)
                .AddField("User Id", $"{e.Context.User.Id}", true)
                .AddField("Channel", $"#{e.Context.Channel.Name}", false)
                .AddField("Message", $"{e.Context.Message.Content}", false)
                );
                if (e.Context.Message != null && e.Command.Module.ModuleType.Name.ToLower() != "nsfw")
                    await funciones.BorrarMensaje(e.Context, e.Context.Message.Id).ConfigureAwait(false);
            });
            return Task.CompletedTask;
        }

        private Task Commands_CommandErrored(CommandsNextExtension cm, CommandErrorEventArgs e)
        {
            e.Handled = true;
            _ = Task.Run(async () =>
            {
                string web = ConfigurationManager.AppSettings["Web"] + "#commands";
                if (e.Exception.Message == "Specified command was not found.")
                {
                    var emoji = DiscordEmoji.FromName(e.Context.Client, ":no_entry:");
                    var embed = new DiscordEmbedBuilder
                    {
                        Title = "Command not found",
                        Description = $"You can see the available commands in my [website]({web}).",
                        Color = DiscordColor.Red,
                        Footer = new EmbedFooter()
                        {
                            Text = $"Executed by {e.Context.Member.DisplayName} ({e.Context.Member.Username}#{e.Context.Member.Discriminator})",
                            IconUrl = e.Context.Member.AvatarUrl
                        }
                    };
                    var mensajeErr = e.Context.Channel.SendMessageAsync(embed: embed);
                    if (e.Context.Message != null && mensajeErr != null)
                    {
                        await Task.Delay(7000);
                        await funciones.BorrarMensaje(e.Context, e.Context.Message.Id);
                        await funciones.BorrarMensaje(e.Context, mensajeErr.Result.Id);
                    }
                }
                else if (e.Exception.Message == "Could not find a suitable overload for the command.")
                {
                    var emoji = DiscordEmoji.FromName(e.Context.Client, ":warning:");
                    var embed = new DiscordEmbedBuilder
                    {
                        Title = "Misspelled command",
                        Description = $"You can see the available commands in my [website]({web}).",
                        Color = DiscordColor.Yellow,
                        Footer = new EmbedFooter()
                        {
                            Text = $"Executed by {e.Context.Member.DisplayName} ({e.Context.Member.Username}#{e.Context.Member.Discriminator}) | {e.Context.Prefix}{e.Command.Name}",
                            IconUrl = e.Context.Member.AvatarUrl
                        }
                    };
                    var mensajeErr = await e.Context.Channel.SendMessageAsync(embed: embed);
                    var commandDesc = await funciones.GetInfoComando(e.Context, e.Command);
                    if (e.Context.Message != null && mensajeErr != null && commandDesc != null)
                    {
                        await Task.Delay(7000);
                        await funciones.BorrarMensaje(e.Context, e.Context.Message.Id);
                        await funciones.BorrarMensaje(e.Context, mensajeErr.Id);
                        await funciones.BorrarMensaje(e.Context, commandDesc.Id);
                    }
                }
                else if (e.Exception.Message == "Unauthorized: 403")
                {
                    var emoji = DiscordEmoji.FromName(e.Context.Client, ":no_entry:");
                    var embed = new DiscordEmbedBuilder
                    {
                        Title = "Missing permissions",
                        Description = $"{emoji} I need extra permissions to be able to execute this command.",
                        Color = DiscordColor.Red
                    };
                    var mensajeErr = e.Context.Channel.SendMessageAsync(embed: embed);
                    if (e.Context.Message != null && mensajeErr != null)
                    {
                        await Task.Delay(3000);
                        await funciones.BorrarMensaje(e.Context, e.Context.Message.Id);
                        await funciones.BorrarMensaje(e.Context, mensajeErr.Result.Id);
                    }
                }
                else
                {
                    if (e.Exception is ChecksFailedException ex)
                    {
                        List<DiscordMessage> mensajes = new();
                        string titulo, descripcion;
                        foreach (CheckBaseAttribute check in ex.FailedChecks)
                        {
                            switch (check)
                            {
                                case RequireOwnerAttribute:
                                    titulo = "Access denied";
                                    descripcion = "Only the bot owner can execute this command.";
                                    break;
                                case RequireNsfwAttribute:
                                    titulo = "NSFW Required";
                                    descripcion = "This command can only be executed in a NSFW channel.";
                                    break;
                                case CooldownAttribute ca:
                                    TimeSpan cd = ca.GetRemainingCooldown(e.Context);
                                    titulo = "Cooldown";
                                    descripcion = $"You have to wait until `{cd.Hours} hours, {cd.Minutes} minutes and {cd.Seconds} seconds` to execute this command.";
                                    break;
                                case RequirePermissionsAttribute:
                                    titulo = "Insufficient permissions";
                                    descripcion = "Yumiko or you don't have enough permissions to execute this command.";
                                    break;
                                case RequireUserPermissionsAttribute up:
                                    //up.Permissions listar permisos necesarios
                                    titulo = "Insufficient permissions";
                                    descripcion = "You don't have enough permissions to execute this command.";
                                    break;
                                case RequireBotPermissionsAttribute:
                                    titulo = "Insufficient permissions";
                                    descripcion = "Yumiko doesn't have enough permissions to execute this command.";
                                    break;
                                default:
                                    titulo = "Unhandled error";
                                    descripcion = $"An unhandled error has ocurred.";
                                    await LogChannelErrores.SendMessageAsync(embed: new DiscordEmbedBuilder
                                    {
                                        Title = titulo,
                                        Description = $"Attribute: {check}",
                                        Footer = funciones.GetFooter(e.Context),
                                        Author = new EmbedAuthor()
                                        {
                                            IconUrl = e.Context.Guild.IconUrl,
                                            Name = $"{e.Context.Guild.Name}"
                                        },
                                        Color = DiscordColor.Yellow
                                    }.AddField("Guild Id", $"{e.Context.Guild.Id}", true)
                                    .AddField("Channel Id", $"{e.Context.Channel.Id}", true)
                                    .AddField("User Id", $"{e.Context.User.Id}", true)
                                    .AddField("Channel", $"#{e.Context.Channel.Name}", false)
                                    .AddField("Message", $"{e.Context.Message.Content}", false));
                                    break;
                            }
                            var miembro = e.Context.Member;
                            EmbedFooter footer = new()
                            {
                                Text = "Executed by " + miembro.DisplayName + " (" + miembro.Username + "#" + miembro.Discriminator + ")",
                                IconUrl = miembro.AvatarUrl
                            };
                            DiscordMessage msg = await e.Context.Channel.SendMessageAsync(embed: new DiscordEmbedBuilder
                            {
                                Title = titulo,
                                Description = descripcion,
                                Color = new DiscordColor(0xFF0000),
                                Footer = footer
                            });
                            mensajes.Add(msg);
                        }
                        await Task.Delay(5000);
                        if (e.Context.Message != null)
                            await funciones.BorrarMensaje(e.Context, e.Context.Message.Id);
                        foreach (DiscordMessage mensaje in mensajes)
                        {
                            await funciones.BorrarMensaje(e.Context, mensaje.Id);
                        }
                    }
                    else
                    {
                        var miembro = e.Context.Member;
                        EmbedFooter footer = new()
                        {
                            Text = "Executed by " + miembro.DisplayName + " (" + miembro.Username + "#" + miembro.Discriminator + ")",
                            IconUrl = miembro.AvatarUrl
                        };
                        var msg = await e.Context.Channel.SendMessageAsync(embed: new DiscordEmbedBuilder
                        {
                            Title = "Unhandled error",
                            Description = "An unhandled error has ocurred",
                            Color = new DiscordColor(0xFF0000),
                            Footer = footer,
                            Author = new EmbedAuthor()
                            {
                                IconUrl = e.Context.Guild.IconUrl,
                                Name = $"{e.Context.Guild.Name}"
                            },
                        });
                        await LogChannelErrores.SendMessageAsync(embed: new DiscordEmbedBuilder
                        {
                            Title = "Unknow error",
                            Description = $"{e.Exception.Message}```{e.Exception.StackTrace}```",
                            Color = DiscordColor.Red,
                            Footer = footer
                        }.AddField("Guild Id", $"{e.Context.Guild.Id}", true)
                        .AddField("Channel Id", $"{e.Context.Channel.Id}", true)
                        .AddField("User Id", $"{e.Context.User.Id}", true)
                        .AddField("Channel", $"#{e.Context.Channel.Name}", false)
                        .AddField("Message", $"{e.Context.Message.Content}", false));
                        await Task.Delay(3000);
                        if (e.Context.Message != null)
                            await funciones.BorrarMensaje(e.Context, e.Context.Message.Id);
                        await funciones.BorrarMensaje(e.Context, msg.Id);
                    }
                }
            });
            return Task.CompletedTask;
        }
    }
}
