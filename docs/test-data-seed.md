# Dados ficticios de teste

Ferramenta opt-in: `tools/Decor.TestDataSeed`. Nao participa do startup, nao altera
a UI e nao executa migrations nem bootstrap de repositorios administrativos.

## Estado verificado em 2026-10-09

O responsavel autorizou a carga no schema local de desenvolvimento `dcg`, usando
`src/Decor.AvaloniaUI/appsettings.json`. Destino loopback e schema exato conferidos
pela ferramenta. Dry-run concluiu com zero escritas. Apply retornou `COMMITTED`:
5 funcionarios, 5 fornecedores, 5 clientes e 5 usuarios (20 registros principais),
5 roles, 5 user_roles e 56 role_permissions inseridos. `Services.View` estava
ausente e foi explicitamente omitida apenas do Comercial, que manteve suas 12
permissoes principais. Nenhuma outra permissao exigida estava ausente.

Segunda execucao apply retornou `COMMITTED`, encontrou todos esses totais
persistidos e inseriu zero linhas em todas as sete tabelas. Registros existentes
nao foram modificados. Nenhuma permissao criada, nenhuma migration executada.
Tarefas instaladas: `seed-dcg-dry-run` e `seed-dcg-apply`.

Autoteste passou sem banco. Os dois testes de integracao passaram em MariaDB
10.11.6 descartavel, com catalogo completo e sem `Services.View`. Cobrem rollback
por colisao e outra permissao ausente, perfil Comercial, idempotencia, revogacao,
inativacoes, hashes e administrador existente preservados. Resultado:
`artifacts/seed-compatibility-tests/results/seed-compatibility.trx` (2/2).
A configuracao de uma instancia em execucao pode ser diferente, por
ambiente/argumentos/arquivo overlay.

## Executar

Na raiz do repositorio, primeiro valide sem banco:

```sh
dotnet run --project tools/Decor.TestDataSeed/Decor.TestDataSeed.csproj -- --self-test
dotnet run --project tools/Decor.TestDataSeed/Decor.TestDataSeed.csproj -- --inspect-config --config src/Decor.AvaloniaUI/appsettings.json
```

`--inspect-config` nao conecta. Le apenas o arquivo indicado e respeita a variavel
`ConnectionStrings__MariaDBConnector`, se definida. Nao imprime string, host,
usuario, senha ou hash. Nao incorpora outros arquivos appsettings ou argumentos
do aplicativo: escolha explicitamente a configuracao correta da instancia.

**Antes de continuar**, o responsavel deve confirmar que `dcg` e uma copia local
descartavel de desenvolvimento, sem dados reais, sem tunel/proxy para remoto.
Se nao for, prepare uma copia local apropriada e indique seu arquivo privado de
configuracao e o nome exato do schema. Nao passe credenciais na linha de comando.

Somente depois dessa confirmacao, para o destino acima:

```sh
dotnet run --project tools/Decor.TestDataSeed/Decor.TestDataSeed.csproj -- --dry-run --config src/Decor.AvaloniaUI/appsettings.json --expected-database dcg --confirm-local-development
dotnet run --project tools/Decor.TestDataSeed/Decor.TestDataSeed.csproj -- --apply --config src/Decor.AvaloniaUI/appsettings.json --expected-database dcg --confirm-local-development
```

Destinos nao TCP loopback, nomes com prod/live/production, divergencia do schema,
colisoes, schema incompleto ou tabelas nao InnoDB sao bloqueados. As permissoes
precisam existir previamente no catalogo, com uma unica excecao: `Services.View`
e opcional somente quando ausente, por pertencer ao modulo recem-separado.
Nesse caso o perfil Comercial conserva suas 12 permissoes principais e a ferramenta
reporta `OMITTED: Services.View from Comercial` em dry-run e apply; sao 56
role_permissions iniciais em vez de 57. Se presente, a permissao e incluida
normalmente. Qualquer outra permissao ausente bloqueia toda a carga e seus codigos
sao reportados. Nenhuma permissao e inventada/criada. A omissao nao habilita o
modulo de servicos nem substitui o gate de manutencao em
[service-separation-migration.md](service-separation-migration.md).
**A migracao offline de servicos nunca e executada automaticamente por esta ferramenta.**

Dry-run requer a mesma confirmacao, consulta o schema e identidades e faz zero
INSERT/UPDATE/DELETE. Os numeros `planned` nao sao insercoes. Apply usa transacao
Serializable unica e lock nomeado entre seeds. So `COMMITTED` confirma sucesso;
saidas anteriores `pending-commit` nao comprovam persistencia. Erros sao sanitizados.
DDL e criacao de permissoes nao fazem parte da carga.

## Identidades

Nomes de dados mestres recebem o prefixo `[FICTICIO DSEED1]` e documentos artificiais
`DSEED1-E01` a `E05`, `DSEED1-C01` a `C05`, `DSEED1-S01` a `S05`. Nao sao CPF/CNPJ.
Contatos de clientes/fornecedores usam o dominio reservado `example.invalid`.

| Funcionario | Usuario | Grupo | Cliente | Fornecedor |
| --- | --- | --- | --- | --- |
| Ana Ficticia | devseed.cadastros | Teste DSEED1 Cadastros | Casa Aurora | Tecidos Aurora |
| Bruno Ficticio | devseed.comercial | Teste DSEED1 Comercial | Casa Horizonte | Trilhos Horizonte |
| Carla Ficticia | devseed.compras | Teste DSEED1 Compras | Casa Primavera | Persianas Primavera |
| Diego Ficticio | devseed.estoque | Teste DSEED1 Estoque | Casa Jardim | Acessorios Jardim |
| Elisa Ficticia | devseed.financeiro | Teste DSEED1 Financeiro | Casa Estrela | Ferragens Estrela |

Cada funcionario tem UserID ligado ao usuario correspondente. Cada usuario novo
tem exatamente seu grupo de tarefa. Roles novas: nivel 10, nao protegidas.

## Permissoes iniciais

Sem Users.*, Roles.*, DatabaseMaintenance.*, exclusoes, cancelamentos ou ajuste
de inventario. Sem wildcard: cada codigo e conferido no catalogo existente.

| Grupo | Codigos (expansao literal das acoes indicadas) |
| --- | --- |
| Cadastros (9) | Customers.View/Create/Edit; Suppliers.View/Create/Edit; Employees.View/Create/Edit |
| Comercial (12 ou 13) | Customers.View; Products.View; Services.View (somente se existir no catalogo); Employees.View; Quotes.View/Create/Edit/Send/Approve; Orders.View/ConvertFromQuote; PaymentMethods.View; UnitsOfMeasure.View |
| Compras (12) | Suppliers.View; Products.View; StockLocations.View; UnitsOfMeasure.View; PurchaseOrders.View/Create/Edit; PurchaseOrderItems.View/Create/Edit; GoodsReceipts.View/Register |
| Estoque (8) | Products.View; UnitsOfMeasure.View; StockLocations.View; StockMovements.View/Entry/Exit/Transfer; StockReservations.View |
| Financeiro (15) | Customers.View; Suppliers.View; Orders.View; PurchaseOrders.View; PaymentMethods.View; CashAccounts.View; CashTransactions.View/Create; OrderInstallments.View/RegisterPayment; PurchaseOrderInstallments.View/RegisterPayment; AccountsPayable.View/Create/RegisterPayment |

## Preservacao e acesso

O seed so insere ausentes. Nomes/documentos/markers conflitantes abortam a
transacao, inclusive nomes pertencentes a contas reais. Nenhum UPDATE, DELETE,
upsert, reset de senha, concessao a grupo existente ou restauracao de revogacao.
Grupos e contas existentes precisam manter o marker exclusivo deste seed.
Associacoes alteradas sao bloqueadas, nao restauradas. Estado ativo e permissoes
podem ser alterados posteriormente; novas execucoes os preservam.

Usa os DTOValidators reais e PasswordPolicy/Pbkdf2PasswordHasher da Application.
Nao simula sessao admin nem chama servicos com bootstrap de migrations. Os
RepositoryValidators atuais de Customer/Supplier/Employee nao adicionam regras.
SQL parametrizado conecta diretamente para manter uma unica transacao.

Senhas aleatorias distintas sao geradas em memoria, validadas, hasheadas e
descartadas, nunca impressas/persistidas em texto. `MustChangePassword=1`.
**Para entrar nas contas**, um operador autorizado precisa usar o fluxo existente
de redefinicao de senha temporaria; o seed nao fornece nem redefine credenciais.

## Teste reutilizavel

Exige Docker. Cria apenas schema de fixture dentro de MariaDB descartavel, sem
ler appsettings do aplicativo nem executar migrations do repositorio:

Inclui catalogo completo e ausencia de `Services.View`: ambos preservam as 12
permissoes principais do Comercial, todos os 5 usuarios/grupos e 15 cadastros,
rollback por outra permissao ausente, idempotencia e revogacoes existentes.

```sh
dotnet test tools/Decor.TestDataSeed.Tests/Decor.TestDataSeed.Tests.csproj --artifacts-path artifacts/test-data-seed-integration --results-directory artifacts/test-data-seed-integration/results --logger 'trx;LogFileName=seed.trx'
```