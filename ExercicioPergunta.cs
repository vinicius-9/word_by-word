using System.Collections.Generic;

namespace WordByWord
{
    /// <summary>Uma pergunta de múltipla escolha gerada para praticar uma palavra.</summary>
    public class ExercicioPergunta
    {
        public string Pergunta { get; set; } = "";

        public string FraseAlvo { get; set; } = "";

        public List<string> Alternativas { get; set; } = new();

        /// <summary>Índice (0-based) da alternativa correta em <see cref="Alternativas"/>.</summary>
        public int RespostaCorreta { get; set; }

        public string Traducao    { get; set; } = "";
        public string Explicacao  { get; set; } = "";
        public string Gramatica   { get; set; } = "";
        public string ExemploUso  { get; set; } = "";
    }
}
