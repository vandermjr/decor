# Decor

Aplicação desktop multiplataforma em migração de WinForms para AvaloniaUI.

## Estrutura

```text
src/
├── Decor.Core/
├── Decor.Application/
├── Decor.Infrastructure/
├── Decor.FluentSqlBuilder/
└── Decor.AvaloniaUI/

tests/
└── Decor.FluentSqlBuilder.Tests/
```

O frontend WinForms legado permanece fora da solução em `Decor.WinForms/` durante a migração.
