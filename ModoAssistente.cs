namespace WordByWord
{
    /// <summary>
    /// Os modos do painel do assistente: tirar dúvidas de inglês por texto e bater papo por
    /// texto pra praticar.
    /// </summary>
    public enum ModoAssistente
    {
        /// <summary>Tira dúvidas sobre palavras, significados, gramática etc. (estrela).</summary>
        Duvidas,

        /// <summary>Bate-papo casual em inglês pra praticar o idioma (balão de chat).</summary>
        Conversacao,

        /// <summary>Conversa falada com a Ana: a pessoa fala (em inglês ou português) e ouve
        /// a resposta em voz, além de ver o texto — pensado pra treinar conversação de verdade.</summary>
        Audio
    }
}
