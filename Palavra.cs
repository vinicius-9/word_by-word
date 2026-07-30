using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace WordByWord
{
    public class Palavra : INotifyPropertyChanged
    {
        private string _palavraTexto = "";
        private string _significado  = "";
        private string _exemplo      = "";

        public string PalavraTexto
        {
            get => _palavraTexto;
            set { _palavraTexto = value; OnPropertyChanged(); }
        }

        public string Significado
        {
            get => _significado;
            set { _significado = value; OnPropertyChanged(); }
        }

        public string Exemplo
        {
            get => _exemplo;
            set { _exemplo = value; OnPropertyChanged(); }
        }

        // Campos extras preenchidos pelo recurso "Gerar Palavra" (também gravados no SQLite, ver DicionarioServico)
        public string? Traducao      { get; set; }
        public string? IdeiaPrincipal{ get; set; }
        public List<string> Significados { get; set; } = new();
        public List<string> Exemplos     { get; set; } = new();
        public List<string> Expressoes   { get; set; } = new();
        public string? Dica          { get; set; }

        /// <summary>Sentidos diferentes da palavra (ex.: "work" = trabalhar/funcionar/malhar).</summary>
        public List<SentidoPalavra> Sentidos { get; set; } = new();

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    /// <summary>Um dos possíveis significados de uma palavra com múltiplos sentidos.</summary>
    public class SentidoPalavra
    {
        /// <summary>Ex.: "trabalhar", "funcionar", "malhar (fazer exercício)".</summary>
        public string Significado { get; set; } = "";

        /// <summary>Frase de exemplo já com a tradução, ex.: "I work at a bank. = Eu trabalho num banco."</summary>
        public string Exemplo { get; set; } = "";

        /// <summary>Dica curta de quando usar esse sentido específico (o que diferencia dos outros).</summary>
        public string QuandoUsar { get; set; } = "";
    }
}
