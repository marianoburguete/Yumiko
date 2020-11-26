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
        public async Task QuizCharactersGlobal(CommandContext ctx, [Description("Para activar modo megu escribe -m o -megu")]string modoMegu = null)
        {
            bool meguMode = false;
            if (modoMegu == "-m" || modoMegu == "-megu")
                meguMode = true;
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
                    Description = $"Sesión iniciada por {ctx.User.Mention}",
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
                                SiteUrl = x.siteUrl
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        DiscordMessage msg;
                        switch (ex.Message)
                        {
                            default:
                                msg = await ctx.RespondAsync($"Error inesperado").ConfigureAwait(false);
                                break;
                        }
                        await Task.Delay(3000);
                        await ctx.Message.DeleteAsync("Auto borrado de yumiko");
                        await msg.DeleteAsync("Auto borrado de yumiko");
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
                    if (meguMode)
                    {
                        await ctx.RespondAsync("ATENTOOOOS");
                        await Task.Delay(funciones.GetNumeroRandom(100, 3000));
                    }
                    await ctx.RespondAsync(embed: new DiscordEmbedBuilder
                    {
                        Color = DiscordColor.Gold,
                        Title = "Adivina el personaje",
                        Description = $"Ronda {ronda} de {rondas}",
                        ImageUrl = elegido.Image
                    }).ConfigureAwait(false);
                    var msg = await interactivity.WaitForMessageAsync
                        (xm => (xm.Channel == ctx.Channel) &&
                        (xm.Content.ToLower().Trim() == elegido.NameFull.ToLower().Trim() || xm.Content.ToLower().Trim() == elegido.NameFirst.ToLower().Trim() || (elegido.NameLast != null && xm.Content.ToLower().Trim() == elegido.NameLast.ToLower().Trim())) || (xm.Content.ToLower() == "cancelar" && xm.Author == ctx.User)
                        , TimeSpan.FromSeconds(Convert.ToDouble(ConfigurationManager.AppSettings["GuessTimeGames"])));
                    if (!msg.TimedOut)
                    {
                        if (msg.Result.Author == ctx.User && msg.Result.Content.ToLower() == "cancelar")
                        {
                            await ctx.RespondAsync($"El juego ha sido cancelado por **{ctx.User.Username}#{ctx.User.Discriminator}**").ConfigureAwait(false);
                            await funciones.GetResultados(ctx, participantes, lastRonda, settings.Dificultad);
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
                await funciones.GetResultados(ctx, participantes, rondas, settings.Dificultad);
            }
            else
            {
                var error = await ctx.RespondAsync(settings.MsgError).ConfigureAwait(false);
            }
        }

        [Command("quizA"), Aliases("adivinaelanime"), Description("Empieza el juego de adivina el anime."), RequireGuild]
        public async Task QuizAnimeGlobal(CommandContext ctx, [Description("Para activar modo megu escribe -m o -megu")]string modoMegu = null)
        {
            bool meguMode = false;
            if (modoMegu == "-m" || modoMegu == "-megu")
                meguMode = true;
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
                    Description = $"Sesión iniciada por {ctx.User.Mention}",
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
                        "           media(type:ANIME){" +
                        "               nodes{" +
                        "                   title{" +
                        "                       romaji," +
                        "                       english" +
                        "                   }," +
                        "                   siteUrl" +
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
                                Animes = new List<Anime>()
                            };
                            foreach (var y in x.media.nodes)
                            {
                                string titleEnglish = y.title.english;
                                string titleRomaji = y.title.romaji;
                                c.Animes.Add(new Anime()
                                {
                                    TitleEnglish = funciones.QuitarCaracteresEspeciales(titleEnglish),
                                    TitleRomaji = funciones.QuitarCaracteresEspeciales(titleRomaji),
                                    SiteUrl = y.siteUrl
                                });
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
                                msg = await ctx.RespondAsync($"Error inesperado").ConfigureAwait(false);
                                break;
                        }
                        await Task.Delay(3000);
                        await ctx.Message.DeleteAsync("Auto borrado de yumiko");
                        await msg.DeleteAsync("Auto borrado de yumiko");
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
                    if (meguMode)
                    {
                        await ctx.RespondAsync("ATENTOOOOS");
                        await Task.Delay(funciones.GetNumeroRandom(100, 3000));
                    }
                    await ctx.RespondAsync(embed: new DiscordEmbedBuilder
                    {
                        Color = DiscordColor.Gold,
                        Title = $"Adivina el anime del personaje",
                        Description = $"Ronda {ronda} de {rondas}",
                        ImageUrl = elegido.Image
                    }).ConfigureAwait(false);
                    var msg = await interactivity.WaitForMessageAsync
                        (xm => (xm.Channel == ctx.Channel) &&
                        ((xm.Content.ToLower() == "cancelar" && xm.Author == ctx.User) ||
                        (elegido.Animes.Find(x => x.TitleEnglish != null && x.TitleEnglish.ToLower().Trim() == xm.Content.ToLower().Trim()) != null) ||
                        (elegido.Animes.Find(x => x.TitleRomaji != null && x.TitleRomaji.ToLower().Trim() == xm.Content.ToLower().Trim()) != null)),
                        TimeSpan.FromSeconds(Convert.ToDouble(ConfigurationManager.AppSettings["GuessTimeGames"])));
                    string descAnimes = $"Los animes de [{elegido.NameFull}]({elegido.SiteUrl}) son:\n\n";
                    foreach (Anime anim in elegido.Animes)
                    {
                        descAnimes += $"- [{anim.TitleRomaji}]({anim.SiteUrl})\n";
                    }
                    if (!msg.TimedOut)
                    {
                        if (msg.Result.Author == ctx.User && msg.Result.Content.ToLower() == "cancelar")
                        {
                            await ctx.RespondAsync($"El juego ha sido cancelado por **{ctx.User.Username}#{ctx.User.Discriminator}**").ConfigureAwait(false);
                            await funciones.GetResultados(ctx, participantes, lastRonda, settings.Dificultad);
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
                await funciones.GetResultados(ctx, participantes, rondas, settings.Dificultad);
            }
            else
            {
                var error = await ctx.RespondAsync(settings.MsgError).ConfigureAwait(false);
            }
        }

        [Command("statsC"), Aliases("estadisticaspersonajes"), Description("Estadisticas de adivina el personaje."), RequireGuild]
        public async Task EstadisticasAdivinaPersonaje(CommandContext ctx)
        {
            LeaderboardPersonajes leaderboardPjs = new LeaderboardPersonajes();
            List<StatsJuego> resFacil = leaderboardPjs.GetLeaderboard(Int64.Parse(ctx.Guild.Id.ToString()), "Fácil");
            string facil = "";
            int pos = 1;
            foreach(StatsJuego jugador in resFacil)
            {
                long x = jugador.UserId;
                ulong id = (ulong)x;
                DiscordMember miembro = await ctx.Guild.GetMemberAsync(id);
                if(miembro != null)
                {
                    facil += $"#{pos} - {miembro.DisplayName} - Aciertos: {jugador.PorcentajeAciertos}% - Partidas: {jugador.PartidasTotales}\n";
                    pos++;
                }
            }
            if (resFacil.Count > 0)
            {
                await ctx.RespondAsync(embed: new DiscordEmbedBuilder
                {
                    Title = "Estadisticas - Adivina el personaje (Fácil)",
                    Description = facil,
                    Footer = funciones.GetFooter(ctx),
                    Color = funciones.GetColor()
                });
            }
            List<StatsJuego> resMedia = leaderboardPjs.GetLeaderboard(Int64.Parse(ctx.Guild.Id.ToString()), "Media");
            string media = "";
            pos = 1;
            foreach (StatsJuego jugador in resMedia)
            {
                long x = jugador.UserId;
                ulong id = (ulong)x;
                DiscordMember miembro = await ctx.Guild.GetMemberAsync(id);
                if (miembro != null)
                {
                    media += $"#{pos} - {miembro.DisplayName} - Aciertos: {jugador.PorcentajeAciertos}% - Partidas: {jugador.PartidasTotales}\n";
                    pos++;
                }
            }
            if (resMedia.Count > 0)
            {
                await ctx.RespondAsync(embed: new DiscordEmbedBuilder
                {
                    Title = "Estadisticas - Adivina el personaje (Media)",
                    Description = media,
                    Footer = funciones.GetFooter(ctx),
                    Color = funciones.GetColor()
                });
            }
            List<StatsJuego> resDificil = leaderboardPjs.GetLeaderboard(Int64.Parse(ctx.Guild.Id.ToString()), "Dificil");
            string dificil = "";
            pos = 1;
            foreach (StatsJuego jugador in resDificil)
            {
                long x = jugador.UserId;
                ulong id = (ulong)x;
                DiscordMember miembro = await ctx.Guild.GetMemberAsync(id);
                if (miembro != null)
                {
                    dificil += $"#{pos} - {miembro.DisplayName} - Aciertos: {jugador.PorcentajeAciertos}% - Partidas: {jugador.PartidasTotales}\n";
                    pos++;
                }
            }
            if (resDificil.Count > 0)
            {
                await ctx.RespondAsync(embed: new DiscordEmbedBuilder
                {
                    Title = "Estadisticas - Adivina el personaje (Dificil)",
                    Description = dificil,
                    Footer = funciones.GetFooter(ctx),
                    Color = funciones.GetColor()
                });
            }
            List<StatsJuego> resExtremo = leaderboardPjs.GetLeaderboard(Int64.Parse(ctx.Guild.Id.ToString()), "Extremo");
            string extremo = "";
            pos = 1;
            foreach (StatsJuego jugador in resExtremo)
            {
                long x = jugador.UserId;
                ulong id = (ulong)x;
                DiscordMember miembro = await ctx.Guild.GetMemberAsync(id);
                if (miembro != null)
                {
                    extremo += $"#{pos} - {miembro.DisplayName} - Aciertos: {jugador.PorcentajeAciertos}% - Partidas: {jugador.PartidasTotales}\n";
                    pos++;
                }
            }
            if (resExtremo.Count > 0)
            {
                await ctx.RespondAsync(embed: new DiscordEmbedBuilder
                {
                    Title = "Estadisticas - Adivina el personaje (Extremo)",
                    Description = extremo,
                    Footer = funciones.GetFooter(ctx),
                    Color = funciones.GetColor()
                });
            }
            List<StatsJuego> resKusan = leaderboardPjs.GetLeaderboard(Int64.Parse(ctx.Guild.Id.ToString()), "Kusan");
            string kusan = "";
            pos = 1;
            foreach (StatsJuego jugador in resKusan)
            {
                long x = jugador.UserId;
                ulong id = (ulong)x;
                DiscordMember miembro = await ctx.Guild.GetMemberAsync(id);
                if (miembro != null)
                {
                    kusan += $"#{pos} - {miembro.DisplayName} - Aciertos: {jugador.PorcentajeAciertos}% - Partidas: {jugador.PartidasTotales}\n";
                    pos++;
                }
            }
            if(resKusan.Count > 0)
            {
                await ctx.RespondAsync(embed: new DiscordEmbedBuilder
                {
                    Title = "Estadisticas - Adivina el personaje (Kusan)",
                    Description = kusan,
                    Footer = funciones.GetFooter(ctx),
                    Color = funciones.GetColor()
                });
            }
            await ctx.Message.DeleteAsync("Auto borrado de yumiko");
        }
    }
}
