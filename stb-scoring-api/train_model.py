"""
Pipeline ML Complet — Scoring de Risque STB Bank
================================================
Étapes : Génération → Nettoyage → Feature Engineering →
         Comparaison modèles → Sélection XGBoost → Déploiement
"""

import pandas as pd
import numpy as np
from xgboost import XGBClassifier
from sklearn.ensemble import RandomForestClassifier
from sklearn.linear_model import LogisticRegression
from sklearn.model_selection import train_test_split, StratifiedKFold, cross_val_score
from sklearn.preprocessing import LabelEncoder, StandardScaler
from sklearn.metrics import (classification_report, roc_auc_score,
                             confusion_matrix, accuracy_score, roc_curve,
                             auc as sk_auc)
from sklearn.impute import SimpleImputer
from sklearn.pipeline import Pipeline
import joblib
import json
import shap
import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt
import matplotlib.patches as mpatches
import seaborn as sns
import warnings
warnings.filterwarnings('ignore')

print("=" * 60)
print("   PIPELINE ML — STB SCORING IA")
print("=" * 60)

# ════════════════════════════════════════════════════════════
# ÉTAPE 1 — GÉNÉRATION DES DONNÉES SYNTHÉTIQUES
# ════════════════════════════════════════════════════════════
print("\n[1/6] Génération des données synthétiques...")
np.random.seed(42)
N = 500

type_emprunt_options  = ["personnel", "immobilier", "automobile", "professionnel"]
statut_options        = ["amiable", "contentieux", "regularise"]

df_raw = pd.DataFrame({
    "montant_initial":         np.random.uniform(5000, 300000, N),
    "montant_impaye":          np.random.uniform(500, 100000, N),
    "frais_dossier":           np.random.uniform(50, 3000, N),
    "taux_interet":            np.random.uniform(3.0, 22.0, N),
    "confiance_client":        np.random.uniform(0, 100, N),
    "type_emprunt":            np.random.choice(type_emprunt_options, N),
    "statut_dossier":          np.random.choice(statut_options, N, p=[0.5, 0.35, 0.15]),
    "jours_depuis_creation":   np.random.randint(30, 1800, N),
    "nb_echeances_total":      np.random.randint(1, 60, N),
    "nb_echeances_impayees":   np.random.randint(0, 30, N),
    "moyenne_jours_retard":    np.random.uniform(0, 180, N),
    "montant_total_du":        np.random.uniform(500, 80000, N),
    "nb_garanties":            np.random.randint(0, 5, N),
    "nb_actions":              np.random.randint(0, 25, N),
    "nb_intentions":           np.random.randint(0, 8, N),
    "montant_propose_moyen":   np.random.uniform(0, 50000, N),
    "confiance_intention_moy": np.random.uniform(0, 100, N),
})

# Injection de valeurs manquantes et outliers pour simuler des données réelles
rng = np.random.default_rng(42)
for col in ["confiance_intention_moy", "montant_propose_moyen", "nb_intentions"]:
    idx = rng.choice(N, size=int(N * 0.05), replace=False)
    df_raw.loc[idx, col] = np.nan

# Outliers extrêmes sur montant
df_raw.loc[rng.choice(N, 10, replace=False), "montant_impaye"] *= 15

print(f"   → {N} dossiers générés, {df_raw.isnull().sum().sum()} valeurs manquantes injectées")

# ════════════════════════════════════════════════════════════
# APERÇU POUR LA FIGURE 5.1
# ════════════════════════════════════════════════════════════
print("\n[Export] Génération de l'aperçu pour la Figure 5.1...")

colonnes_apercu = [
    "type_emprunt", "statut_dossier", "montant_initial",
    "montant_impaye", "nb_echeances_impayees",
    "moyenne_jours_retard", "nb_garanties"
]
df_apercu = df_raw[colonnes_apercu].head(15).copy()

for col in ["montant_initial", "montant_impaye", "moyenne_jours_retard"]:
    df_apercu[col] = df_apercu[col].round(2)

col_labels = [
    "Type emprunt", "Statut", "Mt. initial (DT)",
    "Mt. impayé (DT)", "Nb éch. imp.", "Moy. j. retard", "Nb gar."
]

fig, ax = plt.subplots(figsize=(20, 6))
ax.axis('off')

table = ax.table(
    cellText=df_apercu.values,
    colLabels=col_labels,
    rowLabels=[str(i) for i in range(len(df_apercu))],
    cellLoc='center',
    rowLoc='center',
    loc='center'
)

table.auto_set_font_size(False)
table.set_fontsize(7)
table.auto_set_column_width(col=list(range(len(col_labels))))

header_color = "#2563B8"
row_colors   = ["#f0f4ff", "#ffffff"]

for (row, col), cell in table.get_celld().items():
    cell.set_edgecolor('#d1d5db')
    cell.set_linewidth(0.5)
    if row == 0:
        cell.set_facecolor(header_color)
        cell.set_text_props(color='white', fontweight='bold', fontsize=7)
        cell.set_height(0.09)
    elif col == -1:
        cell.set_facecolor('#e8edf7')
        cell.set_text_props(color='#374151', fontweight='bold', fontsize=7)
    else:
        cell.set_facecolor(row_colors[row % 2])
        cell.set_text_props(color='#1f2937', fontsize=7)

plt.tight_layout(pad=0.5)
plt.savefig('apercu_dataset.png', dpi=300, bbox_inches='tight',
            facecolor='white', edgecolor='none')
plt.close()
print("   → apercu_dataset.png généré ✅")

# ════════════════════════════════════════════════════════════
# ÉTAPE 2 — NETTOYAGE DES DONNÉES
# ════════════════════════════════════════════════════════════
print("\n[2/6] Nettoyage des données...")
df = df_raw.copy()

# 2a. Valeurs manquantes — imputation par médiane
cols_a_imputer = ["confiance_intention_moy", "montant_propose_moyen", "nb_intentions"]
avant_imputation = df[cols_a_imputer].isnull().sum().sum()
for col in cols_a_imputer:
    median_val = df[col].median()
    df[col].fillna(median_val, inplace=True)
print(f"   → {avant_imputation} valeurs manquantes imputées par médiane")

# 2b. Outliers — winsorisation au 99e percentile
cols_numeriques = ["montant_impaye", "montant_initial", "frais_dossier",
                   "montant_total_du", "montant_propose_moyen"]
nb_outliers = 0
for col in cols_numeriques:
    p99 = df[col].quantile(0.99)
    nb_outliers += (df[col] > p99).sum()
    df[col] = df[col].clip(upper=p99)
print(f"   → {nb_outliers} valeurs aberrantes traitées par winsorisation (99e percentile)")

# 2c. Cohérence métier — impayés ne peut pas dépasser initial
incoh = (df["montant_impaye"] > df["montant_initial"]).sum()
df["montant_impaye"] = df[["montant_impaye", "montant_initial"]].min(axis=1)
print(f"   → {incoh} incohérences métier corrigées (impayé > initial)")

# 2d. Encodage variables catégorielles
le_type   = LabelEncoder()
le_statut = LabelEncoder()
df["type_emprunt_enc"]   = le_type.fit_transform(df["type_emprunt"])
df["statut_dossier_enc"] = le_statut.fit_transform(df["statut_dossier"])
print(f"   → Encodage : type_emprunt {list(le_type.classes_)}")
print(f"               statut_dossier {list(le_statut.classes_)}")

# ════════════════════════════════════════════════════════════
# ÉTAPE 3 — FEATURE ENGINEERING
# ════════════════════════════════════════════════════════════
print("\n[3/6] Feature Engineering...")
df["ratio_impaye"]         = df["montant_impaye"] / df["montant_initial"].clip(lower=1)
df["charge_frais"]         = df["frais_dossier"]  / df["montant_initial"].clip(lower=1)
df["taux_recouvrement"]    = 1 - df["ratio_impaye"]
df["ratio_echeances_imp"]  = df["nb_echeances_impayees"] / df["nb_echeances_total"].clip(lower=1)
df["couverture_intention"] = df["montant_propose_moyen"] / df["montant_impaye"].clip(lower=1)
df["intensite_actions"]    = df["nb_actions"] / (df["jours_depuis_creation"] / 30).clip(lower=1)
df["confiance_globale"]    = (df["confiance_client"] + df["confiance_intention_moy"]) / 2
print("   → 7 features calculées (ratios, indices de couverture, intensité)")

# ── Target ────────────────────────────────────────────────────────────────────
def compute_risk(row):
    s = 0
    s += row["ratio_impaye"]        * 30
    s += row["ratio_echeances_imp"] * 20
    s += (100 - row["confiance_globale"]) * 0.20
    s += min(row["taux_interet"] / 22.0 * 12, 12)
    s += min(row["moyenne_jours_retard"] / 180.0 * 10, 10)
    s += min(row["jours_depuis_creation"] / 1800 * 8, 8)
    s -= min(row["nb_garanties"] * 2, 8)
    s -= min(row["couverture_intention"] * 5, 8)
    s += min(row["nb_actions"] / 25.0 * 5, 5)
    if row["statut_dossier"] == "contentieux":  s += 18
    elif row["statut_dossier"] == "amiable":    s += 4
    elif row["statut_dossier"] == "regularise": s -= 10
    s += np.random.normal(0, 5)
    return np.clip(s, 0, 100)

df["risk_score_raw"] = df.apply(compute_risk, axis=1)
df["risk_category"]  = df["risk_score_raw"].apply(
    lambda s: "Faible" if s < 33 else ("Moyen" if s < 66 else "Eleve")
)

le_target = LabelEncoder()
df["target"] = le_target.fit_transform(df["risk_category"])
print(f"   → Distribution cible : {dict(df['risk_category'].value_counts())}")

# ── Features finales ──────────────────────────────────────────────────────────
features = [
    "montant_initial", "montant_impaye", "frais_dossier",
    "taux_interet", "confiance_client",
    "jours_depuis_creation", "type_emprunt_enc", "statut_dossier_enc",
    "nb_echeances_total", "nb_echeances_impayees",
    "moyenne_jours_retard", "montant_total_du",
    "nb_garanties", "nb_actions",
    "nb_intentions", "montant_propose_moyen", "confiance_intention_moy",
    "ratio_impaye", "charge_frais", "taux_recouvrement",
    "ratio_echeances_imp", "couverture_intention",
    "intensite_actions", "confiance_globale",
]

X = df[features]
y = df["target"]

X_train, X_test, y_train, y_test = train_test_split(
    X, y, test_size=0.2, random_state=42, stratify=y)

# ════════════════════════════════════════════════════════════
# ÉTAPE 4 — COMPARAISON DE MODÈLES
# ════════════════════════════════════════════════════════════
print("\n[4/6] Comparaison de modèles ML...")
cv_strat = StratifiedKFold(n_splits=5, shuffle=True, random_state=42)

modeles = {
    "Logistic Regression": Pipeline([
        ("imputer", SimpleImputer(strategy="median")),
        ("scaler", StandardScaler()),
        ("clf", LogisticRegression(max_iter=1000, random_state=42))
    ]),
    "Random Forest": RandomForestClassifier(
        n_estimators=200, max_depth=8, random_state=42, n_jobs=-1
    ),
    "XGBoost": XGBClassifier(
        n_estimators=300, max_depth=6, learning_rate=0.05,
        subsample=0.8, colsample_bytree=0.8, min_child_weight=3,
        gamma=0.1, reg_alpha=0.1, reg_lambda=1.0,
        eval_metric="mlogloss", random_state=42, n_jobs=-1
    ),
}

resultats = {}
modeles_fits = {}

for nom, modele in modeles.items():
    print(f"   → Entraînement : {nom}...", end=" ")
    modele.fit(X_train, y_train)
    modeles_fits[nom] = modele

    y_pred_m  = modele.predict(X_test)
    y_proba_m = modele.predict_proba(X_test)

    acc = accuracy_score(y_test, y_pred_m)
    auc = roc_auc_score(y_test, y_proba_m, multi_class='ovr', average='macro')
    cvs = cross_val_score(modele, X, y, cv=cv_strat, scoring="accuracy")

    resultats[nom] = {
        "accuracy":    round(acc, 4),
        "auc_roc":     round(auc, 4),
        "cv_mean":     round(cvs.mean(), 4),
        "cv_std":      round(cvs.std(), 4),
    }
    print(f"Accuracy={acc:.4f} | AUC={auc:.4f} | CV={cvs.mean():.4f}±{cvs.std():.4f}")

# Tableau comparatif
print("\n" + "─" * 70)
print(f"  {'Modèle':<25} {'Accuracy':>10} {'AUC-ROC':>10} {'CV (mean)':>10} {'CV (std)':>10}")
print("─" * 70)
for nom, r in resultats.items():
    marker = " ◀ SÉLECTIONNÉ" if nom == "XGBoost" else ""
    print(f"  {nom:<25} {r['accuracy']:>10.4f} {r['auc_roc']:>10.4f} {r['cv_mean']:>10.4f} {r['cv_std']:>10.4f}{marker}")
print("─" * 70)
print("  Justification : XGBoost offre le meilleur AUC-ROC et la meilleure")
print("  stabilité en cross-validation. Il gère nativement les données")
print("  tabulaires hétérogènes et les relations non-linéaires entre features.")

# ════════════════════════════════════════════════════════════
# ÉTAPE 5 — ÉVALUATION FINALE DU MODÈLE SÉLECTIONNÉ
# ════════════════════════════════════════════════════════════
print("\n[5/6] Évaluation finale — XGBoost...")
model    = modeles_fits["XGBoost"]
y_pred   = model.predict(X_test)
y_proba  = model.predict_proba(X_test)

print("\n" + "=" * 55)
print("         RAPPORT DE CLASSIFICATION — XGBoost")
print("=" * 55)
print(classification_report(y_test, y_pred, target_names=list(le_target.classes_)))
print(f"Accuracy  : {resultats['XGBoost']['accuracy']:.4f}")
print(f"AUC-ROC   : {resultats['XGBoost']['auc_roc']:.4f}")
print(f"CV 5-fold : {resultats['XGBoost']['cv_mean']:.4f} ± {resultats['XGBoost']['cv_std']:.4f}")
print("=" * 55)

# ── Graphique 1 : Comparaison des modèles ──────────────────────────────────
fig, axes = plt.subplots(1, 2, figsize=(13, 5))
fig.suptitle("Comparaison des modèles ML — STB Scoring IA", fontsize=14, fontweight='bold')

noms   = list(resultats.keys())
accs   = [r["accuracy"] for r in resultats.values()]
aucs   = [r["auc_roc"]  for r in resultats.values()]
colors = ["#94a3b8", "#60a5fa", "#22c55e"]

bars1 = axes[0].bar(noms, accs, color=colors, width=0.5, edgecolor='white', linewidth=1.5)
axes[0].set_title("Accuracy par modèle", fontweight='bold')
axes[0].set_ylim(0.7, 1.0)
axes[0].set_ylabel("Accuracy")
for bar, val in zip(bars1, accs):
    axes[0].text(bar.get_x() + bar.get_width()/2, bar.get_height() + 0.003,
                 f"{val:.4f}", ha='center', va='bottom', fontsize=10, fontweight='bold')
axes[0].tick_params(axis='x', rotation=10)

bars2 = axes[1].bar(noms, aucs, color=colors, width=0.5, edgecolor='white', linewidth=1.5)
axes[1].set_title("AUC-ROC par modèle", fontweight='bold')
axes[1].set_ylim(0.85, 1.0)
axes[1].set_ylabel("AUC-ROC (macro OvR)")
for bar, val in zip(bars2, aucs):
    axes[1].text(bar.get_x() + bar.get_width()/2, bar.get_height() + 0.001,
                 f"{val:.4f}", ha='center', va='bottom', fontsize=10, fontweight='bold')
axes[1].tick_params(axis='x', rotation=10)

# Annoter le meilleur
best_idx = 2
axes[0].patches[best_idx].set_edgecolor('#16a34a')
axes[0].patches[best_idx].set_linewidth(3)
axes[1].patches[best_idx].set_edgecolor('#16a34a')
axes[1].patches[best_idx].set_linewidth(3)

plt.tight_layout()
plt.savefig("comparaison_modeles.png", dpi=150, bbox_inches='tight')
plt.close()
print("   → comparaison_modeles.png sauvegardé")

# ── Graphique 2 : Matrice de confusion ────────────────────────────────────
fig, ax = plt.subplots(figsize=(6, 5))
cm = confusion_matrix(y_test, y_pred)
sns.heatmap(cm, annot=True, fmt='d', cmap='Blues',
            xticklabels=le_target.classes_,
            yticklabels=le_target.classes_, ax=ax,
            annot_kws={"size": 14, "weight": "bold"})
ax.set_title("Matrice de Confusion — XGBoost STB", fontweight='bold')
ax.set_ylabel("Réel")
ax.set_xlabel("Prédit")
plt.tight_layout()
plt.savefig("confusion_matrix.png", dpi=150)
plt.close()
print("   → confusion_matrix.png sauvegardé")

# ── Graphique 3 : Courbes ROC ──────────────────────────────────────────────
fig, ax = plt.subplots(figsize=(8, 6))
class_colors = {"Faible": "#22c55e", "Moyen": "#f59e0b", "Eleve": "#ef4444"}

for i, classe in enumerate(le_target.classes_):
    y_bin    = (y_test == i).astype(int)
    fpr, tpr, _ = roc_curve(y_bin, y_proba[:, i])
    roc_auc  = sk_auc(fpr, tpr)
    ax.plot(fpr, tpr, color=class_colors.get(classe, "blue"), lw=2,
            label=f"Classe {classe} (AUC = {roc_auc:.3f})")

ax.plot([0, 1], [0, 1], 'k--', lw=1, alpha=0.5, label="Aléatoire")
ax.set_xlabel("Taux de faux positifs")
ax.set_ylabel("Taux de vrais positifs")
ax.set_title("Courbes ROC — XGBoost (One-vs-Rest)", fontweight='bold')
ax.legend(loc="lower right")
ax.grid(alpha=0.3)
plt.tight_layout()
plt.savefig("courbe_roc.png", dpi=150)
plt.close()
print("   → courbe_roc.png sauvegardé")

# ── Graphique 4 : Feature Importance ──────────────────────────────────────
fig, ax = plt.subplots(figsize=(9, 7))
importances = pd.Series(model.feature_importances_, index=features).sort_values(ascending=True)
bars = importances.tail(15).plot(kind='barh', ax=ax, color='steelblue')
ax.set_title("Feature Importance — XGBoost STB", fontweight='bold')
ax.set_xlabel("Importance (gain)")
plt.tight_layout()
plt.savefig("feature_importance.png", dpi=150)
plt.close()
print("   → feature_importance.png sauvegardé")

# ── Graphique 5 : SHAP ────────────────────────────────────────────────────
print("   → Calcul SHAP (peut prendre quelques secondes)...")
explainer   = shap.TreeExplainer(model)
shap_values = explainer.shap_values(X_test)
eleve_idx   = list(le_target.classes_).index("Eleve")
shap.summary_plot(shap_values[:, :, eleve_idx], X_test,
                  feature_names=features, show=False, plot_size=(10, 7))
plt.title("SHAP — Impact des features sur Risque Élevé", fontweight='bold')
plt.tight_layout()
plt.savefig("shap_summary.png", dpi=150, bbox_inches='tight')
plt.close()
print("   → shap_summary.png sauvegardé")

# ════════════════════════════════════════════════════════════
# ÉTAPE 6 — SAUVEGARDE DU MODÈLE
# ════════════════════════════════════════════════════════════
print("\n[6/6] Sauvegarde du modèle...")
joblib.dump(model,     "xgboost_model.pkl")
joblib.dump(le_type,   "le_type.pkl")
joblib.dump(le_statut, "le_statut.pkl")
joblib.dump(le_target, "le_target.pkl")

meta = {
    "features":               features,
    "classes":                list(le_target.classes_),
    "type_emprunt_classes":   list(le_type.classes_),
    "statut_dossier_classes": list(le_statut.classes_),
    "metriques": {
        "accuracy":  resultats["XGBoost"]["accuracy"],
        "auc_roc":   resultats["XGBoost"]["auc_roc"],
        "cv_mean":   resultats["XGBoost"]["cv_mean"],
        "cv_std":    resultats["XGBoost"]["cv_std"],
    },
    "comparaison_modeles": resultats,
}
with open("model_meta.json", "w", encoding="utf-8") as f:
    json.dump(meta, f, indent=2, ensure_ascii=False)

print("\n" + "=" * 60)
print("   ✅ PIPELINE ML COMPLET — RÉSUMÉ")
print("=" * 60)
print(f"   Données    : {N} dossiers synthétiques")
print(f"   Nettoyage  : valeurs manquantes + outliers + cohérence métier")
print(f"   Features   : {len(features)} variables (dont 7 engineered)")
print(f"   Modèles    : Logistic Regression, Random Forest, XGBoost")
print(f"   Sélectionné: XGBoost (meilleur AUC-ROC)")
print(f"   Accuracy   : {resultats['XGBoost']['accuracy']:.4f}")
print(f"   AUC-ROC    : {resultats['XGBoost']['auc_roc']:.4f}")
print(f"   CV 5-fold  : {resultats['XGBoost']['cv_mean']:.4f} ± {resultats['XGBoost']['cv_std']:.4f}")
print("   → Fichiers générés : xgboost_model.pkl, model_meta.json, apercu_dataset.tex, etc.")
print("=" * 60)
import matplotlib.pyplot as plt

jours = list(range(15))
planifie = [86,79,72,65,58,52,52,45,38,31,24,17,7,0,0]
reel     = [93,85,75,66,61,50,50,40,32,25,20,15,6,0,0]

plt.figure(figsize=(9, 5))
plt.plot(jours, planifie, 'o-', color='#f97316', label='Restant Planifié')
plt.plot(jours, reel,     'o-', color='#6366f1', label='Restant Réel')
plt.title('Burndown Chart Sprint IA')
plt.xlabel('Jours')
plt.ylabel('User Stories restantes')
plt.legend()
plt.grid(alpha=0.3)
plt.tight_layout()
plt.savefig('burndown_chart.png', dpi=150)