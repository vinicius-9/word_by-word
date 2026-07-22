using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace WordByWord
{
    /// <summary>
    /// Tela de exercícios de múltipla escolha pra praticar uma palavra já cadastrada
    /// no dicionário. Segue o mesmo padrão visual e de navegação das outras telas
    /// (cabeçalho + botão voltar + cards, igual Gerar Palavra/Assistente).
    /// </summary>
    public partial class ExerciciosPainel : UserControl
    {
        private readonly GeminiServico _servico = new();
        private ConfiguracaoAssistente _config = ConfiguracaoAssistente.Carregar();

        private Palavra? _palavra;
        private List<ExercicioPergunta> _perguntas = new();
        private int _indiceAtual;
        private int _acertos;
        private int _erros;

        public event EventHandler? VoltarClicado;
        public event EventHandler? PrecisaConfigurarChave;

        public ExerciciosPainel()
        {
            InitializeComponent();
        }

        /// <summary>Abre a tela e já começa a gerar os exercícios pra essa palavra.</summary>
        public async void Ativar(Palavra palavra)
        {
            _palavra = palavra;
            _config = ConfiguracaoAssistente.Carregar();
            lblStatus.Text = $"Praticando \"{palavra.PalavraTexto}\"";

            if (!_config.TemChave)
            {
                PrecisaConfigurarChave?.Invoke(this, EventArgs.Empty);
                return;
            }

            await GerarEComecarAsync();
        }

        private async System.Threading.Tasks.Task GerarEComecarAsync()
        {
            if (_palavra == null) return;

            MostrarEtapa(painelCarregando);

            try
            {
                _perguntas = await _servico.GerarExerciciosAsync(_config, _palavra, 15);
                _indiceAtual = 0;
                _acertos = 0;
                _erros = 0;
                MostrarPergunta();
            }
            catch (Exception ex)
            {
                MostrarEtapa(painelPergunta);
                lblPergunta.Text = $"⚠ {ex.Message}";
                listaAlternativas.ItemsSource = null;
            }
        }

        private void MostrarEtapa(FrameworkElement etapa)
        {
            painelCarregando.Visibility = etapa == painelCarregando ? Visibility.Visible : Visibility.Collapsed;
            painelPergunta.Visibility   = etapa == painelPergunta   ? Visibility.Visible : Visibility.Collapsed;
            scrollFeedback.Visibility   = etapa == scrollFeedback   ? Visibility.Visible : Visibility.Collapsed;
            painelResumo.Visibility     = etapa == painelResumo     ? Visibility.Visible : Visibility.Collapsed;

            // Sempre que uma etapa com rolagem entra em cena (nova pergunta ou feedback de
            // acerto/erro), volta pro topo dela. Sem isso, a rolagem ficava "lembrando" a
            // posição da etapa anterior e a pessoa via a tela vazia lá embaixo, precisando
            // subir manualmente pra ver o resultado.
            if (etapa is ScrollViewer scroll)
            {
                Dispatcher.InvokeAsync(() => scroll.ScrollToTop(),
                    System.Windows.Threading.DispatcherPriority.Background);
            }
        }

        private void Voltar_Click(object sender, RoutedEventArgs e) => VoltarClicado?.Invoke(this, EventArgs.Empty);

        private void PraticarDeNovo_Click(object sender, RoutedEventArgs e) => _ = GerarEComecarAsync();

        // ───────────────────────── PERGUNTA ─────────────────────────

        private void MostrarPergunta()
        {
            if (_palavra == null || _indiceAtual >= _perguntas.Count)
            {
                MostrarResumo();
                return;
            }

            var pergunta = _perguntas[_indiceAtual];

            lblPalavra.Text    = _palavra.PalavraTexto.ToUpperInvariant();
            lblProgresso.Text  = $"Exercício {_indiceAtual + 1}/{_perguntas.Count}";
            lblPlacar.Text     = $"✅ {_acertos}   ❌ {_erros}";
            lblPergunta.Text   = pergunta.Pergunta;

            var botoes = new List<Button>();
            for (int i = 0; i < pergunta.Alternativas.Count; i++)
            {
                var indice = i;
                var btn = new Button
                {
                    Content = pergunta.Alternativas[i],
                    Style = (Style)Resources["BtnAlternativa"],
                    Tag = indice
                };
                btn.Click += (s, e) => ResponderAlternativa(indice);
                botoes.Add(btn);
            }
            listaAlternativas.ItemsSource = botoes;

            MostrarEtapa(painelPergunta);
        }

        private void ResponderAlternativa(int indiceEscolhido)
        {
            if (_indiceAtual >= _perguntas.Count) return;
            var pergunta = _perguntas[_indiceAtual];
            var acertou = indiceEscolhido == pergunta.RespostaCorreta;

            if (acertou) _acertos++; else _erros++;

            // ── Resultado ──
            if (acertou)
            {
                boxResultado.Background = new SolidColorBrush(Color.FromRgb(0x16, 0xa3, 0x4a));
                lblIconeResultado.Text = "✅";
                lblResultado.Text = "Certo! Mandou bem.";
            }
            else
            {
                boxResultado.Background = new SolidColorBrush(Color.FromRgb(0xef, 0x44, 0x44));
                lblIconeResultado.Text = "❌";
                lblResultado.Text = "Não foi dessa vez.";
            }

            var respostaCorretaTexto = pergunta.RespostaCorreta >= 0 && pergunta.RespostaCorreta < pergunta.Alternativas.Count
                ? pergunta.Alternativas[pergunta.RespostaCorreta]
                : "";

            lblFbRespostaCorreta.Text = respostaCorretaTexto;
            lblFbTraducao.Text        = pergunta.Traducao;
            lblFbExplicacao.Text      = pergunta.Explicacao;
            lblFbExemplo.Text         = pergunta.ExemploUso;

            if (!string.IsNullOrWhiteSpace(pergunta.Gramatica))
            {
                painelFbGramatica.Visibility = Visibility.Visible;
                lblFbGramatica.Text = pergunta.Gramatica;
            }
            else
            {
                painelFbGramatica.Visibility = Visibility.Collapsed;
            }

            btnContinuar.Content = _indiceAtual + 1 < _perguntas.Count ? "Continuar" : "Ver resultado";

            MostrarEtapa(scrollFeedback);
        }

        private void Continuar_Click(object sender, RoutedEventArgs e)
        {
            _indiceAtual++;
            MostrarPergunta();
        }

        // ───────────────────────── RESUMO FINAL ─────────────────────────

        private void MostrarResumo()
        {
            var total = _perguntas.Count;
            lblResumoPlacar.Text = $"Você acertou {_acertos} de {total}";
            lblResumoTitulo.Text = _acertos == total
                ? "Mandou muito bem!"
                : _acertos >= total / 2
                    ? "Bom trabalho!"
                    : "Vamos praticar mais um pouco";

            MostrarEtapa(painelResumo);
        }
    }
}
