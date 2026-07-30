#  Word By Word — Código-Fonte



**Word By Word** ("palavra por palavra") é um caderno de inglês pessoal: anote e
aprenda o idioma palavra por palavra, guardando o que não conhece — com significado
e exemplo — e revise depois através de exercícios. Conta também com bate-papo em
inglês e um assistente com IA pra tirar dúvidas na hora.

💡 O aplicativo gera exemplos e textos base para auxiliar nos estudos, mas o usuário pode pesquisar, editar e complementar o conteúdo conforme sua necessidade.

🚧 O projeto está sujeito a mudanças.
---

## ✨ Funcionalidades

- **Cadastro de palavras** com significado e exemplo
- Busca rápida por palavra ou significado
- **Pronúncia em voz** das palavras (requer Python + edge-tts — veja abaixo)
- Tema claro/escuro (preferência salva)
- 64 palavras pré-cadastradas para já começar estudando
- Dados salvos localmente
- **Exercícios** para revisar o que você foi anotando
- **Assistente com IA (Gemini) — a Ana:** tira dúvidas de inglês ou bate papo pra
  praticar. Exige chave de API gratuita — veja abaixo.

O dicionário básico funciona direto, sem configurar nada. Só o Assistente precisa da
chave do Gemini, e só a pronúncia precisa do Python.

---

## 🔊 Pronúncia em voz (Python + edge-tts)

1. Instale o Python em **python.org** (marque **"Add to PATH"** na instalação)
2. `pip install edge-tts`
3. Reinicie o app

---

##  Assistente (chave de API do Gemini)

1. Clique em **✦ Assistente** — na primeira vez, o painel de configuração abre sozinho
2. Cole sua chave (crie grátis em **aistudio.google.com/apikey**)
3. Escolha modelo e voz, clique em **Salvar**

Cada pessoa usa a própria chave, gerada por ela mesma.

---
## 🛠️ Como compilar

**Requisitos:** Windows 10/11, .NET 8 SDK, Python + edge-tts pra voz)

---

## 🖥️ Tecnologias

- **C# / WPF** — interface e lógica do app
- **.NET 8**
- **SQLite** (`Microsoft.Data.Sqlite`) — banco de dados local (`dicionario.db`)
- **Python + edge-tts** — geração de voz para pronúncia
- **Gemini API** — assistente com IA (Ana)

---

## 📥 Baixar  

 já compilado, pronto pra usar

[**⬇️ Baixar Word By Word (Portable)**](https://www.mediafire.com/file/8jc7srdw2bquuk0/WordByWord+Portable.zip/file)

Baixe o `.zip`, extraia em qualquer pasta e rode o `WordByWord.exe` — não precisa instalar nada.

> ⚠️ **O Windows pode exibir um aviso "O Windows protegeu o computador" (SmartScreen) ao abrir o `.exe`.**
> Isso é normal e acontece porque o app ainda não tem um certificado de assinatura digital (code signing) —
> não é um vírus nem malware, só uma proteção padrão do Windows para arquivos baixados da internet sem essa assinatura.
>
> **Como resolver (escolha uma das opções):**
>
> 1. **Direto no aviso:** clique em **"Mais informações"** → **"Executar assim mesmo"**.
> 2. **Desbloqueando o arquivo antes:** clique com o botão direito em `WordByWord.exe` → **Propriedades** → na aba **Geral**, marque a caixinha **"Desbloquear"** (perto do rodapé) → **Aplicar**. O aviso não aparece mais para esse arquivo.
--- 

## 👨‍💻 Autor

Vinicius Pereira
📧 vinicius.pereiragoncalves.online@gmail.com
