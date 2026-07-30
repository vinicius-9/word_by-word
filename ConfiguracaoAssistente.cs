using System;
using System.IO;
using System.Text.Json;

namespace WordByWord
{
    /// <summary>Configuração do Assistente (chave de API, modelo etc.), salva em JSON na pasta de dados do usuário.</summary>
    public class ConfiguracaoAssistente
    {
        public string ApiKey { get; set; } = "";
        public string Modelo { get; set; } = "gemini-2.0-flash";

        /// <summary>Nível de inglês do usuário no bate-papo ("iniciante"/"intermediario"/"avancado"), evolui sozinho via GeminiServico.</summary>
        public string NivelConversa { get; set; } = "iniciante";

        /// <summary>Voz do Edge-TTS usada no botão "Ouvir pronúncia".</summary>
        public string VozId { get; set; } = "en-US-AvaMultilingualNeural";

        private static readonly string _pasta = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "WordByWord");

        private static readonly string _arquivo = Path.Combine(_pasta, "assistente_config.json");

        public bool TemChave => !string.IsNullOrWhiteSpace(ApiKey);

        public static ConfiguracaoAssistente Carregar()
        {
            try
            {
                if (File.Exists(_arquivo))
                {
                    var json = File.ReadAllText(_arquivo);
                    var cfg = JsonSerializer.Deserialize<ConfiguracaoAssistente>(json);
                    if (cfg != null) return cfg;
                }
            }
            catch
            {
                // arquivo corrompido/ilegível
            }

            return new ConfiguracaoAssistente();
        }

        public void Salvar()
        {
            Directory.CreateDirectory(_pasta);
            var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_arquivo, json);
        }
    }
}
