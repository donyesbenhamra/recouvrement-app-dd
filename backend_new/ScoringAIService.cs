#nullable enable
using Microsoft.ML;
using RecouvrementAPI.Models.ML;

namespace RecouvrementAPI.Services
{
    public class ScoringAIService
    {
        private readonly MLContext _mlContext;
        private readonly string _modelPath = Path.Combine(
            AppContext.BaseDirectory, "MLModels", "scoring_model.zip");

        private ITransformer? _model;
        private PredictionEngine<ScoringInput, ScoringPrediction>? _engine;
        private readonly object _lock = new();

        public ScoringAIService()
        {
            _mlContext = new MLContext(seed: 0);

            if (File.Exists(_modelPath))
            {
                try
                {
                    _model = _mlContext.Model.Load(_modelPath, out _);
                    _engine = _mlContext.Model.CreatePredictionEngine<ScoringInput, ScoringPrediction>(_model);
                    Console.WriteLine("✅ Modèle IA chargé automatiquement depuis le disque.");
                }
                catch
                {
                    Console.WriteLine("⚠️ Fichier modèle trouvé mais impossible de le charger. Réentraînement requis.");
                }
            }
        }

        // =========================
        // 📌 ETAT DU MODELE
        // =========================
        public bool IsModelLoaded => _model != null;

        // =========================
        // 🧠 TRAINING
        // =========================
        public void TrainAndSave(List<ScoringInput> data)
        {
            if (data == null || data.Count < 10)
                throw new ArgumentException("Dataset insuffisant (min 10 recommandé).");

            // ✅ Créer le dossier EN PREMIER avant toute opération
            var modelDir = Path.GetDirectoryName(_modelPath)!;
            Directory.CreateDirectory(modelDir);

            var dataset = _mlContext.Data.LoadFromEnumerable(data);
            var split = _mlContext.Data.TrainTestSplit(dataset, testFraction: 0.2);

            var pipeline =
                _mlContext.Transforms.Concatenate("Features",
                    nameof(ScoringInput.Retard),
                    nameof(ScoringInput.Historique),
                    nameof(ScoringInput.Garantie),
                    nameof(ScoringInput.Intention))
                .Append(_mlContext.Transforms.NormalizeMinMax("Features"))
                .Append(_mlContext.Regression.Trainers.Sdca(
                    labelColumnName: "Label",
                    featureColumnName: "Features"));

            _model = pipeline.Fit(split.TrainSet);

            var predictions = _model.Transform(split.TestSet);
            var metrics = _mlContext.Regression.Evaluate(predictions, "Label");

            // ✅ Sauvegarde avec chemin absolu, dossier déjà créé
            _mlContext.Model.Save(_model, dataset.Schema, _modelPath);

            lock (_lock)
            {
                _engine?.Dispose();
                _engine = _mlContext.Model.CreatePredictionEngine<ScoringInput, ScoringPrediction>(_model);
            }

            Console.WriteLine($"✅ Modèle sauvegardé : {_modelPath}");
            Console.WriteLine($"📊 R² Score: {metrics.RSquared:0.##}");
        }

        // =========================
        // 📥 LOAD MODEL
        // =========================
        public void LoadModel()
        {
            if (!File.Exists(_modelPath))
                throw new InvalidOperationException($"Modèle non trouvé : {_modelPath}. Entraînement requis.");

            _model = _mlContext.Model.Load(_modelPath, out _);

            lock (_lock)
            {
                _engine?.Dispose();
                _engine = _mlContext.Model.CreatePredictionEngine<ScoringInput, ScoringPrediction>(_model);
            }

            Console.WriteLine("✅ Modèle chargé depuis le disque.");
        }

        // =========================
        // 🔮 PREDICTION
        // =========================
        public int Predict(ScoringInput input)
        {
            if (_model == null || _engine == null)
                throw new InvalidOperationException("Modèle non chargé. Appelez /train ou /load.");

            lock (_lock)
            {
                var result = _engine.Predict(input);
                return (int)Math.Clamp(result.Score, 0, 100);
            }
        }
    }
}