using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace WordByWord
{
    /// <summary>
    /// Painel do Assistente embutido na janela do dicionário (troca de lugar com a lista/detalhe).
    /// Tem dois modos: tirar dúvidas por texto e bate-papo por texto.
    /// </summary>
    public partial class AssistentePainel : UserControl
    {
        public event EventHandler? VoltarClicado;

        private readonly GeminiServico _servico = new();
        private readonly EdgeTtsServico _tts = new();
        private ConfiguracaoAssistente _config = ConfiguracaoAssistente.Carregar();

        private readonly ObservableCollection<MensagemChat> _mensagensDuvidas = new();
        private readonly ObservableCollection<MensagemChat> _mensagensConversacao = new();

        private ModoAssistente _modo = ModoAssistente.Duvidas;
        private bool _iniciadoDuvidas;
        private bool _iniciadoConversacao;

        private bool _enviando;
        private DispatcherTimer? _timerStatus;

        private ObservableCollection<MensagemChat> MensagensAtuais => _modo switch
        {
            ModoAssistente.Conversacao => _mensagensConversacao,
            _                          => _mensagensDuvidas
        };

        public AssistentePainel()
        {
            InitializeComponent();
            listaMensagensTexto.ItemsSource = _mensagensDuvidas;
        }

        /// <summary>Prepara o painel pra tirar dúvidas de inglês: mostra boas-vindas na primeira vez e abre a config se faltar chave.</summary>
        public void Ativar()
        {
            _modo = ModoAssistente.Duvidas;
            listaMensagensTexto.ItemsSource = _mensagensDuvidas;
            lblStatus.Text = TextoStatusPadrao;
            iconeCabecalhoDuvidas.Visibility = Visibility.Visible;
            iconeCabecalhoConversa.Visibility = Visibility.Collapsed;
            lblTituloModo.Text = "Assistente";
            cardChatTexto.Visibility    = Visibility.Visible;
            inputTexto.Visibility       = Visibility.Visible;

            // O campo de digitação é compartilhado com o Bate-papo; limpa pra não vazar texto entre os modos.
            txtPergunta.Clear();

            if (!_iniciadoDuvidas)
            {
                _iniciadoDuvidas = true;
                _mensagensDuvidas.Add(new MensagemChat
                {
                    EhUsuario = false,
                    Texto = "Oi! Sou o assistente do dicionário. Pode me perguntar o significado de " +
                            "uma palavra, pedir exemplos de uso, sinônimos ou dúvidas de gramática."
                });
            }

            if (!_config.TemChave)
                AbrirPainelConfig();

            txtPergunta.Focus();
            RolarParaFinal();
        }

        /// <summary>Prepara o painel para um bate-papo casual em inglês (histórico próprio, separado do modo dúvidas).</summary>
        public void AtivarConversacao()
        {
            _modo = ModoAssistente.Conversacao;
            listaMensagensTexto.ItemsSource = _mensagensConversacao;
            lblStatus.Text = TextoStatusPadrao;
            iconeCabecalhoDuvidas.Visibility = Visibility.Collapsed;
            iconeCabecalhoConversa.Visibility = Visibility.Visible;
            lblTituloModo.Text = "Bate-papo em inglês";
            lblStatus.Text = "Treine seu inglês";

            txtPergunta.Clear();

            if (!_config.TemChave)
            {
                AbrirPainelConfig();
                return;
            }

            if (!_iniciadoConversacao)
            {
                _iniciadoConversacao = true;
                var abertura = AberturasDeConversa[new Random().Next(AberturasDeConversa.Length)];
                _mensagensConversacao.Add(new MensagemChat { EhUsuario = false, ModoConversacao = true, Texto = abertura });
            }

            cardChatTexto.Visibility   = Visibility.Visible;
            inputTexto.Visibility      = Visibility.Visible;

            txtPergunta.Focus();
            RolarParaFinal();
        }

        /// <summary>Aberturas variadas de conversa; uma é sorteada só na primeira vez que a pessoa entra no bate-papo.</summary>
        private static readonly string[] AberturasDeConversa =
        {
            "Hey! I'm Ana 👋 Nice to meet you. So, what's up — how's your day going?",
            "Hi there! I'm Ana 👋 What have you been up to today?",
            "Hey! I'm Ana 👋 How's everything going with you?",
            "Hi! I'm Ana 👋 So, what's on your mind today?"
        };

        public void Desativar()
        {
            _tts.Parar();
        }

        private string TextoStatusPadrao => _modo switch
        {
            ModoAssistente.Conversacao => "Bate-papo em inglês",
            _                          => "Tire dúvidas por texto"
        };

        // ───────────────────────── ENVIO DE TEXTO ─────────────────────────

        private void TxtPergunta_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                _ = EnviarPerguntaAsync(txtPergunta.Text);
            }
        }

        private void Enviar_Click(object sender, RoutedEventArgs e)
            => _ = EnviarPerguntaAsync(txtPergunta.Text);

        private async System.Threading.Tasks.Task EnviarPerguntaAsync(string pergunta)
        {
            pergunta = pergunta?.Trim() ?? "";
            if (string.IsNullOrEmpty(pergunta) || _enviando) return;

            if (!_config.TemChave)
            {
                AbrirPainelConfig();
                return;
            }

            _enviando = true;
            btnEnviar.IsEnabled = false;
            txtPergunta.Clear();

            bool ehConversacao = _modo == ModoAssistente.Conversacao;
            var msgUsuario = new MensagemChat { EhUsuario = true, Texto = pergunta, ModoConversacao = ehConversacao };
            MensagensAtuais.Add(msgUsuario);

            var msgResposta = new MensagemChat { EhUsuario = false, Texto = "Pensando...", Carregando = true, ModoConversacao = ehConversacao };
            MensagensAtuais.Add(msgResposta);
            RolarParaFinal();

            lblStatus.Text = "Pensando...";

            try
            {
                var (texto, novoNivel) = await _servico.PerguntarAsync(_config, MensagensAtuais, _modo, _config.NivelConversa);
                msgResposta.Texto = texto;
                msgResposta.Carregando = false;

                if (novoNivel != null && novoNivel != _config.NivelConversa)
                {
                    _config.NivelConversa = novoNivel;
                    _config.Salvar();
                }
            }
            catch (Exception ex)
            {
                msgResposta.Texto = $"⚠ {ex.Message}";
                msgResposta.Carregando = false;
            }
            finally
            {
                lblStatus.Text = TextoStatusPadrao;
                _enviando = false;
                btnEnviar.IsEnabled = true;
                RolarParaFinal();
            }
        }

        private void RolarParaFinal()
        {
            Dispatcher.InvokeAsync(() => scrollChatTexto.ScrollToBottom(),
                DispatcherPriority.Background);
        }

        // ───────────────────────── TRADUZIR (link embaixo de cada balão do bate-papo) ─────────────────────────

        private async void TraduzirMensagem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.DataContext is not MensagemChat msg) return;
            if (msg.Traduzindo || string.IsNullOrWhiteSpace(msg.Texto)) return;

            // Já tem tradução em cache: só alterna entre mostrar ela ou o original.
            if (msg.JaTraduzido)
            {
                msg.MostrandoTraducao = !msg.MostrandoTraducao;
                return;
            }

            if (!_config.TemChave)
            {
                AbrirPainelConfig();
                return;
            }

            msg.Traduzindo = true;
            try
            {
                msg.Traducao = await _servico.TraduzirAsync(_config, msg.Texto);
                msg.MostrandoTraducao = true;
            }
            catch (Exception ex)
            {
                msg.Traducao = $"⚠ {ex.Message}";
                msg.MostrandoTraducao = true;
            }
            finally
            {
                msg.Traduzindo = false;
            }
        }

        private void Limpar_Click(object sender, RoutedEventArgs e)
        {
            if (MensagensAtuais.Count == 0) return;
            overlayConfirmarLimpar.Visibility = Visibility.Visible;
        }

        private void CancelarLimpar_Click(object sender, RoutedEventArgs e)
        {
            overlayConfirmarLimpar.Visibility = Visibility.Collapsed;
        }

        private void ConfirmarLimpar_Click(object sender, RoutedEventArgs e)
        {
            overlayConfirmarLimpar.Visibility = Visibility.Collapsed;
            MensagensAtuais.Clear();

            if (_modo == ModoAssistente.Conversacao)
            {
                // No bate-papo, mostra uma abertura nova na hora (nível salvo continua o mesmo).
                _iniciadoConversacao = false;
                AtivarConversacao();
                return;
            }
            // No modo "tirar dúvidas" não readiciona a mensagem de boas-vindas: o chat some
            // vazio e só volta a ter conteúdo quando a pessoa perguntar algo de novo.
        }

        /// <summary>Mostra um erro no lugar do status por alguns segundos e volta ao texto padrão.</summary>
        private void MostrarErroTemporario(string mensagem)
        {
            lblStatus.Text = mensagem;

            _timerStatus?.Stop();
            _timerStatus = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            _timerStatus.Tick += (s, e) =>
            {
                _timerStatus?.Stop();
                lblStatus.Text = TextoStatusPadrao;
            };
            _timerStatus.Start();
        }

        // ───────────────────────── CONFIGURAÇÃO DA CHAVE ─────────────────────────

        private void Config_Click(object sender, RoutedEventArgs e)
        {
            painelConfig.Visibility = painelConfig.Visibility == Visibility.Visible
                ? Visibility.Collapsed
                : Visibility.Visible;

            if (painelConfig.Visibility == Visibility.Visible)
            {
                txtApiKey.Text = _config.ApiKey;
                SelecionarModeloAtual();
                SelecionarVozAtual();
            }
        }

        private void AbrirPainelConfig()
        {
            txtApiKey.Text = _config.ApiKey;
            SelecionarModeloAtual();
            SelecionarVozAtual();
            painelConfig.Visibility = Visibility.Visible;
        }

        private void SelecionarModeloAtual()
        {
            foreach (ComboBoxItem item in cmbModelo.Items)
            {
                if ((string)item.Content == _config.Modelo)
                {
                    cmbModelo.SelectedItem = item;
                    return;
                }
            }
            cmbModelo.SelectedIndex = 0;
        }

        private void SelecionarVozAtual()
        {
            foreach (ComboBoxItem item in cmbVoz.Items)
            {
                if ((string)item.Tag == _config.VozId)
                {
                    cmbVoz.SelectedItem = item;
                    return;
                }
            }
            cmbVoz.SelectedIndex = 0;
        }

        private void SalvarChave_Click(object sender, RoutedEventArgs e)
        {
            var chave = txtApiKey.Text.Trim();
            if (string.IsNullOrWhiteSpace(chave))
            {
                MostrarErroTemporario("⚠ Cole uma chave de API válida.");
                return;
            }

            _config.ApiKey = chave;
            if (cmbModelo.SelectedItem is ComboBoxItem modeloSelecionado)
                _config.Modelo = (string)modeloSelecionado.Content;
            if (cmbVoz.SelectedItem is ComboBoxItem vozSelecionada)
                _config.VozId = (string)vozSelecionada.Tag;

            _config.Salvar();
            painelConfig.Visibility = Visibility.Collapsed;
            txtPergunta.Focus();
        }

        // ───────────────────────── VOLTAR ─────────────────────────

        private void Voltar_Click(object sender, RoutedEventArgs e)
        {
            Desativar();
            VoltarClicado?.Invoke(this, EventArgs.Empty);
        }
    }
}