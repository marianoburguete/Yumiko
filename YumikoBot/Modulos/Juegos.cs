﻿using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using System.Threading.Tasks;
using System.Collections.Generic;
using DSharpPlus.Entities;
using System;
using GraphQL.Client.Http;
using GraphQL;
using GraphQL.Client.Serializer.Newtonsoft;
using System.Linq;
using System.Configuration;
using DSharpPlus.Interactivity.Extensions;
using YumikoBot.Data_Access_Layer;

namespace Discord_Bot.Modulos
{
    public class Juegos : BaseCommandModule
    {
        private readonly FuncionesAuxiliares funciones = new FuncionesAuxiliares();
        private readonly GraphQLHttpClient graphQLClient = new GraphQLHttpClient("https://graphql.anilist.co", new NewtonsoftJsonSerializer());

        [Command("quizC"), Aliases("adivinaelpersonaje"), Description("Empieza el juego de adivina el personaje."), RequireGuild]
        public async Task QuizCharactersGlobal(CommandContext ctx)
        {
            var interactivity = ctx.Client.GetInteractivity();
            SettingsJuego settings = await funciones.InicializarJuego(ctx, interactivity);
            if (settings.Ok)
            {
                int rondas = settings.Rondas;
                string dificultadStr = settings.Dificultad;
                int iterIni = settings.IterIni;
                int iterFin = settings.IterFin;
                DiscordEmbed embebido = new DiscordEmbedBuilder
                {
                    Title = "Adivina el personaje",
                    Description = $"Sesión iniciada por {ctx.User.Mention}\n\nPuedes escribir `cancelar` en cualquiera de las rondas para terminar la partida.",
                    Color = funciones.GetColor()
                }.AddField("Rondas", $"{rondas}").AddField("Dificultad", $"{dificultadStr}");
                await ctx.RespondAsync(embed: embebido).ConfigureAwait(false);
                List<Character> characterList = new List<Character>();
                Random rnd = new Random();
                List<UsuarioJuego> participantes = new List<UsuarioJuego>();
                DiscordMessage mensaje = await ctx.RespondAsync($"Obteniendo personajes...").ConfigureAwait(false);
                string query = "query($pagina : Int){" +
                        "   Page(page: $pagina){" +
                        "       characters(sort:";
                query += settings.Orden;
                query +="){" +
                        "           siteUrl," +
                        "           favourites," +
                        "           name{" +
                        "               first," +
                        "               last," +
                        "               full" +
                        "           }," +
                        "           image{" +
                        "               large" +
                        "           }" +
                        "       }" +
                        "   }" +
                        "}";
                for (int i = iterIni; i <= iterFin; i++)
                {
                    var request = new GraphQLRequest
                    {
                        Query = query,
                        Variables = new
                        {
                            pagina = i
                        }
                    };
                    try
                    {
                        var data = await graphQLClient.SendQueryAsync<dynamic>(request);
                        foreach (var x in data.Data.Page.characters)
                        {
                            characterList.Add(new Character()
                            {
                                Image = x.image.large,
                                NameFull = x.name.full,
                                NameFirst = x.name.first,
                                NameLast = x.name.last,
                                SiteUrl = x.siteUrl,
                                Favoritos = x.favourites
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        DiscordMessage msg;
                        switch (ex.Message)
                        {
                            default:
                                msg = await ctx.RespondAsync($"Error inesperado: {ex.Message}").ConfigureAwait(false);
                                break;
                        }
                        await Task.Delay(3000);
                        await msg.DeleteAsync("Auto borrado de Yumiko");
                        return;
                    }
                }
                await mensaje.DeleteAsync("Auto borrado de Yumiko");
                int lastRonda;
                for (int ronda = 1; ronda <= rondas; ronda++)
                {
                    lastRonda = ronda;
                    int random = funciones.GetNumeroRandom(0, characterList.Count - 1);
                    Character elegido = characterList[random];
                    DiscordEmoji corazon = DiscordEmoji.FromName(ctx.Client, ":heart:");
                    await ctx.RespondAsync(embed: new DiscordEmbedBuilder
                    {
                        Color = DiscordColor.Gold,
                        Title = "Adivina el personaje",
                        Description = $"Ronda {ronda} de {rondas}",
                        ImageUrl = elegido.Image,
                        Footer = new DiscordEmbedBuilder.EmbedFooter
                        {
                            Text = $"{elegido.Favoritos} {corazon}"
                        }
                    }).ConfigureAwait(false);
                    var msg = await interactivity.WaitForMessageAsync
                        (xm => (xm.Channel == ctx.Channel) &&
                        ((xm.Content.ToLower().Trim() == elegido.NameFull.ToLower().Trim() || xm.Content.ToLower().Trim() == elegido.NameFirst.ToLower().Trim() || (elegido.NameLast != null && xm.Content.ToLower().Trim() == elegido.NameLast.ToLower().Trim())) && xm.Author.Id != ctx.Client.CurrentUser.Id) || (xm.Content.ToLower() == "cancelar" && xm.Author == ctx.User)
                        , TimeSpan.FromSeconds(Convert.ToDouble(ConfigurationManager.AppSettings["GuessTimeGames"])));
                    if (!msg.TimedOut)
                    {
                        if (msg.Result.Author == ctx.User && msg.Result.Content.ToLower() == "cancelar")
                        {
                            await ctx.RespondAsync(embed: new DiscordEmbedBuilder
                            {
                                Title = "¡Juego cancelado!",
                                Description = $"El nombre era: [{elegido.NameFull}]({elegido.SiteUrl})",
                                Color = DiscordColor.Red
                            }).ConfigureAwait(false);
                            await funciones.GetResultados(ctx, participantes, lastRonda, settings.Dificultad, "personaje");
                            await ctx.RespondAsync($"El juego ha sido **cancelado** por **{ctx.User.Username}#{ctx.User.Discriminator}**").ConfigureAwait(false);
                            return;
                        }
                        DiscordMember acertador = await ctx.Guild.GetMemberAsync(msg.Result.Author.Id);
                        UsuarioJuego usr = participantes.Find(x => x.Usuario == msg.Result.Author);
                        if (usr != null)
                        {
                            usr.Puntaje++;
                        }
                        else
                        {
                            participantes.Add(new UsuarioJuego()
                            {
                                Usuario = msg.Result.Author,
                                Puntaje = 1
                            });
                        }
                        await ctx.RespondAsync(embed: new DiscordEmbedBuilder
                        {
                            Title = $"¡**{acertador.DisplayName}** ha acertado!",
                            Description = $"El nombre es: [{elegido.NameFull}]({elegido.SiteUrl})",
                            Color = DiscordColor.Green
                        }).ConfigureAwait(false);
                    }
                    else
                    {
                        await ctx.RespondAsync(embed: new DiscordEmbedBuilder
                        {
                            Title = "¡Nadie ha acertado!",
                            Description = $"El nombre era: [{elegido.NameFull}]({elegido.SiteUrl})",
                            Color = DiscordColor.Red
                        }).ConfigureAwait(false);
                    }
                    characterList.Remove(characterList[random]);
                }
                await funciones.GetResultados(ctx, participantes, rondas, settings.Dificultad, "personaje");
            }
            else
            {
                var error = await ctx.RespondAsync(settings.MsgError).ConfigureAwait(false);
            }
        }

        [Command("quizA"), Aliases("adivinaelanime"), Description("Empieza el juego de adivina el anime."), RequireGuild]
        public async Task QuizAnimeGlobal(CommandContext ctx)
        {
            var interactivity = ctx.Client.GetInteractivity();
            SettingsJuego settings = await funciones.InicializarJuego(ctx, interactivity);
            if (settings.Ok)
            {
                int rondas = settings.Rondas;
                string dificultadStr = settings.Dificultad;
                int iterIni = settings.IterIni;
                int iterFin = settings.IterFin;
                DiscordEmbed embebido = new DiscordEmbedBuilder
                {
                    Title = "Adivina el anime",
                    Description = $"Sesión iniciada por {ctx.User.Mention}\n\nPuedes escribir `cancelar` en cualquiera de las rondas para terminar la partida.",
                    Color = funciones.GetColor()
                }.AddField("Rondas", $"{rondas}").AddField("Dificultad", $"{dificultadStr}");
                await ctx.RespondAsync(embed: embebido).ConfigureAwait(false);
                Random rnd = new Random();
                List<UsuarioJuego> participantes = new List<UsuarioJuego>();
                DiscordMessage mensaje = await ctx.RespondAsync($"Obteniendo personajes...").ConfigureAwait(false);
                var characterList = new List<Character>();
                string query = "query($pagina : Int){" +
                        "   Page(page: $pagina){" +
                        "       characters(sort: ";
                query += settings.Orden;
                query += "){" +
                        "           siteUrl," +
                        "           name{" +
                        "               full" +
                        "           }," +
                        "           image{" +
                        "               large" +
                        "           }," +
                        "           favourites," +
                        "           media(type:ANIME){" +
                        "               nodes{" +
                        "                   title{" +
                        "                       romaji," +
                        "                       english" +
                        "                   }," +
                        "                   siteUrl," +
                        "                   synonyms" +
                        "               }" +
                        "           }" +
                        "       }" +
                        "   }" +
                        "}";
                for (int i = iterIni; i <= iterFin; i++)
                {
                    var request = new GraphQLRequest
                    {
                        Query = query,
                        Variables = new
                        {
                            pagina = i
                        }
                    };
                    try
                    {
                        var data = await graphQLClient.SendQueryAsync<dynamic>(request);
                        foreach (var x in data.Data.Page.characters)
                        {
                            Character c = new Character()
                            {
                                Image = x.image.large,
                                NameFull = x.name.full,
                                SiteUrl = x.siteUrl,
                                Favoritos = x.favourites,
                                Animes = new List<Anime>()
                            };
                            foreach (var y in x.media.nodes)
                            {
                                string titleEnglish = y.title.english;
                                string titleRomaji = y.title.romaji;
                                Anime anim = new Anime()
                                {
                                    TitleEnglish = funciones.QuitarCaracteresEspeciales(titleEnglish),
                                    TitleRomaji = funciones.QuitarCaracteresEspeciales(titleRomaji),
                                    SiteUrl = y.siteUrl,
                                    Sinonimos = new List<string>()
                                };
                                foreach (var syn in y.synonyms)
                                {
                                    string value = syn.Value;
                                    string bien = funciones.QuitarCaracteresEspeciales(value);
                                    anim.Sinonimos.Add(bien);
                                }
                                c.Animes.Add(anim);
                            }
                            if (c.Animes.Count() > 0)
                            {
                                characterList.Add(c);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        DiscordMessage msg;
                        switch (ex.Message)
                        {
                            default:
                                msg = await ctx.RespondAsync($"Error inesperado: {ex.Message}").ConfigureAwait(false);
                                break;
                        }
                        await Task.Delay(3000);
                        await msg.DeleteAsync("Auto borrado de Yumiko");
                        return;
                    }
                }
                await mensaje.DeleteAsync("Auto borrado de Yumiko");
                int lastRonda;
                for (int ronda = 1; ronda <= rondas; ronda++)
                {
                    lastRonda = ronda;
                    int random = funciones.GetNumeroRandom(0, characterList.Count - 1);
                    Character elegido = characterList[random];
                    DiscordEmoji corazon = DiscordEmoji.FromName(ctx.Client, ":heart:");
                    await ctx.RespondAsync(embed: new DiscordEmbedBuilder
                    {
                        Color = DiscordColor.Gold,
                        Title = $"Adivina el anime del personaje",
                        Description = $"Ronda {ronda} de {rondas}",
                        ImageUrl = elegido.Image,
                        Footer = new DiscordEmbedBuilder.EmbedFooter
                        {
                            Text = $"{elegido.Favoritos} {corazon}"
                        }
                    }).ConfigureAwait(false);
                    var msg = await interactivity.WaitForMessageAsync
                        (xm => (xm.Channel == ctx.Channel) &&
                        ((xm.Content.ToLower() == "cancelar" && xm.Author == ctx.User) || (
                        (elegido.Animes.Find(x => x.TitleEnglish != null && x.TitleEnglish.ToLower().Trim() == xm.Content.ToLower().Trim()) != null) ||
                        (elegido.Animes.Find(x => x.TitleRomaji != null && x.TitleRomaji.ToLower().Trim() == xm.Content.ToLower().Trim()) != null) ||
                        (elegido.Animes.Find(x => x.Sinonimos.Find(y => y.ToLower().Trim() == xm.Content.ToLower().Trim()) != null) != null)
                        ) && xm.Author.Id != ctx.Client.CurrentUser.Id),
                        TimeSpan.FromSeconds(Convert.ToDouble(ConfigurationManager.AppSettings["GuessTimeGames"])));
                    string descAnimes = $"Los animes de [{elegido.NameFull}]({elegido.SiteUrl}) son:\n\n";
                    foreach (Anime anim in elegido.Animes)
                    {
                        descAnimes += $"- [{anim.TitleRomaji}]({anim.SiteUrl})\n";
                    }
                    descAnimes = funciones.NormalizarDescription(descAnimes);
                    if (!msg.TimedOut)
                    {
                        if (msg.Result.Author == ctx.User && msg.Result.Content.ToLower() == "cancelar")
                        {
                            await ctx.RespondAsync(embed: new DiscordEmbedBuilder
                            {
                                Title = "¡Juego cancelado!",
                                Description = descAnimes,
                                Color = DiscordColor.Red
                            }).ConfigureAwait(false);
                            await funciones.GetResultados(ctx, participantes, lastRonda, settings.Dificultad, "anime");
                            await ctx.RespondAsync($"El juego ha sido cancelado por **{ctx.User.Username}#{ctx.User.Discriminator}**").ConfigureAwait(false);
                            return;
                        }
                        DiscordMember acertador = await ctx.Guild.GetMemberAsync(msg.Result.Author.Id);
                        UsuarioJuego usr = participantes.Find(x => x.Usuario == msg.Result.Author);
                        if (usr != null)
                        {
                            usr.Puntaje++;
                        }
                        else
                        {
                            participantes.Add(new UsuarioJuego()
                            {
                                Usuario = msg.Result.Author,
                                Puntaje = 1
                            });
                        }
                        await ctx.RespondAsync(embed: new DiscordEmbedBuilder
                        {
                            Title = $"¡**{acertador.DisplayName}** ha acertado!",
                            Description = descAnimes,
                            Color = DiscordColor.Green
                        }).ConfigureAwait(false);
                    }
                    else
                    {
                        await ctx.RespondAsync(embed: new DiscordEmbedBuilder
                        {
                            Title = "¡Nadie ha acertado!",
                            Description = descAnimes,
                            Color = DiscordColor.Red
                        }).ConfigureAwait(false);
                    }
                    characterList.Remove(characterList[random]);
                }
                await funciones.GetResultados(ctx, participantes, rondas, settings.Dificultad, "anime");
            }
            else
            {
                var error = await ctx.RespondAsync(settings.MsgError).ConfigureAwait(false);
            }
        }

        [Command("quizM"), Aliases("adivinaelmanga"), Description("Empieza el juego de adivina el manga."), RequireGuild]
        public async Task QuizMangaGlobal(CommandContext ctx)
        {
            var interactivity = ctx.Client.GetInteractivity();
            SettingsJuego settings = await funciones.InicializarJuego(ctx, interactivity);
            if (settings.Ok)
            {
                int rondas = settings.Rondas;
                string dificultadStr = settings.Dificultad;
                int iterIni = settings.IterIni;
                int iterFin = settings.IterFin;
                DiscordEmbed embebido = new DiscordEmbedBuilder
                {
                    Title = "Adivina el manga",
                    Description = $"Sesión iniciada por {ctx.User.Mention}\n\nPuedes escribir `cancelar` en cualquiera de las rondas para terminar la partida.",
                    Color = funciones.GetColor()
                }.AddField("Rondas", $"{rondas}").AddField("Dificultad", $"{dificultadStr}");
                await ctx.RespondAsync(embed: embebido).ConfigureAwait(false);
                List<Anime> animeList = new List<Anime>();
                Random rnd = new Random();
                List<UsuarioJuego> participantes = new List<UsuarioJuego>();
                DiscordMessage mensaje = await ctx.RespondAsync($"Obteniendo mangas...").ConfigureAwait(false);
                string query = "query($pagina : Int){" +
                        "   Page(page: $pagina){" +
                        "       media(type: MANGA, sort: ";
                query += settings.Orden;
                query += "){" +
                        "           siteUrl," +
                        "           favourites," +
                        "           title{" +
                        "               romaji," +
                        "               english" +
                        "           }," +
                        "           coverImage{" +
                        "               large" +
                        "           }," +
                        "           synonyms" +
                        "       }" +
                        "   }" +
                        "}";
                for (int i = iterIni; i <= iterFin; i++)
                {
                    var request = new GraphQLRequest
                    {
                        Query = query,
                        Variables = new
                        {
                            pagina = i
                        }
                    };
                    try
                    {
                        var data = await graphQLClient.SendQueryAsync<dynamic>(request);
                        foreach (var x in data.Data.Page.media)
                        {
                            string titleEnglish = x.title.english;
                            string titleRomaji = x.title.romaji;
                            Anime anim = new Anime()
                            {
                                Image = x.coverImage.large,
                                TitleEnglish = funciones.QuitarCaracteresEspeciales(titleEnglish),
                                TitleRomaji = funciones.QuitarCaracteresEspeciales(titleRomaji),
                                SiteUrl = x.siteUrl,
                                Favoritos = x.favourites,
                                Sinonimos = new List<string>()
                            };
                            foreach (var syn in x.synonyms)
                            {
                                string value = syn.Value;
                                string bien = funciones.QuitarCaracteresEspeciales(value);
                                anim.Sinonimos.Add(bien);
                            }
                            animeList.Add(anim);
                        }
                    }
                    catch (Exception ex)
                    {
                        DiscordMessage msg;
                        switch (ex.Message)
                        {
                            default:
                                msg = await ctx.RespondAsync($"Error inesperado: {ex.Message}").ConfigureAwait(false);
                                break;
                        }
                        await Task.Delay(3000);
                        await msg.DeleteAsync("Auto borrado de Yumiko");
                        return;
                    }
                }
                await mensaje.DeleteAsync("Auto borrado de Yumiko");
                int lastRonda;
                for (int ronda = 1; ronda <= rondas; ronda++)
                {
                    lastRonda = ronda;
                    int random = funciones.GetNumeroRandom(0, animeList.Count - 1);
                    Anime elegido = animeList[random];
                    DiscordEmoji corazon = DiscordEmoji.FromName(ctx.Client, ":heart:");
                    await ctx.RespondAsync(embed: new DiscordEmbedBuilder
                    {
                        Color = DiscordColor.Gold,
                        Title = "Adivina el manga",
                        Description = $"Ronda {ronda} de {rondas}",
                        ImageUrl = elegido.Image,
                        Footer = new DiscordEmbedBuilder.EmbedFooter
                        {
                            Text = $"{elegido.Favoritos} {corazon}"
                        }
                    }).ConfigureAwait(false);
                    var msg = await interactivity.WaitForMessageAsync
                        (xm => (xm.Channel == ctx.Channel) &&
                        (elegido.TitleRomaji != null && (xm.Content.ToLower().Trim() == elegido.TitleRomaji.ToLower().Trim()) || elegido.TitleEnglish != null && (xm.Content.ToLower().Trim() == elegido.TitleEnglish.ToLower().Trim()) ||
                        (elegido.Sinonimos.Find(y => y.ToLower().Trim() == xm.Content.ToLower().Trim()) != null)
                        ) && xm.Author.Id != ctx.Client.CurrentUser.Id || (xm.Content.ToLower() == "cancelar" && xm.Author == ctx.User)
                        , TimeSpan.FromSeconds(Convert.ToDouble(ConfigurationManager.AppSettings["GuessTimeGames"])));
                    if (!msg.TimedOut)
                    {
                        if (msg.Result.Author == ctx.User && msg.Result.Content.ToLower() == "cancelar")
                        {
                            await ctx.RespondAsync(embed: new DiscordEmbedBuilder
                            {
                                Title = "¡Juego cancelado!",
                                Description = $"El nombre era: [{elegido.TitleRomaji}]({elegido.SiteUrl})",
                                Color = DiscordColor.Red
                            }).ConfigureAwait(false);
                            await funciones.GetResultados(ctx, participantes, lastRonda, settings.Dificultad, "manga");
                            await ctx.RespondAsync($"El juego ha sido **cancelado** por **{ctx.User.Username}#{ctx.User.Discriminator}**").ConfigureAwait(false);
                            return;
                        }
                        DiscordMember acertador = await ctx.Guild.GetMemberAsync(msg.Result.Author.Id);
                        UsuarioJuego usr = participantes.Find(x => x.Usuario == msg.Result.Author);
                        if (usr != null)
                        {
                            usr.Puntaje++;
                        }
                        else
                        {
                            participantes.Add(new UsuarioJuego()
                            {
                                Usuario = msg.Result.Author,
                                Puntaje = 1
                            });
                        }
                        await ctx.RespondAsync(embed: new DiscordEmbedBuilder
                        {
                            Title = $"¡**{acertador.DisplayName}** ha acertado!",
                            Description = $"El nombre es: [{elegido.TitleRomaji}]({elegido.SiteUrl})",
                            Color = DiscordColor.Green
                        }).ConfigureAwait(false);
                    }
                    else
                    {
                        await ctx.RespondAsync(embed: new DiscordEmbedBuilder
                        {
                            Title = "¡Nadie ha acertado!",
                            Description = $"El nombre era: [{elegido.TitleRomaji}]({elegido.SiteUrl})",
                            Color = DiscordColor.Red
                        }).ConfigureAwait(false);
                    }
                    animeList.Remove(animeList[random]);
                }
                await funciones.GetResultados(ctx, participantes, rondas, settings.Dificultad, "manga");
            }
            else
            {
                var error = await ctx.RespondAsync(settings.MsgError).ConfigureAwait(false);
            }
        }

        [Command("leaderboardC"), Aliases("estadisticaspersonajes"), Description("Estadisticas de adivina el personaje."), RequireGuild]
        public async Task EstadisticasAdivinaPersonaje(CommandContext ctx)
        {
            string facil = await funciones.GetEstadisticas(ctx, "personaje", "Fácil");
            string media = await funciones.GetEstadisticas(ctx, "personaje", "Media");
            string dificil = await funciones.GetEstadisticas(ctx, "personaje", "Dificil");
            string extremo = await funciones.GetEstadisticas(ctx, "personaje", "Extremo");
            string kusan = await funciones.GetEstadisticas(ctx, "personaje", "Kusan");

            var builder = new DiscordEmbedBuilder
            {
                Title = "Estadisticas - Adivina el personaje",
                Footer = funciones.GetFooter(ctx),
                Color = funciones.GetColor()
            };
            if (!String.IsNullOrEmpty(facil))
                builder.AddField("Dificultad Fácil", facil);
            if (!String.IsNullOrEmpty(media))
                builder.AddField("Dificultad Media", media);
            if (!String.IsNullOrEmpty(dificil))
                builder.AddField("Dificultad Dificil", dificil);
            if (!String.IsNullOrEmpty(extremo))
                builder.AddField("Dificultad Extremo", extremo);
            if (!String.IsNullOrEmpty(kusan))
                builder.AddField("Dificultad Kusan", kusan);
            await ctx.RespondAsync(embed: builder);
        }

        [Command("leaderboardA"), Aliases("estadisticasanimes"), Description("Estadisticas de adivina el anime."), RequireGuild]
        public async Task EstadisticasAdivinaAnime(CommandContext ctx)
        {
            string facil = await funciones.GetEstadisticas(ctx, "anime", "Fácil");
            string media = await funciones.GetEstadisticas(ctx, "anime", "Media");
            string dificil = await funciones.GetEstadisticas(ctx, "anime", "Dificil");
            string extremo = await funciones.GetEstadisticas(ctx, "anime", "Extremo");
            string kusan = await funciones.GetEstadisticas(ctx, "anime", "Kusan");

            var builder = new DiscordEmbedBuilder
            {
                Title = "Estadisticas - Adivina el anime del personaje",
                Footer = funciones.GetFooter(ctx),
                Color = funciones.GetColor()
            };
            if (!String.IsNullOrEmpty(facil))
                builder.AddField("Dificultad Fácil", facil);
            if (!String.IsNullOrEmpty(media))
                builder.AddField("Dificultad Media", media);
            if (!String.IsNullOrEmpty(dificil))
                builder.AddField("Dificultad Dificil", dificil);
            if (!String.IsNullOrEmpty(extremo))
                builder.AddField("Dificultad Extremo", extremo);
            if (!String.IsNullOrEmpty(kusan))
                builder.AddField("Dificultad Kusan", kusan);
            await ctx.RespondAsync(embed: builder);
        }

        [Command("leaderboardM"), Aliases("estadisticasmangas"), Description("Estadisticas de adivina el anime."), RequireGuild]
        public async Task EstadisticasAdivinaManga(CommandContext ctx)
        {
            string facil = await funciones.GetEstadisticas(ctx, "manga", "Fácil");
            string media = await funciones.GetEstadisticas(ctx, "manga", "Media");
            string dificil = await funciones.GetEstadisticas(ctx, "manga", "Dificil");
            string extremo = await funciones.GetEstadisticas(ctx, "manga", "Extremo");
            string kusan = await funciones.GetEstadisticas(ctx, "manga", "Kusan");

            var builder = new DiscordEmbedBuilder
            {
                Title = "Estadisticas - Adivina el manga",
                Footer = funciones.GetFooter(ctx),
                Color = funciones.GetColor()
            };
            if (!String.IsNullOrEmpty(facil))
                builder.AddField("Dificultad Fácil", facil);
            if (!String.IsNullOrEmpty(media))
                builder.AddField("Dificultad Media", media);
            if (!String.IsNullOrEmpty(dificil))
                builder.AddField("Dificultad Dificil", dificil);
            if (!String.IsNullOrEmpty(extremo))
                builder.AddField("Dificultad Extremo", extremo);
            if (!String.IsNullOrEmpty(kusan))
                builder.AddField("Dificultad Kusan", kusan);
            await ctx.RespondAsync(embed: builder);
        }

        [Command("statsC"), Description("Estadisticas de adivina el personaje por usuario."), RequireGuild]
        public async Task EstadisticasAdivinaPersonajeUsuario(CommandContext ctx, DiscordUser usuario = null)
        {
            if (usuario == null)
                usuario = ctx.User;

            int partidasTotales = 0;
            int rondasAcertadas = 0;
            int rondasTotales = 0;

            string facil = funciones.GetEstadisticasUsuario(ctx, "personaje", usuario, "Fácil", out int partidasTotalesF, out int rondasAcertadasF, out int rondasTotalesF);
            partidasTotales += partidasTotalesF;
            rondasAcertadas += rondasAcertadasF;
            rondasTotales += rondasTotalesF;
                
            string media = funciones.GetEstadisticasUsuario(ctx, "personaje", usuario, "Media", out int partidasTotalesM, out int rondasAcertadasM, out int rondasTotalesM);
            partidasTotales += partidasTotalesM;
            rondasAcertadas += rondasAcertadasM;
            rondasTotales += rondasTotalesM;

            string dificil = funciones.GetEstadisticasUsuario(ctx, "personaje", usuario, "Dificil", out int partidasTotalesD, out int rondasAcertadasD, out int rondasTotalesD);
            partidasTotales += partidasTotalesD;
            rondasAcertadas += rondasAcertadasD;
            rondasTotales += rondasTotalesD;

            string extremo = funciones.GetEstadisticasUsuario(ctx, "personaje", usuario, "Extremo", out int partidasTotalesE, out int rondasAcertadasE, out int rondasTotalesE);
            partidasTotales += partidasTotalesE;
            rondasAcertadas += rondasAcertadasE;
            rondasTotales += rondasTotalesE;

            string kusan = funciones.GetEstadisticasUsuario(ctx, "personaje", usuario, "Kusan", out int partidasTotalesK, out int rondasAcertadasK, out int rondasTotalesK);
            partidasTotales += partidasTotalesK;
            rondasAcertadas += rondasAcertadasK;
            rondasTotales += rondasTotalesK;

            int porcentajeAciertos = 0;
            if (rondasTotales > 0)
                porcentajeAciertos = (rondasAcertadas * 100) / rondasTotales;
            string totales =
                $"  - Porcentaje de aciertos: **{porcentajeAciertos}%**\n" +
                $"  - Partidas totales: **{partidasTotales}**\n" +
                $"  - Rondas acertadas: **{rondasAcertadas}**\n" +
                $"  - Rondas totales: **{rondasTotales}**\n\n";

            var builder = new DiscordEmbedBuilder
            {
                Title = $"Estadisticas de **{usuario.Username}#{usuario.Discriminator}** - Adivina el personaje",
                Footer = funciones.GetFooter(ctx),
                Color = funciones.GetColor()
            };
            if (!String.IsNullOrEmpty(facil))
                builder.AddField("Dificultad Fácil", facil);
            if (!String.IsNullOrEmpty(media))
                builder.AddField("Dificultad Media", media);
            if (!String.IsNullOrEmpty(dificil))
                builder.AddField("Dificultad Dificil", dificil);
            if (!String.IsNullOrEmpty(extremo))
                builder.AddField("Dificultad Extremo", extremo);
            if (!String.IsNullOrEmpty(kusan))
                builder.AddField("Dificultad Kusan", kusan);
            builder.AddField("Totales", totales);

            await ctx.RespondAsync(embed: builder);
        }

        [Command("statsA"), Description("Estadisticas de adivina el anime por usuario."), RequireGuild]
        public async Task EstadisticasAdivinaAnimeUsuario(CommandContext ctx, DiscordUser usuario = null)
        {
            if (usuario == null)
                usuario = ctx.User;

            int partidasTotales = 0;
            int rondasAcertadas = 0;
            int rondasTotales = 0;

            string facil = funciones.GetEstadisticasUsuario(ctx, "anime", usuario, "Fácil", out int partidasTotalesF, out int rondasAcertadasF, out int rondasTotalesF);
            partidasTotales += partidasTotalesF;
            rondasAcertadas += rondasAcertadasF;
            rondasTotales += rondasTotalesF;

            string media = funciones.GetEstadisticasUsuario(ctx, "anime", usuario, "Media", out int partidasTotalesM, out int rondasAcertadasM, out int rondasTotalesM);
            partidasTotales += partidasTotalesM;
            rondasAcertadas += rondasAcertadasM;
            rondasTotales += rondasTotalesM;

            string dificil = funciones.GetEstadisticasUsuario(ctx, "anime", usuario, "Dificil", out int partidasTotalesD, out int rondasAcertadasD, out int rondasTotalesD);
            partidasTotales += partidasTotalesD;
            rondasAcertadas += rondasAcertadasD;
            rondasTotales += rondasTotalesD;

            string extremo = funciones.GetEstadisticasUsuario(ctx, "anime", usuario, "Extremo", out int partidasTotalesE, out int rondasAcertadasE, out int rondasTotalesE);
            partidasTotales += partidasTotalesE;
            rondasAcertadas += rondasAcertadasE;
            rondasTotales += rondasTotalesE;

            string kusan = funciones.GetEstadisticasUsuario(ctx, "anime", usuario, "Kusan", out int partidasTotalesK, out int rondasAcertadasK, out int rondasTotalesK);
            partidasTotales += partidasTotalesK;
            rondasAcertadas += rondasAcertadasK;
            rondasTotales += rondasTotalesK;

            int porcentajeAciertos = 0;
            if (rondasTotales > 0)
                porcentajeAciertos = (rondasAcertadas * 100) / rondasTotales;
            string totales =
                $"  - Porcentaje de aciertos: **{porcentajeAciertos}%**\n" +
                $"  - Partidas totales: **{partidasTotales}**\n" +
                $"  - Rondas acertadas: **{rondasAcertadas}**\n" +
                $"  - Rondas totales: **{rondasTotales}**\n\n";

            var builder = new DiscordEmbedBuilder
            {
                Title = $"Estadisticas de **{usuario.Username}#{usuario.Discriminator}** - Adivina el anime",
                Footer = funciones.GetFooter(ctx),
                Color = funciones.GetColor()
            };
            if (!String.IsNullOrEmpty(facil))
                builder.AddField("Dificultad Fácil", facil);
            if (!String.IsNullOrEmpty(media))
                builder.AddField("Dificultad Media", media);
            if (!String.IsNullOrEmpty(dificil))
                builder.AddField("Dificultad Dificil", dificil);
            if (!String.IsNullOrEmpty(extremo))
                builder.AddField("Dificultad Extremo", extremo);
            if (!String.IsNullOrEmpty(kusan))
                builder.AddField("Dificultad Kusan", kusan);
            builder.AddField("Totales", totales);

            await ctx.RespondAsync(embed: builder);
        }

        [Command("statsM"), Description("Estadisticas de adivina el manga por usuario."), RequireGuild]
        public async Task EstadisticasAdivinaMangaUsuario(CommandContext ctx, DiscordUser usuario = null)
        {
            if (usuario == null)
                usuario = ctx.User;

            int partidasTotales = 0;
            int rondasAcertadas = 0;
            int rondasTotales = 0;

            string facil = funciones.GetEstadisticasUsuario(ctx, "manga", usuario, "Fácil", out int partidasTotalesF, out int rondasAcertadasF, out int rondasTotalesF);
            partidasTotales += partidasTotalesF;
            rondasAcertadas += rondasAcertadasF;
            rondasTotales += rondasTotalesF;

            string media = funciones.GetEstadisticasUsuario(ctx, "manga", usuario, "Media", out int partidasTotalesM, out int rondasAcertadasM, out int rondasTotalesM);
            partidasTotales += partidasTotalesM;
            rondasAcertadas += rondasAcertadasM;
            rondasTotales += rondasTotalesM;

            string dificil = funciones.GetEstadisticasUsuario(ctx, "manga", usuario, "Dificil", out int partidasTotalesD, out int rondasAcertadasD, out int rondasTotalesD);
            partidasTotales += partidasTotalesD;
            rondasAcertadas += rondasAcertadasD;
            rondasTotales += rondasTotalesD;

            string extremo = funciones.GetEstadisticasUsuario(ctx, "manga", usuario, "Extremo", out int partidasTotalesE, out int rondasAcertadasE, out int rondasTotalesE);
            partidasTotales += partidasTotalesE;
            rondasAcertadas += rondasAcertadasE;
            rondasTotales += rondasTotalesE;

            string kusan = funciones.GetEstadisticasUsuario(ctx, "manga", usuario, "Kusan", out int partidasTotalesK, out int rondasAcertadasK, out int rondasTotalesK);
            partidasTotales += partidasTotalesK;
            rondasAcertadas += rondasAcertadasK;
            rondasTotales += rondasTotalesK;

            int porcentajeAciertos = 0;
            if (rondasTotales > 0)
                porcentajeAciertos = (rondasAcertadas * 100) / rondasTotales;
            string totales =
                $"  - Porcentaje de aciertos: **{porcentajeAciertos}%**\n" +
                $"  - Partidas totales: **{partidasTotales}**\n" +
                $"  - Rondas acertadas: **{rondasAcertadas}**\n" +
                $"  - Rondas totales: **{rondasTotales}**\n\n";

            var builder = new DiscordEmbedBuilder
            {
                Title = $"Estadisticas de **{usuario.Username}#{usuario.Discriminator}** - Adivina el manga",
                Footer = funciones.GetFooter(ctx),
                Color = funciones.GetColor()
            };
            if (!String.IsNullOrEmpty(facil))
                builder.AddField("Dificultad Fácil", facil);
            if (!String.IsNullOrEmpty(media))
                builder.AddField("Dificultad Media", media);
            if (!String.IsNullOrEmpty(dificil))
                builder.AddField("Dificultad Dificil", dificil);
            if (!String.IsNullOrEmpty(extremo))
                builder.AddField("Dificultad Extremo", extremo);
            if (!String.IsNullOrEmpty(kusan))
                builder.AddField("Dificultad Kusan", kusan);
            builder.AddField("Totales", totales);

            await ctx.RespondAsync(embed: builder);
        }
    }
}
