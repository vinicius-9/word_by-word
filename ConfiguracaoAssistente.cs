using System;
using System.IO;
using System.Text.Json;

namespace WordByWord
{
    /// <summary>
    /// Guarda a configuração do Assistente (chave de API e modelo usado)
    /// em um arquivo JSON dentro da pasta de dados do usuário do Windows
   
    ///
    /// Isso evita que a chave fique gravada dentro da pasta do projeto/instalação
  
    /// </summary>
    public class ConfiguracaoAssistente
    {
        public string ApiKey { get; set; } = "";
        public string Modelo { get; set; } = "gemini-2.0-flash";

        /// <summary>
        /// Nível de inglês do usuário no bate-papo ("iniciante", "intermediario" ou
        /// "avancado"). Evolui sozinho conforme a IA percebe o progresso da pessoa
        /// (ver GeminiServico) — não tem seletor manual, pra não virar mais uma
        /// categoria pra escolher.
        /// </summary>
        public string NivelConversa { get; set; } = "iniciante";

        /// <summary>
        /// Voz do Edge-TTS usada para ler as palavras em voz alta (botão "Ouvir pronúncia" na
        /// lista/detalhe de palavras). O Modo Áudio (bate-papo falado) foi removido da tela
        /// principal, então hoje essa voz só é usada pra pronúncia. "Multilingual" = entende/
        /// fala tanto inglês quanto português com a mesma voz.
        /// </summary>
        public string VozId { get; set; } = "en-US-AvaMultilingualNeural";

        /// <summary>
        /// Último idioma em que o reconhecimento de voz (microfone) do Modo Áudio entendeu a
        /// pessoa com sucesso ("en-US" ou "pt-BR"). A troca entre os dois é automática durante
        /// a conversa (ver AssistentePainel.TrocarIdiomaAutomaticamente) — isso aqui só guarda
        /// por qual idioma começar a escutar da próxima vez que o Modo Áudio for aberto.
        /// </summary>
        public string IdiomaEscuta { get; set; } = "en-US";

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
                // arquivo corrompido/ilegível: segue com configuração vazia
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
