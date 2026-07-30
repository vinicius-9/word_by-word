using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Media;

namespace WordByWord
{
    /// <summary>
    /// Integração com o Microsoft Edge TTS via pacote Python "edge-tts".
    /// </summary>
    public class EdgeTtsServico
    {
        /// <summary>Voz usada (multilíngue — entende tanto inglês quanto português).</summary>
        public string Voz { get; set; } = "en-US-AvaMultilingualNeural";

        private MediaPlayer? _player;

        private static string? _comandoQueFunciona;

        // Timeout curto: comandos inexistentes na máquina não devem travar a UI por muito tempo.
        private static readonly TimeSpan TimeoutProcesso = TimeSpan.FromSeconds(9);

        /// <summary>Descobre em segundo plano qual comando do edge-tts funciona, sem tocar som.</summary>
        public async Task AquecerAsync()
        {
            if (_comandoQueFunciona != null) return;
            try { await FalarSemTocarAsync("hello"); }
            catch { /* aquecimento falhou; o primeiro uso real tenta de novo */ }
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

        /// <summary>Gera o áudio do texto via Edge TTS e toca. Lança exceção amigável se o edge-tts não estiver instalado.</summary>
        public async Task FalarAsync(string texto)
        {
            if (string.IsNullOrWhiteSpace(texto)) return;

            var caminhoCache = CaminhoCache(texto);

            if (File.Exists(caminhoCache))
            {
                TocarArquivo(caminhoCache, apagarDepois: false);
                return;
            }

            var argsEdgeTts = $"--voice \"{Voz}\" --text \"{EscaparAspas(texto)}\" --write-media \"{caminhoCache}\"";

            // Tenta várias formas de chamar o edge-tts (o comando direto nem sempre está no PATH).
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

                    nenhumComandoEncontrado = false; // comando existe; falha daqui pra frente é outro problema

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
                        TocarArquivo(caminhoCache, apagarDepois: false);
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

        public void Parar() => _player?.Stop();

        private static string EscaparAspas(string texto) => texto.Replace("\"", "'");
    }
}
