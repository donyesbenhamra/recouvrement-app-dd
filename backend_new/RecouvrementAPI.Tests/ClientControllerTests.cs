using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RecouvrementAPI.Controllers;
using RecouvrementAPI.Data;
using RecouvrementAPI.DTOs;
using RecouvrementAPI.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Xunit;

namespace RecouvrementAPI.Tests
{
    public class ClientControllerTests
    {
        private ApplicationDbContext GetDb(string name)
        {
            var opts = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(name).Options;
            return new ApplicationDbContext(opts);
        }

        private ClientController BuildController(ApplicationDbContext db)
        {
            var env = new FakeWebHostEnvironment();
            var ctrl = new ClientController(db, env);
            ctrl.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            };
            ctrl.ControllerContext.HttpContext.Connection.RemoteIpAddress = IPAddress.Loopback;
            return ctrl;
        }

        private Client MakeClient(string token, bool expired = false)
        {
            var dossier = new DossierRecouvrement
            {
                IdDossier = 1, TypeEmprunt = "Immobilier",
                MontantImpaye = 5000, MontantInitial = 10000,
                FraisDossier = 100, StatutDossier = "amiable",
                TauxInteret = 5, DateCreation = DateTime.Now.AddMonths(-1),
                Echeances = new List<Echeance>(),
                HistoriquePaiements = new List<HistoriquePaiement>(),
                Relances = new List<RelanceClient>(),
                Communications = new List<Communication>(),
                Garanties = new List<Garantie>(),
                Intentions = new List<IntentionClient>()
            };
            return new Client
            {
                IdClient = 1, Nom = "Mansouri", Prenom = "Ines",
                Email = "ines@gmail.com", Telephone = "21000000",
                Adresse = "Tunis", CIN = "12345678",
                TokenAcces = token,
                TokenExpiration = expired ? DateTime.Now.AddDays(-1) : DateTime.Now.AddDays(7),
                Statut = "Actif",
                Agence = new Agence { IdAgence = 1, Ville = "Tunis", NomAgence = "STB Tunis", Adresse = "Rue de la Banque" },
                Dossiers = new List<DossierRecouvrement> { dossier }
            };
        }

        [Fact]
        public async Task GetHistorique_TokenVide_RetourneBadRequest()
        {
            using var db = GetDb("h1");
            var result = await BuildController(db).GetHistorique("");
            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task GetHistorique_TokenInvalide_RetourneUnauthorized()
        {
            using var db = GetDb("h2");
            var result = await BuildController(db).GetHistorique("tok_invalid");
            Assert.IsType<UnauthorizedObjectResult>(result);
        }

        [Fact]
        public async Task GetHistorique_TokenValide_RetourneOk()
        {
            using var db = GetDb("h3");
            db.Clients.Add(MakeClient("tok_ok1")); await db.SaveChangesAsync();
            var result = await BuildController(db).GetHistorique("tok_ok1");
            Assert.IsType<OkObjectResult>(result);
        }

        [Fact]
        public async Task GetHistorique_TokenExpire_RetourneUnauthorized()
        {
            using var db = GetDb("h4");
            db.Clients.Add(MakeClient("tok_exp", expired: true)); await db.SaveChangesAsync();
            var result = await BuildController(db).GetHistorique("tok_exp");
            Assert.IsType<UnauthorizedObjectResult>(result);
        }

        [Fact]
        public async Task EnvoyerMessage_ContenuVide_RetourneBadRequest()
        {
            using var db = GetDb("m1");
            var result = await BuildController(db).EnvoyerMessage("tok", new EnvoyerMessageDto { Contenu = "" });
            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task EnvoyerMessage_TokenInvalide_RetourneUnauthorized()
        {
            using var db = GetDb("m2");
            var result = await BuildController(db).EnvoyerMessage("tok_bad", new EnvoyerMessageDto { Contenu = "Bonjour" });
            Assert.IsType<UnauthorizedObjectResult>(result);
        }

        [Fact]
        public async Task EnvoyerMessage_TokenValide_RetourneOk()
        {
            using var db = GetDb("m3");
            db.Clients.Add(MakeClient("tok_msg1")); await db.SaveChangesAsync();
            var result = await BuildController(db).EnvoyerMessage("tok_msg1", new EnvoyerMessageDto { Contenu = "Bonjour" });
            Assert.IsType<OkObjectResult>(result);
        }

        [Fact]
        public async Task RepondreRelance_ContenuVide_RetourneBadRequest()
        {
            using var db = GetDb("r1");
            var result = await BuildController(db).RepondreRelance("tok", 1, new EnvoyerMessageDto { Contenu = "" });
            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task RepondreRelance_TokenInvalide_RetourneUnauthorized()
        {
            using var db = GetDb("r2");
            var result = await BuildController(db).RepondreRelance("tok_bad", 1, new EnvoyerMessageDto { Contenu = "ok" });
            Assert.IsType<UnauthorizedObjectResult>(result);
        }

        [Fact]
        public async Task RepondreRelance_RelanceIntrouvable_RetourneNotFound()
        {
            using var db = GetDb("r3");
            db.Clients.Add(MakeClient("tok_rel1")); await db.SaveChangesAsync();
            var result = await BuildController(db).RepondreRelance("tok_rel1", 999, new EnvoyerMessageDto { Contenu = "ok" });
            Assert.IsType<NotFoundObjectResult>(result);
        }

        [Fact]
        public async Task RepondreRelance_RelanceValide_RetourneOk()
        {
            using var db = GetDb("r4");
            var client = MakeClient("tok_rel2");
            client.Dossiers.First().Relances.Add(new RelanceClient
            {
                IdRelance = 1, IdDossier = 1, Moyen = "email",
                Statut = "envoye", Contenu = "test", DateRelance = DateTime.Now
            });
            db.Clients.Add(client); await db.SaveChangesAsync();
            var result = await BuildController(db).RepondreRelance("tok_rel2", 1, new EnvoyerMessageDto { Contenu = "Je paie" });
            Assert.IsType<OkObjectResult>(result);
        }

        [Fact]
        public async Task PostIntention_TokenInvalide_RetourneUnauthorized()
        {
            using var db = GetDb("i1");
            var dto = new ClientController.SubmitIntentionDto { TypeIntention = "promesse_paiement" };
            var result = await BuildController(db).PostIntention("tok_bad", dto);
            Assert.IsType<UnauthorizedObjectResult>(result);
        }

        [Fact]
        public async Task PostIntention_TokenValide_RetourneOk()
        {
            using var db = GetDb("i2");
            db.Clients.Add(MakeClient("tok_int1")); await db.SaveChangesAsync();
            var dto = new ClientController.SubmitIntentionDto
            {
                TypeIntention = "promesse_paiement",
                MontantPropose = 1000,
                DatePaiementPrevue = DateTime.Now.AddDays(10)
            };
            var result = await BuildController(db).PostIntention("tok_int1", dto);
            Assert.IsType<OkObjectResult>(result);
        }

        [Fact]
        public async Task GetIntentionExistante_TokenInvalide_RetourneUnauthorized()
        {
            using var db = GetDb("ie1");
            var result = await BuildController(db).GetIntentionExistante("tok_bad", 1);
            Assert.IsType<UnauthorizedObjectResult>(result);
        }

        [Fact]
        public async Task GetIntentionExistante_AucuneIntention_RetourneExisteFalse()
        {
            using var db = GetDb("ie2");
            db.Clients.Add(MakeClient("tok_ie1")); await db.SaveChangesAsync();
            var result = await BuildController(db).GetIntentionExistante("tok_ie1", 1);
            var ok = Assert.IsType<OkObjectResult>(result);
            var prop = ok.Value.GetType().GetProperty("existe");
            Assert.False((bool)prop.GetValue(ok.Value));
        }

        [Fact]
        public async Task AnnulerIntention_TokenInvalide_RetourneUnauthorized()
        {
            using var db = GetDb("a1");
            var result = await BuildController(db).AnnulerIntention("tok_bad", 1);
            Assert.IsType<UnauthorizedObjectResult>(result);
        }

        [Fact]
        public async Task AnnulerIntention_IntentionIntrouvable_RetourneNotFound()
        {
            using var db = GetDb("a2");
            db.Clients.Add(MakeClient("tok_ann1")); await db.SaveChangesAsync();
            var result = await BuildController(db).AnnulerIntention("tok_ann1", 999);
            Assert.IsType<NotFoundObjectResult>(result);
        }

        [Fact]
        public async Task AnnulerIntention_IntentionValide_RetourneOk()
        {
            using var db = GetDb("a3");
            var client = MakeClient("tok_ann2");
            db.Clients.Add(client);
            db.Intentions.Add(new IntentionClient
            {
                IdIntention = 1, IdDossier = 1,
                TypeIntention = "promesse_paiement",
                DateIntention = DateTime.Now, Statut = "En attente"
            });
            await db.SaveChangesAsync();
            var result = await BuildController(db).AnnulerIntention("tok_ann2", 1);
            Assert.IsType<OkObjectResult>(result);
        }
    }

    public class FakeWebHostEnvironment : Microsoft.AspNetCore.Hosting.IWebHostEnvironment
    {
        public string WebRootPath { get; set; } = "";
        public Microsoft.Extensions.FileProviders.IFileProvider? WebRootFileProvider { get; set; }
        public string ApplicationName { get; set; } = "RecouvrementAPI";
        public Microsoft.Extensions.FileProviders.IFileProvider? ContentRootFileProvider { get; set; }
        public string ContentRootPath { get; set; } = System.IO.Path.GetTempPath();
        public string EnvironmentName { get; set; } = "Testing";
    }
}

