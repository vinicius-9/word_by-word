using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace WordByWord
{
    /// <summary>
    /// Representa uma mensagem exibida no chat do Assistente
    /// (tanto a pergunta do usuário quanto a resposta).
    /// </summary>
    public class MensagemChat : INotifyPropertyChanged
    {
        private string _texto = "";

        public string Texto
        {
            get => _texto;
            set { _texto = value; OnPropertyChanged(); }
        }

        /// <summary>true = mensagem do usuário (balão à direita) / false = do assistente (balão à esquerda)</summary>
        public bool EhUsuario { get; set; }

        /// <summary>true enquanto a resposta ainda está sendo gerada ("Pensando...")</summary>
        private bool _carregando;
        public bool Carregando
        {
            get => _carregando;
            set { _carregando = value; OnPropertyChanged(); }
        }

        /// <summary>true = essa mensagem pertence ao modo bate-papo (mostra o link "Traduzir")</summary>
        public bool ModoConversacao { get; set; }

        private string? _traducao;
        public string? Traducao
        {
            get => _traducao;
            set { _traducao = value; OnPropertyChanged(); OnPropertyChanged(nameof(JaTraduzido)); OnPropertyChanged(nameof(TemTraducao)); }
        }

        /// <summary>true assim que a tradução já foi buscada pelo menos uma vez.</summary>
        public bool JaTraduzido => !string.IsNullOrEmpty(Traducao);

        private bool _mostrandoTraducao;
        /// <summary>Alterna entre mostrar a tradução ou o texto original (link "Traduzir" / "Ver original").</summary>
        public bool MostrandoTraducao
        {
            get => _mostrandoTraducao;
            set { _mostrandoTraducao = value; OnPropertyChanged(); OnPropertyChanged(nameof(TemTraducao)); }
        }

        /// <summary>true = deve exibir a tradução agora (já buscada e o usuário quer vê-la).</summary>
        public bool TemTraducao => JaTraduzido && MostrandoTraducao;

        private bool _traduzindo;
        public bool Traduzindo
        {
            get => _traduzindo;
            set { _traduzindo = value; OnPropertyChanged(); }
        }

        public DateTime Horario { get; set; } = DateTime.Now;

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
