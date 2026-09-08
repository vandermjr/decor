# Decor.AvaloniaUI

Novo frontend multiplataforma do Decor.

Nesta etapa, o projeto foi transformado em uma aplicação desktop Avalonia mínima, mantendo a implementação da interface deliberadamente simples. O objetivo é validar a fundação de execução no .NET 8/Linux antes de iniciar a migração das telas e das abstrações específicas do WinForms.

## Estado atual

- Avalonia Desktop 12.1.1
- Avalonia Fluent Theme 12.1.1
- .NET 8
- `Program.cs` como ponto de entrada desktop
- `App.axaml` como aplicação Avalonia
- `MainWindow` mínima para validação da execução
- Referência mantida para `Decor.Application`

## Executar

Na raiz do repositório:

```fish
dotnet run --project src/Decor.AvaloniaUI
```

A próxima etapa deve conectar a inicialização da aplicação à infraestrutura de DI existente, sem migrar telas ainda.
