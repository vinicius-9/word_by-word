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

        // ── Campos extras (preenchidos pelo recurso "Gerar Palavra") ──
        // Ficam vazios em palavras antigas cadastradas manualmente, sem problema — a tela de
        // detalhe já esconde as seções correspondentes quando estão vazias.
        // IMPORTANTE: esses campos SÃO gravados no banco SQLite (dicionario.db), veja
        // DicionarioServico. Se ficarem de fora, a tela de detalhe perde "Significados por
        // contexto"/Expressões/Dica ao reabrir o app, porque eles não seriam recarregados.
        public string? Traducao      { get; set; }
        public string? IdeiaPrincipal{ get; set; }
        public List<string> Significados { get; set; } = new();
        public List<string> Exemplos     { get; set; } = new();
        public List<string> Expressoes   { get; set; } = new();
        public string? Dica          { get; set; }

        /// <summary>
        /// Os diferentes sentidos da palavra em inglês (ex.: "work" = trabalhar / funcionar /
        /// malhar), cada um com seu próprio exemplo e uma dica de quando usar aquele sentido.
        /// Fica vazio em palavras que só têm um significado, ou em palavras antigas.
        /// </summary>
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
