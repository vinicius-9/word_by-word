# 📖 Word By Word — Código-Fonte

> 🚧 **Projeto em andamento** — ainda em desenvolvimento, sujeito a mudanças e ajustes.

**Word By Word** ("palavra por palavra") é um caderno de inglês pessoal: anote e
aprenda o idioma palavra por palavra, guardando o que não conhece — com significado
e exemplo — e revise depois através de exercícios. Conta também com bate-papo em
inglês e um assistente com IA pra tirar dúvidas na hora.

---

## ✨ Funcionalidades

- **Cadastro de palavras** com significado e exemplo
- Busca rápida por palavra ou significado
- **Pronúncia em voz** das palavras (requer Python + edge-tts — veja abaixo)
- Tema claro/escuro (preferência salva)
- 59 palavras pré-cadastradas para já começar estudando
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

C# / WPF · .NET 8 · Python + edge-tts · Gemini API

---

## 👨‍💻 Autor

Vinicius Pereira
📧 vinicius.pereiragoncalves.online@gmail.com
