using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Media;

namespace WordByWord
{
    /// <summary>
    /// Integração com o Microsoft Edge TTS (Text-to-Speech), usando o pacote Python
    /// "edge-tts" (https://github.com/rany2/edge-tts) — gratuito, usa as mesmas vozes
    /// neurais do Microsoft Edge, sem precisar de chave de API nem servidor rodando
    /// o tempo todo: cada fala é gerada chamando o comando "edge-tts" na hora.
    ///
    /// Pré-requisito na máquina do usuário: Python instalado + "pip install edge-tts".
    /// </summary>
    public class EdgeTtsServico
    {
        /// <summary>Voz usada (multilíngue — entende tanto inglês quanto português).</summary>
        public string Voz { get; set; } = "en-US-AvaMultilingualNeural";

        private MediaPlayer? _player;

        // Depois que descobre qual comando funciona nessa máquina, guarda pra não perder
        // tempo tentando os outros de novo em toda fala (isso é o que deixava lento).
        private static string? _comandoQueFunciona;

        // Tempo máximo de espera pelo processo do edge-tts antes de desistir e tentar o
        // próximo comando candidato. Reduzido de 20s pra 9s: no comando que JÁ sabemos que
        // funciona (_comandoQueFunciona), 9s é mais que suficiente pra qualquer frase normal;
        // e para os comandos candidatos que nem existem na máquina, esperar 20s antes de cair
        // pro próximo é o que fazia o app parecer "travado" na primeira fala. Uma frase real
        // que demorar mais que isso provavelmente é problema de internet mesmo, não do timeout.
        private static readonly TimeSpan TimeoutProcesso = TimeSpan.FromSeconds(9);

        /// <summary>
        /// Descobre em segundo plano qual comando do edge-tts funciona nessa máquina, sem
        /// tocar nenhum som — só "aquece" o processo. Chamado uma vez ao abrir o app pra que,
        /// na hora em que a pessoa realmente pedir um áudio (exercícios, dicionário), o app já
        /// saiba direto qual comando usar em vez de tentar vários e parecer lento/travado.
        /// Falha silenciosamente: se não achar nada, o fluxo normal tenta de novo na hora do uso.
        /// </summary>
        public async Task AquecerAsync()
        {
            if (_comandoQueFunciona != null) return;
            try
            {
                await FalarSemTocarAsync("hello");
            }
            catch
            {
                // Sem problema: só era um aquecimento. O primeiro uso real tenta de novo.
            }
        }

        /// <summary>
        /// Gera (e deixa em cache) o áudio de um texto específico SEM tocar nada — usado pra
        /// "adiantar o trabalho" assim que se sabe qual vai ser a próxima frase falada (ex.:
        /// assim que uma pergunta de exercício aparece na tela, já pede esse método pra frase
        /// que vai ser falada se a pessoa acertar). Assim, na hora real de tocar o som pra
        /// pessoa, o áudio já está pronto em cache e toca na hora, sem esperar o edge-tts.
        /// Falha silenciosamente: é só uma otimização de velocidade, não pode travar nada.
        /// </summary>
        public async Task PreCarregarAsync(string texto)
        {
            if (string.IsNullOrWhiteSpace(texto)) return;
            try
            {
                await FalarSemTocarAsync(texto);
            }
            catch
            {
                // Sem problema: se não deu pra adiantar, toca na hora normalmente depois.
            }
        }

        private async Task FalarSemTocarAsync(string texto)
        {
            var caminhoCache = CaminhoCache(texto);
            if (File.Exists(caminhoCache)) return;

            var argsEdgeTts = $"--voice \"{Voz}\" --text \"{EscaparAspas(texto)}\" --write-media \"{caminhoCache}\"";
            var todas = new (string arquivo, string args)[]
            {
                ("python",   $"-m edge_tts {argsEdgeTts}"),
                ("py",       $"-m edge_tts {argsEdgeTts}"),
                ("python3",  $"-m edge_tts {argsEdgeTts}"),
                ("edge-tts", argsEdgeTts),
            };

            foreach (var (arquivo, args) in todas)
            {
                var psi = new ProcessStartInfo
                {
                    FileName = arquivo,
                    Arguments = args,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                try
                {
                    using var processo = Process.Start(psi);
                    if (processo == null) continue;

                    using var cts = new System.Threading.CancellationTokenSource(TimeoutProcesso);
                    try { await processo.WaitForExitAsync(cts.Token); }
                    catch (OperationCanceledException) { try { processo.Kill(true); } catch { } continue; }

                    if (processo.ExitCode == 0 && File.Exists(caminhoCache))
                    {
                        _comandoQueFunciona = arquivo;
                        return;
                    }
                }
                catch (System.ComponentModel.Win32Exception)
                {
                    continue;
                }
            }
        }

        /// <summary>
        /// Gera o áudio do texto usando o Edge TTS e toca imediatamente.
        /// Lança exceção com mensagem amigável se o "edge-tts" não estiver instalado.
        /// </summary>
        public async Task FalarAsync(string texto)
        {
            if (string.IsNullOrWhiteSpace(texto)) return;

            var caminhoCache = CaminhoCache(texto);

            // Já gerou esse áudio antes com essa voz? Toca na hora, sem chamar processo nenhum.
            if (File.Exists(caminhoCache))
            {
                TocarArquivo(caminhoCache, apagarDepois: false);
                return;
            }

            var argsEdgeTts = $"--voice \"{Voz}\" --text \"{EscaparAspas(texto)}\" --write-media \"{caminhoCache}\"";

            // Tenta algumas formas diferentes de chamar o edge-tts, porque no Windows o comando
            // direto só funciona se a pasta "Scripts" do Python estiver no PATH — o que nem
            // sempre acontece. "python -m edge_tts" costuma funcionar mesmo quando o comando
            // direto não é encontrado. Se já descobrimos qual funciona, vai direto nela.
            var todas = new (string arquivo, string args)[]
            {
                ("python",   $"-m edge_tts {argsEdgeTts}"),
                ("py",       $"-m edge_tts {argsEdgeTts}"),
                ("python3",  $"-m edge_tts {argsEdgeTts}"),
                ("edge-tts", argsEdgeTts),
            };

            var tentativas = _comandoQueFunciona != null
                ? todas.Where(t => t.arquivo == _comandoQueFunciona)
                       .Concat(todas.Where(t => t.arquivo != _comandoQueFunciona))
                : todas.AsEnumerable();

            Exception? ultimoErro = null;
            bool nenhumComandoEncontrado = true;

            foreach (var (arquivo, args) in tentativas)
            {
                var psi = new ProcessStartInfo
                {
                    FileName = arquivo,
                    Arguments = args,
                    RedirectStandardError = true,
                    RedirectStandardOutput = false,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                try
                {
                    using var processo = Process.Start(psi);
                    if (processo == null) { ultimoErro = new Exception("Não consegui iniciar o processo."); continue; }

                    // Chegou até aqui: o comando existe e rodou (falhar depois disso é outro
                    // problema — internet, versão desatualizada etc. — não "não instalado").
                    nenhumComandoEncontrado = false;

                    var tarefaErro = processo.StandardError.ReadToEndAsync();
                    using var cts = new System.Threading.CancellationTokenSource(TimeoutProcesso);

                    try
                    {
                        await processo.WaitForExitAsync(cts.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        try { processo.Kill(true); } catch { /* já pode ter terminado sozinho */ }
                        ultimoErro = new Exception(
                            $"'{arquivo}' demorou demais pra responder (mais de {TimeoutProcesso.TotalSeconds:0}s). " +
                            "Verifique sua conexão com a internet.");
                        continue;
                    }

                    var erro = await tarefaErro;

                    if (processo.ExitCode == 0 && File.Exists(caminhoCache))
                    {
                        _comandoQueFunciona = arquivo;
                        TocarArquivo(caminhoCache, apagarDepois: false); // fica em cache pra próxima vez ser instantâneo
                        return;
                    }

                    ultimoErro = new Exception(string.IsNullOrWhiteSpace(erro)
                        ? $"'{arquivo}' não gerou o áudio (código {processo.ExitCode})."
                        : $"Erro do edge-tts: {erro.Trim()}");
                }
                catch (System.ComponentModel.Win32Exception)
                {
                    // Esse comando não existe na máquina — tenta o próximo da lista.
                    ultimoErro = new Exception($"Comando '{arquivo}' não encontrado.");
                    continue;
                }
            }

            // Nenhum dos 4 comandos (python/py/python3/edge-tts) existe nesta máquina: é quase
            // certo que o Python ou o pacote edge-tts não estão instalados — mensagem direta em
            // vez do checklist genérico de 3 passos.
            if (nenhumComandoEncontrado)
            {
                throw new Exception(
                    "A voz da Ana precisa do Python instalado com o pacote \"edge-tts\", e não " +
                    "encontrei nenhum dos dois nesta máquina. Instale o Python (python.org, marque " +
                    "\"Add to PATH\" na instalação) e depois rode 'pip install edge-tts' no terminal. " +
                    "Reinicie o app depois de instalar.");
            }

            throw new Exception(
                "Não consegui gerar o áudio. Confirme que: 1) instalou com 'pip install --upgrade edge-tts' " +
                "(uma versão desatualizada pode parar de funcionar); 2) o Python está no PATH do sistema " +
                "(reinicie o terminal/o app depois de instalar); 3) você está com internet, já que o " +
                $"edge-tts precisa se conectar aos servidores da Microsoft. Detalhe: {ultimoErro?.Message}");
        }

        private void TocarArquivo(string caminho, bool apagarDepois)
        {
            _player?.Close();
            _player = new MediaPlayer();
            if (apagarDepois)
            {
                _player.MediaEnded += (_, _) =>
                {
                    _player?.Close();
                    try { File.Delete(caminho); } catch { /* arquivo temporário, sem problema se falhar */ }
                };
            }
            _player.Open(new Uri(caminho));
            _player.Play();
        }

        /// <summary>Pasta onde ficam os áudios já gerados (cache), pra tocar de novo na hora.</summary>
        private static string PastaCache()
        {
            var pasta = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "WordByWord", "audio_cache");
            Directory.CreateDirectory(pasta);
            return pasta;
        }

        private string CaminhoCache(string texto)
        {
            using var sha = System.Security.Cryptography.SHA256.Create();
            var bytes = System.Text.Encoding.UTF8.GetBytes($"{Voz}|{texto}");
            var hash = Convert.ToHexString(sha.ComputeHash(bytes));
            return Path.Combine(PastaCache(), $"{hash}.mp3");
        }

        /// <summary>Para a fala em andamento, se houver.</summary>
        public void Parar() => _player?.Stop();

        private static string EscaparAspas(string texto) => texto.Replace("\"", "'");
    }
}
