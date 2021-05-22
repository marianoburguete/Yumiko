﻿using Google.Cloud.Firestore;
using System;

namespace YumikoBot.DAL
{
    [FirestoreData]
    public class UsuarioDiscordFirebase
    {
        [FirestoreProperty]
        public long user_id { get; set; }

        [FirestoreProperty]
        public DateTime Birthday { get; set; }

        [FirestoreProperty]
        public bool MostrarYear { get; set; }
    }
}
