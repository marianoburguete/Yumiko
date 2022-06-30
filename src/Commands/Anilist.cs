﻿namespace Yumiko.Commands
{
    using GraphQL;
    using GraphQL.Client.Abstractions.Utilities;
    using GraphQL.Client.Http;
    using GraphQL.Client.Serializer.Newtonsoft;
    using Microsoft.Extensions.Configuration;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using RestSharp;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Net;
    using System.Threading.Tasks;

    [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Not with D#+ Command classes")]
    [SlashCommandGroup("anilist", "Anilist queries")]
    public class Anilist : ApplicationCommandModule
    {
        public IConfigurationRoot Configuration { private get; set; } = null!;
        private readonly GraphQLHttpClient graphQlClient = new("https://graphql.anilist.co", new NewtonsoftJsonSerializer());

        public override Task<bool> BeforeSlashExecutionAsync(InteractionContext ctx)
        {
            Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo(ctx.Interaction.Locale!);
            return Task.FromResult(true);
        }

        [SlashCommand("setprofile", "Sets your AniList profile")]
        [NameLocalization(Localization.Spanish, "asignarperfil")]
        [DescriptionLocalization(Localization.Spanish, "Asigna tu perfil de AniList")]
        public async Task SetAnilist(InteractionContext ctx)
        {
            string anilistApplicationId = ConfigurationUtils.GetConfiguration<string>(Configuration, Configurations.AnilistApiClientId);

            await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder()
                .AsEphemeral(true)
                .AddEmbed(new DiscordEmbedBuilder
                {
                    Title = translations.setup_anilist_profile,
                    Description =
                        $"{translations.anilist_setprofile_instructions}:\n\n" +
                        $"{translations.anilist_setprofile_instructions_1}\n" +
                        $"{translations.anilist_setprofile_instructions_2}\n" +
                        $"{translations.anilist_setprofile_instructions_3}\n" +
                        $"{translations.anilist_setprofile_instructions_4}",
                    Color = Constants.YumikoColor
                })
                .AddComponents(
                    new DiscordLinkButtonComponent($"https://anilist.co/api/v2/oauth/authorize?client_id={anilistApplicationId}&response_type=token", translations.authorize),
                    new DiscordButtonComponent(ButtonStyle.Primary, $"modal-anilistprofileset-{ctx.User.Id}", translations.paste_code_here)
                )
            );

            DiscordMessage message = await ctx.GetOriginalResponseAsync();
            var interactivity = ctx.Client.GetInteractivity();
            var interactivityBtnResult = await interactivity.WaitForButtonAsync(message, TimeSpan.FromMinutes(5));

            if (!interactivityBtnResult.TimedOut)
            {
                var btnInteraction = interactivityBtnResult.Result.Interaction;
                string modalId = $"modal-{btnInteraction.Id}";

                var modal = new DiscordInteractionResponseBuilder()
                    .WithCustomId(modalId)
                    .WithTitle(translations.set_anilist_profile)
                    .AddComponents(new TextInputComponent(label: translations.code, placeholder: translations.paste_code_here, customId: "AniListToken"));

                await btnInteraction.CreateResponseAsync(InteractionResponseType.Modal, modal);

                var interactivityModalResult = await interactivity.WaitForModalAsync(modalId, TimeSpan.FromMinutes(5));

                if (!interactivityModalResult.TimedOut)
                {
                    var modalInteraction = interactivityModalResult.Result.Interaction;
                    string ALToken = interactivityModalResult.Result.Values.First().Value;

                    await modalInteraction.CreateResponseAsync(InteractionResponseType.DeferredChannelMessageWithSource);

                    GraphQLHttpClient graphQlCli = new("https://graphql.anilist.co", new NewtonsoftJsonSerializer());
                    graphQlCli.HttpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {ALToken}");

                    var request = new GraphQLRequest
                    {
                        Query =
                            "query {" +
                            "   Viewer {" +
                            "       id," +
                            "       name," +
                            "       siteUrl," +
                            "       avatar {" +
                            "           medium" +
                            "       }," +
                            "       bannerImage" +
                            "   }" +
                            "}"
                    };
                    try
                    {
                        var data = await graphQlCli.SendQueryAsync<dynamic>(request);
                        if (data != null)
                        {
                            if (data.Data != null)
                            {
                                int id = data.Data.Viewer.id;
                                string name = data.Data.Viewer.name;
                                string siteUrl = data.Data.Viewer.siteUrl;
                                string avatar = data.Data.Viewer.avatar.medium;
                                string banner = data.Data.Viewer.bannerImage;

                                var newProfileEmbed = new DiscordEmbedBuilder
                                {
                                    Color = DiscordColor.Green,
                                    Title = translations.new_profile_saved,
                                    Description = string.Format(translations.new_profile_saved_mention, ctx.User.Mention),
                                    Thumbnail = new()
                                    {
                                        Url = avatar
                                    },
                                    Author = new()
                                    {
                                        Url = siteUrl,
                                        Name = name,
                                        IconUrl = ctx.User.AvatarUrl
                                    }
                                };

                                if (!string.IsNullOrEmpty(banner))
                                {
                                    newProfileEmbed.WithImageUrl(banner);
                                }

                                await UsuariosAnilist.SetAnilistAsync(id, ctx.Member.Id);
                                await modalInteraction.CreateFollowupMessageAsync(new DiscordFollowupMessageBuilder().AsEphemeral(false).AddEmbed(embed: newProfileEmbed));
                                return;
                            }
                        }

                        await modalInteraction.CreateFollowupMessageAsync(new DiscordFollowupMessageBuilder().AsEphemeral(true).AddEmbed(new DiscordEmbedBuilder
                        {
                            Title = translations.error,
                            Description = translations.unknown_error,
                            Color = DiscordColor.Red
                        }));
                    }
                    catch (GraphQLHttpRequestException ex)
                    {
                        if (ex.Content != null)
                        {
                            dynamic data = JObject.Parse(ex.Content);
                            if (data.errors != null)
                            {
                                foreach (var error in data.errors)
                                {
                                    await modalInteraction.CreateFollowupMessageAsync(new DiscordFollowupMessageBuilder().AsEphemeral(true).AddEmbed(new DiscordEmbedBuilder
                                    {
                                        Title = translations.error,
                                        Description = error.message,
                                        Color = DiscordColor.Red
                                    }));
                                }
                                return;
                            }
                        }

                        await modalInteraction.CreateFollowupMessageAsync(new DiscordFollowupMessageBuilder().AsEphemeral(true).AddEmbed(new DiscordEmbedBuilder
                        {
                            Title = translations.error,
                            Description = translations.unknown_error,
                            Color = DiscordColor.Red
                        }));
                    }
                }
                else
                {
                    await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().AddEmbed(new DiscordEmbedBuilder
                    {
                        Title = translations.response_timed_out,
                        Color = DiscordColor.Red
                    }));
                }
            }
            else
            {
                await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().AddEmbed(new DiscordEmbedBuilder
                {
                    Title = translations.response_timed_out,
                    Color = DiscordColor.Red
                }));
            }
        }

        [SlashCommand("deleteprofile", "Deletes your AniList profile")]
        [NameLocalization(Localization.Spanish, "eliminarperfil")]
        [DescriptionLocalization(Localization.Spanish, "Elimina tu perfil de AniList")]
        public async Task DeleteAnilist(InteractionContext ctx)
        {
            await ctx.DeferAsync();
            var confirmar = await Common.GetYesNoInteractivityAsync(ctx, ConfigurationUtils.GetConfiguration<double>(Configuration, Configurations.TimeoutGeneral), ctx.Client.GetInteractivity(), translations.confirm_delete_profile, translations.action_cannont_be_undone);
            if (confirmar)
            {
                var borrado = await UsuariosAnilist.DeleteAnilistAsync(ctx.User.Id);
                if (borrado)
                {
                    await ctx.EditResponseAsync(new DiscordWebhookBuilder().AddEmbed(new DiscordEmbedBuilder
                    {
                        Title = translations.success,
                        Description = translations.anilist_profile_deleted_successfully,
                        Color = DiscordColor.Green,
                    }));
                }
                else
                {
                    await ctx.EditResponseAsync(new DiscordWebhookBuilder().AddEmbed(new DiscordEmbedBuilder
                    {
                        Title = translations.error,
                        Description = translations.anilist_profile_not_found,
                        Color = DiscordColor.Red,
                    }));
                }
            }
            else
            {
                await ctx.DeleteResponseAsync();
            }
        }

        [SlashCommand("profile", "Searchs for an AniList profile")]
        [NameLocalization(Localization.Spanish, "perfil")]
        [DescriptionLocalization(Localization.Spanish, "Busca un perfil de AniList")]
        public async Task Profile(InteractionContext ctx, [Option("Member", "Member whose Anilist profile you want to see")] DiscordUser user)
        {
            await ctx.DeferAsync();
            user ??= ctx.User;

            var userAnilistDb = await UsuariosAnilist.GetPerfilAsync(user.Id);
            if (userAnilistDb != null)
            {
                var anilistUser = await ProfileQuery.GetProfile(ctx, userAnilistDb.AnilistId);
                if (anilistUser != null)
                {
                    DiscordEmbedBuilder builder = AnilistUtils.GetProfileEmbed(ctx, anilistUser);
                    DiscordLinkButtonComponent profile = new($"{anilistUser.SiteUrl}", translations.profile, false, new DiscordComponentEmoji("👤"));
                    DiscordLinkButtonComponent animeList = new($"{anilistUser.SiteUrl}/animelist", translations.anime_list, false, new DiscordComponentEmoji("📺"));
                    DiscordLinkButtonComponent mangaList = new($"{anilistUser.SiteUrl}/mangalist", translations.manga_list, false, new DiscordComponentEmoji("📖"));

                    await ctx.EditResponseAsync(new DiscordWebhookBuilder().AddEmbed(builder).AddComponents(profile, animeList, mangaList));
                    return;
                }
            }

            await ctx.EditResponseAsync(new DiscordWebhookBuilder().AddEmbed(new DiscordEmbedBuilder
            {
                Color = DiscordColor.Red,
                Title = translations.anilist_profile_not_found,
                Description = $"{string.Format(translations.no_anilist_profile_vinculated, user.Mention)}.\n\n" +
                                $"{translations.to_vinculate_anilist_profile}: `/anilist setanilist`",
            }));
        }

        [SlashCommand("anime", "Searchs for an anime")]
        [DescriptionLocalization(Localization.Spanish, "Busca un anime")]
        public async Task Anime(InteractionContext ctx, [Option("Anime", "Anime to search")] string anime, [Option("User", "User's Anilist stats")] DiscordUser? usuario = null)
        {
            await ctx.DeferAsync();
            usuario ??= ctx.User;

            var media = await MediaQuery.GetMedia(ctx, ConfigurationUtils.GetConfiguration<double>(Configuration, Configurations.TimeoutGeneral), anime, MediaType.ANIME);
            if (media != null)
            {
                var builder = new DiscordWebhookBuilder();

                if (media.IsAdult && !ctx.Channel.IsNSFW)
                {
                    await ctx.EditResponseAsync(builder.AddEmbed(Constants.NsfwWarning));
                    return;
                }
                
                builder.AddEmbed(AnilistUtils.GetMediaEmbed(ctx, media, MediaType.ANIME));

                var userAnilistProfile = await UsuariosAnilist.GetPerfilAsync(usuario.Id);
                if (userAnilistProfile != null)
                {
                    var statsUser = await MediaUserQuery.GetMediaFromUser(ctx, userAnilistProfile.AnilistId, media.Id);
                    if(statsUser != null)
                    {
                        builder.AddEmbed(AnilistUtils.GetMediaUserStats(statsUser));
                    }
                }

                await ctx.EditResponseAsync(builder);
            }
        }

        [SlashCommand("manga", "Searchs for a manga")]
        [DescriptionLocalization(Localization.Spanish, "Busca un manga")]
        public async Task Manga(InteractionContext ctx, [Option("Manga", "Manga to search")] string manga, [Option("User", "User's Anilist stats")] DiscordUser? usuario = null)
        {
            await ctx.DeferAsync();
            usuario ??= ctx.User;

            var media = await MediaQuery.GetMedia(ctx, ConfigurationUtils.GetConfiguration<double>(Configuration, Configurations.TimeoutGeneral), manga, MediaType.MANGA);
            if (media != null)
            {
                var builder = new DiscordWebhookBuilder();

                if (media.IsAdult && !ctx.Channel.IsNSFW)
                {
                    await ctx.EditResponseAsync(builder.AddEmbed(Constants.NsfwWarning));
                    return;
                }

                builder.AddEmbed(AnilistUtils.GetMediaEmbed(ctx, media, MediaType.ANIME));

                var userAnilistProfile = await UsuariosAnilist.GetPerfilAsync(usuario.Id);
                if (userAnilistProfile != null)
                {
                    var statsUser = await MediaUserQuery.GetMediaFromUser(ctx, userAnilistProfile.AnilistId, media.Id);
                    if (statsUser != null)
                    {
                        builder.AddEmbed(AnilistUtils.GetMediaUserStats(statsUser));
                    }
                }

                await ctx.EditResponseAsync(builder);
            }
        }

        [SlashCommand("character", "Searchs for a Character")]
        [NameLocalization(Localization.Spanish, "personaje")]
        [DescriptionLocalization(Localization.Spanish, "Busca un personaje")]
        public async Task Character(InteractionContext ctx, [Option("Character", "character to search")] string personaje)
        {
            await ctx.DeferAsync();
            var request = new GraphQLRequest
            {
                Query =
                "query($nombre : String){" +
                "   Page(perPage:5){" +
                "       characters(search: $nombre){" +
                "           name{" +
                "               full" +
                "           }," +
                "           image{" +
                "               large" +
                "           }," +
                "           siteUrl," +
                "           description," +
                "           animes: media(type: ANIME){" +
                "               nodes{" +
                "                   title{" +
                "                       romaji" +
                "                   }," +
                "                   siteUrl" +
                "               }" +
                "           }" +
                "           mangas: media(type: MANGA){" +
                "               nodes{" +
                "                   title{" +
                "                       romaji" +
                "                   }," +
                "                   siteUrl" +
                "               }" +
                "           }" +
                "       }" +
                "   }" +
                "}",
                Variables = new
                {
                    nombre = personaje,
                },
            };
            try
            {
                GraphQLResponse<dynamic> data = await graphQlClient.SendQueryAsync<dynamic>(request);
                if (data != null && data.Data != null)
                {
                    var cont = 0;
                    List<TitleDescription> opc = new();
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                    foreach (var animeP in data.Data.Page.characters)
                    {
                        cont++;

                        string opcName = animeP.name.full;
                        string opcDesc;
                        Newtonsoft.Json.Linq.JArray animes = animeP.animes.nodes;
                        if (animes.Count > 0)
                        {
                            string opcAnime = animeP.animes.nodes[0].title.romaji;
                            opcDesc = opcAnime;
                        }
                        else
                        {
                            Newtonsoft.Json.Linq.JArray mangas = animeP.mangas.nodes;
                            if (mangas.Count > 0)
                            {
                                string opcManga = animeP.mangas.nodes[0].title.romaji;
                                opcDesc = opcManga;
                            }
                            else
                            {
                                opcDesc = translations.without_animes_or_mangas;
                            }
                        }

                        opc.Add(new TitleDescription
                        {
                            Title = opcName,
                            Description = opcDesc,
                        });
                    }
#pragma warning restore CS8602 // Dereference of a possibly null reference.

                    var elegido = await Common.GetElegidoAsync(ctx, ConfigurationUtils.GetConfiguration<double>(Configuration, Configurations.TimeoutGeneral), opc);
                    if (elegido > 0)
                    {
                        var datos = data.Data.Page.characters[elegido - 1];
                        string descripcion = datos.description;
                        descripcion = Common.LimpiarTexto(descripcion).NormalizeDescription();
                        if (descripcion == string.Empty)
                        {
                            descripcion = translations.without_description;
                        }

                        string nombre = datos.name.full;
                        string imagen = datos.image.large;
                        string urlAnilist = datos.siteUrl;
                        var animes = string.Empty;
                        foreach (var anime in datos.animes.nodes)
                        {
                            animes += $"[{anime.title.romaji}]({anime.siteUrl})\n";
                        }

                        var mangas = string.Empty;
                        foreach (var manga in datos.mangas.nodes)
                        {
                            mangas += $"[{manga.title.romaji}]({manga.siteUrl})\n";
                        }

                        var builder = new DiscordEmbedBuilder
                        {
                            Title = nombre,
                            Url = urlAnilist,
                            Thumbnail = new DiscordEmbedBuilder.EmbedThumbnail()
                            {
                                Url = imagen,
                            },
                            Color = Constants.YumikoColor,
                            Description = descripcion,
                        };
                        if (animes != null && animes.Length > 0)
                        {
                            builder.AddField($"{DiscordEmoji.FromName(ctx.Client, ":tv:")} Animes", animes.NormalizeField(), false);
                        }

                        if (mangas != null && mangas.Length > 0)
                        {
                            builder.AddField($"{DiscordEmoji.FromName(ctx.Client, ":book:")} Mangas", mangas.NormalizeField(), false);
                        }

                        await ctx.EditResponseAsync(new DiscordWebhookBuilder().AddEmbed(builder));
                    }
                    else
                    {
                        await ctx.DeleteResponseAsync();
                    }
                }
                else
                {
                    if (data?.Errors == null)
                    {
                        await ctx.EditResponseAsync(new DiscordWebhookBuilder().AddEmbed(new DiscordEmbedBuilder
                        {
                            Color = DiscordColor.Red,
                            Title = translations.error,
                            Description = $"{translations.character_not_found}: `{personaje}`",
                        }));
                    }
                    else
                    {
                        foreach (var x in data.Errors)
                        {
                            var msg = await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().WithContent($"Error: {x.Message}"));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                var mensaje = ex.Message switch
                {
                    "The HTTP request failed with status code NotFound" => $"{translations.character_not_found}: `{personaje}`",
                    _ => $"{translations.unknown_error}, {translations.message}: {ex.Message}"
                };
                await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent(mensaje));
            }
        }

        // Staff, algun dia
        [SlashCommand("sauce", "Searchs for the anime of an image")]
        [DescriptionLocalization(Localization.Spanish, "Busca el anime de una imágen")]
        public async Task Sauce(InteractionContext ctx, [Option("Image", "Image link")] string url)
        {
            await ctx.DeferAsync();
            var msg = "OK";
            if (Uri.IsWellFormedUriString(url, UriKind.Absolute))
            {
                var extension = url[^4..];
                if (extension == ".jpg" || extension == ".png" || extension == "jpeg")
                {
                    var client = new RestClient("https://api.trace.moe/search?url=" + url);
                    var request = new RestRequest();
                    request.AddHeader("content-type", "application/json");
                    await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().WithContent($"{translations.processing_image}.."));
                    var response = await client.ExecuteAsync(request);
                    if (response.IsSuccessful)
                    {
                        if (response.Content != null)
                        {
                            var resp = JsonConvert.DeserializeObject<dynamic>(response.Content);
                            var resultados = string.Empty;
                            var titulo = $"{translations.the_possible_anime_is}:";
                            var encontro = false;
                            if (resp != null)
                            {
                                foreach (var resultado in resp.result)
                                {
                                    var enlace = "https://anilist.co/anime/";
                                    int similaridad = resultado.similarity * 100;
                                    if (similaridad >= 87)
                                    {
                                        encontro = true;
                                        int id = resultado.anilist;
                                        var media = await AnilistServices.GetAniListMediaTitleAndNsfwFromId(ctx, id, MediaType.ANIME);
                                        string mediaTitle = media.Item1;
                                        bool nsfw = media.Item2;
                                        int from = resultado.from;
                                        string videoLink = resultado.video;
                                        if (!ctx.Channel.IsNSFW && nsfw)
                                        {
                                            msg = $"{translations.image_from_nsfw_anime}, {translations.use_command_in_nsfw_channel}";
                                        }
                                        resultados =
                                            $"{Formatter.Bold($"{translations.name}:")} [{mediaTitle}]({enlace += id})\n" +
                                            $"{Formatter.Bold($"{translations.similarity}:")} {similaridad}%\n" +
                                            $"{Formatter.Bold($"{translations.episode}:")} {resultado.episode} ({translations.minute}: {TimeSpan.FromSeconds(from):mm\\:ss}\n" +
                                            $"{Formatter.Bold($"{translations.video}:")} [{translations.link}]({videoLink})";
                                        break;
                                    }
                                }

                                if (!encontro)
                                {
                                    titulo = translations.no_results_found_image;
                                    resultados = translations.sauce_remember;
                                }

                                var embed = new DiscordEmbedBuilder
                                {
                                    Title = titulo,
                                    Description = resultados,
                                    ImageUrl = url,
                                    Color = Constants.YumikoColor,
                                };
                                embed.WithFooter($"{translations.retrieved_from} trace.moe", "https://trace.moe/favicon.png");
                                if (msg == "OK")
                                {
                                    await ctx.EditResponseAsync(new DiscordWebhookBuilder().AddEmbed(embed));
                                    return;
                                }
                            }
                        }
                        else
                        {
                            msg = translations.unknown_error;
                        }
                    }
                    else
                    {
                        msg = response.StatusCode switch
                        {
                            HttpStatusCode.BadRequest => "Invalid image url",
                            HttpStatusCode.PaymentRequired => "Search quota depleted / Concurrency limit exceeded",
                            HttpStatusCode.Forbidden => "	Invalid API key",
                            HttpStatusCode.MethodNotAllowed => "Method Not Allowed",
                            HttpStatusCode.InternalServerError => "Internal Server Error",
                            HttpStatusCode.ServiceUnavailable => "Search queue is full / Database is not responding",
                            HttpStatusCode.GatewayTimeout => "Server is overloaded",
                            _ => "Unknown error",
                        };
                        await Common.GrabarLogErrorAsync(ctx, "Error retriving image from trace.moe with `sauce` command.\nError: " + msg);
                        await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent(translations.unknown_error_tracemoe));
                        return;
                    }
                }
                else
                {
                    msg = translations.image_format_error;
                }
            }
            else
            {
                msg = translations.image_must_enter_link;
            }

            await ctx.EditResponseAsync(new DiscordWebhookBuilder().AddEmbed(new DiscordEmbedBuilder()
            {
                Title = translations.error,
                Description = msg,
                Color = DiscordColor.Red,
            }));
        }

        [SlashCommand("pj", "Random character")]
        [DescriptionLocalization(Localization.Spanish, "Personaje aleatorio")]
        public async Task Pj(InteractionContext ctx)
        {
            await ctx.DeferAsync();
            var pag = Common.GetRandomNumber(1, 10000);
            var personaje = await Common.GetRandomCharacterAsync(ctx, pag);
            if (personaje != null)
            {
                var corazon = DiscordEmoji.FromName(ctx.Client, ":heart:");
                var builder = new DiscordEmbedBuilder
                {
                    Title = personaje.NameFull,
                    Url = personaje.SiteUrl,
                    ImageUrl = personaje.Image,
                    Description = $"[{personaje.AnimePrincipal?.TitleRomaji}]({personaje.AnimePrincipal?.SiteUrl})\n{personaje.Favoritos} {corazon} (nº {pag} {translations.in_popularity_rank})",
                    Color = Constants.YumikoColor
                };
                await ctx.EditResponseAsync(new DiscordWebhookBuilder().AddEmbed(builder));
            }
        }

        [SlashCommand("recommendations", "Auto recommendations based on your list")]
        [NameLocalization(Localization.Spanish, "recomendaciones")]
        [DescriptionLocalization(Localization.Spanish, "Recomendaciones automáticas basada en tu lista")]
        public async Task AutoRecomendation(
            InteractionContext ctx,
            [Option("Type", "The type of media")] MediaType type,
            [Option("User", "The user's recommendation to retrieve")] DiscordUser? user = null)
        {
            await ctx.DeferAsync();
            user ??= ctx.User;
            var userAnilist = await UsuariosAnilist.GetPerfilAsync(user.Id);
            if (userAnilist != null)
            {
                var recommendationsEmbed = await AnilistServices.GetUserRecommendationsAsync(ctx, user, type, userAnilist.AnilistId);
                if (recommendationsEmbed != null)
                {
                    await ctx.EditResponseAsync(new DiscordWebhookBuilder().AddEmbed(recommendationsEmbed));
                }
                else
                {
                    await ctx.EditResponseAsync(new DiscordWebhookBuilder().AddEmbed(new DiscordEmbedBuilder
                    {
                        Title = translations.error,
                        Description = translations.unknown_error,
                        Color = DiscordColor.Red
                    }));
                }
            }
            else
            {
                await ctx.EditResponseAsync(new DiscordWebhookBuilder().AddEmbed(new DiscordEmbedBuilder
                {
                    Title = translations.error,
                    Description = translations.anilist_profile_not_found,
                    Color = DiscordColor.Red
                }));
            }
        }
    }
}
