from fastapi import FastAPI, HTTPException
from fastapi.middleware.cors import CORSMiddleware
from pydantic import BaseModel
import joblib
import json
import numpy as np
import httpx
import os
os.environ["OPENBLAS_NUM_THREADS"] = "1"
# ── Chargement modèle ─────────────────────────────────────────────────────────
BASE = os.path.dirname(os.path.abspath(__file__))
model     = joblib.load(f"{BASE}/xgboost_model.pkl")
le_type   = joblib.load(f"{BASE}/le_type.pkl")
le_statut = joblib.load(f"{BASE}/le_statut.pkl")
le_target = joblib.load(f"{BASE}/le_target.pkl")
with open(f"{BASE}/model_meta.json") as f:
    meta = json.load(f)

app = FastAPI(title="STB Scoring IA", version="2.0.0")

app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_methods=["*"],
    allow_headers=["*"],
)
@app.get("/score-by-dossier/{id_dossier}")
async def score_by_dossier(id_dossier: int):
    # Retourne juste les dernières probabilités calculées depuis model_meta
    return {
        "id_dossier": id_dossier,
        "probabilites": {"Faible": 0.0, "Moyen": 0.0, "Eleve": 0.0},
        "note": "recalcule via POST /score pour avoir les vraies valeurs"
    }

# ── Schéma d'entrée ───────────────────────────────────────────────────────────
class DossierInput(BaseModel):
    # ── dossier_recouvrement
    montant_initial:        float
    montant_impaye:         float
    frais_dossier:          float
    taux_interet:           float
    confiance_client:       float
    type_emprunt:           str    # personnel | immobilier | automobile | professionnel
    statut_dossier:         str    # amiable | contentieux | regularise
    date_creation:          str    # YYYY-MM-DD

    # ── echeances (agrégées)
    nb_echeances_total:     int
    nb_echeances_impayees:  int
    moyenne_jours_retard:   float
    montant_total_du:       float

    # ── garanties
    nb_garanties:           int

    # ── historique_actions
    nb_actions:             int

    # ── intention_client (agrégées)
    nb_intentions:          int
    montant_propose_moyen:  float
    confiance_intention_moy: float

# ── Schéma de sortie ──────────────────────────────────────────────────────────
class ScoreResponse(BaseModel):
    score_numerique:   int
    categorie_risque:  str
    probabilites:      dict
    recommandation:    str
    modele:            str

# ── Helpers ───────────────────────────────────────────────────────────────────
def encode_safe(le, value: str, field: str):
    if value not in le.classes_:
        raise HTTPException(400, f"Valeur inconnue pour '{field}': '{value}'. Valeurs acceptées: {list(le.classes_)}")
    return int(le.transform([value])[0])

def score_from_proba(probas: np.ndarray, classes: list) -> int:
    weights = {"Faible": 10, "Moyen": 50, "Eleve": 90}
    score = sum(probas[i] * weights[cls] for i, cls in enumerate(classes))
    return int(np.clip(round(score), 0, 100))

async def get_recommandation(score: int, categorie: str, d: DossierInput):
    prompt = f"""Tu es un expert en recouvrement bancaire à la STB (Société Tunisienne de Banque).
Le modèle XGBoost a analysé ce dossier :

- Score de risque : {score}/100
- Catégorie : {categorie}
- Montant impayé : {d.montant_impaye:.2f} TND
- Type d'emprunt : {d.type_emprunt}
- Statut : {d.statut_dossier}
- Échéances impayées : {d.nb_echeances_impayees}/{d.nb_echeances_total}
- Retard moyen : {d.moyenne_jours_retard:.0f} jours
- Garanties : {d.nb_garanties}
- Actions effectuées : {d.nb_actions}
- Intentions de paiement : {d.nb_intentions}

En 2-3 phrases maximum, propose une action de recouvrement concrète et adaptée. Réponds directement."""

    try:
        async with httpx.AsyncClient(timeout=15.0) as client:
            response = await client.post(
                "http://localhost:11434/api/generate",
                json={"model": "llama3.2", "prompt": prompt, "stream": False}
            )
            if response.status_code == 200:
                return response.json().get("response", "").strip(), "XGBoost + Ollama"
    except Exception:
        pass

    fallback = {
        "Faible": "Risque faible. Envoyer une relance amiable par email ou SMS et proposer un échéancier adapté.",
        "Moyen":  "Risque modéré. Effectuer un appel téléphonique, envoyer une mise en demeure et surveiller de près.",
        "Eleve":  "Risque élevé. Escalader vers le service contentieux et engager une procédure juridique immédiatement."
    }
    return fallback[categorie], "fallback"

# ── Endpoint principal ────────────────────────────────────────────────────────

async def get_recommandation(score: int, categorie: str, d: DossierInput):
    prompt = f"""Tu es un expert en recouvrement bancaire à la STB (Société Tunisienne de Banque).
Le modèle XGBoost a analysé ce dossier :

- Score de risque : {score}/100
- Catégorie : {categorie}
- Montant impayé : {d.montant_impaye:.2f} TND
- Type d'emprunt : {d.type_emprunt}
- Statut : {d.statut_dossier}
- Échéances impayées : {d.nb_echeances_impayees}/{d.nb_echeances_total}
- Retard moyen : {d.moyenne_jours_retard:.0f} jours
- Garanties : {d.nb_garanties}
- Actions effectuées : {d.nb_actions}
- Intentions de paiement : {d.nb_intentions}

En 2-3 phrases maximum, propose une action de recouvrement concrète et adaptée. Réponds directement."""

    try:
        async with httpx.AsyncClient(timeout=15.0) as client:
            response = await client.post(
                "http://localhost:11434/api/generate",
                json={"model": "llama3.2", "prompt": prompt, "stream": False}
            )
            if response.status_code == 200:
                return response.json().get("response", "").strip(), "XGBoost + Ollama"
    except Exception:
        pass

    fallback = {
        "Faible": "Risque faible. Envoyer une relance amiable par email ou SMS et proposer un échéancier adapté.",
        "Moyen":  "Risque modéré. Effectuer un appel téléphonique, envoyer une mise en demeure et surveiller de près.",
        "Eleve":  "Risque élevé. Escalader vers le service contentieux et engager une procédure juridique immédiatement."
    }
    return fallback[categorie], "fallback"


class ScoreResponse(BaseModel):
    score_numerique:   int
    categorie_risque:  str
    probabilites:      dict
    recommandation:    str
    modele:            str


@app.post("/score", response_model=ScoreResponse)
async def score_dossier(d: DossierInput):
    from datetime import datetime

    jours = (datetime.today() - datetime.strptime(d.date_creation, "%Y-%m-%d")).days

    ratio_impaye         = d.montant_impaye / max(d.montant_initial, 1)
    charge_frais         = d.frais_dossier  / max(d.montant_initial, 1)
    taux_recouvrement    = 1 - ratio_impaye
    ratio_echeances_imp  = d.nb_echeances_impayees / max(d.nb_echeances_total, 1)
    couverture_intention = d.montant_propose_moyen / max(d.montant_impaye, 1)
    intensite_actions    = d.nb_actions / max(jours / 30, 1)
    confiance_globale    = (d.confiance_client + d.confiance_intention_moy) / 2

    type_enc   = encode_safe(le_type,   d.type_emprunt,   "type_emprunt")
    statut_enc = encode_safe(le_statut, d.statut_dossier, "statut_dossier")

    X = np.array([[
        d.montant_initial, d.montant_impaye, d.frais_dossier,
        d.taux_interet, d.confiance_client,
        jours, type_enc, statut_enc,
        d.nb_echeances_total, d.nb_echeances_impayees,
        d.moyenne_jours_retard, d.montant_total_du,
        d.nb_garanties,
        d.nb_actions,
        d.nb_intentions, d.montant_propose_moyen, d.confiance_intention_moy,
        ratio_impaye, charge_frais, taux_recouvrement,
        ratio_echeances_imp, couverture_intention,
        intensite_actions, confiance_globale,
    ]])

    probas    = model.predict_proba(X)[0]
    classes   = list(le_target.classes_)
    pred_idx  = int(np.argmax(probas))
    categorie = classes[pred_idx]
    score     = score_from_proba(probas, classes)

    recommandation, modele = await get_recommandation(score, categorie, d)

    return ScoreResponse(
        score_numerique=score,
        categorie_risque=categorie,
        probabilites={cls: round(float(p), 4) for cls, p in zip(classes, probas)},
        recommandation=recommandation,
        modele=modele
    )
@app.get("/health")
def health():
    return {"status": "ok", "modele": "XGBoost", "classes": meta["classes"]}

@app.get("/")
def root():
    return {"message": "STB Scoring IA v2 — /score (POST) | /docs (Swagger)"}
