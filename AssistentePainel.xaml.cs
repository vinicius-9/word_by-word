using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace WordByWord
{
    /// <summary>
    /// Painel do Assistente embutido na própria janela do dicionário
    /// (troca de lugar com a lista/detalhe, igual à visão de detalhe da palavra).
    /// Não abre janela nenhuma — fica dentro da MainWindow.
    ///
    /// Tem dois modos: tirar dúvidas por texto e bate-papo por texto.
    /// </summary>
    public partial class AssistentePainel : UserControl
    {
        /// <summary>Disparado quando o usuário clica em "Voltar para a lista".</summary>
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

        /// <summary>Coleção de mensagens do modo atualmente exibido.</summary>
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

        /// <summary>   
        /// Prepara o painel para tirar dúvidas de inglês (palavras, gramática, tradução):
        /// mostra a mensagem de boas-vindas na primeira vez e abre a configuração de
        /// chave se ainda não houver uma.
        /// </summary> 
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

            // O campo de digitação é o mesmo controle físico usado pelo Bate-papo (só o
            // painel inteiro troca de "modo"). Sem limpar aqui, o que a pessoa tivesse
            // digitado — mas não enviado — em uma tela aparecia também na outra.
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

        /// <summary>
        /// Prepara o painel para um bate-papo casual em inglês, pra praticar o idioma
        /// (conversa separada da de tirar dúvidas, com seu próprio histórico).
        /// Usado pelo botão de conversação do cabeçalho principal.
        /// </summary>
        public void AtivarConversacao()
        {
            _modo = ModoAssistente.Conversacao;
            listaMensagensTexto.ItemsSource = _mensagensConversacao;
            lblStatus.Text = TextoStatusPadrao;
            iconeCabecalhoDuvidas.Visibility = Visibility.Collapsed;
            iconeCabecalhoConversa.Visibility = Visibility.Visible;
            lblTituloModo.Text = "Bate-papo em inglês";
            lblStatus.Text = "Treine seu inglês";

            // Mesmo motivo do Ativar(): é o mesmo campo de texto físico das duas telas.
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

        /// <summary>Aberturas variadas de conversa livre (sem categoria fixa) — uma é sorteada só na
        /// primeira vez que a pessoa entra no bate-papo. A conversa em si vai se adaptando ao
        /// nível dela conforme ela responde, sem precisar escolher nada antes.
        /// </summary>
        private static readonly string[] AberturasDeConversa =
        {
            "Hey! I'm Ana 👋 Nice to meet you. So, what's up — how's your day going?",
            "Hi there! I'm Ana 👋 What have you been up to today?",
            "Hey! I'm Ana 👋 How's everything going with you?",
            "Hi! I'm Ana 👋 So, what's on your mind today?"
        };

        /// <summary>Chamado pela janela principal ao sair do painel do assistente.</summary>
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

            // Já tem tradução em cache: só alterna entre mostrar ela ou o texto original.
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

        /// <summary>Abre o diálogo de confirmação (dentro do próprio painel, com o tema do app).</summary>
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
                // No bate-papo, ao limpar mostra uma abertura nova na hora (o nível de
                // inglês salvo continua o mesmo, só a conversa em si recomeça).
                _iniciadoConversacao = false;
                AtivarConversacao();
                return;
            }
            // No modo "tirar dúvidas" não readiciona a mensagem de boas-vindas aqui: ela ficava
            // "presa" na tela mesmo sem nenhuma interação. Agora o chat some vazio e só volta a
            // ter conteúdo quando a pessoa perguntar algo de novo.
        }

        /// <summary>
        /// Mostra uma mensagem de erro no lugar do status por alguns segundos
        /// e depois volta ao texto padrão.
        /// </summary>
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