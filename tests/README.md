# Tests

Os projetos de teste ficam separados dos projetos de produção.

Atualmente há testes unitários para `Decor.Application` e `Decor.FluentSqlBuilder`, além de testes de integração para `Decor.Infrastructure`.

`Decor.Infrastructure.IntegrationTests` sobe um container MariaDB efêmero com Testcontainers, aplica a migration de autenticação/autorização e descarta o container ao fim da execução. É necessário que o Docker esteja disponível para executar esse projeto.
