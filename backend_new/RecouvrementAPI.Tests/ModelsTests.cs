using RecouvrementAPI.Models;
using System;
using System.Collections.Generic;
using Xunit;

namespace RecouvrementAPI.Tests
{
    public class ModelsTests
    {
        // ===== Agence =====
        [Fact]
        public void Agence_CreationProprietes_OK()
        {
            var a = new Agence { IdAgence = 1, NomAgence = "STB Tunis", Ville = "Tunis", Adresse = "Rue de la Banque" };
            Assert.Equal(1, a.IdAgence);
            Assert.Equal("STB Tunis", a.NomAgence);
            Assert.Equal("Tunis", a.Ville);
            Assert.Equal("Rue de la Banque", a.Adresse);
        }

        // ===== Client =====
        [Fact]
        public void Client_CreationProprietes_OK()
        {
            var c = new Client
            {
                IdClient = 1, Nom = "Mansouri", Prenom = "Ines",
                Email = "ines@gmail.com", Telephone = "21000000",
                Adresse = "Tunis", CIN = "12345678",
                TokenAcces = "tok_abc", TokenExpiration = DateTime.Now.AddDays(7),
                Statut = "Actif"
            };
            Assert.Equal("Mansouri", c.Nom);
            Assert.Equal("Ines", c.Prenom);
            Assert.Equal("12345678", c.CIN);
            Assert.Equal("Actif", c.Statut);
        }

        [Fact]
        public void Client_StatutDefaut_EstActif()
        {
            var c = new Client { Nom = "Test", Prenom = "Test", Adresse = "A", CIN = "0" };
            Assert.Equal("Actif", c.Statut);
        }

        [Fact]
        public void Client_NavigationAgence_OK()
        {
            var agence = new Agence { IdAgence = 1, NomAgence = "STB", Ville = "Tunis", Adresse = "Rue A" };
            var client = new Client { Nom = "X", Prenom = "Y", Adresse = "Z", CIN = "1", Agence = agence };
            Assert.Equal("STB", client.Agence.NomAgence);
        }

        // ===== DossierRecouvrement =====
        [Fact]
        public void Dossier_CreationProprietes_OK()
        {
            var d = new DossierRecouvrement
            {
                IdDossier = 1, TypeEmprunt = "Immobilier",
                MontantInitial = 10000, MontantImpaye = 5000,
                FraisDossier = 100, StatutDossier = "amiable",
                TauxInteret = 5, ConfianceClient = 80
            };
            Assert.Equal("Immobilier", d.TypeEmprunt);
            Assert.Equal(10000, d.MontantInitial);
            Assert.Equal(5000, d.MontantImpaye);
            Assert.Equal(0, d.ConfianceClient == 80 ? 0 : 1);
        }

        [Fact]
        public void Dossier_ConfianceClientDefaut_EstZero()
        {
            var d = new DossierRecouvrement { TypeEmprunt = "Auto", StatutDossier = "amiable" };
            Assert.Equal(0, d.ConfianceClient);
        }

        [Fact]
        public void Dossier_DateCreationDefaut_EstAujourdhui()
        {
            var d = new DossierRecouvrement();
            Assert.True((DateTime.Now - d.DateCreation).TotalSeconds < 5);
        }

        // ===== Communication =====
        [Fact]
        public void Communication_CreationProprietes_OK()
        {
            var c = new Communication
            {
                IdCommunication = 1, IdDossier = 1,
                Message = "Bonjour", Origine = "client",
                DateEnvoi = DateTime.Now
            };
            Assert.Equal("Bonjour", c.Message);
            Assert.Equal("client", c.Origine);
            Assert.Null(c.IdRelance);
        }

        [Fact]
        public void Communication_IdRelanceNullable_OK()
        {
            var c = new Communication { Message = "test", Origine = "systeme", DateEnvoi = DateTime.Now };
            Assert.Null(c.IdRelance);
            c.IdRelance = 5;
            Assert.Equal(5, c.IdRelance);
        }

        // ===== HistoriqueAction =====
        [Fact]
        public void HistoriqueAction_CreationProprietes_OK()
        {
            var h = new HistoriqueAction
            {
                IdAction = 1, IdDossier = 1,
                ActionDetail = "Client connecté", Acteur = "client",
                DateAction = DateTime.Now
            };
            Assert.Equal("Client connecté", h.ActionDetail);
            Assert.Equal("client", h.Acteur);
        }

        [Fact]
        public void HistoriqueAction_ActeurSysteme_OK()
        {
            var h = new HistoriqueAction { ActionDetail = "Auto", Acteur = "systeme", DateAction = DateTime.Now };
            Assert.Equal("systeme", h.Acteur);
        }

        // ===== RelanceClient =====
        [Fact]
        public void RelanceClient_CreationProprietes_OK()
        {
            var r = new RelanceClient
            {
                IdRelance = 1, IdDossier = 1,
                Moyen = "email", Statut = "envoye",
                Contenu = "Relance test", DateRelance = DateTime.Now
            };
            Assert.Equal("email", r.Moyen);
            Assert.Equal("envoye", r.Statut);
            Assert.Equal("Relance test", r.Contenu);
        }

        [Fact]
        public void RelanceClient_StatutRepondu_OK()
        {
            var r = new RelanceClient { Moyen = "sms", Statut = "repondu", Contenu = "ok", DateRelance = DateTime.Now };
            Assert.Equal("repondu", r.Statut);
        }

        [Fact]
        public void RelanceClient_MoyenAppel_OK()
        {
            var r = new RelanceClient { Moyen = "appel", Statut = "envoye", Contenu = "test", DateRelance = DateTime.Now };
            Assert.Equal("appel", r.Moyen);
        }

        // ===== UtilisateurBack =====
        [Fact]
        public void UtilisateurBack_CreationProprietes_OK()
        {
            var u = new UtilisateurBack
            {
                IdAgent = 1, Nom = "Admin", Prenom = "Super",
                Email = "admin@stb.tn", MotDePasse = "hash123",
                Role = "admin", Statut = "Actif"
            };
            Assert.Equal("admin", u.Role);
            Assert.Equal("Actif", u.Statut);
            Assert.Equal("admin@stb.tn", u.Email);
        }

        [Fact]
        public void UtilisateurBack_StatutDefaut_EstActif()
        {
            var u = new UtilisateurBack { Nom = "X", Prenom = "Y", Email = "x@y.com", MotDePasse = "p", Role = "agent" };
            Assert.Equal("Actif", u.Statut);
        }

        [Fact]
        public void UtilisateurBack_TelephoneNullable_OK()
        {
            var u = new UtilisateurBack { Nom = "X", Prenom = "Y", Email = "x@y.com", MotDePasse = "p", Role = "agent" };
            Assert.Null(u.Telephone);
            u.Telephone = "21000000";
            Assert.Equal("21000000", u.Telephone);
        }

        [Fact]
        public void UtilisateurBack_DerniereConnexionNullable_OK()
        {
            var u = new UtilisateurBack { Nom = "X", Prenom = "Y", Email = "x@y.com", MotDePasse = "p", Role = "agent" };
            Assert.Null(u.DerniereConnexion);
            u.DerniereConnexion = DateTime.Now;
            Assert.NotNull(u.DerniereConnexion);
        }
    }
}
