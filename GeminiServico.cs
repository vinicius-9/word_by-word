using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace WordByWord
{
    /// <summary>
    /// Camada de integração do Assistente com o modelo de IA generativa (Gemini).
    /// Fica isolada em um serviço próprio para não misturar lógica de rede
    /// com a UI (mesmo padrão usado em DicionarioServico).
    ///
    /// Só texto: envia o histórico da conversa para o Gemini e recebe a resposta em texto.
    /// </summary>
    public class GeminiServico
    {
        private static readonly HttpClient _http = new HttpClient();

        // Tempo máximo de espera por uma resposta da IA. Sem isso, uma resposta lenta ou
        // uma conexão que trava no meio do caminho deixa o HttpClient esperando pelo timeout
        // padrão dele (100s) — na prática, a tela fica "travada" em "Gerando..." por quase
        // 2 minutos antes de qualquer erro aparecer. Com um limite mais curto e uma mensagem
        // clara, a pessoa sabe rápido que precisa tentar de novo.
        private static readonly TimeSpan TimeoutRequisicao = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Envia a requisição já montada com um timeout próprio (em vez de depender do
        /// timeout padrão do HttpClient) e traduz o cancelamento por timeout numa mensagem
        /// amigável, ao invés de deixar a tela parecer travada.
        /// </summary>
        private static async Task<HttpResponseMessage> EnviarComTimeoutAsync(HttpRequestMessage requisicao)
        {
            using var cts = new System.Threading.CancellationTokenSource(TimeoutRequisicao);
            try
            {
                return await _http.SendAsync(requisicao, cts.Token);
            }
            catch (OperationCanceledException)
            {
                throw new Exception(
                    $"A IA demorou demais pra responder (mais de {TimeoutRequisicao.TotalSeconds:0}s). " +
                    "Verifique sua internet e tente de novo.");
            }
            catch (HttpRequestException ex)
            {
                throw new Exception($"Falha de conexão ao chamar a IA: {ex.Message}");
            }
        }

        private const string InstrucaoSistemaDuvidas =
            "Você é o Assistente do Word By Word, um app de estudo de vocabulário " +
            "Inglês/Português. Ajude o usuário a tirar dúvidas sobre palavras, significados, " +
            "exemplos de uso, sinônimos, gramática e tradução. Responda sempre em português do " +
            "Brasil, de forma direta e objetiva.\n\n" +
            "FORMATAÇÃO DA RESPOSTA (muito importante — a resposta é exibida em um app, " +
            "não em markdown puro):\n" +
            "- Organize a resposta em frases curtas e paragrafos bem separados por uma linha em branco.\n" +
            "- Use **negrito** apenas na palavra ou termo principal de cada explicação.\n" +
            "- Quando fizer sentido, use uma lista com itens começando por \"- \" (um por linha), " +
            "por exemplo para listar significados, sinônimos ou exemplos.\n" +
            "- Nunca use tabelas, títulos com #, blocos de código ou markdown complexo.\n" +
            "- Não escreva um único parágrafo grande e desorganizado: quebre a resposta em partes curtas e fáceis de ler.";

        private const string InstrucaoSistemaConversacao =
            "Você é a Ana, uma amiga virtual que bate papo em inglês com o usuário só pelo prazer " +
            "da conversa — isso aqui não é uma aula, é um chat casual entre duas pessoas. Não existem " +
            "categorias nem temas fixos: puxe assunto naturalmente, do jeito que uma conversa de " +
            "verdade flui, mudando de assunto quando fizer sentido.\n\n" +
            "COMO SE COMPORTAR (muito importante):\n" +
            "- Escreva só em inglês, em frases curtas e naturais, do jeito que uma pessoa de " +
            "verdade escreveria batendo papo, não como um professor ou um manual.\n" +
            "- Reaja de verdade ao que o usuário falou antes de continuar: comente, se surpreenda, " +
            "concorde ou discorde às vezes, faça piada leve quando fizer sentido.\n" +
            "- Fale sobre 'você mesma' também, como uma amiga faria: invente detalhes simples e " +
            "coerentes (hobbies, comida favorita, um final de semana, etc.) e mantenha essa mesma " +
            "'personalidade' durante toda a conversa, sem virar um interrogatório.\n" +
            "- Ana é uma MULHER. Isso só importa numa situação específica: se, por qualquer " +
            "motivo, você usar alguma palavra em português na resposta (ex.: numa explicação de " +
            "correção de gramática, ou se o usuário escrever em português), toda concordância de " +
            "gênero referente a você mesma deve ser SEMPRE no feminino — \"obrigada\" (nunca " +
            "\"obrigado\"), \"cansada\", \"certa\", \"sozinha\", etc. Isso não muda o resto da regra " +
            "acima: a conversa em si continua só em inglês.\n" +
            "- No máximo UMA pergunta por resposta, e só quando fizer sentido no papo — não " +
            "pergunte algo a cada frase.\n" +
            "- Varie o tamanho das respostas: às vezes uma frase só, às vezes duas ou três. " +
            "Nunca escreva um texto longo e explicativo, como se fosse aula.\n\n" +
            "NÍVEL DE INGLÊS DA PESSOA (se adapta sozinho, sem a pessoa escolher):\n" +
            "- Você vai receber o nível atual dela: iniciante, intermediário ou avançado.\n" +
            "- Adapte seu próprio inglês a esse nível: com iniciante, use frases bem curtas, " +
            "vocabulário simples e do dia a dia; com intermediário, frases um pouco mais " +
            "elaboradas e algumas expressões comuns; com avançado, fale normalmente, como com " +
            "um nativo, incluindo gírias e expressões idiomáticas.\n" +
            "- Preste bastante atenção em erros de gramática, verbo ou concordância que o usuário " +
            "escrever em inglês. Se a frase dele estiver ERRADA, você DEVE apontar isso: mostre a " +
            "forma certa (pode ser naturalmente dentro da sua própria resposta) e uma explicação bem " +
            "curta entre parênteses do que estava errado. Se a frase dele estiver CORRETA, não " +
            "comente nada sobre gramática — só siga a conversa normalmente.\n" +
            "- Não precisa corrigir gírias, contrações informais (tipo 'gonna', 'wanna') ou erros de " +
            "digitação que não mudam o sentido — só aponte erros de verdade (tempo verbal errado, " +
            "concordância errada, palavra errada, estrutura errada).\n" +
            "- Nunca invente um erro que não existe: se a frase estiver gramaticalmente certa, não " +
            "force uma correção.\n" +
            "- O objetivo é a pessoa ir subindo de nível aos poucos, na prática, sem perceber que " +
            "está sendo avaliada: só suba o nível dela quando ela mostrar, em VÁRIAS mensagens " +
            "seguidas (não uma só), que já domina frases corretas, vocabulário mais amplo e boa " +
            "fluência para o nível atual. Nunca rebaixe o nível. Na dúvida, mantenha o nível atual.\n" +
            "- Nunca fale sobre esse sistema de nível com a pessoa, nem diga frases tipo 'você subiu " +
            "de nível' — isso é só interno, pra você se adaptar. O incentivo deve ser natural, dentro " +
            "da própria conversa (elogiar uma frase bem construída, por exemplo), nunca como um aviso " +
            "de sistema.\n\n" +
            "FORMATAÇÃO DA RESPOSTA: frases curtas, sem markdown, sem listas, sem títulos — só o " +
            "jeito normal de escrever numa conversa de chat.\n\n" +
            "MUITO IMPORTANTE — ÚLTIMA LINHA DA RESPOSTA (obrigatório, formato exato):\n" +
            "Depois de escrever sua resposta normal pro chat, pule uma linha e termine SEMPRE com uma " +
            "linha extra, sozinha, no formato exato \"[NIVEL:iniciante]\", \"[NIVEL:intermediario]\" ou " +
            "\"[NIVEL:avancado]\" — com o nível que a pessoa deve ter na PRÓXIMA mensagem (mesmo nível " +
            "de antes, na maioria das vezes). Essa linha nunca aparece pro usuário (o app remove ela " +
            "antes de mostrar a conversa), então pode e deve estar sempre presente, em toda resposta.";

        /// <summary>
        /// Envia o histórico da conversa para a IA e retorna o texto da resposta (já sem a
        /// tag de nível, que é só uso interno) e o novo nível sugerido pela IA para o bate-papo
        /// (null fora do modo Conversação).
        /// </summary>
        public async Task<(string Texto, string? NovoNivel)> PerguntarAsync(ConfiguracaoAssistente cfg, IEnumerable<MensagemChat> historico,
            ModoAssistente modo = ModoAssistente.Duvidas, string? nivelAtual = null)
        {
            if (!cfg.TemChave)
                throw new InvalidOperationException("Chave de API não configurada.");

            var url =
                $"https://generativelanguage.googleapis.com/v1beta/models/{cfg.Modelo}:generateContent?key={cfg.ApiKey}";

            var contents = historico
                .Where(m => !m.Carregando && !string.IsNullOrWhiteSpace(m.Texto))
                .Select(m => new
                {
                    role = m.EhUsuario ? "user" : "model",
                    parts = new object[] { new { text = m.Texto } }
                })
                .ToArray();

            bool ehConversaLivre = modo == ModoAssistente.Conversacao || modo == ModoAssistente.Audio;

            var instrucaoSistema = ehConversaLivre
                ? InstrucaoSistemaConversacao
                : InstrucaoSistemaDuvidas;

            if (ehConversaLivre)
                instrucaoSistema += $"\n\nNível atual da pessoa: {nivelAtual ?? "iniciante"}.";

            var corpo = new
            {
                systemInstruction = new
                {
                    parts = new object[] { new { text = instrucaoSistema } }
                },
                contents,
                generationConfig = new
                {
                    temperature = 0.6,
                    maxOutputTokens = 800
                }
            };

            var json = JsonSerializer.Serialize(corpo);
            using var requisicao = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };

            using var resposta = await EnviarComTimeoutAsync(requisicao);
            var textoResposta = await resposta.Content.ReadAsStringAsync();

            if (!resposta.IsSuccessStatusCode)
            {
                var mensagemErro = ExtrairMensagemErro(textoResposta) ?? resposta.StatusCode.ToString();
                throw new Exception($"Falha ao consultar o assistente: {mensagemErro}");
            }

            var textoBruto = ExtrairTexto(textoResposta)
                   ?? "Não consegui gerar uma resposta agora. Tente reformular a pergunta.";

            if (!ehConversaLivre)
                return (textoBruto, null);

            return ExtrairNivelDaResposta(textoBruto);
        }

        private static readonly System.Text.RegularExpressions.Regex _regexNivel = new(
            @"\s*\[\s*NIVEL\s*:\s*(iniciante|intermediario|intermediário|avancado|avançado)\s*\]\s*$",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        /// <summary>
        /// Tira a tag oculta "[NIVEL:xxx]" do final da resposta da IA (não deve aparecer pro
        /// usuário) e devolve o nível encontrado, já normalizado (sem acento).
        /// </summary>
        private static (string Texto, string? NovoNivel) ExtrairNivelDaResposta(string texto)
        {
            var match = _regexNivel.Match(texto);
            if (!match.Success)
                return (texto.Trim(), null);

            var nivelBruto = match.Groups[1].Value.ToLowerInvariant();
            var nivel = nivelBruto.Replace("á", "a").Replace("ã", "a").Replace("ç", "c");
            var textoLimpo = texto[..match.Index].TrimEnd();
            return (textoLimpo, nivel);
        }

        private const string InstrucaoSistemaTraducao =
            "Você é um tradutor Inglês → Português (Brasil) para um app de estudo de idiomas. " +
            "O usuário vai te mandar um texto em inglês (uma frase de uma conversa). " +
            "Traduza para português do Brasil, de forma natural e fluida. " +
            "Responda SOMENTE com a tradução, sem explicações, sem aspas, sem comentários — " +
            "só o texto traduzido.";

        /// <summary>
        /// Traduz uma palavra ou frase avulsa (Inglês↔Português), sem usar o histórico da
        /// conversa — usado pela caixinha de tradução rápida do modo bate-papo.
        /// </summary>
        public async Task<string> TraduzirAsync(ConfiguracaoAssistente cfg, string texto)
        {
            if (!cfg.TemChave)
                throw new InvalidOperationException("Chave de API não configurada.");

            var url =
                $"https://generativelanguage.googleapis.com/v1beta/models/{cfg.Modelo}:generateContent?key={cfg.ApiKey}";

            var corpo = new
            {
                systemInstruction = new
                {
                    parts = new object[] { new { text = InstrucaoSistemaTraducao } }
                },
                contents = new[]
                {
                    new
                    {
                        role = "user",
                        parts = new object[] { new { text = texto } }
                    }
                },
                generationConfig = new
                {
                    temperature = 0.2,
                    maxOutputTokens = 500
                }
            };

            var json = JsonSerializer.Serialize(corpo);
            using var requisicao = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };

            using var resposta = await EnviarComTimeoutAsync(requisicao);
            var textoResposta = await resposta.Content.ReadAsStringAsync();

            if (!resposta.IsSuccessStatusCode)
            {
                var mensagemErro = ExtrairMensagemErro(textoResposta) ?? resposta.StatusCode.ToString();
                throw new Exception($"Falha ao traduzir: {mensagemErro}");
            }

            return ExtrairTexto(textoResposta) ?? "Não consegui traduzir agora.";
        }

        // ── GERADOR DE PALAVRAS ──
        //  pede um texto em Markdown (não JSON),
        // com o significado principal na primeira linha, seguido de seções por sentido,
        // expressões, resumo e dica de contexto.
        private const string PromptGerarPalavra =
            "\n\nVocê é um professor de inglês para iniciantes.\n\n" +
            "Sua tarefa é explicar apenas UMA palavra em inglês.\n\n" +
            "FORMATO DA RESPOSTA (muito importante — a resposta é exibida em um app, " +
            "não em markdown puro, então NUNCA use \"#\", \"##\", \"###\" nem linhas de " +
            "traços como \"---\" para separar seções):\n\n" +
            "PALAVRA = significado principal / outros significados\n\n" +
            "A ideia principal de PALAVRA é:\n\n" +
            "\"Explique em uma frase simples.\"\n\n" +
            "PALAVRA = significado principal\n\n" +
            "1. Primeiro significado\n\n" +
            "Explique quando esse significado é usado.\n\n" +
            "Exemplo 1. = Tradução.\n" +
            "Exemplo 2. = Tradução.\n\n" +
            "Dica:\n\n" +
            "2. Segundo significado\n\n" +
            "Explique quando esse significado é usado.\n\n" +
            "Exemplo 1. = Tradução.\n" +
            "Exemplo 2. = Tradução.\n\n" +
            "Dica:\n\n" +
            "(Crie outras seções somente se necessário.)\n\n" +
            "Expressões comuns\n\n" +
            "Liste de 5 a 10 expressões.\n\n" +
            "Expressão = Tradução.\n\n" +
            "Resumo\n\n" +
            "Resumo curto.\n\n" +
            "• significado 1\n" +
            "• significado 2\n" +
            "• significado 3\n\n" +
            "Dica\n\n" +
            "Explique como descobrir o significado pelo contexto.\n\n" +
            "• Se aparecer...\n" +
            "• Se aparecer...\n" +
            "• Se aparecer...\n\n" +
            "REGRAS\n\n" +
            "- Explique apenas a palavra informada.\n" +
            "- Não invente palavras.\n" +
            "- Não faça introduções.\n" +
            "- Não elogie a palavra.\n" +
            "- Comece diretamente pela primeira linha (\"PALAVRA = ...\"), sem título.\n" +
            "- Use inglês americano.\n" +
            "- Tradução fiel para o português.\n" +
            "- Exemplos curtos.\n" +
            "- Um exemplo e sua tradução na mesma linha.\n" +
            "- Priorize significados usados no dia a dia.\n" +
            "- NUNCA use \"#\", \"##\", \"###\" ou linhas de traços (\"---\") na resposta — " +
            "separe as seções só com uma linha em branco.\n" +
            "- Pode usar **negrito** e listas com \"- \" ou \"• \", nada além disso.\n\n";

        /// <summary>
        /// Gera o conteúdo de uma palavra fornecida
        /// (retorna Markdown, não JSON). Preenche o formulário manual: a primeira linha
        /// ("# palavra = significado...") vira o campo Significado, e o resto do texto
        /// gerado (seções por sentido, expressões, resumo, dica) vira o campo Exemplo.
        /// Não salva nada — só retorna os dados pra revisão do usuário no formulário.
        /// </summary>
        public async Task<Palavra> GerarPalavraAsync(ConfiguracaoAssistente cfg, string palavraTexto)
        {
            if (!cfg.TemChave)
                throw new InvalidOperationException("Chave de API não configurada.");

            var url =
                $"https://generativelanguage.googleapis.com/v1beta/models/{cfg.Modelo}:generateContent?key={cfg.ApiKey}";

        
            // mensagem de usuário (sem systemInstruction separado).
            var prompt = PromptGerarPalavra + palavraTexto.Trim();

            var corpo = new
            {
                contents = new[]
                {
                    new
                    {
                        role = "user",
                        parts = new object[] { new { text = prompt } }
                    }
                },
                generationConfig = new
                {
                    temperature = 0.5,
                    maxOutputTokens = 1500
                }
            };

            var json = JsonSerializer.Serialize(corpo);
            using var requisicao = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };

            using var resposta = await EnviarComTimeoutAsync(requisicao);
            var textoResposta = await resposta.Content.ReadAsStringAsync();

            if (!resposta.IsSuccessStatusCode)
            {
                var mensagemErro = ExtrairMensagemErro(textoResposta) ?? resposta.StatusCode.ToString();
                throw new Exception($"Falha ao gerar a palavra: {mensagemErro}");
            }

            var textoGerado = (ExtrairTexto(textoResposta)
                ?? throw new Exception("Não consegui gerar essa palavra agora. Tente de novo.")).Trim();

            // Segurança extra: mesmo com o prompt pedindo pra não usar "---"/"#"/"##"/"###",
            // às vezes a IA ainda inclui algum resquício de markdown. Limpa aqui antes de
            // separar o texto em Significado/Exemplo, pra nunca sobrar símbolo literal na tela.
            textoGerado = LimparMarkdown(textoGerado);

            // Primeira linha: "palavra = significado1 / significado2" → vira o Significado.
            // O resto do texto (seções, exemplos, expressões, dica) → vira o Exemplo.
            var linhas = textoGerado.Replace("\r\n", "\n").Split('\n');
            var primeiraLinha = linhas.Length > 0 ? linhas[0].TrimStart('#', ' ').Trim() : "";
            var resto = linhas.Length > 1 ? string.Join("\n", linhas.Skip(1)).Trim() : "";

            return new Palavra
            {
                PalavraTexto = palavraTexto.Trim(),
                Significado  = !string.IsNullOrWhiteSpace(primeiraLinha) ? primeiraLinha : palavraTexto.Trim(),
                Exemplo      = !string.IsNullOrWhiteSpace(resto) ? resto : textoGerado
            };
        }

        private const string InstrucaoSistemaExercicios =
            "Você é um gerador de exercícios de múltipla escolha para um app de estudo de inglês. " +
            "Vai receber uma palavra em inglês (e o que já se sabe sobre ela: significado, exemplos, " +
            "possíveis sentidos diferentes). Gere exercícios de múltipla escolha (4 alternativas " +
            "cada) testando o entendimento dessa palavra: significado, tradução, e uso em contexto " +
            "(frases com lacuna, ou 'qual desses exemplos usa a palavra corretamente'). Responda " +
            "SOMENTE com um JSON válido (sem markdown, sem ```), no formato exato:\n" +
            "{\n" +
            "  \"perguntas\": [\n" +
            "    {\n" +
            "      \"pergunta\": \"texto da pergunta (em português, direto)\",\n" +
            "      \"fraseAlvo\": \"o texto em INGLÊS que está sendo testado nessa pergunta " +
            "(a palavra sozinha, ou a frase/expressão completa entre aspas dentro de 'pergunta', " +
            "exatamente como aparece lá — sem as aspas)\",\n" +
            "      \"alternativas\": [\"opção A\", \"opção B\", \"opção C\", \"opção D\"],\n" +
            "      \"respostaCorreta\": 0,\n" +
            "      \"traducao\": \"tradução relevante pra essa pergunta\",\n" +
            "      \"explicacao\": \"explicação curta do significado usado nessa pergunta\",\n" +
            "      \"gramatica\": \"observação curta de gramática relevante (ou vazio se não houver)\",\n" +
            "      \"exemploUso\": \"Frase em inglês. = Tradução.\"\n" +
            "    }\n" +
            "  ]\n" +
            "}\n" +
            "REGRAS:\n" +
            "- \"fraseAlvo\" é sempre em INGLÊS. Se a pergunta testa só a palavra isolada, repita a " +
            "  palavra. Se a pergunta testa a palavra dentro de uma expressão/frase maior (ex.: " +
            "  \"All the cake\"), \"fraseAlvo\" deve ser essa expressão/frase inteira, não só a " +
            "  palavra — é o que vai ser falado em voz alta pra pessoa como pronúncia.\n" +
            "- \"respostaCorreta\" é o ÍNDICE (0 a 3) da alternativa certa dentro de \"alternativas\".\n" +
            "- As alternativas erradas devem ser plausíveis (não óbvias demais), mas claramente erradas.\n" +
            "- Tudo curto e direto, sem parágrafos longos.\n" +
            "- Gere exatamente a quantidade de perguntas pedida pelo usuário.\n" +
            "- MUITO IMPORTANTE — VARIEDADE: cada pergunta do lote deve ser DIFERENTE das outras " +
            "  (não repita a mesma frase-base, o mesmo exemplo, nem o mesmo tipo de pergunta duas " +
            "  vezes). Alterne bastante entre os tipos: pergunta de significado direto, tradução, " +
            "  frase com lacuna pra completar, identificar o uso correto num contexto, diferenciar " +
            "  sentidos parecidos (quando a palavra tiver mais de um sentido), reconhecer sinônimo/" +
            "  antônimo, e apontar o erro numa frase quase certa. Com lotes maiores (10+ perguntas), " +
            "  repita cada tipo no máximo 2 ou 3 vezes, espalhadas, nunca seguidas. Use frases de " +
            "  exemplo diferentes das que já foram usadas no significado original da palavra, sempre " +
            "  que possível, pra a prática não virar decoreba (nunca reaproveite a mesma frase-modelo " +
            "  em mais de uma pergunta do mesmo lote).";

        /// <summary>
        /// Gera exercícios de múltipla escolha pra praticar uma palavra já cadastrada no
        /// dicionário, usando o que já se sabe sobre ela (significado, exemplos, sentidos).
        /// </summary>
        public async Task<List<ExercicioPergunta>> GerarExerciciosAsync(ConfiguracaoAssistente cfg, Palavra p, int quantidade = 15)
        {
            if (!cfg.TemChave)
                throw new InvalidOperationException("Chave de API não configurada.");

            var url =
                $"https://generativelanguage.googleapis.com/v1beta/models/{cfg.Modelo}:generateContent?key={cfg.ApiKey}";

            var contextoPalavra = new StringBuilder();
            contextoPalavra.AppendLine($"Palavra: {p.PalavraTexto}");
            if (!string.IsNullOrWhiteSpace(p.Significado)) contextoPalavra.AppendLine($"Significado: {p.Significado}");
            if (!string.IsNullOrWhiteSpace(p.Exemplo)) contextoPalavra.AppendLine($"Exemplo: {p.Exemplo}");
            if (p.Sentidos.Count > 0)
            {
                contextoPalavra.AppendLine("Sentidos diferentes dessa palavra:");
                foreach (var s in p.Sentidos)
                    contextoPalavra.AppendLine($"- {s.Significado}: {s.Exemplo}");
            }
            contextoPalavra.AppendLine($"Gere exatamente {quantidade} perguntas.");
            contextoPalavra.AppendLine(
                $"(Tentativa nº {Guid.NewGuid().ToString()[..8]} — gere perguntas novas e diferentes de " +
                "qualquer tentativa anterior, com frases de exemplo distintas.)");

            var corpo = new
            {
                systemInstruction = new
                {
                    parts = new object[] { new { text = InstrucaoSistemaExercicios } }
                },
                contents = new[]
                {
                    new
                    {
                        role = "user",
                        parts = new object[] { new { text = contextoPalavra.ToString() } }
                    }
                },
                generationConfig = new
                {
                    temperature = 1.0,
                    maxOutputTokens = 6000,
                    responseMimeType = "application/json"
                }
            };

            var json = JsonSerializer.Serialize(corpo);
            using var requisicao = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };

            using var resposta = await EnviarComTimeoutAsync(requisicao);
            var textoResposta = await resposta.Content.ReadAsStringAsync();

            if (!resposta.IsSuccessStatusCode)
            {
                var mensagemErro = ExtrairMensagemErro(textoResposta) ?? resposta.StatusCode.ToString();
                throw new Exception($"Falha ao gerar os exercícios: {mensagemErro}");
            }

            var textoGerado = ExtrairTexto(textoResposta)
                ?? throw new Exception("Não consegui gerar os exercícios agora. Tente de novo.");

            try
            {
                using var doc = JsonDocument.Parse(textoGerado);
                var perguntas = new List<ExercicioPergunta>();

                if (doc.RootElement.TryGetProperty("perguntas", out var vPerguntas) && vPerguntas.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in vPerguntas.EnumerateArray())
                    {
                        var alternativas = new List<string>();
                        if (item.TryGetProperty("alternativas", out var vAlt) && vAlt.ValueKind == JsonValueKind.Array)
                            foreach (var alt in vAlt.EnumerateArray())
                                alternativas.Add(alt.GetString() ?? "");

                        perguntas.Add(new ExercicioPergunta
                        {
                            Pergunta        = item.TryGetProperty("pergunta", out var pg) ? pg.GetString() ?? "" : "",
                            FraseAlvo       = item.TryGetProperty("fraseAlvo", out var fa) ? fa.GetString() ?? "" : "",
                            Alternativas    = alternativas,
                            RespostaCorreta = item.TryGetProperty("respostaCorreta", out var rc) ? rc.GetInt32() : 0,
                            Traducao        = item.TryGetProperty("traducao", out var tr) ? tr.GetString() ?? "" : "",
                            Explicacao      = item.TryGetProperty("explicacao", out var ex) ? ex.GetString() ?? "" : "",
                            Gramatica       = item.TryGetProperty("gramatica", out var gr) ? gr.GetString() ?? "" : "",
                            ExemploUso      = item.TryGetProperty("exemploUso", out var eu) ? eu.GetString() ?? "" : ""
                        });
                    }
                }

                if (perguntas.Count == 0)
                    throw new Exception("A IA não gerou nenhuma pergunta. Tente de novo.");

                return perguntas;
            }
            catch (JsonException)
            {
                throw new Exception("A resposta da IA veio em um formato inesperado. Tente gerar de novo.");
            }
        }

        private static readonly System.Text.RegularExpressions.Regex _regexLinhaSeparadora = new(
            @"^[ \t]*([-_*])\1{2,}[ \t]*$", System.Text.RegularExpressions.RegexOptions.Multiline);

        private static readonly System.Text.RegularExpressions.Regex _regexTitulo = new(
            @"^[ \t]{0,3}#{1,6}[ \t]*", System.Text.RegularExpressions.RegexOptions.Multiline);

        /// <summary>
        /// Remove linhas que são só separadores de markdown ("---", "___", "***") e tira o
        /// "#"/"##"/"###" do início de linhas de título, sem apagar o texto do título em si.
        /// Usado como segurança extra além da instrução no prompt.
        /// </summary>
        private static string LimparMarkdown(string texto)
        {
            var semSeparadores = _regexLinhaSeparadora.Replace(texto, "");
            var semTitulos = _regexTitulo.Replace(semSeparadores, "");

            // Depois de remover as linhas de separador sobram várias linhas em branco seguidas;
            // reduz pra no máximo uma linha em branco entre parágrafos.
            var linhas = semTitulos.Replace("\r\n", "\n").Split('\n');
            var resultado = new StringBuilder();
            int vaziasSeguidas = 0;
            foreach (var linha in linhas)
            {
                if (string.IsNullOrWhiteSpace(linha))
                {
                    vaziasSeguidas++;
                    if (vaziasSeguidas > 1) continue;
                }
                else
                {
                    vaziasSeguidas = 0;
                }
                resultado.Append(linha).Append('\n');
            }
            return resultado.ToString().Trim();
        }

        private static string? ExtrairTexto(string json)
        {
            using var doc = JsonDocument.Parse(json);

            if (!doc.RootElement.TryGetProperty("candidates", out var candidatos) ||
                candidatos.GetArrayLength() == 0)
                return null;

            var primeiro = candidatos[0];
            if (!primeiro.TryGetProperty("content", out var conteudo) ||
                !conteudo.TryGetProperty("parts", out var partes) ||
                partes.GetArrayLength() == 0)
                return null;

            var sb = new StringBuilder();
            foreach (var parte in partes.EnumerateArray())
            {
                if (parte.TryGetProperty("text", out var textoEl))
                    sb.Append(textoEl.GetString());
            }

            var resultado = sb.ToString().Trim();
            return string.IsNullOrEmpty(resultado) ? null : resultado;
        }

        private static string? ExtrairMensagemErro(string json)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("error", out var erro) &&
                    erro.TryGetProperty("message", out var msg))
                    return msg.GetString();
            }
            catch
            {
                // resposta não era JSON válido
            }
            return null;
        }
    }
}
