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
using System.Globalization;
using DSharpPlus;
using YumikoBot.DAL;

namespace Discord_Bot.Modulos
{
    public class Usuarios : BaseCommandModule
    {
        private readonly FuncionesAuxiliares funciones = new FuncionesAuxiliares();
        private readonly UsuariosDiscord usuariosService = new UsuariosDiscord();

        [Command("cumpleaños"), Aliases("birthday"), Description("Muestra los próximos cumpleaños del mes."), RequireGuild]
        public async Task Birthdays(CommandContext ctx, string flag = null)
        {
            List<UserCumple> lista;
            string titulo;
            if (!string.IsNullOrEmpty(flag) && flag == "-all")
            {
                lista = await usuariosService.GetBirthdays(ctx, false);
                titulo = "Próximos cumpleaños";
            }
            else
            {
                lista = await usuariosService.GetBirthdays(ctx, true);
                titulo = "Próximos cumpleaños en este mes";
            }
            string desc = "";
            foreach (var user in lista)
            {
                try
                {
                    var miembro = await ctx.Guild.GetMemberAsync((ulong)user.Id);
                    int anios = DateTime.Now.Year - user.Birthday.Year;
                    if (DateTime.Now > new DateTime(day: user.Birthday.Day, month: user.Birthday.Month, year: DateTime.Now.Year))
                        anios += 1;
                    string dia = user.BirthdayActual.ToString("dddd", CultureInfo.CreateSpecificCulture("es"));
                    string mes = user.BirthdayActual.ToString("MMMM", CultureInfo.CreateSpecificCulture("es"));
                    if (user.MostrarYear ?? false)
                        desc += $"- **{miembro.DisplayName}** ({miembro.Username}#{miembro.Discriminator}) - Cumple **{anios} años** el {dia} {user.BirthdayActual.Day} de {mes} del {user.BirthdayActual.Year}\n";
                    else
                        desc += $"- **{miembro.DisplayName}** ({miembro.Username}#{miembro.Discriminator}) - Cumple el {dia} {user.BirthdayActual.Day} de {mes} del {user.BirthdayActual.Year}\n";
                }
                catch (Exception){}
            }
            if (string.IsNullOrEmpty(desc))
            {
                desc = "(No hay ningún usuario registrado que cumpla años este mes)\n";
            }

            if (string.IsNullOrEmpty(flag) || flag != "-all")
                desc += "\n**Tip:** Si agregas ` -all` al final del comando, veras todos los cumpleaños registrados.";

            await ctx.Channel.SendMessageAsync(embed: new DiscordEmbedBuilder
            {
                Footer = funciones.GetFooter(ctx),
                Color = funciones.GetColor(),
                Title = titulo,
                Description = desc
            }).ConfigureAwait(false);
        }

        [Command("setcumpleaños"), Aliases("setbirthday"), Description("Agrega o modifica el cumpleaños del usuario."), RequireGuild]
        public async Task SetBirthday(CommandContext ctx)
        {
            DateTime? cumple = await funciones.CrearDate(ctx);
            if (cumple != null)
            {
                DateTime fecha = cumple ?? DateTime.Today;
                DiscordMessage msgError = null;
                var interactivity = ctx.Client.GetInteractivity();
                var msgOcultar = await ctx.Channel.SendMessageAsync(embed: new DiscordEmbedBuilder
                {
                    Title = "¿Quieres que se muestre tu edad?",
                    Description = "1- Si\n2- No"
                });
                var msgOcultarInter = await interactivity.WaitForMessageAsync(xm => xm.Channel == ctx.Channel && xm.Author == ctx.User, TimeSpan.FromSeconds(Convert.ToDouble(ConfigurationManager.AppSettings["TimeoutGeneral"])));
                if (!msgOcultarInter.TimedOut)
                {
                    bool result2 = int.TryParse(msgOcultarInter.Result.Content, out int mostrarEdadInt);
                    if (result2)
                    {
                        await funciones.BorrarMensaje(ctx, msgOcultarInter.Result.Id);
                        await funciones.BorrarMensaje(ctx, msgOcultar.Id);
                        switch (mostrarEdadInt)
                        {
                            case 1:
                                await usuariosService.SetBirthday(ctx, fecha, true);
                                break;
                            case 2:
                                await usuariosService.SetBirthday(ctx, fecha, false);
                                break;
                            default:
                                msgError = await ctx.Channel.SendMessageAsync("Ingresa bien la respuesta");
                                break;
                        }
                    }
                    else
                    {
                        string content = msgOcultarInter.Result.Content.ToLower().Trim();
                        if (content == "1- si" || content == "si")
                            await usuariosService.SetBirthday(ctx, fecha, true);
                        else if (content == "2- no" || content == "no")
                            await usuariosService.SetBirthday(ctx, fecha, false);
                        else
                            msgError = await ctx.Channel.SendMessageAsync("Ingresa bien la respuesta");
                        await funciones.BorrarMensaje(ctx, msgOcultarInter.Result.Id);
                        await funciones.BorrarMensaje(ctx, msgOcultar.Id);
                    }
                }
                else
                {
                    msgError = await ctx.Channel.SendMessageAsync("Tiempo agotado esperando la respuesta");
                }
                if (msgError != null)
                {
                    await Task.Delay(3000);
                    await funciones.BorrarMensaje(ctx, msgError.Id);
                }
            }
        }

        [Command("borrarcumpleaños"), Aliases("deletebirthday", "deletecumpleaños", "eliminarcumpleaños"), Description("Borra el cumpleaños del usuario."), RequireGuild]
        public async Task DeleteBirthday(CommandContext ctx)
        {
            await usuariosService.DeleteBirthday(ctx);
            var msg = await ctx.Channel.SendMessageAsync("Cumpleaños borrado correctamente");
            await Task.Delay(3000);
            await funciones.BorrarMensaje(ctx, msg.Id);
        }
    }
}
