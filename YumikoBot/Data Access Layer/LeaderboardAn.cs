//------------------------------------------------------------------------------
// <auto-generated>
//     Este código se generó a partir de una plantilla.
//
//     Los cambios manuales en este archivo pueden causar un comportamiento inesperado de la aplicación.
//     Los cambios manuales en este archivo se sobrescribirán si se regenera el código.
// </auto-generated>
//------------------------------------------------------------------------------

namespace YumikoBot.Data_Access_Layer
{
    using System;
    using System.Collections.Generic;
    
    public partial class LeaderboardAn
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2214:DoNotCallOverridableMethodsInConstructors")]
        public LeaderboardAn()
        {
            this.partidasJugadas = 0;
            this.rondasAcertadas = 0;
            this.rondasTotales = 0;
        }
    
        public int Id { get; set; }
        public long user_id { get; set; }
        public long guild_id { get; set; }
        public string dificultad { get; set; }
        public int partidasJugadas { get; set; }
        public int rondasAcertadas { get; set; }
        public int rondasTotales { get; set; }
    }
}
