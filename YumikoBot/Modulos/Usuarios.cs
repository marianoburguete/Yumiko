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
            if (string.IsNullOrEmpty(desc))
            {
                desc = "(No hay ningún usuario registrado que cumpla años este mes)";
            }

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
            var interactivity = ctx.Client.GetInteractivity();
            DiscordMessage msgError = null;
            var msgFecha = await ctx.RespondAsync(embed: new DiscordEmbedBuilder
            {
                Title = "Escribe tu fecha de nacimiento",
                Description = "En este formato: **dd/mm/yyyy**\n  Ejemplo: 30/01/2000"
            });
            var msgFechaInter = await interactivity.WaitForMessageAsync(xm => xm.Channel == ctx.Channel && xm.Author == ctx.User, TimeSpan.FromSeconds(Convert.ToDouble(ConfigurationManager.AppSettings["TimeoutGeneral"])));
            if (!msgFechaInter.TimedOut)
            {
                bool result = funciones.ParsearFechaFromString(msgFechaInter.Result.Content, out DateTime fecha);
                if (funciones.ChequearPermisoYumiko(ctx, DSharpPlus.Permissions.ManageMessages))
                {
                    await msgFechaInter.Result.DeleteAsync("Auto borrado de Yumiko");
                    await msgFecha.DeleteAsync("Auto borrado de Yumiko");
                }
                if (result)
                {
                    var msgOcultar = await ctx.RespondAsync(embed: new DiscordEmbedBuilder
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
                            if (funciones.ChequearPermisoYumiko(ctx, DSharpPlus.Permissions.ManageMessages))
                            {
                                await msgOcultarInter.Result.DeleteAsync("Auto borrado de Yumiko");
                                await msgOcultar.DeleteAsync("Auto borrado de Yumiko");
                            }
                            switch (mostrarEdadInt)
                            {
                                case 1:
                                    await usuariosService.SetBirthday(ctx, fecha, true);
                                    break;
                                case 2:
                                    await usuariosService.SetBirthday(ctx, fecha, false);
                                    break;
                                default:
                                    msgError = await ctx.RespondAsync("Ingresa bien la respuesta, baka");
                                    break;
                            }
                        }
                        else
                        {
                            msgError = await ctx.RespondAsync("Ingresa bien la respuesta, baka");
                        }
                    }
                    else
                    {
                        msgError = await ctx.RespondAsync("Tiempo agotado esperando la respuesta");
                    }
                }
                else
                {
                    msgError = await ctx.RespondAsync("Ingresa bien la fecha, baka");
                }
            }
            else
            {
                msgError = await ctx.RespondAsync("Tiempo agotado esperando la fecha de nacimiento");
            }
            if (msgError != null)
            {
                if (funciones.ChequearPermisoYumiko(ctx, DSharpPlus.Permissions.ManageMessages))
                {
                    await Task.Delay(3000);
                    await msgError.DeleteAsync("Auto borrado de Yumiko");
                } 
            }
        }

        [Command("borrarcumpleaños"), Aliases("deletebirthday", "deletecumpleaños", "eliminarcumpleaños"), Description("Borra el cumpleaños del usuario."), RequireGuild]
        public async Task DeleteBirthday(CommandContext ctx)
        {
            await usuariosService.DeleteBirthday(ctx);
            var msg = await ctx.RespondAsync("Cumpleaños borrado correctamente");
            if (funciones.ChequearPermisoYumiko(ctx, DSharpPlus.Permissions.ManageMessages))
            {
                await Task.Delay(3000);
                await msg.DeleteAsync("Auto borrado de Yumiko");
            }
        }
    }
}
