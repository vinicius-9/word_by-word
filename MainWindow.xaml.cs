using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace WordByWord
{
    public partial class MainWindow : Window
    {
        // Serviço e estado
        private readonly DicionarioServico _servico = new();
        private readonly EdgeTtsServico _tts = new();
        private readonly GeminiServico _gemini = new();
        private Palavra? _palavraSelecionada; // para edição via formulário
        private Palavra? _palavraNoDetalhe;   // palavra exibida no painel de detalhe
        private bool _editando;

        // Construtor
        public MainWindow()
        {
            InitializeComponent();
            AbrirMaximizado();
            painelAssistente.VoltarClicado += PainelAssistente_VoltarClicado;

            painelExercicios.VoltarClicado += PainelExercicios_VoltarClicado;
            painelExercicios.PrecisaConfigurarChave += (_, _) =>
            {
                painelExercicios.Visibility = Visibility.Collapsed;
                Assistente_Click(this, new RoutedEventArgs());
            };

            _servico.Carregar();
            AtualizarLista();
            _ = _tts.AquecerAsync();
        }

        // TELA CHEIA — por padrão o app abre MAXIMIZADO (janela normal, com barra de
        // título e respeitando a barra de tarefas do Windows). A tela cheia de verdade
        // (sem bordas, cobrindo a barra de tarefas) só é ativada quando o usuário aperta
        // F11. Apertando de novo, volta para a janela maximizada normal.
        private bool _telaCheiaAtiva;
        private WindowState _estadoAntesDaTelaCheia = WindowState.Maximized;

        private void AbrirMaximizado()
        {
            WindowStyle = WindowStyle.SingleBorderWindow;
            ResizeMode = ResizeMode.CanResize;
            WindowState = WindowState.Maximized;
            _telaCheiaAtiva = false;
        }

        /// <summary>
        /// A janela nasce transparente (ver XAML, Opacity="0") e some suavemente na tela assim
        /// que é exibida, em vez de "estalar" já em opacidade máxima logo depois que a tela de
        /// carregamento termina de sumir — sem isso, a troca entre as duas telas parecia um
        /// corte seco em vez de uma transição.
        /// </summary>
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            BeginAnimation(OpacityProperty,
                new System.Windows.Media.Animation.DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(320))
                {
                    EasingFunction = new System.Windows.Media.Animation.QuadraticEase
                        { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut }
                });
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.F11)
            {
                AlternarTelaCheia();
                e.Handled = true;
            }
        }

        private void AlternarTelaCheia()
        {
            if (!_telaCheiaAtiva)
            {
                // Guarda o estado atual (maximizada ou normal) para restaurar depois.
                _estadoAntesDaTelaCheia = WindowState;

                WindowStyle = WindowStyle.None;
                ResizeMode = ResizeMode.NoResize;
                WindowState = WindowState.Maximized;
                _telaCheiaAtiva = true;
            }
            else
            {
                WindowStyle = WindowStyle.SingleBorderWindow;
                ResizeMode = ResizeMode.CanResize;
                WindowState = _estadoAntesDaTelaCheia;
                _telaCheiaAtiva = false;
            }
        }

        // TEMA — alterna entre claro e escuro (a escolha fica salva)
        private void Tema_Click(object sender, RoutedEventArgs e)
        {
            App.AlternarTema();
        }

        // SCROLL — repassa o scroll do ListBox para o ScrollViewer pai
        private void ListaPalavras_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            scrollLista.ScrollToVerticalOffset(scrollLista.VerticalOffset - e.Delta / 3.0);
            e.Handled = true;
        }

        // FORMULÁRIO

        // ───────────────────────── TOAST (notificação com o visual do app) ─────────────────────────

        private DispatcherTimer? _timerToast;

        private enum TipoToast { Sucesso, Aviso, Erro }

        private void MostrarToast(string mensagem, TipoToast tipo)
        {
            string icone;
            System.Windows.Media.Brush corFundo;

            switch (tipo)
            {
                case TipoToast.Sucesso:
                    corFundo = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#16a34a"));
                    icone = "\uE73E"; // check
                    break;
                case TipoToast.Aviso:
                    corFundo = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#f59e0b"));
                    icone = "\uE7BA"; // aviso
                    break;
                case TipoToast.Erro:
                    corFundo = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#dc2626"));
                    icone = "\uE711"; // x
                    break;
                default:
                    // Segue a cor de acento do tema ativo (mesma cor da logo) em vez de um azul fixo.
                    corFundo = (System.Windows.Media.Brush)Application.Current.Resources["CorAcento"];
                    icone = "\uE946";
                    break;
            }

            cardToast.Background = corFundo;
            iconeToastFundo.Background = new SolidColorBrush(Color.FromArgb(70, 255, 255, 255));
            iconeToast.Text = icone;
            textoToast.Text = mensagem;

            cardToast.Visibility = Visibility.Visible;
            cardToast.Opacity = 0;

            var subir = new System.Windows.Media.Animation.DoubleAnimation
            {
                From = 30,
                To = 0,
                Duration = TimeSpan.FromMilliseconds(220),
                EasingFunction = new System.Windows.Media.Animation.QuadraticEase
                {
                    EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut
                }
            };
            var aparecer = new System.Windows.Media.Animation.DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromMilliseconds(220)
            };
            transformToast.BeginAnimation(TranslateTransform.YProperty, subir);
            cardToast.BeginAnimation(OpacityProperty, aparecer);

            _timerToast?.Stop();
            _timerToast = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            _timerToast.Tick += (s, e) =>
            {
                _timerToast?.Stop();
                var sumir = new System.Windows.Media.Animation.DoubleAnimation
                {
                    From = 1,
                    To = 0,
                    Duration = TimeSpan.FromMilliseconds(200)
                };
                sumir.Completed += (s2, e2) => cardToast.Visibility = Visibility.Collapsed;
                cardToast.BeginAnimation(OpacityProperty, sumir);
            };
            _timerToast.Start();
        }

        private void MostrarSucesso(string mensagem) => MostrarToast(mensagem, TipoToast.Sucesso);
        private void MostrarAviso(string mensagem)   => MostrarToast(mensagem, TipoToast.Aviso);
        private void MostrarErro(string mensagem)    => MostrarToast(mensagem, TipoToast.Erro);

        // Guarda a palavra digitada quando o overlay de duplicidade é aberto,
        // para poder concluir o cadastro depois que o usuário escolher uma opção.
        private Palavra? _palavraDuplicadaEncontrada;

        /// <summary>
        /// Usa a IA (com o mesmo prompt do gerador.py fornecido) pra preencher automaticamente
        /// Significado e Exemplo a partir da palavra digitada. O usuário ainda revisa/edita
        /// antes de clicar em "Adicionar" — nada é salvo aqui.
        /// </summary>
        private async void GerarComIA_Click(object sender, RoutedEventArgs e)
        {
            var palavra = txtPalavra.Text.Trim();
            if (string.IsNullOrWhiteSpace(palavra))
            {
                MostrarAviso("Digite a palavra em inglês antes de gerar.");
                return;
            }

            var cfg = ConfiguracaoAssistente.Carregar();
            if (!cfg.TemChave)
            {
                MostrarAviso("Configure a chave de API do Assistente antes de gerar com IA.");
                OcultarTodosOsPaineis();
                painelAssistente.Visibility = Visibility.Visible;
                painelAssistente.Ativar();
                return;
            }

            btnGerarComIA.IsEnabled = false;
            var textoOriginalBotao = btnGerarComIA.Content;
            btnGerarComIA.Content = "Gerando...";

            try
            {
                var gerada = await _gemini.GerarPalavraAsync(cfg, palavra);
                txtSignificado.Text = gerada.Significado;
                txtExemplo.Text     = gerada.Exemplo;
            }
            catch (Exception ex)
            {
                MostrarErro($"Não consegui gerar a palavra: {ex.Message}");
            }
            finally
            {
                btnGerarComIA.IsEnabled = true;
                btnGerarComIA.Content = textoOriginalBotao;
            }
        }

        private void Adicionar_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtPalavra.Text) ||
                string.IsNullOrWhiteSpace(txtSignificado.Text))
            {
                MostrarAviso("Preencha a palavra e o significado.");
                return;
            }

            // Se não estamos editando uma palavra já existente, verifica se o texto
            // digitado já combina com alguma palavra cadastrada no projeto/dicionário.
            if (!_editando)
            {
                var existente = _servico.BuscarExata(txtPalavra.Text.Trim());
                if (existente != null)
                {
                    _palavraDuplicadaEncontrada = existente;
                    lblPalavraDuplicadaInfo.Text =
                        $"\"{existente.PalavraTexto}\" já existe no seu dicionário com o significado " +
                        $"\"{existente.Significado}\". O que você quer fazer?";
                    overlayPalavraDuplicada.Visibility = Visibility.Visible;
                    return;
                }
            }

            SalvarPalavraDoFormulario();
        }

        /// <summary>Efetivamente grava a palavra do formulário (adição ou edição).</summary>
        private void SalvarPalavraDoFormulario()
        {
            try
            {
                if (_editando && _palavraSelecionada != null)
                {
                    _palavraSelecionada.PalavraTexto = txtPalavra.Text.Trim();
                    _palavraSelecionada.Significado  = txtSignificado.Text.Trim();
                    _palavraSelecionada.Exemplo      = txtExemplo.Text.Trim();
                    _servico.Salvar();
                    MostrarSucesso("Palavra atualizada com sucesso!");
                }
                else
                {
                    _servico.Adicionar(
                        txtPalavra.Text.Trim(),
                        txtSignificado.Text.Trim(),
                        txtExemplo.Text.Trim());
                    MostrarSucesso("Palavra adicionada com sucesso!");
                }

                LimparFormulario();
                AtualizarLista();
                FecharFormulario();
            }
            catch (Exception ex)
            {
                MostrarErro($"Erro ao salvar palavra: {ex.Message}");
            }
        }

        // ── OVERLAY DE PALAVRA DUPLICADA ──

        /// <summary>Atualiza a palavra já existente com os novos dados digitados no formulário.</summary>
        private void DuplicadaAtualizar_Click(object sender, RoutedEventArgs e)
        {
            overlayPalavraDuplicada.Visibility = Visibility.Collapsed;
            if (_palavraDuplicadaEncontrada == null) return;

            _palavraDuplicadaEncontrada.Significado = txtSignificado.Text.Trim();
            _palavraDuplicadaEncontrada.Exemplo      = txtExemplo.Text.Trim();
            _servico.Salvar();

            MostrarSucesso("Palavra atualizada com sucesso!");

            _palavraDuplicadaEncontrada = null;
            LimparFormulario();
            AtualizarLista();
            FecharFormulario();
        }

        /// <summary>Cadastra uma nova entrada mesmo já existindo uma palavra igual.</summary>
        private void DuplicadaCadastrarMesmoAssim_Click(object sender, RoutedEventArgs e)
        {
            overlayPalavraDuplicada.Visibility = Visibility.Collapsed;
            _palavraDuplicadaEncontrada = null;
            SalvarPalavraDoFormulario();
        }

        private void DuplicadaCancelar_Click(object sender, RoutedEventArgs e)
        {
            overlayPalavraDuplicada.Visibility = Visibility.Collapsed;
            _palavraDuplicadaEncontrada = null;
        }

        private void Limpar_Click(object sender, RoutedEventArgs e) => LimparFormulario();

        private void Buscar_TextChanged(object sender, TextChangedEventArgs e)
            => AtualizarLista(txtBusca.Text.Trim());

        // FORMULÁRIO DE PALAVRA — agora abre em tela cheia dentro da própria janela,
        // trocando de lugar com a lista/detalhe/assistente (mesmo padrão de navegação).

        /// <summary>Esconde lista, detalhe e assistente, e mostra o formulário.</summary>
        private void MostrarFormulario()
        {
            scrollLista.Visibility        = Visibility.Collapsed;
            scrollDetalhe.Visibility      = Visibility.Collapsed;
            painelAssistente.Visibility   = Visibility.Collapsed;
            painelFormulario.Visibility   = Visibility.Visible;
            scrollCamposFormulario.ScrollToTop();
            txtPalavra.Focus();
        }

        /// <summary>Fecha o formulário e volta para a lista de palavras.</summary>
        private void FecharFormulario()
        {
            painelFormulario.Visibility = Visibility.Collapsed;
            scrollLista.Visibility      = Visibility.Visible;
        }

        /// <summary>Link "← Voltar para a lista" dentro do formulário.</summary>
        private void FormularioVoltar_Click(object sender, RoutedEventArgs e)
        {
            LimparCamposFormulario();
            FecharFormulario();
        }

        // ASSISTENTE — também é um painel embutido na própria janela,
        // trocando de lugar com a lista/detalhe/formulário (mesmo padrão de navegação).

        // Esconde todas as telas de conteúdo — chamado antes de mostrar qualquer uma delas,
        // pra nunca deixar duas abertas ao mesmo tempo (era isso que causava a sobreposição visual).
        private void OcultarTodosOsPaineis()
        {
            scrollLista.Visibility        = Visibility.Collapsed;
            scrollDetalhe.Visibility      = Visibility.Collapsed;
            painelFormulario.Visibility   = Visibility.Collapsed;
            painelAssistente.Visibility   = Visibility.Collapsed;
            painelExercicios.Visibility   = Visibility.Collapsed;
        }

        private void Assistente_Click(object sender, RoutedEventArgs e)
        {
            OcultarTodosOsPaineis();
            painelAssistente.Visibility = Visibility.Visible;
            painelAssistente.Ativar();
        }

        /// <summary>Botão de atalho no cabeçalho: abre o assistente já direto na conversa,
        /// sem passar pela tela de configuração.</summary>
        private void Conversa_Click(object sender, RoutedEventArgs e)
        {
            OcultarTodosOsPaineis();
            painelAssistente.Visibility = Visibility.Visible;
            painelAssistente.AtivarConversacao();
        }

        private void PainelAssistente_VoltarClicado(object? sender, EventArgs e)
        {
            OcultarTodosOsPaineis();
            scrollLista.Visibility = Visibility.Visible;
        }

        // NOVA PALAVRA — abre o formulário manual de cadastro.

        private void NovaPalavra_Click(object sender, RoutedEventArgs e)
        {
            _editando = false;
            LimparCamposFormulario();
            MostrarFormulario();
        }

        // EXERCÍCIOS — mesma lógica de navegação embutida na janela.

        private void ExerciciosDetalhe_Click(object sender, RoutedEventArgs e)
        {
            if (_palavraNoDetalhe == null) return;
            OcultarTodosOsPaineis();
            painelExercicios.Visibility = Visibility.Visible;
            painelExercicios.Ativar(_palavraNoDetalhe);
        }

        private void PainelExercicios_VoltarClicado(object? sender, EventArgs e)
        {
            OcultarTodosOsPaineis();
            if (_palavraNoDetalhe != null)
                MostrarDetalhe(_palavraNoDetalhe);
            else
                scrollLista.Visibility = Visibility.Visible;
        }

        // LISTA DE PALAVRAS

        private void ItemCard_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (OrigemEhBotao(e.OriginalSource as DependencyObject))
                return;

            if (sender is Border border && border.DataContext is Palavra p)
            {
                MostrarDetalhe(p);
            }
        }

        private void EditarItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.CommandParameter is Palavra p)
                CarregarNoFormulario(p);
        }

        private void RemoverItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.CommandParameter is Palavra p)
                PedirConfirmacaoRemocao(p);
        }

        private async void OuvirItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.CommandParameter is Palavra p)
                await FalarPalavraAsync(p.PalavraTexto);
        }

        private async void OuvirDetalhe_Click(object sender, RoutedEventArgs e)
        {
            if (_palavraNoDetalhe != null)
                await FalarPalavraAsync(_palavraNoDetalhe.PalavraTexto, iconeCarregandoAudio, iconeOuvir);
        }

        /// <summary>
        /// Pede a pronúncia ao Edge TTS e toca. Mostra um erro amigável
        /// (com um toast) se o edge-tts não estiver instalado na máquina.
        /// </summary>
        private async System.Threading.Tasks.Task FalarPalavraAsync(string texto, FrameworkElement? iconeCarregando = null, FrameworkElement? iconeNormal = null)
        {
            if (iconeCarregando != null) iconeCarregando.Visibility = Visibility.Visible;
            if (iconeNormal != null) iconeNormal.Visibility = Visibility.Collapsed;

            try
            {
                await _tts.FalarAsync(texto);
            }
            catch (Exception ex)
            {
                MostrarErro(ex.Message);
            }
            finally
            {
                if (iconeCarregando != null) iconeCarregando.Visibility = Visibility.Collapsed;
                if (iconeNormal != null) iconeNormal.Visibility = Visibility.Visible;
            }
        }

        // PAINEL DE DETALHE

        /// <summary>
        /// Exibe o painel de detalhe para a palavra escolhida.
        /// O texto do exemplo é exibido como um único bloco corrido (via FormatadorTexto),
        /// em vez de um card separado por parágrafo — layout mais limpo e mais fácil de ler.
        /// </summary>
        private void MostrarDetalhe(Palavra p)
        {
            _palavraNoDetalhe = p;

            lblDetalhePalavra.Text    = p.PalavraTexto;
            lblDetalheSignificado.Text = p.Significado;

            FormatadorTexto.SetTexto(lblDetalheExemplo, p.Exemplo);

            boxExemplo.Visibility = !string.IsNullOrWhiteSpace(p.Exemplo)
                ? Visibility.Visible
                : Visibility.Collapsed;

            if (p.Sentidos.Count > 0)
            {
                listaDetalheSentidos.ItemsSource = p.Sentidos;
                boxSentidos.Visibility = Visibility.Visible;
            }
            else
            {
                boxSentidos.Visibility = Visibility.Collapsed;
            }

            if (p.Expressoes.Count > 0)
            {
                listaDetalheExpressoes.ItemsSource = p.Expressoes;
                boxExpressoesDetalhe.Visibility = Visibility.Visible;
            }
            else
            {
                boxExpressoesDetalhe.Visibility = Visibility.Collapsed;
            }

            if (!string.IsNullOrWhiteSpace(p.Dica))
            {
                lblDetalheDica.Text = p.Dica;
                boxDicaDetalhe.Visibility = Visibility.Visible;
            }
            else
            {
                boxDicaDetalhe.Visibility = Visibility.Collapsed;
            }

            scrollLista.Visibility        = Visibility.Collapsed;
            painelFormulario.Visibility   = Visibility.Collapsed;
            painelAssistente.Visibility   = Visibility.Collapsed;
            scrollDetalhe.Visibility      = Visibility.Visible;
            scrollDetalhe.ScrollToTop();
        }

        private void Voltar_Click(object sender, RoutedEventArgs e)
        {
            scrollDetalhe.Visibility = Visibility.Collapsed;
            scrollLista.Visibility   = Visibility.Visible;
            _palavraNoDetalhe = null;
        }

        private void EditarDetalhe_Click(object sender, RoutedEventArgs e)
        {
            if (_palavraNoDetalhe == null) return;
            CarregarNoFormulario(_palavraNoDetalhe);
        }

        private void RemoverDetalhe_Click(object sender, RoutedEventArgs e)
        {
            if (_palavraNoDetalhe == null) return;
            var p = _palavraNoDetalhe;
            PedirConfirmacaoRemocao(p, aoConfirmar: () =>
            {
                Voltar_Click(sender, e);
                AtualizarLista(txtBusca.Text.Trim());
            });
        }

        private void CarregarNoFormulario(Palavra p)
        {
            _palavraSelecionada = p;
            _editando           = true;
            txtPalavra.Text     = p.PalavraTexto;
            txtSignificado.Text = p.Significado;
            txtExemplo.Text     = p.Exemplo;
            btnAdicionar.Content = "Salvar";
            lblFormularioTitulo.Text = "Editar palavra";
            lblFormularioSub.Text    = "Atualize as informações desta palavra";
            MostrarFormulario();
        }

        // ── OVERLAY DE CONFIRMAÇÃO DE REMOÇÃO ──

        // Guarda a palavra a remover e o que fazer depois de confirmar,
        // já que o overlay não bloqueia a execução como o MessageBox fazia.
        private Palavra? _palavraParaRemover;
        private Action? _aoConfirmarRemocao;

        /// <summary>
        /// Pede confirmação (via overlay com o visual do app) antes de remover uma palavra.
        /// <paramref name="aoConfirmar"/> é executado somente se o usuário confirmar.
        /// </summary>
        private void PedirConfirmacaoRemocao(Palavra p, Action? aoConfirmar = null)
        {
            _palavraParaRemover = p;
            _aoConfirmarRemocao = aoConfirmar;
            lblConfirmarRemocaoInfo.Text = $"Tem certeza que quer remover \"{p.PalavraTexto}\"? Essa ação não pode ser desfeita.";
            overlayConfirmarRemocao.Visibility = Visibility.Visible;
        }

        private void RemocaoCancelar_Click(object sender, RoutedEventArgs e)
        {
            overlayConfirmarRemocao.Visibility = Visibility.Collapsed;
            _palavraParaRemover = null;
            _aoConfirmarRemocao = null;
        }

        private void RemocaoConfirmar_Click(object sender, RoutedEventArgs e)
        {
            overlayConfirmarRemocao.Visibility = Visibility.Collapsed;

            if (_palavraParaRemover != null)
            {
                _servico.Deletar(_palavraParaRemover.PalavraTexto);
                AtualizarLista(txtBusca.Text.Trim());
                MostrarSucesso("Palavra removida com sucesso!");
                _aoConfirmarRemocao?.Invoke();
            }

            _palavraParaRemover = null;
            _aoConfirmarRemocao = null;
        }

        /// <summary>Limpa só os campos digitados, sem mexer na lista/busca.</summary>
        private void LimparCamposFormulario()
        {
            txtPalavra.Clear();
            txtSignificado.Clear();
            txtExemplo.Clear();
            _palavraSelecionada  = null;
            _editando            = false;
            btnAdicionar.Content = "Adicionar";
        }

        private void LimparFormulario()
        {
            LimparCamposFormulario();
            txtBusca.Clear();
            listaPalavras.SelectedItem = null;
            AtualizarLista();
        }

        private void AtualizarLista(string filtro = "")
        {
            var palavras = _servico.Buscar(filtro);
            listaPalavras.ItemsSource = palavras;
            lblTotal.Text = $"{palavras.Count} palavras";
        }

        private static bool OrigemEhBotao(DependencyObject? elemento)
        {
            var atual = elemento;
            while (atual != null)
            {
                if (atual is Button) return true;
                atual = VisualTreeHelper.GetParent(atual);
            }
            return false;
        }
    }
}
