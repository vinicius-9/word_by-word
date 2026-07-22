using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Speech.Recognition;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace WordByWord
{
    /// <summary>
    /// Painel do Assistente embutido na própria janela do dicionário
    /// (troca de lugar com a lista/detalhe, igual à visão de detalhe da palavra).
    /// Não abre janela nenhuma — fica dentro da MainWindow.
    ///
    /// Tem três modos: tirar dúvidas por texto, bate-papo por texto, e o Modo Áudio
    /// (fala com a Ana e também pode digitar — ver <see cref="AtivarAudio"/>).
    /// </summary>
    public partial class AssistentePainel : UserControl
    {
        /// <summary>Disparado quando o usuário clica em "Voltar para a lista".</summary>
        public event EventHandler? VoltarClicado;

        private readonly GeminiServico _servico = new();
        private ConfiguracaoAssistente _config = ConfiguracaoAssistente.Carregar();

        private readonly ObservableCollection<MensagemChat> _mensagensDuvidas = new();
        private readonly ObservableCollection<MensagemChat> _mensagensConversacao = new();
        private readonly ObservableCollection<MensagemChat> _mensagensAudio = new();

        private ModoAssistente _modo = ModoAssistente.Duvidas;
        private bool _iniciadoDuvidas;
        private bool _iniciadoConversacao;
        private bool _iniciadoAudio;

        private bool _enviando;
        private DispatcherTimer? _timerStatus;

        // ── Modo Áudio: reconhecimento de voz (microfone) + fala da Ana ──
        private readonly EdgeTtsServico _tts = new();
        private SpeechRecognitionEngine? _reconhecedor;
        private bool _ouvindo;

        /// <summary>Idioma que o reconhecedor está usando agora — troca sozinho (ver
        /// <see cref="TrocarIdiomaAutomaticamente"/>), a pessoa não escolhe mais na mão.</summary>
        private string _idiomaEscuta = "en-US";

        /// <summary>Quantas vezes seguidas o reconhecedor "ouviu" algo mas não bateu com o
        /// idioma atual (confiança baixa ou rejeição) — sinal de que a pessoa provavelmente
        /// trocou de idioma no meio da conversa.</summary>
        private int _tentativasSemEntender;

        /// <summary>Depois de quantas tentativas malsucedidas seguidas o microfone troca de
        /// idioma sozinho e tenta de novo.</summary>
        private const int MaxTentativasAntesDeTrocarIdioma = 2;

        /// <summary>Coleção de mensagens do modo atualmente exibido.</summary>
        private ObservableCollection<MensagemChat> MensagensAtuais => _modo switch
        {
            ModoAssistente.Conversacao => _mensagensConversacao,
            ModoAssistente.Audio       => _mensagensAudio,
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
            PararEscuta();

            _modo = ModoAssistente.Duvidas;
            listaMensagensTexto.ItemsSource = _mensagensDuvidas;
            lblStatus.Text = TextoStatusPadrao;
            iconeCabecalhoDuvidas.Visibility = Visibility.Visible;
            iconeCabecalhoConversa.Visibility = Visibility.Collapsed;
            lblTituloModo.Text = "Assistente";
            cardChatTexto.Visibility    = Visibility.Visible;
            inputTexto.Visibility       = Visibility.Visible;
            inputAudio.Visibility       = Visibility.Collapsed;

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
        ///
        /// Não existe mais escolha de categoria/tema: a conversa começa direto e vai se
        /// adaptando sozinha ao nível de inglês da pessoa (ver GeminiServico).
        /// </summary>
        public void AtivarConversacao()
        {
            PararEscuta();

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
            inputAudio.Visibility      = Visibility.Collapsed;

            txtPergunta.Focus();
            RolarParaFinal();
        }

        /// <summary>
        /// Prepara o painel para o Modo Áudio: conversa falada com a Ana. A pessoa toca no
        /// microfone e fala em português ou em inglês, sem precisar avisar qual — o
        /// reconhecedor detecta e troca de idioma sozinho (ver <see cref="TrocarIdiomaAutomaticamente"/>),
        /// igual ao modo de voz ao vivo do Gemini Studio. Também dá pra digitar a pergunta em
        /// vez de falar: o campo de texto normal continua disponível, lado a lado com o
        /// microfone. Tem seu próprio histórico, separado dos outros dois modos.
        /// </summary>
        public void AtivarAudio()
        {
            PararEscuta(); // por segurança, caso já estivesse ouvindo em outro estado

            _modo = ModoAssistente.Audio;
            _idiomaEscuta = _config.IdiomaEscuta;
            _tentativasSemEntender = 0;
            listaMensagensTexto.ItemsSource = _mensagensAudio;
            iconeCabecalhoDuvidas.Visibility = Visibility.Collapsed;
            iconeCabecalhoConversa.Visibility = Visibility.Visible;
            lblTituloModo.Text = "Modo Áudio";
            lblStatus.Text = TextoStatusPadrao;

            txtPergunta.Clear();
            AtualizarIndicadorIdioma();

            if (!_config.TemChave)
            {
                AbrirPainelConfig();
                return;
            }

            if (!_iniciadoAudio)
            {
                _iniciadoAudio = true;
                var abertura = AberturasDeConversa[new Random().Next(AberturasDeConversa.Length)];
                _mensagensAudio.Add(new MensagemChat { EhUsuario = false, ModoConversacao = true, Texto = abertura });
            }

            cardChatTexto.Visibility = Visibility.Visible;
            // No Modo Áudio a pessoa pode falar OU digitar: os dois campos de entrada ficam
            // visíveis ao mesmo tempo (barra do microfone em cima, caixa de texto embaixo).
            inputTexto.Visibility = Visibility.Visible;
            inputAudio.Visibility = Visibility.Visible;

            RolarParaFinal();
        }

        /// <summary>Atualiza o indicador (somente leitura) de qual idioma o microfone está
        /// entendendo agora — não é mais um botão pra pessoa escolher antes de falar.</summary>
        private void AtualizarIndicadorIdioma()
        {
            lblIdiomaAtual.Text = _idiomaEscuta == "pt-BR" ? "PT" : "EN";
        }

        // ───────────────────────── MODO ÁUDIO: MICROFONE ─────────────────────────

        private void Microfone_Click(object sender, RoutedEventArgs e)
        {
            if (_ouvindo) PararEscuta();
            else IniciarEscuta();
        }

        private void IniciarEscuta()
        {
            if (_ouvindo || _enviando) return;

            if (!_config.TemChave)
            {
                AbrirPainelConfig();
                return;
            }

            if (!CriarReconhecedor(_idiomaEscuta, out var erro))
            {
                lblStatusAudio.Text = erro;
                return;
            }

            _ouvindo = true;
            _tentativasSemEntender = 0;
            lblStatusAudio.Text = "Ouvindo... toque de novo pra parar";
            btnMicrofone.Background = new SolidColorBrush(Color.FromRgb(0xef, 0x44, 0x44));
        }

        /// <summary>Cria e liga o reconhecedor de voz no idioma pedido, já escutando o
        /// microfone. Usado tanto pra começar a ouvir quanto pra trocar de idioma sozinho no
        /// meio de uma escuta (ver <see cref="TrocarIdiomaAutomaticamente"/>).</summary>
        private bool CriarReconhecedor(string idioma, out string mensagemErro)
        {
            mensagemErro = "";
            try
            {
                _reconhecedor = new SpeechRecognitionEngine(new CultureInfo(idioma));
                _reconhecedor.LoadGrammar(new DictationGrammar());
                _reconhecedor.SetInputToDefaultAudioDevice();
                _reconhecedor.SpeechRecognized += Reconhecedor_SpeechRecognized;
                _reconhecedor.SpeechRecognitionRejected += Reconhecedor_SpeechRecognitionRejected;
                _reconhecedor.RecognizeAsync(RecognizeMode.Multiple);
                return true;
            }
            catch (Exception ex)
            {
                _reconhecedor?.Dispose();
                _reconhecedor = null;
                mensagemErro = idioma == "pt-BR"
                    ? "⚠ Não consegui usar o reconhecimento de voz em português. Verifique se o pacote de idioma \"Português (Brasil)\" está instalado em Configurações → Hora e Idioma → Fala do Windows."
                    : $"⚠ Não consegui usar o microfone: {ex.Message}";
                return false;
            }
        }

        private void PararEscuta()
        {
            if (_reconhecedor != null)
            {
                try { _reconhecedor.RecognizeAsyncStop(); } catch { /* já pode ter parado sozinho */ }
                _reconhecedor.SpeechRecognized -= Reconhecedor_SpeechRecognized;
                _reconhecedor.SpeechRecognitionRejected -= Reconhecedor_SpeechRecognitionRejected;
                _reconhecedor.Dispose();
                _reconhecedor = null;
            }

            _ouvindo = false;
            _tentativasSemEntender = 0;

            // ClearValue() aqui apagava também o DynamicResource "CorAcento" definido no XAML
            // (era o mesmo "valor local" que o vermelho de "ouvindo" substituiu), deixando o
            // botão sem cor nenhuma (aparecia branco/transparente). Em vez de limpar, religa
            // explicitamente o azul de accent.
            if (btnMicrofone != null) btnMicrofone.SetResourceReference(Control.BackgroundProperty, "CorAcento");
            if (lblStatusAudio != null && _modo == ModoAssistente.Audio)
                lblStatusAudio.Text = "Toque no microfone e fale";
        }

        /// <summary>
        /// Troca sozinho o idioma que o microfone escuta (português ↔ inglês) sem interromper
        /// a sensação de "estar ouvindo" pra pessoa — o botão continua vermelho e ativo. É
        /// disparado depois de algumas tentativas seguidas em que o reconhecedor não entendeu
        /// nada (sinal de que a pessoa mudou de idioma no meio da conversa), imitando o Modo
        /// Áudio ao vivo do Gemini Studio, que entende os dois idiomas sem precisar escolher.
        /// </summary>
        private void TrocarIdiomaAutomaticamente()
        {
            if (!_ouvindo) return;

            var novoIdioma = _idiomaEscuta == "pt-BR" ? "en-US" : "pt-BR";

            if (_reconhecedor != null)
            {
                try { _reconhecedor.RecognizeAsyncStop(); } catch { /* ignora */ }
                _reconhecedor.SpeechRecognized -= Reconhecedor_SpeechRecognized;
                _reconhecedor.SpeechRecognitionRejected -= Reconhecedor_SpeechRecognitionRejected;
                _reconhecedor.Dispose();
                _reconhecedor = null;
            }

            if (CriarReconhecedor(novoIdioma, out _))
            {
                _idiomaEscuta = novoIdioma;
                _tentativasSemEntender = 0;
                AtualizarIndicadorIdioma();
            }
            else
            {
                // Pacote de idioma indisponível nessa máquina: melhor continuar ouvindo no
                // idioma que já sabemos que funciona do que deixar o microfone mudo — mas
                // avisa a pessoa (antes isso falhava calado e ela achava que o app só não
                // estava entendendo o que ela falava).
                if (novoIdioma == "pt-BR")
                {
                    lblStatusAudio.Text = "⚠ Reconhecimento de voz em português não disponível " +
                        "neste Windows — continuando a ouvir em inglês.";
                    _timerStatus?.Stop();
                    _timerStatus = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
                    _timerStatus.Tick += (s, e) =>
                    {
                        _timerStatus?.Stop();
                        if (_modo == ModoAssistente.Audio)
                            lblStatusAudio.Text = _ouvindo ? "Ouvindo... toque de novo pra parar" : TextoStatusPadrao;
                    };
                    _timerStatus.Start();
                }
                CriarReconhecedor(_idiomaEscuta, out _);
            }
        }

        private void Reconhecedor_SpeechRecognized(object? sender, SpeechRecognizedEventArgs e)
        {
            var texto = e.Result?.Text?.Trim();

            if (string.IsNullOrWhiteSpace(texto) || e.Result!.Confidence < 0.35)
            {
                // O reconhecedor "ouviu" algo, mas não bateu com o vocabulário do idioma
                // atual — provável sinal de que a pessoa falou no outro idioma.
                Dispatcher.InvokeAsync(RegistrarTentativaSemEntender);
                return;
            }

            _tentativasSemEntender = 0;

            // Confirma no config qual idioma funcionou por último, pra já começar por ele na
            // próxima vez que a pessoa abrir o Modo Áudio.
            if (_config.IdiomaEscuta != _idiomaEscuta)
            {
                _config.IdiomaEscuta = _idiomaEscuta;
                _config.Salvar();
            }

            // Pausa a escuta enquanto processa e fala a resposta — sem isso, o microfone
            // podia captar a própria voz da Ana saindo pelo alto-falante e "ouvir" ela como
            // se fosse a pessoa falando de novo.
            try { _reconhecedor?.RecognizeAsyncStop(); } catch { /* ignora */ }

            // O evento dispara numa thread de fundo do reconhecedor; toda interação com a UI
            // (e o envio pra IA, que mexe em coleções ligadas à tela) precisa voltar pra
            // thread da interface.
            Dispatcher.InvokeAsync(() => _ = EnviarPerguntaAsync(texto));
        }

        /// <summary>Trecho de áudio captado, mas rejeitado pelo reconhecedor por não bater com
        /// o vocabulário do idioma atual — mesmo sinal de "idioma provavelmente trocou" que a
        /// confiança baixa em <see cref="Reconhecedor_SpeechRecognized"/>.</summary>
        private void Reconhecedor_SpeechRecognitionRejected(object? sender, SpeechRecognitionRejectedEventArgs e)
            => Dispatcher.InvokeAsync(RegistrarTentativaSemEntender);

        private void RegistrarTentativaSemEntender()
        {
            if (!_ouvindo) return;

            _tentativasSemEntender++;
            if (_tentativasSemEntender >= MaxTentativasAntesDeTrocarIdioma)
                TrocarIdiomaAutomaticamente();
        }

        /// <summary>Volta a escutar depois que a Ana termina de responder — só se a pessoa
        /// não tiver desligado o microfone nesse meio-tempo.</summary>
        private void RetomarEscutaSePreciso()
        {
            if (_modo == ModoAssistente.Audio && _ouvindo && _reconhecedor != null)
            {
                try { _reconhecedor.RecognizeAsync(RecognizeMode.Multiple); }
                catch { /* se falhar, a pessoa pode tocar o microfone de novo manualmente */ }
            }
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
            PararEscuta();
            _tts.Parar();
        }

        private string TextoStatusPadrao => _modo switch
        {
            ModoAssistente.Conversacao => "Bate-papo em inglês",
            ModoAssistente.Audio       => "Fale em português ou inglês, ou digite abaixo",
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

            bool ehConversacao = _modo == ModoAssistente.Conversacao || _modo == ModoAssistente.Audio;
            var msgUsuario = new MensagemChat { EhUsuario = true, Texto = pergunta, ModoConversacao = ehConversacao };
            MensagensAtuais.Add(msgUsuario);

            var msgResposta = new MensagemChat { EhUsuario = false, Texto = "Pensando...", Carregando = true, ModoConversacao = ehConversacao };
            MensagensAtuais.Add(msgResposta);
            RolarParaFinal();

            lblStatus.Text = "Pensando...";
            if (_modo == ModoAssistente.Audio) lblStatusAudio.Text = "Ana está pensando...";

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

                if (_modo == ModoAssistente.Audio)
                {
                    lblStatusAudio.Text = "Ana está respondendo...";
                    _tts.Voz = _config.VozId;
                    try { await _tts.FalarAsync(texto); }
                    catch (Exception exFala)
                    {
                        lblStatusAudio.Text = $"⚠ Não consegui falar a resposta: {exFala.Message}";
                    }
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
                if (_modo == ModoAssistente.Audio && !lblStatusAudio.Text.StartsWith("⚠"))
                    lblStatusAudio.Text = _ouvindo ? "Ouvindo... toque de novo pra parar" : "Toque no microfone e fale";
                _enviando = false;
                btnEnviar.IsEnabled = true;
                RolarParaFinal();
                RetomarEscutaSePreciso();
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

            if (_modo == ModoAssistente.Audio)
            {
                _iniciadoAudio = false;
                AtivarAudio();
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
