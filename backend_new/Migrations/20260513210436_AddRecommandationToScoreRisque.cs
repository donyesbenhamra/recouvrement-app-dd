using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RecouvrementAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddRecommandationToScoreRisque : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "agence",
                columns: table => new
                {
                    id_agence = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    nom_agence = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ville = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    adresse = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agence", x => x.id_agence);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "client",
                columns: table => new
                {
                    id_client = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    id_agence = table.Column<int>(type: "int", nullable: false),
                    nom = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    prenom = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    telephone = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    email = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    token_acces = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    token_expiration = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    cin = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    adresse = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    statut = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_client", x => x.id_client);
                    table.ForeignKey(
                        name: "FK_client_agence_id_agence",
                        column: x => x.id_agence,
                        principalTable: "agence",
                        principalColumn: "id_agence",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "utilisateur_back",
                columns: table => new
                {
                    id_agent = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    id_agence = table.Column<int>(type: "int", nullable: true),
                    nom = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    prenom = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    email = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    mot_de_passe = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    role = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    telephone = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    statut = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    derniere_connexion = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_utilisateur_back", x => x.id_agent);
                    table.ForeignKey(
                        name: "FK_utilisateur_back_agence_id_agence",
                        column: x => x.id_agence,
                        principalTable: "agence",
                        principalColumn: "id_agence");
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "dossier_recouvrement",
                columns: table => new
                {
                    id_dossier = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    id_client = table.Column<int>(type: "int", nullable: false),
                    type_emprunt = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    montant_initial = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    montant_impaye = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    frais_dossier = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    statut_dossier = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    date_creation = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    taux_interet = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    confiance_client = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dossier_recouvrement", x => x.id_dossier);
                    table.ForeignKey(
                        name: "FK_dossier_recouvrement_client_id_client",
                        column: x => x.id_client,
                        principalTable: "client",
                        principalColumn: "id_client",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "echeance",
                columns: table => new
                {
                    id_echeance = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    id_dossier = table.Column<int>(type: "int", nullable: false),
                    date_echeance = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    montant_du = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    montant_paye = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    statut = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    nombre_jours_retard = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_echeance", x => x.id_echeance);
                    table.ForeignKey(
                        name: "FK_echeance_dossier_recouvrement_id_dossier",
                        column: x => x.id_dossier,
                        principalTable: "dossier_recouvrement",
                        principalColumn: "id_dossier",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "garantie",
                columns: table => new
                {
                    id_garantie = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    id_dossier = table.Column<int>(type: "int", nullable: false),
                    type_garantie = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    description = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_garantie", x => x.id_garantie);
                    table.ForeignKey(
                        name: "FK_garantie_dossier_recouvrement_id_dossier",
                        column: x => x.id_dossier,
                        principalTable: "dossier_recouvrement",
                        principalColumn: "id_dossier",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "historique_action",
                columns: table => new
                {
                    id_action = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    id_dossier = table.Column<int>(type: "int", nullable: false),
                    action_detail = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    acteur = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    date_action = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_historique_action", x => x.id_action);
                    table.ForeignKey(
                        name: "FK_historique_action_dossier_recouvrement_id_dossier",
                        column: x => x.id_dossier,
                        principalTable: "dossier_recouvrement",
                        principalColumn: "id_dossier",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "historique_paiement",
                columns: table => new
                {
                    id_paiement = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    id_dossier = table.Column<int>(type: "int", nullable: false),
                    montant_paye = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    type_paiement = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    date_paiement = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_historique_paiement", x => x.id_paiement);
                    table.ForeignKey(
                        name: "FK_historique_paiement_dossier_recouvrement_id_dossier",
                        column: x => x.id_dossier,
                        principalTable: "dossier_recouvrement",
                        principalColumn: "id_dossier",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "intention_client",
                columns: table => new
                {
                    id_intention = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    id_dossier = table.Column<int>(type: "int", nullable: false),
                    type_intention = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    date_intention = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    date_paiement_prevue = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    montant_propose = table.Column<decimal>(type: "decimal(65,30)", nullable: true),
                    statut = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    confiance_client = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_intention_client", x => x.id_intention);
                    table.ForeignKey(
                        name: "FK_intention_client_dossier_recouvrement_id_dossier",
                        column: x => x.id_dossier,
                        principalTable: "dossier_recouvrement",
                        principalColumn: "id_dossier",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "relance_client",
                columns: table => new
                {
                    id_relance = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    id_dossier = table.Column<int>(type: "int", nullable: false),
                    moyen = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    statut = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    date_relance = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    contenu = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_relance_client", x => x.id_relance);
                    table.ForeignKey(
                        name: "FK_relance_client_dossier_recouvrement_id_dossier",
                        column: x => x.id_dossier,
                        principalTable: "dossier_recouvrement",
                        principalColumn: "id_dossier",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "score_risque",
                columns: table => new
                {
                    id_score = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    id_dossier = table.Column<int>(type: "int", nullable: false),
                    valeur = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    points_retard = table.Column<int>(type: "int", nullable: false),
                    points_historique = table.Column<int>(type: "int", nullable: false),
                    points_garantie = table.Column<int>(type: "int", nullable: false),
                    points_intention = table.Column<int>(type: "int", nullable: false),
                    niveau = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    date_calcul = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    recommandation = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_score_risque", x => x.id_score);
                    table.ForeignKey(
                        name: "FK_score_risque_dossier_recouvrement_id_dossier",
                        column: x => x.id_dossier,
                        principalTable: "dossier_recouvrement",
                        principalColumn: "id_dossier",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "communication",
                columns: table => new
                {
                    id_communication = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    id_dossier = table.Column<int>(type: "int", nullable: false),
                    message = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    origine = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    date_envoi = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    id_relance = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_communication", x => x.id_communication);
                    table.ForeignKey(
                        name: "FK_communication_dossier_recouvrement_id_dossier",
                        column: x => x.id_dossier,
                        principalTable: "dossier_recouvrement",
                        principalColumn: "id_dossier",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_communication_relance_client_id_relance",
                        column: x => x.id_relance,
                        principalTable: "relance_client",
                        principalColumn: "id_relance");
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_client_id_agence",
                table: "client",
                column: "id_agence");

            migrationBuilder.CreateIndex(
                name: "IX_communication_id_dossier",
                table: "communication",
                column: "id_dossier");

            migrationBuilder.CreateIndex(
                name: "IX_communication_id_relance",
                table: "communication",
                column: "id_relance");

            migrationBuilder.CreateIndex(
                name: "IX_dossier_recouvrement_id_client",
                table: "dossier_recouvrement",
                column: "id_client");

            migrationBuilder.CreateIndex(
                name: "IX_echeance_id_dossier",
                table: "echeance",
                column: "id_dossier");

            migrationBuilder.CreateIndex(
                name: "IX_garantie_id_dossier",
                table: "garantie",
                column: "id_dossier");

            migrationBuilder.CreateIndex(
                name: "IX_historique_action_id_dossier",
                table: "historique_action",
                column: "id_dossier");

            migrationBuilder.CreateIndex(
                name: "IX_historique_paiement_id_dossier",
                table: "historique_paiement",
                column: "id_dossier");

            migrationBuilder.CreateIndex(
                name: "IX_intention_client_id_dossier",
                table: "intention_client",
                column: "id_dossier");

            migrationBuilder.CreateIndex(
                name: "IX_relance_client_id_dossier",
                table: "relance_client",
                column: "id_dossier");

            migrationBuilder.CreateIndex(
                name: "IX_score_risque_id_dossier",
                table: "score_risque",
                column: "id_dossier");

            migrationBuilder.CreateIndex(
                name: "IX_utilisateur_back_id_agence",
                table: "utilisateur_back",
                column: "id_agence");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "communication");

            migrationBuilder.DropTable(
                name: "echeance");

            migrationBuilder.DropTable(
                name: "garantie");

            migrationBuilder.DropTable(
                name: "historique_action");

            migrationBuilder.DropTable(
                name: "historique_paiement");

            migrationBuilder.DropTable(
                name: "intention_client");

            migrationBuilder.DropTable(
                name: "score_risque");

            migrationBuilder.DropTable(
                name: "utilisateur_back");

            migrationBuilder.DropTable(
                name: "relance_client");

            migrationBuilder.DropTable(
                name: "dossier_recouvrement");

            migrationBuilder.DropTable(
                name: "client");

            migrationBuilder.DropTable(
                name: "agence");
        }
    }
}
