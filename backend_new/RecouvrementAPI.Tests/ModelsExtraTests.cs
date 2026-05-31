using RecouvrementAPI.Models;
using System;
using Xunit;

namespace RecouvrementAPI.Tests
{
    public class ModelsExtraTests
    {
        [Fact]
        public void Agence_ToutesProprietes_OK()
        {
            var a = new Agence { IdAgence=2, NomAgence="STB Sfax", Ville="Sfax", Adresse="Rue Sfax" };
            Assert.Equal(2, a.IdAgence);
            Assert.Equal("STB Sfax", a.NomAgence);
            Assert.Equal("Sfax", a.Ville);
        }

        [Fact]
        public void Echeance_ProprietesPrincipales_OK()
        {
            var e = new Echeance { IdEcheance=1, IdDossier=1, MontantDu=500, DateEcheance=DateTime.Now, Statut="impaye" };
            Assert.Equal(500, e.MontantDu);
            Assert.Equal("impaye", e.Statut);
        }

        [Fact]
        public void Garantie_ProprietesPrincipales_OK()
        {
            var g = new Garantie { IdGarantie=1, IdDossier=1, TypeGarantie="Hypotheque", Description="Bien immobilier" };
            Assert.Equal("Hypotheque", g.TypeGarantie);
            Assert.Equal("Bien immobilier", g.Description);
        }

        [Fact]
        public void HistoriquePaiement_ProprietesPrincipales_OK()
        {
            var h = new HistoriquePaiement { IdPaiement=1, IdDossier=1, MontantPaye=1000, TypePaiement="virement", DatePaiement=DateTime.Now };
            Assert.Equal(1000, h.MontantPaye);
            Assert.Equal("virement", h.TypePaiement);
        }

        [Fact]
        public void IntentionClient_ProprietesPrincipales_OK()
        {
            var i = new IntentionClient
            {
                IdIntention=1, IdDossier=1, TypeIntention="promesse_paiement",
                DateIntention=DateTime.Now, Statut="En attente",
                MontantPropose=500, ConfianceClient=70,
                DatePaiementPrevue=DateTime.Now.AddDays(7)
            };
            Assert.Equal("promesse_paiement", i.TypeIntention);
            Assert.Equal("En attente", i.Statut);
            Assert.Equal(500, i.MontantPropose);
        }

        [Fact]
        public void ScoreRisque_ProprietesPrincipales_OK()
        {
            var s = new ScoreRisque { IdScore=1, IdDossier=1, Score=75, DateCalcul=DateTime.Now, Recommandation="Risque moyen" };
            Assert.Equal(75, s.Score);
            Assert.Equal("Risque moyen", s.Recommandation);
        }
    }
}
